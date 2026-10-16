using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.Counter;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

public class CDCPart_Counters : CDCPart
{
	public readonly struct CounterSortingData(CounterCategory counterCategory, CounterType counterType)
	{
		public readonly CounterCategory CounterCategory = counterCategory;

		public readonly CounterType CounterType = counterType;
	}

	public enum CounterCategory
	{
		None,
		P1P1,
		M1M1,
		Loyalty,
		Shield,
		Generic,
		Keyword,
		Defense
	}

	[SerializeField]
	protected Transform _counterRoot;

	protected CounterTypeDiffOutput _diffOutput = CounterTypeDiffOutput.empty;

	private readonly Dictionary<CounterType, int> _counters = new Dictionary<CounterType, int>();

	protected readonly Dictionary<string, ViewCounter> _activeCounters = new Dictionary<string, ViewCounter>();

	private readonly CounterSortingDataComparer _counterSortingDataComparer = new CounterSortingDataComparer();

	private readonly Dictionary<string, CounterEffects> _pendingAddCounterEffects = new Dictionary<string, CounterEffects>();

	public void PlayCounterAddedFX(CounterType counterType)
	{
		CounterEffects counterSpawnedEffects = CounterAssetUtil.GetCounterSpawnedEffects(_assetLookupSystem, counterType, _cachedModel, _cachedCardHolderType);
		string visibleKey = GetVisibleKey(counterType);
		if (_activeCounters.TryGetValue(visibleKey, out var value) && (bool)value.gameObject)
		{
			GameObject parentObj = value.gameObject;
			PlayCounterEffects(parentObj, counterSpawnedEffects);
		}
		else
		{
			_pendingAddCounterEffects[visibleKey] = counterSpawnedEffects;
		}
	}

	protected override void HandleUpdateInternal()
	{
		UpdateCounters(_cachedModel.Counters, _cachedCardHolderType == CardHolderType.Battlefield && _cachedChangedProps.Contains(PropertyType.Counters));
	}

	private void UpdateCounters(IReadOnlyDictionary<CounterType, int> newCounters, bool showVFX)
	{
		CounterTypeDiff.GetOutput(ref _diffOutput, new CounterTypeDiffInput(_counters, newCounters));
		AddCounters(_diffOutput.Added, newCounters, showVFX);
		RemoveCounters(_diffOutput.Removed, showVFX);
		SortCounters(_diffOutput.Active);
		IncrementCounters(_diffOutput.Incremented, newCounters, showVFX);
		DecrementCounters(_diffOutput.Decremented, newCounters, showVFX);
		_counters.Clear();
		foreach (KeyValuePair<CounterType, int> newCounter in newCounters)
		{
			_counters.Add(newCounter.Key, newCounter.Value);
		}
		CheckCurrentViewConditions(_diffOutput.Active, showVFX);
	}

	private void CheckCurrentViewConditions(IReadOnlyCollection<CounterType> counters, bool showVFX)
	{
		bool flag = false;
		foreach (CounterType counter in counters)
		{
			string visibleKey = GetVisibleKey(counter);
			if (CounterTypeUtil.IsKeywordCounter(counter))
			{
				if (flag)
				{
					continue;
				}
				flag = true;
			}
			string counterPrefabPath = CounterAssetUtil.GetCounterPrefabPath(_assetLookupSystem, counter, _cachedModel, _cachedCardHolderType);
			if (_activeCounters.ContainsKey(visibleKey) && string.IsNullOrEmpty(counterPrefabPath))
			{
				RemoveCounter(counter, showVFX);
			}
			else if (!_activeCounters.ContainsKey(visibleKey) && !string.IsNullOrEmpty(counterPrefabPath) && !_diffOutput.Removed.Contains(counter))
			{
				AddCounter(counter, _counters, showVFX);
			}
		}
	}

	private void AddCounters(IReadOnlyCollection<CounterType> typesToAdd, IReadOnlyDictionary<CounterType, int> newCounters, bool showVFX)
	{
		foreach (CounterType item in typesToAdd)
		{
			AddCounter(item, newCounters, showVFX);
		}
	}

