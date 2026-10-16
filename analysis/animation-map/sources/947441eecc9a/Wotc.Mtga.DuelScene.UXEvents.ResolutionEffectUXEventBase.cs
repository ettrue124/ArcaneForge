using System;
using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.Resolution;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtga.Extensions;

namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class ResolutionEffectUXEventBase : UXEvent
{
	private readonly Data _data;

	private readonly IReadOnlyCollection<VFX_Base> _vfxPayloads;

	private readonly IReadOnlyCollection<SFX_Base> _sfxPayloads;

	private readonly Projectile _projectile;

	private readonly float _defaultDuration;

	private readonly float _duration;

	protected readonly IResolutionEffectController _resolutionEffectController;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly IAssetCacheProvider<SplineMovementData> _splineCache;

	private readonly IVfxProvider _vfxProvider;

	public uint InstigatorInstanceId => Instigator.InstanceId;

	public MtgCardInstance Instigator { get; }

	public CardPrintingData CardPrinting { get; }

	public AbilityPrintingData AbilityPrinting { get; }

	public ICardDataAdapter InstigatorModel => CardDataExtensions.CreateWithDatabase(Instigator, _cardDatabase);

	public bool IgnoreDamageEvents
	{
		get
		{
			if (_data != null)
			{
				return _data.IgnoreDamageEvents;
			}
			return false;
		}
	}

	public bool IgnoreCoinFlipEvents
	{
		get
		{
			if (_data != null)
			{
				return _data.IgnoreCoinFlipEvents;
			}
			return false;
		}
	}

	public override bool IsBlocking
	{
		get
		{
			if (_data != null)
			{
				return _data.BlocksEvents;
			}
			return true;
		}
	}

	public override bool HasWeight
	{
		get
		{
			if (_data != null)
			{
				return _data.BlocksWorkflows;
			}
			return false;
		}
	}

	protected ResolutionEffectUXEventBase()
	{
	}

	protected ResolutionEffectUXEventBase(IResolutionEffectController resolutionEffectController, ICardDatabaseAdapter cardDatabase, ISplineMovementSystem splineMovementSystem, IAssetCacheProvider<SplineMovementData> splineCache, IVfxProvider vfxProvider, ICardHolderProvider cardHolderProvider, IEntityViewProvider entityViewProvider, MtgCardInstance instigator, CardPrintingData cardPrinting, AbilityPrintingData abilityPrinting, Data resolutionData, IReadOnlyCollection<VFX_Base> vfx, IReadOnlyCollection<SFX_Base> sfx, Projectile projectile, float defaultDuration, float duration)
	{
		_canTimeOut = false;
		_resolutionEffectController = resolutionEffectController ?? NullResolutionEffectController.Default;
		_cardDatabase = cardDatabase;
		_splineMovementSystem = splineMovementSystem;
		_splineCache = splineCache;
		_vfxProvider = vfxProvider;
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderProvider);
		_entityViewProvider = entityViewProvider;
		_data = resolutionData;
		_vfxPayloads = vfx ?? Array.Empty<VFX_Base>();
		_sfxPayloads = sfx ?? Array.Empty<SFX_Base>();
		_projectile = projectile;
		_defaultDuration = defaultDuration;
		_duration = duration;
		Instigator = instigator;
		CardPrinting = cardPrinting;
		AbilityPrinting = abilityPrinting;
	}

	protected ResolutionEffectUXEventBase(MtgCardInstance instigator)
	{
		Instigator = instigator;
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		foreach (UXEvent currentlyRunningEvent in currentlyRunningEvents)
		{
			if (currentlyRunningEvent.HasWeight || currentlyRunningEvent.IsBlocking)
			{
				return false;
			}
		}
		return true;
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if (!IsComplete)
		{
			float num = _duration;
			if ((bool)CardHoverController.HoveredCard && CardHoverController.HoveredCard.CurrentCardHolder.CardHolderType == CardHolderType.Stack)
			{
				num = _defaultDuration;
			}
			if (_timeRunning > num)
			{
				Complete();
			}
		}
	}

	public override void Execute()
	{
		PlayVfx(_vfxPayloads, InstigatorModel);
		PlaySfx(_sfxPayloads, _stack.Get().gameObject);
		PlayProjectileVFX(_projectile);
	}

	public override IEnumerable<uint> GetInvolvedIds()
	{
		yield return InstigatorInstanceId;
	}

	private void PlayVfx(IEnumerable<VFX_Base> visualEffects, ICardDataAdapter effectContext)
	{
		foreach (VFX_Base visualEffect in visualEffects)
		{
			foreach (VfxData vfxData in visualEffect.VfxDatas)
			{
				Transform localSpaceOverride = null;
				DuelScene_AvatarView avatar;
				if (_entityViewProvider.TryGetCardView(Instigator.InstanceId, out var cardView))
				{
					localSpaceOverride = cardView.EffectsRoot;
				}
				else if (_entityViewProvider.TryGetAvatarById(Instigator.InstanceId, out avatar))
				{
					localSpaceOverride = avatar.TargetTransform;
				}
				_vfxProvider.PlayVFX(vfxData, effectContext, Instigator, localSpaceOverride);
			}
		}
	}

	private static void PlaySfx(IEnumerable<SFX_Base> soundEffects, GameObject target)
	{
		foreach (SFX_Base soundEffect in soundEffects)
		{
			AudioManager.PlayAudio(soundEffect.SfxData.AudioEvents, target);
		}
	}

	private void PlayProjectileVFX(Projectile projectile)
	{
		if (projectile == null)
		{
			return;
		}
		ICardDataAdapter instigatorModel = InstigatorModel;
		OffsetData offset = projectile.BirthVfx.Offset;
		OffsetData offset2 = projectile.HitVfx.Offset;
		float time = ((projectile.BirthVfx != null) ? projectile.BirthVfx.PrefabData.StartTime : 0f);
		float time2 = ((projectile.SustainVfx != null) ? projectile.SustainVfx.PrefabData.StartTime : 0f);
		float time3 = ((projectile.HitVfx != null) ? projectile.HitVfx.PrefabData.StartTime : 1f);
		foreach (Transform item3 in _vfxProvider.ResolveSpaceIntoTransforms(projectile.BirthVfx.SpaceData.Space, Instigator, null, projectile.BirthVfx.PlayOnStackChildren))
		{
			foreach (Transform item4 in _vfxProvider.ResolveSpaceIntoTransforms(projectile.HitVfx.SpaceData.Space, Instigator, null, projectile.HitVfx.PlayOnStackChildren))
			{
				Transform transform = new GameObject($"Resolution Projectile: #{Instigator.InstanceId} (title {Instigator.TitleId})").transform;
				transform.SetParent(item3);
				transform.localPosition = offset.PositionOffset;
				transform.localEulerAngles = offset.RotationOffset;
				transform.localScale = offset.ScaleMultiplier;
				transform.SetParent(null);
				SplineEventData splineEventData = new SplineEventData();
				AudioManager.SetSwitch("color", AudioManager.Instance.GetColorKey(instigatorModel.PresentationColor), transform.gameObject);
				if (projectile.BirthSfx != null)
				{
					splineEventData.Events.Add(new SplineEventAudio(time, projectile.BirthSfx.AudioEvents, transform.gameObject));
				}
				if (projectile.BirthVfx != null)
				{
					Transform item = item3;
					splineEventData.Events.Add(new SplineEventCallbackWithParams<(IVfxProvider, VfxData, Transform, ICardDataAdapter)>(time, (_vfxProvider, projectile.BirthVfx, item, instigatorModel), (float _, (IVfxProvider, VfxData, Transform, ICardDataAdapter) paramBlob) =>
					{
						var (vfxProvider, vfxData, localSpaceOverride, cardDataAdapter) = paramBlob;
						vfxProvider.PlayVFX(vfxData, cardDataAdapter, cardDataAdapter.Instance, localSpaceOverride);
					}));
				}
				if (projectile.SustainSfx != null)
				{
					splineEventData.Events.Add(new SplineEventAudio(time2, projectile.SustainSfx.AudioEvents, transform.gameObject));
				}
				if (projectile.SustainVfx != null)
				{
					splineEventData.Events.Add(new SplineEventCallbackWithParams<(IVfxProvider, VfxData, Transform, ICardDataAdapter)>(time2, (_vfxProvider, projectile.SustainVfx, transform, instigatorModel), (float _, (IVfxProvider, VfxData, Transform, ICardDataAdapter) paramBlob) =>
					{
						var (vfxProvider, vfxData, localSpaceOverride, cardDataAdapter) = paramBlob;
						vfxProvider.PlayVFX(vfxData, cardDataAdapter, cardDataAdapter.Instance, localSpaceOverride);
					}));
				}
				if (projectile.HitSfx != null)
				{
					splineEventData.Events.Add(new SplineEventAudio(time3, projectile.HitSfx.AudioEvents, item4.gameObject));
				}
				if (projectile.HitVfx != null)
				{
					Transform item2 = item4;
					splineEventData.Events.Add(new SplineEventCallbackWithParams<(IVfxProvider, VfxData, Transform, ICardDataAdapter)>(time3, (_vfxProvider, projectile.HitVfx, item2, instigatorModel), (float _, (IVfxProvider, VfxData, Transform, ICardDataAdapter) paramBlob) =>
					{
						var (vfxProvider, vfxData, localSpaceOverride, cardDataAdapter) = paramBlob;
						vfxProvider.PlayVFX(vfxData, cardDataAdapter, cardDataAdapter.Instance, localSpaceOverride);
					}));
				}
				splineEventData.Events.Add(new SplineEventCallbackWithParams<Transform>(1f, transform, (float _, Transform innerTransform) =>
				{
					innerTransform.gameObject.AddOrGetComponent<SelfCleanup>().SetLifetime(0.1f, SelfCleanup.CleanupType.Destroy, onlyWhenChildless: true);
				}));
				SplineMovementData splineMovementData = null;
				string text = projectile.SplineRef?.RelativePath;
				if (!string.IsNullOrEmpty(text))
				{
					splineMovementData = _splineCache.Get(text);
				}
				if (splineMovementData == null)
				{
					splineMovementData = ScriptableObject.CreateInstance<SplineMovementData>();
					splineMovementData.Spline = SplineData.Parabolic;
				}
				IdealPoint endPoint = new IdealPoint(item4);
				endPoint.Position += item4.TransformVector(offset2.PositionOffset);
				endPoint.Rotation *= Quaternion.Euler(offset2.RotationOffset);
				endPoint.Scale = Vector3.Scale(endPoint.Scale, offset2.ScaleMultiplier);
				_splineMovementSystem.AddTemporaryGoal(transform, endPoint, allowInteractions: false, splineMovementData, splineEventData);
			}
		}
	}

	protected override void Cleanup()
	{
		_stack.ClearCache();
		base.Cleanup();
	}
}
