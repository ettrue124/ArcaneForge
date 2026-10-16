using System;
using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.Combat;
using AssetLookupTree.Payloads.Projectile;
using AssetLookupTree.Payloads.ZoneTransfer;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using Pooling;
using UnityEngine;
using Wotc.Mtga.Duel;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

public class CombatAnimationPlayer : IDisposable
{
	private readonly IUnityObjectPool _unityPool;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly IVfxProvider _vfxProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IAssetCacheProvider<SplineMovementData> _splineCache;

	private readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	public readonly List<Transform> SourceTransformsInCombat = new List<Transform>(10);

	public bool IsPlaying => SourceTransformsInCombat.Count > 0;

	public static event Action<MtgCardInstance, int> DamageDealtByCard;

	public event Action<int> DamageDealt;

	public event Action<uint> PlayerDamaged;

	public event System.Action OpponentDamaged;

	public CombatAnimationPlayer(IUnityObjectPool unityPool, IVfxProvider vfxProvider, ISplineMovementSystem splineMovementSystem, IAssetCacheProvider<SplineMovementData> splineCacheProvider, ICardHolderProvider cardHolderProvider, AssetLookupSystem assetLookupSystem)
	{
		_unityPool = unityPool ?? NullUnityObjectPool.Default;
		_vfxProvider = vfxProvider ?? NullVfxProvider.Default;
		_splineMovementSystem = splineMovementSystem;
		_splineCache = splineCacheProvider;
		_battlefield = CardHolderReference<IBattlefieldCardHolder>.Battlefield(cardHolderProvider);
		_assetLookupSystem = assetLookupSystem;
	}

	public bool PlayDamageEffect(DuelScene_CDC sourceCDC, DamageTargetInfo damageInfo, System.Action callback, bool suppressProjectileDamageFX = false)
	{
		if (sourceCDC == null || damageInfo == null)
		{
			Debug.LogErrorFormat("Tried to PlayDamageEffect with invalid parameters. sourceCDC: {0}, damageInfo: {1}", sourceCDC, damageInfo);
			return false;
		}
		DamageType damageType = damageInfo.DamageType;
		if (damageType == DamageType.Combat || damageType == DamageType.Fight)
		{
			if (SourceTransformsInCombat.Contains(sourceCDC.Root))
			{
				Debug.LogErrorFormat("Tried to PlayDamageEffect for a source already playing a damage effect. sourceCDC: {0}, damageInfo: {1}", sourceCDC, damageInfo);
				return false;
			}
			SourceTransformsInCombat.Add(sourceCDC.Root);
			PlayCombatEffect(sourceCDC, damageInfo, OnHit);
		}
		else
		{
			SourceTransformsInCombat.Add(sourceCDC.Root);
			PlayProjectileEffect(sourceCDC, damageInfo, OnHit, suppressProjectileDamageFX);
		}
		return true;
		void OnHit()
		{
			SourceTransformsInCombat.Remove(sourceCDC.Root);
			callback?.Invoke();
			if (!suppressProjectileDamageFX)
			{
				if (damageInfo.DamageDealtToPlayer)
				{
					PlayerDamaged?.Invoke(damageInfo.PlayerInstanceId);
				}
				if (damageInfo.DamagedPlayerWasOpponent)
				{
					OpponentDamaged?.Invoke();
				}
				DamageDealt?.Invoke(damageInfo.DamageDealt);
				DamageDealtByCard?.Invoke(sourceCDC.Model.Instance, damageInfo.DamageDealt);
			}
		}
	}