	private void AddCounter(CounterType counterType, IReadOnlyDictionary<CounterType, int> newCounters, bool showVFX)
	{
		ViewCounter value = null;
		uint count = (CounterTypeUtil.IsKeywordCounter(counterType) ? GetKeywordCounterSum() : ((uint)newCounters[counterType]));
		string visibleKey = GetVisibleKey(counterType);
		if (!CounterTypeUtil.IsKeywordCounter(counterType) || !_activeCounters.TryGetValue(visibleKey, out value))
		{
			string counterPrefabPath = CounterAssetUtil.GetCounterPrefabPath(_assetLookupSystem, counterType, _cachedModel, _cachedCardHolderType);
			if (!string.IsNullOrEmpty(counterPrefabPath))
			{
				value = SpawnCounter(counterType, counterPrefabPath);
			}
		}
		if ((bool)value)
		{
			SetCounterData(value, counterType, count);
			if (showVFX && _pendingAddCounterEffects.TryGetValue(visibleKey, out var value2))
			{
				PlayCounterEffects(value.gameObject, value2);
				_pendingAddCounterEffects.Remove(visibleKey);
			}
		}
	}

	private void RemoveCounters(IReadOnlyCollection<CounterType> typesToRemove, bool showVFX)
	{
		foreach (CounterType item in typesToRemove)
		{
			RemoveCounter(item, showVFX);
		}
	}

	private void RemoveCounter(CounterType counterType, bool showVFX)
	{
		if (!_activeCounters.TryGetValue(GetVisibleKey(counterType), out var value))
		{
			return;
		}
		bool flag = true;
		_counters.Remove(counterType);
		if (CounterTypeUtil.IsKeywordCounter(counterType))
		{
			value.SetCount(GetKeywordCounterSum());
			if (value.Count > 0)
			{
				flag = false;
			}
		}
		if (showVFX)
		{
			Transform transform = value.gameObject.transform;
			CounterEffects counterRemovedAssets = CounterAssetUtil.GetCounterRemovedAssets(_assetLookupSystem, counterType, _cachedModel, _cachedCardHolderType);
			GameObject gameObject = transform.parent.gameObject;
			PlayCounterEffects(gameObject, transform.localPosition, counterRemovedAssets);
			AudioManager.PlayAudio(WwiseEvents.sfx_combat_debuff_generic, gameObject);
		}
		if (flag)
		{
			GameObject instance = value.gameObject;
			_unityObjectPool.PushObject(instance);
			_activeCounters.Remove(GetVisibleKey(counterType));
		}
	}

	private uint GetKeywordCounterSum()
	{
		uint num = 0u;
		foreach (KeyValuePair<CounterType, int> counter in _cachedModel.Counters)
		{
			if (CounterTypeUtil.IsKeywordCounter(counter.Key))
			{
				num += (uint)counter.Value;
			}
		}
		return num;
	}

