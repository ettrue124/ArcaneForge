using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.UXEventData;
using Pooling;
using UnityEngine;
using Wotc.Mtga.Extensions;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ChooseRandomUXEvent_CoinFlip : UXEvent
{
	private readonly uint _affectorId;

	private readonly uint _affectedId;

	private readonly List<bool> _flipResults;

	private readonly IUnityObjectPool _unityObjectPool;

	private readonly ChooseRandomUXEvent_CoinFlip_Data _data;

	private int _nextCoinToFlip;

	private float _nextCoinFireTime;

	private float _startX;

	private float _stepX = 1f;

	private float _nextX;

	private float _duration;

	private float _stagger;

	public override bool IsBlocking => true;

	public ChooseRandomUXEvent_CoinFlip(uint affectorId, uint affectedId, IReadOnlyList<bool> results, IUnityObjectPool unityObjectPool, AssetLookupSystem assetLookupSystem)
	{
		_affectorId = affectorId;
		_affectedId = affectedId;
		_flipResults = new List<bool>(results);
		_unityObjectPool = unityObjectPool ?? NullUnityObjectPool.Default;
		assetLookupSystem.Blackboard.Clear();
		CoinFlipData payload = assetLookupSystem.TreeLoader.LoadTree<CoinFlipData>().GetPayload(assetLookupSystem.Blackboard);
		_data = AssetLoader.GetObjectData(payload.CoinFlipUXEventDataRef);
	}

	public bool TryAddFlipResults(uint affector, uint affected, IEnumerable<bool> results)
	{
		if (_affectorId == affector && _affectedId == affected)
		{
			_flipResults.AddRange(results);
			return true;
		}
		return false;
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		return !currentlyRunningEvents.Exists((UXEvent x) => x.HasWeight);
	}

	public override void Execute()
	{
		float startX = ((float)(_flipResults.Count - 1) * _data.CoinSize + (float)(_flipResults.Count - 1) * _data.CoinSpacing) * 0.5f;
		_startX = startX;
		_stepX = 0f - (_data.CoinSize + _data.CoinSpacing);
		_nextX = _startX;
		if (_flipResults.Count > 1)
		{
			_duration = _data.MaxDuration;
			_stagger = Mathf.Min((_data.MaxDuration - _data.FlipDuration) / (float)_flipResults.Count, _data.FlipStagger);
		}
		else
		{
			_duration = _data.FlipDuration;
			_stagger = 0f;
		}
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if (_nextCoinToFlip < _flipResults.Count)
		{
			if (_timeRunning > _nextCoinFireTime)
			{
				FlipCoin(_nextX, _flipResults[_nextCoinToFlip]);
				_nextX += _stepX;
				_nextCoinFireTime += _stagger;
				_nextCoinToFlip++;
			}
		}
		else if (_timeRunning > _duration)
		{
			Complete();
		}
	}

	private void FlipCoin(float xPos, bool isHeads)
	{
		AnimationClip animationClip = (isHeads ? _data.HeadsAnimation : _data.TailsAnimation);
		GameObject gameObject = _unityObjectPool.PopObject(_data.CoinPrefab);
		gameObject.transform.parent = null;
		gameObject.transform.ZeroOut();
		gameObject.transform.position = Vector3.left * xPos + Vector3.up * _data.BattlefieldOffset;
		gameObject.transform.localScale = Vector3.one * _data.CoinSize;
		gameObject.AddOrGetComponent<SelfCleanup>().SetLifetime(Mathf.Max(animationClip.length, _data.FlipDuration));
		Animation componentInChildren = gameObject.GetComponentInChildren<Animation>();
		componentInChildren.clip = animationClip;
		componentInChildren.Rewind();
		componentInChildren.Play(PlayMode.StopAll);
		AudioManager.PlayAudio(_data.WwiseEvent, gameObject);
	}
}