	private void PlayCombatEffect(DuelScene_CDC sourceCDC, DamageTargetInfo damageInfo, System.Action callback)
	{
		IBattlefieldCardHolder battlefield = _battlefield.Get();
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(sourceCDC.Model);
		_assetLookupSystem.Blackboard.CardHolderType = CardHolderType.Battlefield;
		_assetLookupSystem.Blackboard.DamageAmount = damageInfo.DamageDealt;
		_assetLookupSystem.Blackboard.DamageType = damageInfo.DamageType;
		_assetLookupSystem.Blackboard.DamageRecipientEntity = damageInfo.TargetEntity;
		SFXBirth sFXBirth = null;
		SFXHit sFXHit = null;
		SFXSustain sFXSustain = null;
		SFXAttackType sFXAttackType = null;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SFXBirth> loadedTree))
		{
			sFXBirth = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SFXHit> loadedTree2))
		{
			sFXHit = loadedTree2.GetPayload(_assetLookupSystem.Blackboard);
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SFXSustain> loadedTree3))
		{
			sFXSustain = loadedTree3.GetPayload(_assetLookupSystem.Blackboard);
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SFXAttackType> loadedTree4))
		{
			sFXAttackType = loadedTree4.GetPayload(_assetLookupSystem.Blackboard);
		}
		VFXBirth vFXBirth = null;
		VFXHit vFXHit = null;
		VFXSustain vFXSustain = null;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<VFXBirth> loadedTree5))
		{
			vFXBirth = loadedTree5.GetPayload(_assetLookupSystem.Blackboard);
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<VFXHit> loadedTree6))
		{
			vFXHit = loadedTree6.GetPayload(_assetLookupSystem.Blackboard);
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<VFXSustain> loadedTree7))
		{
			vFXSustain = loadedTree7.GetPayload(_assetLookupSystem.Blackboard);
		}
		Spline spline = null;
		SplineOffsets splineOffsets = null;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<Spline> loadedTree8))
		{
			spline = loadedTree8.GetPayload(_assetLookupSystem.Blackboard);
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SplineOffsets> loadedTree9))
		{
			splineOffsets = loadedTree9.GetPayload(_assetLookupSystem.Blackboard);
		}
		_assetLookupSystem.Blackboard.Clear();
		SplineEventData splineEventData = new SplineEventData();
		AudioManager.SetSwitch("color", AudioManager.Instance.GetColorKey(sourceCDC.Model.PresentationColor), sourceCDC.EffectsRoot.gameObject);
		if (sFXBirth != null)
		{
			splineEventData.Events.Add(new SplineEventAudio(0f, sFXBirth.AudioEvents, sourceCDC.EffectsRoot.gameObject));
		}
		if (vFXBirth != null && vFXBirth.VfxData != null && vFXBirth.VfxData.Prefabs != null)
		{
			splineEventData.Events.Add(new SplineEventObject(0f, vFXBirth.VfxData.CleanUpAfterSeconds, vFXBirth.VfxData.Prefabs.SelectRandom().RelativePath, vFXBirth.VfxData.Follow, Vector3.zero, Vector3.zero));
		}
		if (sFXSustain != null)
		{
			splineEventData.Events.Add(new SplineEventAudio(0f, sFXSustain.AudioEvents, sourceCDC.EffectsRoot.gameObject));
		}
		if (vFXSustain != null && vFXSustain.VfxData != null && vFXSustain.VfxData.Prefabs != null)
		{
			splineEventData.Events.Add(new SplineEventObject(0f, 1f, vFXSustain.VfxData.Prefabs.SelectRandom().RelativePath, follow: true, Vector3.zero, Vector3.zero));
		}
		if (sFXHit != null)
		{
			splineEventData.Events.Add(new SplineEventAudio(1f, sFXHit.AudioEvents, sourceCDC.EffectsRoot.gameObject));
		}
		if (sFXAttackType != null)
		{
			splineEventData.Events.Add(new SplineEventAudio(1f, sFXAttackType.AudioEvents, sourceCDC.EffectsRoot.gameObject));
		}
		if (vFXHit != null && vFXHit.VfxData != null && vFXHit.VfxData.Prefabs != null)
		{
			splineEventData.Events.Add(new SplineEventObject(1f, vFXHit.VfxData.CleanUpAfterSeconds, vFXHit.VfxData.Prefabs.SelectRandom().RelativePath, vFXHit.VfxData.Follow, Vector3.zero, Vector3.zero, damageInfo.Transform.position + Vector3.up * 0.25f));
		}
		if (damageInfo.DamageDealt > 0 && damageInfo.DamageDealtToCard)
		{
			splineEventData.Events.Add(new SplineEventCallback(1f, (float _) =>
			{
				FlyingText.ShowCdcDamageText(damageInfo.DamageTextTransform.position, damageInfo.DamageDealt, _unityPool, _assetLookupSystem);
			}));
		}
		if (damageInfo.DamageDealt >= 0)
		{
			splineEventData.Events.Add(new SplineEventCallback(1f, (float _) =>
			{
				float value = 0f;
				if (damageInfo.DamageDealt >= 1 && damageInfo.DamageDealt <= 2)
				{
					value = 1f;
				}
				else if (damageInfo.DamageDealt >= 3 && damageInfo.DamageDealt <= 5)
				{
					value = 2f;
				}
				else if (damageInfo.DamageDealt >= 5)
				{
					value = 3f;
				}
				AudioManager.SetRTPCValue("hit_power", value, sourceCDC.EffectsRoot.gameObject);
				AudioManager.PlayAudio(WwiseEvents.sfx_combat_attacker_hit, sourceCDC.EffectsRoot.gameObject);
			}));
		}
		splineEventData.Events.Add(new SplineEventCallback(1f, (float _) =>
		{
			StopAttachmentsFromFollowing(sourceCDC);
			callback();
		}));
		sourceCDC.UpdateVisibility(shouldBeVisible: true);
		SplineMovementData splineMovementData = null;
		if (spline != null)
		{
			splineMovementData = _splineCache.Get(spline.SplineDataRef?.RelativePath);
		}
		if (splineMovementData == null)
		{
			splineMovementData = ScriptableObject.CreateInstance<SplineMovementData>();
			splineMovementData.Spline = SplineData.Parabolic;
		}
		IdealPoint endPoint = new IdealPoint(damageInfo.Transform.position + (splineOffsets?.EndOffset ?? Vector3.zero), damageInfo.Transform.rotation, sourceCDC.transform.localScale);
		_splineMovementSystem.AddTemporaryGoal(sourceCDC.Root, endPoint, allowInteractions: false, splineMovementData, splineEventData);
		IBattlefieldStack stackForCard = battlefield.GetStackForCard(sourceCDC);
		if (stackForCard == null || !stackForCard.HasAttachmentOrExile)
		{
			return;
		}
		CardLayoutData cardLayoutData = battlefield.PreviousLayoutData.Find((CardLayoutData x) => x.Card == sourceCDC);
		if (cardLayoutData == null)
		{
			return;
		}
		Quaternion quaternion = battlefield.Transform.rotation * cardLayoutData.Rotation;
		Matrix4x4 inverse = Matrix4x4.TRS(cardLayoutData.Position, quaternion, cardLayoutData.Scale).inverse;
		foreach (DuelScene_CDC cardView in GetAttachmentsForStack(stackForCard))
		{
			CardLayoutData cardLayoutData2 = battlefield.PreviousLayoutData.Find((CardLayoutData x) => x.Card == cardView);
			if (cardLayoutData2 != null)
			{
				Vector3 positionOffset = inverse.MultiplyPoint3x4(cardLayoutData2.Position);
				Quaternion rotationOffset = battlefield.Transform.rotation * cardLayoutData2.Rotation * Quaternion.Inverse(quaternion);
				Vector3 scaleOffset = new Vector3(cardLayoutData2.Scale.x / cardLayoutData.Scale.x, cardLayoutData2.Scale.y / cardLayoutData.Scale.y, cardLayoutData2.Scale.z / cardLayoutData.Scale.z);
				_splineMovementSystem.AddFollowTransform(sourceCDC.Root, cardLayoutData2.Card.Root, positionOffset, rotationOffset, scaleOffset);
			}
		}
		void StopAttachmentsFromFollowing(DuelScene_CDC source)
		{
			IBattlefieldStack stackForCard2 = battlefield.GetStackForCard(source);
			if (stackForCard2 != null && stackForCard2.HasAttachmentOrExile)
			{
				foreach (DuelScene_CDC item in GetAttachmentsForStack(stackForCard2))
				{
					if (!(item == null))
					{
						_splineMovementSystem.RemoveFollowTransform(item.Root);
					}
				}
			}
		}
	}

	private List<DuelScene_CDC> GetAttachmentsForStack(IBattlefieldStack sourceStack)
	{
		return sourceStack.StackedCards;
	}

	private void PlayProjectileEffect(DuelScene_CDC sourceCDC, DamageTargetInfo damageInfo, System.Action callback, bool suppressProjectileDamageFX)
	{
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(sourceCDC.Model);
		_assetLookupSystem.Blackboard.CardHolderType = sourceCDC.Model.ZoneType.ToCardHolderType();
		_assetLookupSystem.Blackboard.DamageAmount = damageInfo.DamageDealt;
		_assetLookupSystem.Blackboard.DamageType = damageInfo.DamageType;
		_assetLookupSystem.Blackboard.DamageRecipientEntity = damageInfo.TargetEntity;
		AssetLookupTree<AssetLookupTree.Payloads.Projectile.BirthSFX> assetLookupTree = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.Projectile.BirthSFX>(returnNewTree: false);
		AssetLookupTree<AssetLookupTree.Payloads.Projectile.HitSFX> assetLookupTree2 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.Projectile.HitSFX>(returnNewTree: false);
		AssetLookupTree<AssetLookupTree.Payloads.Projectile.SustainSFX> assetLookupTree3 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.Projectile.SustainSFX>(returnNewTree: false);
		AssetLookupTree<AssetLookupTree.Payloads.Projectile.BirthVFX> assetLookupTree4 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.Projectile.BirthVFX>(returnNewTree: false);
		AssetLookupTree<AssetLookupTree.Payloads.Projectile.HitVFX> assetLookupTree5 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.Projectile.HitVFX>(returnNewTree: false);
		AssetLookupTree<AssetLookupTree.Payloads.Projectile.SustainVFX> assetLookupTree6 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.Projectile.SustainVFX>(returnNewTree: false);
		AssetLookupTree<AssetLookupTree.Payloads.Projectile.SplinePayload> assetLookupTree7 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.Projectile.SplinePayload>(returnNewTree: false);
		AssetLookupTree<AssetLookupTree.Payloads.Projectile.SplineOffsetsPayload> assetLookupTree8 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.Projectile.SplineOffsetsPayload>(returnNewTree: false);
		AssetLookupTree.Payloads.Projectile.BirthSFX birthSFX = assetLookupTree?.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.Projectile.HitSFX hitSFX = assetLookupTree2?.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.Projectile.SustainSFX sustainSFX = assetLookupTree3?.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.Projectile.BirthVFX birthVFX = assetLookupTree4?.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.Projectile.HitVFX hitVFX = assetLookupTree5?.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.Projectile.SustainVFX sustainVFX = assetLookupTree6?.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.Projectile.SplinePayload splinePayload = assetLookupTree7?.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.Projectile.SplineOffsetsPayload splineOffsetsPayload = assetLookupTree8?.GetPayload(_assetLookupSystem.Blackboard);
		_assetLookupSystem.Blackboard.Clear();
		Transform projectileTransform = new GameObject("Projectile").transform;
		projectileTransform.position = sourceCDC.EffectsRoot.position;
		if (splineOffsetsPayload != null)
		{
			projectileTransform.position += splineOffsetsPayload.StartOffset;
		}
		SplineEventData splineEventData = new SplineEventData();
		AudioManager.SetSwitch("color", AudioManager.Instance.GetColorKey(sourceCDC.Model.PresentationColor), projectileTransform.gameObject);
		if (birthSFX != null)
		{
			splineEventData.Events.Add(new SplineEventAudio(0f, birthSFX.SfxData.AudioEvents, projectileTransform.gameObject));
		}
		if (birthVFX != null)
		{
			splineEventData.Events.Add(new SplineEventCallbackWithParams<(ICardDataAdapter, VfxData, Transform, IVfxProvider)>(0f, (sourceCDC.Model, birthVFX.VfxData, projectileTransform, _vfxProvider), (float _, (ICardDataAdapter model, VfxData vfxData, Transform projTran, IVfxProvider vfxProvider) paramBlob) =>
			{
				paramBlob.vfxProvider.PlayVFX(paramBlob.vfxData, paramBlob.model, paramBlob.model.Instance, paramBlob.projTran);
			}));
		}
		if (sustainSFX != null)
		{
			splineEventData.Events.Add(new SplineEventAudio(0f, sustainSFX.SfxData.AudioEvents, projectileTransform.gameObject));
		}
		if (sustainVFX != null)
		{
			splineEventData.Events.Add(new SplineEventCallbackWithParams<(ICardDataAdapter, VfxPrefabData, Transform, IVfxProvider)>(0f, (sourceCDC.Model, sustainVFX.PrefabData, projectileTransform, _vfxProvider), (float _, (ICardDataAdapter model, VfxPrefabData prefab, Transform projTran, IVfxProvider vfxProvider) paramBlob) =>
			{
				paramBlob.vfxProvider.PlayVFX(new VfxData
				{
					PrefabData = paramBlob.prefab,
					SpaceData = 
					{
						Space = RelativeSpace.Local
					},
					ParentToSpace = true
				}, paramBlob.model, paramBlob.model.Instance, paramBlob.projTran);
			}));
		}
		if (hitSFX != null)
		{
			splineEventData.Events.Add(new SplineEventAudio(1f, hitSFX.SfxData.AudioEvents, projectileTransform.gameObject));
		}
		if (hitVFX != null)
		{
			splineEventData.Events.Add(new SplineEventCallbackWithParams<(ICardDataAdapter, VfxData, Transform, IVfxProvider)>(1f, (sourceCDC.Model, hitVFX.VfxData, projectileTransform, _vfxProvider), (float _, (ICardDataAdapter model, VfxData vfxData, Transform projTran, IVfxProvider vfxProvider) paramBlob) =>
			{
				paramBlob.vfxProvider.PlayVFX(paramBlob.vfxData, paramBlob.model, paramBlob.model.Instance, paramBlob.projTran);
			}));
		}
		if (damageInfo.DamageDealt > 0 && damageInfo.DamageDealtToCard && !suppressProjectileDamageFX)
		{
			splineEventData.Events.Add(new SplineEventCallback(1f, (float _) =>
			{
				FlyingText.ShowCdcDamageText(damageInfo.DamageTextTransform.position, damageInfo.DamageDealt, _unityPool, _assetLookupSystem);
			}));
		}
		if (damageInfo.DamageDealt >= 0 && !suppressProjectileDamageFX)
		{
			splineEventData.Events.Add(new SplineEventCallback(1f, (float _) =>
			{
				float value = ((damageInfo.DamageDealt == 0) ? 0f : Mathf.Min(2f, Mathf.Ceil((float)damageInfo.DamageDealt / 3f)));
				AudioManager.SetRTPCValue("hit_power", value, projectileTransform.gameObject);
				AudioManager.PlayAudio(WwiseEvents.sfx_combat_attacker_hit, projectileTransform.gameObject);
			}));
		}
		splineEventData.Events.Add(new SplineEventCallback(1f, (float _) =>
		{
			callback();
			projectileTransform.gameObject.AddOrGetComponent<SelfCleanup>().SetLifetime(0.1f, SelfCleanup.CleanupType.Destroy, onlyWhenChildless: true);
		}));
		SplineMovementData splineMovementData = null;
		string text = splinePayload?.SplineDataRef.RelativePath;
		if (!string.IsNullOrEmpty(text))
		{
			splineMovementData = _splineCache.Get(text);
		}
		if (splineMovementData == null)
		{
			splineMovementData = ScriptableObject.CreateInstance<SplineMovementData>();
			splineMovementData.Spline = SplineData.Parabolic;
		}
		IdealPoint endPoint = new IdealPoint(damageInfo.Transform.position, Quaternion.identity, Vector3.one);
		if (splineOffsetsPayload != null)
		{
			endPoint.Position += splineOffsetsPayload.EndOffset;
		}
		_splineMovementSystem.AddTemporaryGoal(projectileTransform, endPoint, allowInteractions: false, splineMovementData, splineEventData);
	}

	public void PlayZoneTransferEffect(DuelScene_CDC sourceCDC, Transform target, ZoneTransferReason reason, ZonePair zonePair, System.Action callback)
	{
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(sourceCDC.Model);
		_assetLookupSystem.Blackboard.CardHolderType = CardHolderType.Battlefield;
		_assetLookupSystem.Blackboard.ZoneTransferReason = reason;
		_assetLookupSystem.Blackboard.ZonePair = zonePair;
		AssetLookupTree<AssetLookupTree.Payloads.ZoneTransfer.SplinePayload> assetLookupTree = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.ZoneTransfer.SplinePayload>();
		AssetLookupTree<AssetLookupTree.Payloads.ZoneTransfer.SplineOffsetsPayload> assetLookupTree2 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.ZoneTransfer.SplineOffsetsPayload>();
		AssetLookupTree<AssetLookupTree.Payloads.ZoneTransfer.BirthVFX> assetLookupTree3 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.ZoneTransfer.BirthVFX>();
		AssetLookupTree<AssetLookupTree.Payloads.ZoneTransfer.BirthSFX> assetLookupTree4 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.ZoneTransfer.BirthSFX>();
		AssetLookupTree<AssetLookupTree.Payloads.ZoneTransfer.SustainVFX> assetLookupTree5 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.ZoneTransfer.SustainVFX>();
		AssetLookupTree<AssetLookupTree.Payloads.ZoneTransfer.SustainSFX> assetLookupTree6 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.ZoneTransfer.SustainSFX>();
		AssetLookupTree<AssetLookupTree.Payloads.ZoneTransfer.HitVFX> assetLookupTree7 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.ZoneTransfer.HitVFX>();
		AssetLookupTree<AssetLookupTree.Payloads.ZoneTransfer.HitSFX> assetLookupTree8 = _assetLookupSystem.TreeLoader.LoadTree<AssetLookupTree.Payloads.ZoneTransfer.HitSFX>();
		AssetLookupTree.Payloads.ZoneTransfer.SplinePayload payload = assetLookupTree.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.ZoneTransfer.SplineOffsetsPayload payload2 = assetLookupTree2.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.ZoneTransfer.BirthVFX payload3 = assetLookupTree3.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.ZoneTransfer.BirthSFX payload4 = assetLookupTree4.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.ZoneTransfer.SustainVFX payload5 = assetLookupTree5.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.ZoneTransfer.SustainSFX payload6 = assetLookupTree6.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.ZoneTransfer.HitVFX payload7 = assetLookupTree7.GetPayload(_assetLookupSystem.Blackboard);
		AssetLookupTree.Payloads.ZoneTransfer.HitSFX payload8 = assetLookupTree8.GetPayload(_assetLookupSystem.Blackboard);
		_assetLookupSystem.Blackboard.Clear();
		if (payload4 != null || payload3 != null || payload5 != null || payload6 != null)
		{
			BuildZteSpline(sourceCDC, target, payload, payload2, payload3, payload4, payload5, payload6, payload7, payload8, callback);
		}
		else if (payload8 != null || payload7 != null)
		{
			PlayZteHitEffects(sourceCDC, target, payload7, payload8, callback);
		}
		else
		{
			callback();
		}
	}

	private void BuildZteSpline(DuelScene_CDC sourceCDC, Transform target, AssetLookupTree.Payloads.ZoneTransfer.SplinePayload splinePayload, AssetLookupTree.Payloads.ZoneTransfer.SplineOffsetsPayload splineOffsetsPayload, AssetLookupTree.Payloads.ZoneTransfer.BirthVFX birthVfxPayload, AssetLookupTree.Payloads.ZoneTransfer.BirthSFX birthSfxPayload, AssetLookupTree.Payloads.ZoneTransfer.SustainVFX sustainVfxPayload, AssetLookupTree.Payloads.ZoneTransfer.SustainSFX sustainSfxPayload, AssetLookupTree.Payloads.ZoneTransfer.HitVFX hitVfxPayload, AssetLookupTree.Payloads.ZoneTransfer.HitSFX hitSfxPayload, System.Action callback)
	{
		Transform projectileTransform = new GameObject("Projectile").transform;
		projectileTransform.position = sourceCDC.EffectsRoot.position;
		if (splineOffsetsPayload != null)
		{
			projectileTransform.position += splineOffsetsPayload.StartOffset;
		}
		AudioManager.SetSwitch("color", AudioManager.Instance.GetColorKey(sourceCDC.Model.PresentationColor), projectileTransform.gameObject);
		SplineEventData splineEventData = new SplineEventData();
		if (birthSfxPayload != null)
		{
			splineEventData.Events.Add(new SplineEventAudio(0f, birthSfxPayload.SfxData.AudioEvents, projectileTransform.gameObject));
		}
		if (birthVfxPayload != null)
		{
			foreach (VfxData vfxData in birthVfxPayload.VfxDatas)
			{
				splineEventData.Events.Add(new SplineEventPayloadVFXALT(vfxData, sourceCDC.Model, sourceCDC.EffectsRoot, _vfxProvider));
			}
		}
		if (sustainSfxPayload != null)
		{
			splineEventData.Events.Add(new SplineEventAudio(0f, sustainSfxPayload.SfxData.AudioEvents, projectileTransform.gameObject));
		}
		if (sustainVfxPayload != null)
		{
			splineEventData.Events.Add(new SplineEventCallbackWithParams<(ICardDataAdapter, VfxPrefabData, Transform, IVfxProvider)>(0f, (sourceCDC.Model, sustainVfxPayload.PrefabData, projectileTransform, _vfxProvider), (float _, (ICardDataAdapter model, VfxPrefabData prefab, Transform projTran, IVfxProvider vfxProvider) paramBlob) =>
			{
				paramBlob.vfxProvider.PlayVFX(new VfxData
				{
					PrefabData = paramBlob.prefab,
					SpaceData = 
					{
						Space = RelativeSpace.Local
					},
					ParentToSpace = true
				}, paramBlob.model, paramBlob.model.Instance, paramBlob.projTran);
			}));
		}
		if (hitSfxPayload != null)
		{
			splineEventData.Events.Add(new SplineEventAudio(1f, hitSfxPayload.SfxData.AudioEvents, projectileTransform.gameObject));
		}
		if (hitVfxPayload != null)
		{
			foreach (VfxData vfxData2 in hitVfxPayload.VfxDatas)
			{
				splineEventData.Events.Add(new SplineEventPayloadVFXALT(vfxData2, sourceCDC.Model, projectileTransform, _vfxProvider));
			}
		}
		splineEventData.Events.Add(new SplineEventCallback(1f, (float _) =>
		{
			callback();
			projectileTransform.gameObject.AddOrGetComponent<SelfCleanup>().SetLifetime(0.1f, SelfCleanup.CleanupType.Destroy, onlyWhenChildless: true);
		}));
		SplineMovementData splineMovementData = null;
		string text = splinePayload?.SplineDataRef.RelativePath;
		if (!string.IsNullOrEmpty(text))
		{
			splineMovementData = _splineCache.Get(text);
		}
		if (splineMovementData == null)
		{
			splineMovementData = ScriptableObject.CreateInstance<SplineMovementData>();
			splineMovementData.Spline = SplineData.Parabolic;
		}
		IdealPoint endPoint = new IdealPoint(target.position, Quaternion.identity, Vector3.one);
		if (splineOffsetsPayload != null)
		{
			endPoint.Position += splineOffsetsPayload.EndOffset;
		}
		_splineMovementSystem.AddTemporaryGoal(projectileTransform, endPoint, allowInteractions: false, splineMovementData, splineEventData);
	}

	private void PlayZteHitEffects(DuelScene_CDC sourceCDC, Transform target, AssetLookupTree.Payloads.ZoneTransfer.HitVFX hitVfxPayload, AssetLookupTree.Payloads.ZoneTransfer.HitSFX hitSfxPayload, System.Action callback)
	{
		AudioManager.SetSwitch("color", AudioManager.Instance.GetColorKey(sourceCDC.Model.PresentationColor), target.gameObject);
		if (hitVfxPayload != null)
		{
			foreach (VfxData vfxData in hitVfxPayload.VfxDatas)
			{
				_vfxProvider.PlayVFX(vfxData, sourceCDC.Model, sourceCDC.Model.Instance, target);
			}
		}
		if (hitSfxPayload != null)
		{
			AudioManager.PlayAudio(hitSfxPayload.SfxData.AudioEvents, target.gameObject);
		}
		callback();
	}

	public void AbortDamageEffect(DuelScene_CDC source_CDC)
	{
		_splineMovementSystem.RemoveTemporaryGoal(source_CDC.Root);
		SourceTransformsInCombat.Remove(source_CDC.Root);
	}

	public void DrawDebugGUI()
	{
		GUILayout.Label($"Combat Animations: {SourceTransformsInCombat.Count}");
		GUILayout.BeginVertical(GUI.skin.box);
		foreach (Transform item in SourceTransformsInCombat)
		{
			GUILayout.Label($"{item}: {_splineMovementSystem.GetProgress(item):P2}");
		}
		GUILayout.EndVertical();
	}

	public void Dispose()
	{
		_battlefield.ClearCache();
	}
}