	private void SortCounters(List<CounterType> activeCounterTypes)
	{
		List<CounterSortingData> list = _genericObjectPool.PopObject<List<CounterSortingData>>();
		list.Clear();
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<CounterVisuals> loadedTree))
		{
			for (int i = 0; i < activeCounterTypes.Count; i++)
			{
				_assetLookupSystem.Blackboard.Clear();
				_assetLookupSystem.Blackboard.CounterType = activeCounterTypes[i];
				_assetLookupSystem.Blackboard.SetCardDataExtensive(_cachedModel);
				CounterCategory counterCategory = CounterCategory.None;
				CounterVisuals payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
				if (payload != null)
				{
					counterCategory = payload.counterCategory;
				}
				list.Add(new CounterSortingData(counterCategory, activeCounterTypes[i]));
			}
			list.Sort(_counterSortingDataComparer);
			activeCounterTypes.Clear();
			for (int j = 0; j < list.Count; j++)
			{
				activeCounterTypes.Add(list[j].CounterType);
				if (_activeCounters.TryGetValue(GetVisibleKey(list[j].CounterType), out var value))
				{
					value.transform.SetSiblingIndex(j);
				}
			}
		}
		_genericObjectPool.PushObject(list);
	}

	private void IncrementCounters(IReadOnlyCollection<CounterType> typesToIncrement, IReadOnlyDictionary<CounterType, int> newCounters, bool showVFX)
	{
		bool flag = false;
		foreach (CounterType item in typesToIncrement)
		{
			if (!_activeCounters.TryGetValue(GetVisibleKey(item), out var value))
			{
				continue;
			}
			uint count = (CounterTypeUtil.IsKeywordCounter(item) ? GetKeywordCounterSum() : ((uint)newCounters[item]));
			if (CounterTypeUtil.IsKeywordCounter(item))
			{
				if (flag)
				{
					continue;
				}
				flag = true;
			}
			SetCounterData(value, item, count);
			if (showVFX)
			{
				GameObject parentObj = value.gameObject;
				CounterEffects effects = CounterAssetUtil.GetCounterIncrementedEffects(_assetLookupSystem, item, _cachedModel, _cachedCardHolderType);
				if (effects.Equals(default(CounterEffects)))
				{
					effects = CounterAssetUtil.GetCounterSpawnedEffects(_assetLookupSystem, item, _cachedModel, _cachedCardHolderType);
				}
				PlayCounterEffects(parentObj, effects);
			}
		}
	}

	private void DecrementCounters(IReadOnlyCollection<CounterType> typesToDecrement, IReadOnlyDictionary<CounterType, int> newCounters, bool showVFX)
	{
		bool flag = false;
		foreach (CounterType item in typesToDecrement)
		{
			if (!_activeCounters.TryGetValue(GetVisibleKey(item), out var value))
			{
				continue;
			}
			uint count = (CounterTypeUtil.IsKeywordCounter(item) ? GetKeywordCounterSum() : ((uint)newCounters[item]));
			if (CounterTypeUtil.IsKeywordCounter(item))
			{
				if (flag)
				{
					continue;
				}
				flag = true;
			}
			SetCounterData(value, item, count);
			if (showVFX)
			{
				GameObject parentObj = value.gameObject;
				CounterEffects effects = CounterAssetUtil.GetCounterDecrementedAssets(_assetLookupSystem, item, _cachedModel, _cachedCardHolderType);
				if (effects.Equals(default(CounterEffects)))
				{
					effects = CounterAssetUtil.GetCounterRemovedAssets(_assetLookupSystem, item, _cachedModel, _cachedCardHolderType);
				}
				PlayCounterEffects(parentObj, effects);
			}
		}
	}

	private ViewCounter SpawnCounter(CounterType type, string prefabPath)
	{
		string visibleKey = GetVisibleKey(type);
		GameObject gameObject = _unityObjectPool.PopObject(prefabPath);
		gameObject.transform.SetParent(_counterRoot, worldPositionStays: false);
		gameObject.transform.ZeroOut();
		gameObject.name = $"Counter T:{visibleKey}";
		ViewCounter component = gameObject.GetComponent<ViewCounter>();
		component.Init();
		_activeCounters[visibleKey] = component;
		return component;
	}

	protected string GetVisibleKey(CounterType type)
	{
		if (!CounterTypeUtil.IsKeywordCounter(type))
		{
			return type.ToString();
		}
		return "Keywords";
	}

	private void PlayCounterEffects(GameObject parentObj, CounterEffects effects)
	{
		PlayCounterEffects(parentObj, Vector3.zero, effects);
	}

	private void PlayCounterEffects(GameObject parentObj, Vector3 localPos, CounterEffects effects)
	{
		string prefabPath = effects.PrefabPath;
		List<AudioEvent> audioEvents = effects.AudioEvents;
		if (!string.IsNullOrEmpty(prefabPath))
		{
			GameObject gameObject = _unityObjectPool.PopObject(prefabPath);
			Transform transform = gameObject.transform;
			transform.SetParent(parentObj.transform);
			transform.localPosition = localPos;
			gameObject.AddOrGetComponent<SelfCleanup>();
		}
		VfxData cardVFX = effects.CardVFX;
		if (cardVFX != null && cardVFX.PrefabData?.AllPrefabs?.Count > 0)
		{
			VfxProvider?.PlayVFX(effects.CardVFX, _cachedModel);
		}
		if (audioEvents != null)
		{
			AudioManager.PlayAudio(audioEvents, parentObj);
		}
	}

	private void SetCounterData(ViewCounter activeCounter, CounterType counterType, uint count)
	{
		GameObject gameObject = activeCounter.gameObject;
		gameObject.name = $"Counter T:{GetVisibleKey(counterType)}, C:{count}";
		gameObject.SetLayer(base.gameObject.layer);
		activeCounter.SetCount(count);
	}

	public void SetVisible(bool visible)
	{
		if (_counterRoot.gameObject.activeSelf != visible)
		{
			_counterRoot.gameObject.SetActive(visible);
		}
	}

	protected override void HandleDestructionInternal()
	{
		if (_cachedDestroyed)
		{
			foreach (ViewCounter value in _activeCounters.Values)
			{
				_unityObjectPool.PushObject(value.gameObject);
			}
			_activeCounters.Clear();
		}
		base.HandleDestructionInternal();
	}

	public override void HandleCleanup()
	{
		base.HandleCleanup();
		foreach (ViewCounter value in _activeCounters.Values)
		{
			_unityObjectPool.PushObject(value.gameObject);
		}
		_activeCounters.Clear();
		_counters.Clear();
		_counterRoot.gameObject.SetActive(value: true);
	}
}
