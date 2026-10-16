using System;
using System.Collections.Generic;
using GreClient.Rules;
using UnityEngine;
using Wizards.Mtga.Platforms;
using Wizards.Mtga.Utils;
using Wotc.Mtga.DuelScene.Battlefield;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UXEventDamageDealt : UXEvent
{
	public readonly MtgEntity Source;

	public readonly MtgEntity Target;

	public readonly int Amount;

	public readonly bool IsBlockDamage;

	private readonly IResolutionEffectProvider _resolutionEffectProvider;

	private readonly IEntityViewProvider _viewProvider;

	private readonly CombatAnimationPlayer _combatAnimationPlayer;

	private IEnumerator<float?> _playbackRoutine;

	private float _waitForSeconds;

	private BinarySemaphore _pagingAccess;

	public DamageType DamageType { get; private set; }

	public DamageTargetInfo DamageInfo { get; private set; }

	public bool PageToTarget { private get; set; } = true;

	public bool CanHitPageArrow { get; set; } = true;

	public override bool IsBlocking => true;

	public event System.Action OnHit;

	public override IEnumerable<uint> GetInvolvedIds()
	{
		uint sourceId = Source.InstanceId;
		uint targetId = Target.InstanceId;
		yield return sourceId;
		if (sourceId != targetId)
		{
			yield return targetId;
		}
	}

	public override string ToString()
	{
		return $"Damage: {Source.InstanceId} {Target.InstanceId}";
	}

	public UXEventDamageDealt(MtgEntity source, MtgEntity target, int amount, DamageType damageType, bool isBlockDamage, IResolutionEffectProvider resolutionEffectProvider, IEntityViewProvider viewProvider, CombatAnimationPlayer combatAnimationPlayer)
	{
		DamageType = damageType;
		IsBlockDamage = isBlockDamage;
		Source = source;
		Target = target;
		Amount = amount;
		_timeOutTarget = 3f;
		_resolutionEffectProvider = resolutionEffectProvider;
		_viewProvider = viewProvider;
		_combatAnimationPlayer = combatAnimationPlayer;
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		return !currentlyRunningEvents.Exists((UXEvent x) => x.HasWeight);
	}

	public override void Execute()
	{
		_playbackRoutine = PlaybackRoutine(_resolutionEffectProvider.ResolutionEffect);
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		_waitForSeconds -= dt;
		if (!IsComplete && !HasFailed && _waitForSeconds <= 0f && _playbackRoutine != null && _playbackRoutine.MoveNext())
		{
			_waitForSeconds = _playbackRoutine.Current.GetValueOrDefault();
		}
	}

	private IEnumerator<float?> PlaybackRoutine(ResolutionEffectModel resolutionEffect)
	{
		if (DamageType == DamageType.Direct && resolutionEffect != null && resolutionEffect.IgnoreDamageEffects)
		{
			InvokeOnHit();
			yield break;
		}
		DuelScene_CDC sourceCard = _viewProvider.GetCardView(Source.InstanceId);
		DuelScene_CDC targetCdc = null;
		MtgCardInstance targetCardInstance = Target as MtgCardInstance;
		MtgPlayer targetPlayerInstance = Target as MtgPlayer;
		if (targetCardInstance != null)
		{
			targetCdc = _viewProvider.GetCardView(Target.InstanceId);
		}
		bool sourceCardIsVisible = sourceCard != null && sourceCard.IsVisible;
		bool targetIsVisible = targetPlayerInstance != null || (targetCardInstance != null && (bool)targetCdc && targetCdc.IsVisible);
		if ((PageToTarget & sourceCardIsVisible) && !targetIsVisible && (bool)targetCdc)
		{
			ICardHolder currentCardHolder = targetCdc.CurrentCardHolder;
			if (currentCardHolder is BattlefieldCardHolder battlefieldCardHolder)
			{
				BattlefieldRegion targetRegion = battlefieldCardHolder.GetRegionForCard(targetCdc);
				if (targetRegion != null)
				{
					BattlefieldRegionDefinition regionDef = targetRegion.RegionDef;
					if (regionDef == null || regionDef.Type != BattlefieldRegionType.Planeswalker)
					{
						BattlefieldRegionDefinition regionDef2 = targetRegion.RegionDef;
						if (regionDef2 == null || regionDef2.Type != BattlefieldRegionType.Battles)
						{
							goto IL_03a1;
						}
					}
					_canTimeOut = false;
					bool hasAccess = false;
					_pagingAccess = targetRegion.PagingAccess;
					_pagingAccess.RequestAccess(this, () =>
					{
						hasAccess = true;
					});
					while (!hasAccess)
					{
						yield return null;
					}
					yield return 0.375f;
					battlefieldCardHolder.LayoutLocked = false;
					while (!targetRegion.IsInVisibleStack(targetCdc))
					{
						float value = 0.25f;
						if (targetRegion.IsPagedLeft(targetCdc))
						{
							targetRegion.PageLeft();
							battlefieldCardHolder.LayoutNow();
							yield return value;
							continue;
						}
						if (targetRegion.IsPagedRight(targetCdc))
						{
							targetRegion.PageRight();
							battlefieldCardHolder.LayoutNow();
							yield return value;
							continue;
						}
						Debug.LogWarning($"region {targetRegion.RegionDef.Type} cannot page to targetCdc {targetCdc.InstanceId} ({Source.InstanceId})");
						break;
					}
					battlefieldCardHolder.LayoutLocked = true;
					targetIsVisible |= targetCardInstance != null && (bool)targetCdc && targetCdc.IsVisible;
				}
			}
		}
		goto IL_03a1;
		IL_03a1:
		if (sourceCardIsVisible && (targetIsVisible || CanHitPageArrow))
		{
			if (DamageType == DamageType.Direct && resolutionEffect != null && !resolutionEffect.RedirectDamageEventsFromParent && resolutionEffect.CardInstance.ParentId == Source.InstanceId)
			{
				uint instanceId = resolutionEffect.CardInstance.InstanceId;
				if (_viewProvider.TryGetCardView(instanceId, out var cardView))
				{
					sourceCard = cardView;
				}
			}
			_ = Amount;
			if (sourceCard.Model != null)
			{
				_ = sourceCard.Model.Power.Value;
			}
			if (targetCardInstance != null)
			{
				CDCPart_PTBox cDCPart_PTBox = targetCdc.FindPart<CDCPart_PTBox>(AnchorPointType.PowerToughness);
				DamageInfo = new DamageTargetInfo(targetCdc.PartsRoot, (cDCPart_PTBox == null) ? targetCdc.PartsRoot : cDCPart_PTBox.transform, Amount, DamageType, targetCardInstance);
				if (DamageType == DamageType.Combat)
				{
					targetCdc.PlayReactionAnimation(CardReactionEnum.Damage);
				}
				if (!targetIsVisible && targetCdc.CurrentCardHolder is BattlefieldCardHolder battlefieldCardHolder2)
				{
					BattlefieldRegion regionForCard = battlefieldCardHolder2.GetRegionForCard(targetCdc);
					if (regionForCard != null)
					{
						if (regionForCard.IsPagedLeft(targetCdc))
						{
							DamageInfo.Transform = regionForCard.LeftPagingButton.transform;
							CanHitPageArrow = false;
						}
						else if (regionForCard.IsPagedRight(targetCdc))
						{
							DamageInfo.Transform = regionForCard.RightPagingButton.transform;
							CanHitPageArrow = false;
						}
					}
				}
			}
			else
			{
				DuelScene_AvatarView avatarById = _viewProvider.GetAvatarById(targetPlayerInstance.InstanceId);
				DamageInfo = new DamageTargetInfo(PlatformUtils.IsHandheld() ? avatarById.LifeTextTransform : avatarById.transform, avatarById.LifeTextTransform, Amount, DamageType, targetPlayerInstance);
			}
		}
		if (DamageInfo == null)
		{
			InvokeOnHit();
		}
		else if (!_combatAnimationPlayer.PlayDamageEffect(sourceCard, DamageInfo, InvokeOnHit, resolutionEffect?.SuppressProjectileDamageEffects ?? false))
		{
			InvokeOnHit();
		}
	}

	private void InvokeOnHit()
	{
		_pagingAccess?.RepealAccess(this);
		OnHit?.Invoke();
	}

	protected override void Cleanup()
	{
		if (HasFailed)
		{
			InvokeOnHit();
			OnHit = null;
			DuelScene_CDC cardView = _viewProvider.GetCardView(Source.InstanceId);
			_combatAnimationPlayer.AbortDamageEffect(cardView);
		}
	}
}
