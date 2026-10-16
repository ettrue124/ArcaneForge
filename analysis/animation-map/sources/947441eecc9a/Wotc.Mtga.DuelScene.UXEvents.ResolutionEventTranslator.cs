using System;
using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.Resolution;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ResolutionEventTranslator : IEventTranslator
{
	private const float DEFAULT_DURATION = 0.25f;

	private const float MAX_RESOLVE_TIME = 3f;

	private readonly Dictionary<(uint, GameObjectType), uint> _resolvedThisTurnCount = new Dictionary<(uint, GameObjectType), uint>();

	private readonly IObjectPool _objPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IResolutionEffectController _resolutionEffectController;

	private readonly ITurnInfoProvider _turnInfoProvider;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly IAssetCacheProvider<SplineMovementData> _splineCache;

	private readonly IVfxProvider _vfxProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private uint _previousTurnNumber;

	private MtgCardInstance _previousResolvingInstance;

	public ResolutionEventTranslator(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<IObjectPool>(), context.Get<ICardDatabaseAdapter>(), context.Get<IResolutionEffectController>(), context.Get<ITurnInfoProvider>(), context.Get<IEntityViewProvider>(), context.Get<ICardHolderProvider>(), context.Get<ISplineMovementSystem>(), context.Get<IAssetCacheProvider<SplineMovementData>>(), context.Get<IVfxProvider>(), assetLookupSystem)
	{
	}

	public ResolutionEventTranslator(IObjectPool objectPool, ICardDatabaseAdapter cardDatabase, IResolutionEffectController resolutionEffectController, ITurnInfoProvider turnInfoProvider, IEntityViewProvider entityViewProvider, ICardHolderProvider cardHolderProvider, ISplineMovementSystem splineMovementSystem, IAssetCacheProvider<SplineMovementData> splineCache, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
	{
		_objPool = objectPool;
		_cardDatabase = cardDatabase;
		_resolutionEffectController = resolutionEffectController;
		_turnInfoProvider = turnInfoProvider;
		_entityViewProvider = entityViewProvider;
		_cardHolderProvider = cardHolderProvider;
		_splineMovementSystem = splineMovementSystem;
		_splineCache = splineCache;
		_vfxProvider = vfxProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (!(allChanges[changeIndex] is ResolutionEvent resolutionEvent))
		{
			return;
		}
		MtgCardInstance instigator = GetInstigator(resolutionEvent.InstanceID, oldState, newState);
		instigator = (resolutionEvent.IsStart ? instigator : (_previousResolvingInstance ?? instigator));
		if (instigator == null)
		{
			return;
		}
		CardPrintingData cardPrinting = null;
		AbilityPrintingData abilityPrintingData = null;
		if (instigator.GrpId == resolutionEvent.GrpId)
		{
			if (instigator.ObjectType == GameObjectType.Ability)
			{
				abilityPrintingData = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(resolutionEvent.GrpId);
			}
			else
			{
				cardPrinting = _cardDatabase.CardDataProvider.GetCardPrintingById(resolutionEvent.GrpId);
			}
		}
		FillAltBlackboard(_assetLookupSystem.Blackboard, instigator.ToCardData(_cardDatabase), abilityPrintingData);
		Data resolutionData = GetResolutionData(_assetLookupSystem);
		Projectile projectile = (resolutionEvent.IsStart ? GetResolutionProjectile(_assetLookupSystem) : null);
		IReadOnlyCollection<VFX_Base> readOnlyCollection = (resolutionEvent.IsStart ? GetResolutionVFX<Start_VFX>(_objPool, _assetLookupSystem) : GetResolutionVFX<End_VFX>(_objPool, _assetLookupSystem));
		IReadOnlyCollection<SFX_Base> sfx = (resolutionEvent.IsStart ? GetResolutionSFX<Start_SFX>(_objPool, _assetLookupSystem) : GetResolutionSFX<End_SFX>(_objPool, _assetLookupSystem));
		TryResetResolutionCounts(_turnInfoProvider.EventTranslationTurnNumber);
		uint resolvedCount = IncrementResolveCount(instigator);
		float eventDuration = GetEventDuration(readOnlyCollection, projectile, resolvedCount);
		if (resolutionEvent.IsStart)
		{
			events.Add(new ResolutionEventStartedUXEvent(new ResolutionEffectModel(resolutionEvent.InstanceID, resolutionEvent.GrpId, instigator, instigator.ToCardData(_cardDatabase), cardPrinting, abilityPrintingData, resolutionData?.IgnoreDamageEvents ?? false, resolutionData?.IgnoreDestructionEvents ?? false, resolutionData?.IgnoreCoinFlipEvents ?? false, resolutionData?.RedirectDamageFromParent ?? false, resolutionData?.SuppressProjectileDamageEffects ?? false), _resolutionEffectController, _cardDatabase, _splineMovementSystem, _splineCache, _vfxProvider, _cardHolderProvider, _entityViewProvider, instigator, cardPrinting, abilityPrintingData, resolutionData, readOnlyCollection, sfx, projectile, 0.25f, eventDuration));
			_previousResolvingInstance = instigator;
		}
		else
		{
			events.Add(new ResolutionEventEndedUXEvent(_resolutionEffectController, _cardDatabase, _splineMovementSystem, _splineCache, _vfxProvider, _cardHolderProvider, _entityViewProvider, instigator, cardPrinting, abilityPrintingData, resolutionData, readOnlyCollection, sfx, null, 0.25f, eventDuration));
			_previousResolvingInstance = null;
		}
	}

	private static MtgCardInstance GetInstigator(uint instigatorId, MtgGameState prvState, MtgGameState newState)
	{
		if (prvState.TryGetCard(instigatorId, out var card))
		{
			return card;
		}
		if (newState.TryGetCard(instigatorId, out var card2))
		{
			return card2;
		}
		return null;
	}

	private static void FillAltBlackboard(IBlackboard blackboard, ICardDataAdapter resolvingCardData, AbilityPrintingData ability)
	{
		blackboard.Clear();
		blackboard.SetCardDataExtensive(resolvingCardData);
		if (ability != null)
		{
			blackboard.Ability = ability;
		}
	}

	private static Data GetResolutionData(AssetLookupSystem assetLookupSystem)
	{
		if (!assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<Data> loadedTree))
		{
			return null;
		}
		return loadedTree.GetPayload(assetLookupSystem.Blackboard);
	}

	private static Projectile GetResolutionProjectile(AssetLookupSystem assetLookupSystem)
	{
		if (!assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<Projectile> loadedTree))
		{
			return null;
		}
		return loadedTree.GetPayload(assetLookupSystem.Blackboard);
	}

	private static IReadOnlyCollection<VFX_Base> GetResolutionVFX<T>(IObjectPool objectPool, AssetLookupSystem assetLookupSystem) where T : VFX_Base
	{
		HashSet<VFX_Base> hashSet = null;
		HashSet<T> hashSet2 = objectPool.PopObject<HashSet<T>>();
		if (assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<T> loadedTree) && loadedTree.GetPayloadLayered(assetLookupSystem.Blackboard, hashSet2))
		{
			if (hashSet == null)
			{
				hashSet = new HashSet<VFX_Base>();
			}
			hashSet.UnionWith(hashSet2);
		}
		hashSet2.Clear();
		objectPool.PushObject(hashSet2, tryClear: false);
		return hashSet;
	}

	private static IReadOnlyCollection<SFX_Base> GetResolutionSFX<T>(IObjectPool objectPool, AssetLookupSystem assetLookupSystem) where T : SFX_Base
	{
		HashSet<SFX_Base> hashSet = null;
		HashSet<T> hashSet2 = objectPool.PopObject<HashSet<T>>();
		if (assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<T> loadedTree) && loadedTree.GetPayloadLayered(assetLookupSystem.Blackboard, hashSet2))
		{
			if (hashSet == null)
			{
				hashSet = new HashSet<SFX_Base>();
			}
			hashSet.UnionWith(hashSet2);
		}
		hashSet2.Clear();
		objectPool.PushObject(hashSet2, tryClear: false);
		return hashSet;
	}

	private void TryResetResolutionCounts(uint translationTurnNum)
	{
		if (_previousTurnNumber != translationTurnNum)
		{
			_resolvedThisTurnCount.Clear();
			_previousTurnNumber = translationTurnNum;
		}
	}

	private uint IncrementResolveCount(MtgCardInstance instigator)
	{
		return IncrementResolveCount((titleId: instigator.TitleId, objType: instigator.ObjectType));
	}

	private uint IncrementResolveCount((uint titleId, GameObjectType objType) key)
	{
		if (_resolvedThisTurnCount.ContainsKey(key))
		{
			return _resolvedThisTurnCount[key]++;
		}
		return _resolvedThisTurnCount[key] = 1u;
	}

	private static float GetEventDuration(IEnumerable<VFX_Base> vfxPayloads, Projectile projectile, uint resolvedCount)
	{
		float num = 0.25f;
		foreach (VFX_Base item in vfxPayloads ?? Array.Empty<VFX_Base>())
		{
			if (item.Duration > num)
			{
				num = item.Duration;
			}
		}
		if (projectile != null && projectile.Duration > num)
		{
			num = projectile.Duration;
		}
		switch (resolvedCount)
		{
		case 1u:
			return num;
		case 2u:
			return num * 0.7f;
		case 3u:
			return num * 0.4f;
		default:
		{
			float num2 = num * 0.2f;
			uint num3 = resolvedCount - 3;
			float num4 = num2 * (float)num3 * 2f + (num * 2f + num * 0.7f * 2f + num * 0.4f * 2f);
			if (num4 > 3f)
			{
				float num5 = (num4 - 3f) / (float)num3;
				num2 -= num5;
			}
			return num2;
		}
		}
	}
}
