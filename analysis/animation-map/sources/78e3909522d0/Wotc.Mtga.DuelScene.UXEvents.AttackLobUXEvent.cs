using System;
using System.Collections.Generic;
using AssetLookupTree;
using MovementSystem;
using Pooling;
using UnityEngine;
using Wotc.Mtga.Unity;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AttackLobUXEvent : NPEUXEvent
{
	private readonly IUnityObjectPool _unityObjectPool;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly uint _quarryId;

	private readonly uint _attackerId;

	private DuelScene_CDC _cardView;

	private DreamteckIntentionArrowBehavior _arrow;

	private SplineAnimation _easing;

	public override bool HasWeight => true;

	public AttackLobUXEvent(Func<NPEDirector> getNpeDirector, IEntityViewProvider viewProvider, IUnityObjectPool unityObjectPool, uint attackerId, uint quarryId, AssetLookupSystem assetLookupSystem, ISplineMovementSystem splineMovementSystem)
		: base(getNpeDirector)
	{
		_quarryId = quarryId;
		_attackerId = attackerId;
		_entityViewProvider = viewProvider;
		_unityObjectPool = unityObjectPool;
		_assetLookupSystem = assetLookupSystem;
		_splineMovementSystem = splineMovementSystem;
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		return true;
	}

	public override void Execute()
	{
		if (!_entityViewProvider.TryGetCardView(_attackerId, out _cardView) || _cardView.Model == null)
		{
			Debug.LogError("AttackLobUXEvent had a null CardView.");
			Fail();
		}
		else
		{
			_splineMovementSystem.MovementCompleted += OnCardMovementCompleted;
		}
	}

	private void OnCardMovementCompleted(Transform tran)
	{
		if (!_cardView || tran != _cardView.Root)
		{
			return;
		}
		_splineMovementSystem.MovementCompleted -= OnCardMovementCompleted;
		DuelScene_AvatarView avatarById = _entityViewProvider.GetAvatarById(_quarryId);
		_arrow = IntentionLineUtils.CreateIntentionLine(_assetLookupSystem, "NPECombat", _unityObjectPool);
		_arrow.SetStart(_cardView.PartsRoot);
		_arrow.SetEnd(avatarById.TargetTransform);
		AudioManager.PlayAudio(WwiseEvents.sfx_npe_target, _arrow.gameObject);
		_easing = _arrow.GetComponent<SplineAnimation>();
		if ((bool)_easing)
		{
			if (_easing.disableWhenComplete)
			{
				_timeOutTarget += _easing.completionDelay;
			}
			else
			{
				_canTimeOut = false;
			}
			SplineAnimation easing = _easing;
			easing.OnHit = (System.Action)Delegate.Combine(easing.OnHit, new System.Action(OnHit));
			SplineAnimation easing2 = _easing;
			easing2.OnComplete = (System.Action)Delegate.Combine(easing2.OnComplete, new System.Action(OnComplete));
		}
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if (_cardView.Model.Instance.AttackState != AttackState.Declared)
		{
			Complete();
		}
	}

	private void OnHit()
	{
		_getNpeDirector().AttackHaloTabulation(_quarryId, 1);
	}

	protected override void OnComplete()
	{
		if ((bool)_easing && _easing.disableWhenComplete)
		{
			Complete();
		}
	}

	protected override void Cleanup()
	{
		if ((bool)_easing)
		{
			SplineAnimation easing = _easing;
			easing.OnHit = (System.Action)Delegate.Remove(easing.OnHit, new System.Action(OnHit));
			SplineAnimation easing2 = _easing;
			easing2.OnComplete = (System.Action)Delegate.Remove(easing2.OnComplete, new System.Action(OnComplete));
		}
		_easing = null;
		if ((bool)_arrow)
		{
			UnityEngine.Object.Destroy(_arrow.gameObject);
		}
		_arrow = null;
		_splineMovementSystem.MovementCompleted -= OnCardMovementCompleted;
		base.Cleanup();
	}
}
