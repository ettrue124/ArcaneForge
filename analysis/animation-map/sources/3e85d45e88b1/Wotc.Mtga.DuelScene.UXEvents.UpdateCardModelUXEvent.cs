using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.Card;
using AssetLookupTree.Payloads.Projectile;
using GreClient.CardData;
using GreClient.CardData.RulesTextOverrider;
using GreClient.Rules;
using MovementSystem;
using Pooling;
using ReferenceMap;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Duel;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateCardModelUXEvent : UXEvent
{
	private readonly IObjectPool _objectPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	private readonly ICardMovementController _cardMovementController;

	private readonly IVfxProvider _vfxProvider;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly IAssetCacheProvider<SplineMovementData> _splineCacheProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private const string TOSTRING_FORMAT = "CardChange: {0} {1}";

	private const string TOSTRING_NULL_INSTANCE = "NULL";

	public MtgCardInstance NewInstance { get; }

	public MtgCardInstance OldInstance { get; }

	public PropertyType Property { get; }

	public uint AffectorId { get; }

	public HashSet<ReferenceMap.Reference> AddedReferences { get; }

	public HashSet<ReferenceMap.Reference> RemovedReferences { get; }

	public Designation DesignationGained { get; }

	public UpdateCardModelUXEvent(CardChangedEvent cardChangedEvent, IContext context, AssetLookupSystem assetLookupSystem)
		: this(cardChangedEvent, context.Get<IObjectPool>(), context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<ICardViewProvider>(), context.Get<ICardHolderProvider>(), context.Get<IVfxProvider>(), context.Get<ICardMovementController>(), context.Get<ISplineMovementSystem>(), context.Get<IAssetCacheProvider<SplineMovementData>>(), assetLookupSystem)
	{
	}

	public UpdateCardModelUXEvent(CardChangedEvent cardChangedEvent, IObjectPool objectPool, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, ICardHolderProvider cardHolderProvider, IVfxProvider vfxProvider, ICardMovementController cardMovementController, ISplineMovementSystem splineMovementSystem, IAssetCacheProvider<SplineMovementData> splineCacheProvider, AssetLookupSystem assetLookupSystem)
		: this(cardChangedEvent.OldInstance, cardChangedEvent.NewInstance, cardChangedEvent.AffectorId, cardChangedEvent.Property, cardChangedEvent.Designation, cardChangedEvent.AddedReferences, cardChangedEvent.RemovedReferences)
	{
		_objectPool = objectPool;
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_cardViewProvider = cardViewProvider;
		_vfxProvider = vfxProvider;
		_cardMovementController = cardMovementController;
		_cardHolderProvider = cardHolderProvider;
		_splineMovementSystem = splineMovementSystem;
		_splineCacheProvider = splineCacheProvider;
		_stack = CardHolderReference<StackCardHolder>.Stack(_cardHolderProvider);
		_battlefield = CardHolderReference<IBattlefieldCardHolder>.Battlefield(_cardHolderProvider);
		_assetLookupSystem = assetLookupSystem;
	}

	public UpdateCardModelUXEvent(MtgCardInstance oldInstance, MtgCardInstance newInstance, uint affectorId, PropertyType property, Designation designation = Designation.None, HashSet<ReferenceMap.Reference> addedReferences = null, HashSet<ReferenceMap.Reference> removedReferences = null)
	{
		OldInstance = oldInstance;
		NewInstance = newInstance;
		AffectorId = affectorId;
		Property = property;
		AddedReferences = addedReferences;
		RemovedReferences = removedReferences;
		DesignationGained = designation;
	}

	public override void Execute()
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		DuelScene_CDC cardView = null;
		CardData cardData = CardDataExtensions.CreateWithDatabase(NewInstance, _cardDatabase);
		if (cardData.RulesTextOverride is AbilityTextOverride abilityTextOverride)
		{
			List<TargetSpec> list = _objectPool.PopObject<List<TargetSpec>>();
			foreach (TargetSpec item in mtgGameState.TargetInfo)
			{
				if (item.Affector == cardData.InstanceId)
				{
					list.Add(item);
				}
			}
			abilityTextOverride.AddTargetSpecs(list, _assetLookupSystem);
			abilityTextOverride.AddSource(NewInstance);
			list.Clear();
			_objectPool.PushObject(list, tryClear: false);
		}
		if (!_cardViewProvider.TryGetCardView(cardData.InstanceId, out cardView))
		{
			Complete();
			return;
		}
		if (OldInstance != null)
		{
			LinkedFace linkedFaceType = cardView.Model.LinkedFaceType;
			LinkedFace linkedFaceType2 = cardData.LinkedFaceType;
			if ((linkedFaceType == LinkedFace.DfcBack || linkedFaceType == LinkedFace.DfcFront || linkedFaceType == LinkedFace.MdfcBack || linkedFaceType == LinkedFace.MdfcFront) && (linkedFaceType2 == LinkedFace.DfcBack || linkedFaceType2 == LinkedFace.DfcFront || linkedFaceType2 == LinkedFace.MdfcFront || linkedFaceType2 == LinkedFace.MdfcBack) && linkedFaceType != linkedFaceType2 && OldInstance.GrpId != NewInstance.GrpId)
			{
				PlayTransformEffect(cardView);
			}
		}
		cardView.AddUpdatedProperty(Property);
		if (OldInstance != null && _assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<PrePropertyUpdateVFX> loadedTree))
		{
			_assetLookupSystem.Blackboard.Clear();
			_assetLookupSystem.Blackboard.SetCardDataExtensive(cardView.Model);
			_assetLookupSystem.Blackboard.SupplementalCardData[SupplementalKey.OldInstance] = new CardData(OldInstance, cardView.Model.Printing);
			_assetLookupSystem.Blackboard.UpdatedProperties = cardView.UpdatedProperties;
			_assetLookupSystem.Blackboard.CardHolder = cardView.CurrentCardHolder;
			_assetLookupSystem.Blackboard.CardHolderType = cardView.CurrentCardHolder?.CardHolderType ?? CardHolderType.None;
			_assetLookupSystem.Blackboard.Designation = DesignationGained;
			_assetLookupSystem.Blackboard.AffectorCardData = _cardViewProvider.GetCardView(AffectorId)?.Model;
			if (cardView.PreviousCardHolder is ZoneCardHolderBase zoneCardHolderBase)
			{
				_assetLookupSystem.Blackboard.FromZone = zoneCardHolderBase.GetZone;
			}
			if (cardView.CurrentCardHolder is ZoneCardHolderBase zoneCardHolderBase2)
			{
				_assetLookupSystem.Blackboard.ToZone = zoneCardHolderBase2.GetZone;
			}
			_assetLookupSystem.Blackboard.ZonePair = new ZonePair(_assetLookupSystem.Blackboard.FromZone, _assetLookupSystem.Blackboard.ToZone);
			HashSet<PrePropertyUpdateVFX> hashSet = _objectPool.PopObject<HashSet<PrePropertyUpdateVFX>>();
			if (loadedTree.GetPayloadLayered(_assetLookupSystem.Blackboard, hashSet))
			{
				foreach (PrePropertyUpdateVFX item2 in hashSet)
				{
					foreach (VfxData vfxData in item2.VfxDatas)
					{
						_vfxProvider.PlayAnchoredVFX(vfxData, item2.AnchorPointType, cardView.ActiveScaffold, cardView.Model, cardView.Model.Instance, cardView.EffectsRoot);
					}
				}
			}
			hashSet.Clear();
			_objectPool.PushObject(hashSet, tryClear: false);
		}
		cardView.SetModel(cardData);
		ICardHolder currentCardHolder = cardView.CurrentCardHolder;
		if (currentCardHolder.CardHolderType == CardHolderType.Battlefield)
		{
			((currentCardHolder as IBattlefieldCardHolder)?.GetStackForCard(cardView))?.RefreshAbilitiesBasedOnStackPosition();
		}
		switch (Property)
		{
		case PropertyType.Actions:
			if (ShouldCheckForNHC(cardData))
			{
				_cardMovementController.MoveCard(cardView, cardData.Zone);
			}
			if (currentCardHolder.CardHolderType == CardHolderType.Library)
			{
				currentCardHolder.LayoutNow();
			}
			break;
		case PropertyType.BlockState:
		{
			HashSet<uint> hashSet2 = new HashSet<uint>(NewInstance.BlockingIds);
			HashSet<uint> hashSet3 = new HashSet<uint>(OldInstance.BlockingIds);
			hashSet2.ExceptWith(OldInstance.BlockingIds);
			hashSet3.ExceptWith(NewInstance.BlockingIds);
			foreach (uint item3 in hashSet2)
			{
				if (_cardViewProvider.TryGetCardView(item3, out var cardView2))
				{
					AudioManager.PlayAudio(WwiseEvents.sfx_combat_declare_defenders, cardView2.Root.gameObject);
				}
			}
			foreach (uint item4 in hashSet3)
			{
				if (_cardViewProvider.TryGetCardView(item4, out var cardView3))
				{
					AudioManager.PlayAudio(WwiseEvents.sfx_combat_declare_no_defenders, cardView3.Root.gameObject);
				}
			}
			_battlefield.Get().LayoutNow();
			break;
		}
		case PropertyType.AttackState:
		{
			string text = "";
			if (cardData.Instance.AttackState == AttackState.Declared)
			{
				text = WwiseEvents.sfx_combat_declareattackers.EventName;
			}
			else if (cardData.Instance.AttackState == AttackState.None)
			{
				text = WwiseEvents.sfx_combat_declare_no_attackers.EventName;
			}
			if (!string.IsNullOrEmpty(text))
			{
				AudioManager.PlayAudio(text, cardView.gameObject);
			}
			break;
		}
		case PropertyType.IsTapped:
			cardView.SetDimmedState(cardView.Model.IsTapped);
			AudioManager.PlayAudio(cardView.Model.IsTapped ? WwiseEvents.sfx_combat_tap : WwiseEvents.sfx_combat_untap, cardView.Root.gameObject);
			PlayTappedEffect(cardView, mtgGameState, _splineCacheProvider.GetAssetCache());
			break;
		case PropertyType.Controller:
			_splineMovementSystem.RemoveTemporaryGoal(cardView.Root);
			_cardMovementController.MoveCard(cardView, cardData.Zone);
			break;
		case PropertyType.AttachedTo:
			_cardMovementController.MoveCard(cardView, _cardHolderProvider.GetCardHolderByZoneId(cardData.Zone.Id));
			break;
		case PropertyType.Anonymity:
			currentCardHolder.LayoutNow();
			if (ShouldCheckForNHC(cardData))
			{
				_cardMovementController.MoveCard(cardView, cardData.Zone);
			}
			break;
		case PropertyType.Target:
			currentCardHolder.LayoutNow();
			break;
		case PropertyType.AffectedByQualifications:
		{
			bool flag = OldInstance.AffectedByQualifications.Exists(ExertQualCheck);
			bool flag2 = NewInstance.AffectedByQualifications.Exists(ExertQualCheck);
			if (flag2 && !flag)
			{
				AudioManager.PlayAudio(WwiseEvents.sfx_exert_active, cardView.gameObject);
			}
			else if (flag && !flag2)
			{
				AudioManager.PlayAudio(WwiseEvents.sfx_exert_break, cardView.gameObject);
			}
			break;
		}
		case PropertyType.FaceDown:
			_cardMovementController.MoveCard(cardView, currentCardHolder.CardHolderType);
			break;
		}
		Complete();
		static bool ExertQualCheck(QualificationData qd)
		{
			if (qd.Type == QualificationType.CantUntap && qd.Details.ContainsKey("Duration") && qd.Details["Duration"] == "NextUntap")
			{
				return qd.AbilityId == 162;
			}
			return false;
		}
	}

	public override IEnumerable<uint> GetInvolvedIds()
	{
		if (NewInstance != null)
		{
			yield return NewInstance.InstanceId;
		}
	}

	private void PlayTransformEffect(DuelScene_CDC cardView)
	{
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(cardView.Model);
		_assetLookupSystem.Blackboard.CardHolder = cardView.CurrentCardHolder;
		TransformSFX transformSFX = null;
		TransformVFX transformVFX = null;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<TransformSFX> loadedTree))
		{
			transformSFX = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<TransformVFX> loadedTree2))
		{
			transformVFX = loadedTree2.GetPayload(_assetLookupSystem.Blackboard);
		}
		if (transformVFX != null)
		{
			_vfxProvider.PlayVFX(transformVFX.VfxData, cardView.Model);
		}
		if (transformSFX != null)
		{
			AudioManager.PlayAudio(transformSFX.SfxData.AudioEvents, _stack.Get().gameObject);
		}
	}

	private void PlayTappedEffect(DuelScene_CDC cardView, MtgGameState gameState, AssetCache<SplineMovementData> splineCache)
	{
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(cardView.Model);
		_assetLookupSystem.Blackboard.CardHolderType = cardView.Model.ZoneType.ToCardHolderType();
		HashSet<ReferenceMap.Reference> results = null;
		gameState.ReferenceMap.GetReferences(cardView.InstanceId, ReferenceMap.ReferenceType.Triggered, 0u, ref results);
		foreach (ReferenceMap.Reference item in results.Where((ReferenceMap.Reference x) => gameState.TryGetCard(x.B, out var card) && card.ObjectType == GameObjectType.Ability && card.ParentId == AffectorId))
		{
			_assetLookupSystem.Blackboard.LinkInfo = new LinkInfoData(0u, LinkType.Tap, item.B, cardView.InstanceId);
			AssetLookupTree<BirthSFX> assetLookupTree = _assetLookupSystem.TreeLoader.LoadTree<BirthSFX>(returnNewTree: false);
			AssetLookupTree<BirthVFX> assetLookupTree2 = _assetLookupSystem.TreeLoader.LoadTree<BirthVFX>(returnNewTree: false);
			AssetLookupTree<SustainSFX> assetLookupTree3 = _assetLookupSystem.TreeLoader.LoadTree<SustainSFX>(returnNewTree: false);
			AssetLookupTree<SustainVFX> assetLookupTree4 = _assetLookupSystem.TreeLoader.LoadTree<SustainVFX>(returnNewTree: false);
			AssetLookupTree<HitSFX> assetLookupTree5 = _assetLookupSystem.TreeLoader.LoadTree<HitSFX>(returnNewTree: false);
			AssetLookupTree<HitVFX> assetLookupTree6 = _assetLookupSystem.TreeLoader.LoadTree<HitVFX>(returnNewTree: false);
			AssetLookupTree<SplinePayload> assetLookupTree7 = _assetLookupSystem.TreeLoader.LoadTree<SplinePayload>(returnNewTree: false);
			AssetLookupTree<SplineOffsetsPayload> assetLookupTree8 = _assetLookupSystem.TreeLoader.LoadTree<SplineOffsetsPayload>(returnNewTree: false);
			BirthSFX birthSFX = assetLookupTree?.GetPayload(_assetLookupSystem.Blackboard);
			BirthVFX birthVFX = assetLookupTree2?.GetPayload(_assetLookupSystem.Blackboard);
			SustainSFX sustainSFX = assetLookupTree3?.GetPayload(_assetLookupSystem.Blackboard);
			SustainVFX sustainVFX = assetLookupTree4?.GetPayload(_assetLookupSystem.Blackboard);
			HitSFX hitSFX = assetLookupTree5?.GetPayload(_assetLookupSystem.Blackboard);
			HitVFX hitVFX = assetLookupTree6?.GetPayload(_assetLookupSystem.Blackboard);
			SplinePayload splinePayload = assetLookupTree7?.GetPayload(_assetLookupSystem.Blackboard);
			SplineOffsetsPayload splineOffsetsPayload = assetLookupTree8?.GetPayload(_assetLookupSystem.Blackboard);
			GameObject projectile = null;
			SplineEventData splineEventData = null;
			if (birthSFX != null)
			{
				getOrCreateProjectile(ref projectile, cardView, splineOffsetsPayload);
				getOrCreateEvents(ref splineEventData).Events.Add(new SplineEventAudio(0f, birthSFX.SfxData.AudioEvents, projectile));
			}
			if (birthVFX != null)
			{
				getOrCreateProjectile(ref projectile, cardView, splineOffsetsPayload);
				getOrCreateEvents(ref splineEventData).Events.Add(new SplineEventCallbackWithParams<(ICardDataAdapter, VfxPrefabData, GameObject, IVfxProvider)>(0f, (cardView.Model, birthVFX.VfxData.PrefabData, projectile, _vfxProvider), (float _, (ICardDataAdapter model, VfxPrefabData prefab, GameObject projObj, IVfxProvider vfxProvider) paramBlob) =>
				{
					paramBlob.vfxProvider.PlayVFX(new VfxData
					{
						PrefabData = paramBlob.prefab,
						SpaceData = 
						{
							Space = RelativeSpace.Local
						},
						ParentToSpace = true
					}, paramBlob.model, paramBlob.model.Instance);
				}));
			}
			if (sustainSFX != null)
			{
				getOrCreateProjectile(ref projectile, cardView, splineOffsetsPayload);
				getOrCreateEvents(ref splineEventData).Events.Add(new SplineEventAudio(0f, sustainSFX.SfxData.AudioEvents, projectile));
			}
			if (sustainVFX != null)
			{
				getOrCreateProjectile(ref projectile, cardView, splineOffsetsPayload);
				getOrCreateEvents(ref splineEventData).Events.Add(new SplineEventCallbackWithParams<(ICardDataAdapter, VfxPrefabData, GameObject, IVfxProvider)>(0f, (cardView.Model, sustainVFX.PrefabData, projectile, _vfxProvider), (float _, (ICardDataAdapter model, VfxPrefabData prefab, GameObject projObj, IVfxProvider vfxProvider) paramBlob) =>
				{
					paramBlob.vfxProvider.PlayVFX(new VfxData
					{
						PrefabData = paramBlob.prefab,
						SpaceData = 
						{
							Space = RelativeSpace.Local
						},
						ParentToSpace = true
					}, paramBlob.model, paramBlob.model.Instance, paramBlob.projObj.transform);
				}));
			}
			if (hitSFX != null)
			{
				getOrCreateProjectile(ref projectile, cardView, splineOffsetsPayload);
				getOrCreateEvents(ref splineEventData).Events.Add(new SplineEventAudio(1f, hitSFX.SfxData.AudioEvents, projectile));
			}
			if (hitVFX != null && _cardViewProvider.TryGetCardView(AffectorId, out var cardView2))
			{
				getOrCreateProjectile(ref projectile, cardView, splineOffsetsPayload);
				getOrCreateEvents(ref splineEventData).Events.Add(new SplineEventCallbackWithParams<(ICardDataAdapter, VfxPrefabData, GameObject, IVfxProvider)>(1f, (cardView2.Model, hitVFX.VfxData.PrefabData, projectile, _vfxProvider), (float _, (ICardDataAdapter model, VfxPrefabData prefab, GameObject projObj, IVfxProvider vfxProvider) paramBlob) =>
				{
					paramBlob.vfxProvider.PlayVFX(new VfxData
					{
						PrefabData = paramBlob.prefab,
						SpaceData = 
						{
							Space = RelativeSpace.Local
						},
						ParentToSpace = false
					}, paramBlob.model, paramBlob.model.Instance);
				}));
			}
			if ((bool)projectile)
			{
				getOrCreateEvents(ref splineEventData).Events.Add(new SplineEventCallback(1f, (float _) =>
				{
					projectile.AddOrGetComponent<SelfCleanup>().SetLifetime(0.1f, SelfCleanup.CleanupType.Destroy, onlyWhenChildless: true);
				}));
				SplineMovementData splineMovementData = null;
				string text = splinePayload?.SplineDataRef.RelativePath;
				if (!string.IsNullOrEmpty(text))
				{
					splineMovementData = splineCache.Get(text);
				}
				if (splineMovementData == null)
				{
					splineMovementData = ScriptableObject.CreateInstance<SplineMovementData>();
					splineMovementData.Spline = SplineData.Parabolic;
				}
				IdealPoint endPoint = default;
				if (_cardViewProvider.TryGetCardView(AffectorId, out cardView2))
				{
					endPoint = new IdealPoint(cardView2.EffectsRoot.position, Quaternion.identity, Vector3.one);
				}
				if (splineOffsetsPayload != null)
				{
					endPoint.Position += splineOffsetsPayload.EndOffset;
				}
				_splineMovementSystem.AddTemporaryGoal(projectile.transform, endPoint, allowInteractions: false, splineMovementData, splineEventData);
			}
		}
		PlayTappedVFX(cardView);
		static SplineEventData getOrCreateEvents(ref SplineEventData reference)
		{
			reference = reference ?? new SplineEventData();
			return reference;
		}
		static GameObject getOrCreateProjectile(ref GameObject reference, DuelScene_CDC duelScene_CDC, SplineOffsetsPayload offsets)
		{
			if (reference == null)
			{
				reference = new GameObject("Projectile");
				reference.transform.position = duelScene_CDC.EffectsRoot.position + (offsets?.StartOffset ?? default(Vector3));
			}
			return reference;
		}
	}

	private void PlayTappedVFX(DuelScene_CDC cardView)
	{
		if (!cardView.Model.IsTapped)
		{
			return;
		}
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(cardView.Model);
		if (_cardViewProvider.TryGetCardView(AffectorId, out var cardView2))
		{
			_assetLookupSystem.Blackboard.AffectorCardData = cardView2.Model;
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<TappedCardVFX> loadedTree))
		{
			TappedCardVFX payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null)
			{
				_vfxProvider.PlayAnchoredVFX(payload.VfxData, payload.AnchorPointType, cardView.ActiveScaffold, cardView.Model, cardView.Model.Instance, cardView.EffectsRoot);
			}
		}
	}

	private bool ShouldCheckForNHC(CardData cardData)
	{
		return cardData.ZoneType switch
		{
			ZoneType.Hand => true, 
			ZoneType.Library => true, 
			ZoneType.Graveyard => true, 
			ZoneType.Exile => true, 
			_ => false, 
		};
	}

	protected override void Cleanup()
	{
		_stack.ClearCache();
		_battlefield.ClearCache();
		base.Cleanup();
	}

	public override string ToString()
	{
		return string.Format("CardChange: {0} {1}", Property, (NewInstance == null) ? "NULL" : NewInstance.InstanceId.ToString());
	}
}
