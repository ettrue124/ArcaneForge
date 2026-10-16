using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.LifeChange;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtga.Extensions;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class LifeTotalUpdateUXEvent : LifeTotalUpdateUXEventBase
{
	public readonly uint AffectedId;

	private readonly DuelScene_AvatarView _avatar;

	private readonly DuelScene_CDC _affectorCardView;

	private readonly IVfxProvider _vfxProvider;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly IAssetCacheProvider<SplineMovementData> _splineCache;

	private readonly ILifeTotalController _lifeTotalController;

	private readonly AssetLookupSystem _assetLookupSystem;

	private LifeChangeProjectile _projectile;

	public override bool IsBlocking => _projectile != null;

	public int Change { get; private set; }

	public LifeTotalUpdateUXEvent(MtgCardInstance affector, uint affected, int change, ICardDatabaseAdapter cardDatabase, IEntityViewProvider entityViewProvider, ISplineMovementSystem splineMovementSystem, IAssetCacheProvider<SplineMovementData> splineCache, ILifeTotalController lifeTotalController, IVfxProvider vfxProvider, AssetLookupSystem assetLookupSystem)
		: base(affector, cardDatabase)
	{
		_avatar = entityViewProvider.GetAvatarById(affected);
		_affectorCardView = entityViewProvider.GetCardView(affector.InstanceId);
		_splineMovementSystem = splineMovementSystem;
		_lifeTotalController = lifeTotalController;
		_vfxProvider = vfxProvider;
		_splineCache = splineCache;
		_assetLookupSystem = assetLookupSystem;
		AffectedId = affected;
		Change = change;
	}

	public LifeTotalUpdateUXEvent(uint affector, uint affected, int change)
		: base(affector)
	{
		AffectedId = affected;
		Change = change;
	}

	public void SetChangeValue(int change)
	{
		Change = change;
	}

	public override void Execute()
	{
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(_affectorCard);
		_assetLookupSystem.Blackboard.CardHolderType = _affectorCard.ZoneType.ToCardHolderType();
		_assetLookupSystem.Blackboard.LifeChange = Change;
		_projectile = LoadProjectile(_assetLookupSystem);
		LifeChangeVfx vfxPayload = LoadVfx(_assetLookupSystem);
		LifeChangeSfx sfxPayload = LoadSfx(_assetLookupSystem);
		if (_projectile != null && _affectorCardView != null)
		{
			PlayProjectileEffects(_projectile, vfxPayload, sfxPayload);
			return;
		}
		PlayLifeChangeEffects(vfxPayload, sfxPayload);
		ApplyLifeChange();
		Complete();
	}

	private void PlayLifeChangeEffects(LifeChangeVfx vfxPayload, LifeChangeSfx sfxPayload)
	{
		if (vfxPayload != null)
		{
			PlayVfx(vfxPayload);
		}
		if (sfxPayload != null)
		{
			AudioManager.PlayAudio(sfxPayload.SfxData.AudioEvents, _avatar.gameObject);
		}
	}

	private LifeChangeVfx LoadVfx(AssetLookupSystem assetLookupSystem)
	{
		if (Change > 0)
		{
			return assetLookupSystem.TreeLoader.LoadTree<LifeGainVfx>().GetPayload(assetLookupSystem.Blackboard);
		}
		if (Change < 0)
		{
			return assetLookupSystem.TreeLoader.LoadTree<LifeLossVfx>().GetPayload(assetLookupSystem.Blackboard);
		}
		return null;
	}

	private LifeChangeProjectile LoadProjectile(AssetLookupSystem assetLookupSystem)
	{
		if (Change < 0)
		{
			return assetLookupSystem.TreeLoader.LoadTree<LifeLossProjectile>().GetPayload(assetLookupSystem.Blackboard);
		}
		return null;
	}

	private LifeChangeSfx LoadSfx(AssetLookupSystem assetLookupSystem)
	{
		if (Change > 0)
		{
			return assetLookupSystem.TreeLoader.LoadTree<LifeGainSfx>().GetPayload(assetLookupSystem.Blackboard);
		}
		if (Change < 0)
		{
			return assetLookupSystem.TreeLoader.LoadTree<LifeLossSfx>().GetPayload(assetLookupSystem.Blackboard);
		}
		return null;
	}

	private void PlayVfx(LifeChangeVfx vfxPayload)
	{
		foreach (VfxData vfxData in vfxPayload.VfxDatas)
		{
			_vfxProvider.PlayVFX(vfxData, _affectorCard, _avatar.Model, _avatar.LifeTextTransform);
		}
	}

	private void PlayProjectileEffects(LifeChangeProjectile projectile, LifeChangeVfx vfxPayload, LifeChangeSfx sfxPayload)
	{
		_lifeChangeDelay = projectile.Duration;
		Transform transform = new GameObject("Projectile").transform;
		transform.position = _affectorCardView.EffectsRoot.position;
		transform.position += projectile.StartOffset;
		SplineMovementData splineMovementData = null;
		string path = projectile.SplineRef?.RelativePath;
		if (!string.IsNullOrEmpty(projectile.SplineRef?.RelativePath))
		{
			splineMovementData = _splineCache.Get(path);
		}
		if (splineMovementData == null)
		{
			splineMovementData = ScriptableObject.CreateInstance<SplineMovementData>();
			splineMovementData.Spline = SplineData.Parabolic;
		}
		SplineEventData splineEventData = new SplineEventData();
		if (_projectile.BirthVFX != null)
		{
			addVfx(splineEventData.Events, _projectile.BirthVFX, _affectorCard, transform, _vfxProvider, parentToSpace: false);
		}
		if (_projectile.SustainVFX != null)
		{
			addVfx(splineEventData.Events, _projectile.SustainVFX, _affectorCard, transform, _vfxProvider, parentToSpace: true);
		}
		if (_projectile.HitVFX != null)
		{
			addVfx(splineEventData.Events, _projectile.HitVFX, _affectorCard, transform, _vfxProvider, parentToSpace: false);
		}
		splineEventData.Events.Add(new SplineEventCallbackWithParams<(Transform, LifeTotalUpdateUXEvent, LifeChangeVfx, LifeChangeSfx)>(1f, (transform, this, vfxPayload, sfxPayload), (float _, (Transform, LifeTotalUpdateUXEvent, LifeChangeVfx, LifeChangeSfx) paramBlob) =>
		{
			var (transform2, lifeTotalUpdateUXEvent, vfxPayload2, sfxPayload2) = paramBlob;
			transform2.gameObject.AddOrGetComponent<SelfCleanup>().SetLifetime(0.1f, SelfCleanup.CleanupType.Destroy, onlyWhenChildless: true);
			lifeTotalUpdateUXEvent.PlayLifeChangeEffects(vfxPayload2, sfxPayload2);
		}));
		IdealPoint endPoint = new IdealPoint(_avatar.EffectsRoot.position, Quaternion.identity, Vector3.one);
		endPoint.Position += _projectile.EndOffset;
		_splineMovementSystem.AddTemporaryGoal(transform, endPoint, allowInteractions: false, splineMovementData, splineEventData);
		static void addVfx(List<SplineEvent> splineEvents, VfxPrefabData vfx, ICardDataAdapter card, Transform item, IVfxProvider provider, bool parentToSpace)
		{
			splineEvents.Add(new SplineEventCallbackWithParams<(VfxPrefabData, ICardDataAdapter, Transform, IVfxProvider, bool)>(vfx.StartTime, (vfx, card, item, provider, parentToSpace), (float _, (VfxPrefabData, ICardDataAdapter, Transform, IVfxProvider, bool) paramBlob) =>
			{
				var (prefabData, cardDataAdapter, localSpaceOverride, vfxProvider, parentToSpace2) = paramBlob;
				vfxProvider.PlayVFX(new VfxData
				{
					PrefabData = prefabData,
					SpaceData = 
					{
						Space = RelativeSpace.Local
					},
					ParentToSpace = parentToSpace2
				}, cardDataAdapter, cardDataAdapter.Instance, localSpaceOverride);
			}));
		}
	}

	protected override void ApplyLifeChange()
	{
		_lifeTotalController.IncrementPlayerLife(AffectedId, Change);
	}
}
