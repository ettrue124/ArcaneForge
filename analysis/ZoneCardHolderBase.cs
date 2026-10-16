using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.CardHolder;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.DuelScene.Interactions;
using Wotc.Mtgo.Gre.External.Messaging;

public abstract class ZoneCardHolderBase : CardHolderBase
{
	protected enum IdOrderType
	{
		None,
		Normal,
		Reversed
	}

	private class CardViewSorter
	{
		private class CardViewComparer : IComparer<DuelScene_CDC>
		{
			private IdOrderType _orderType;

			private readonly Dictionary<uint, int> _indicesInZone = new Dictionary<uint, int>(100);

			private readonly Dictionary<uint, int> _indicesInCards = new Dictionary<uint, int>(100);

			public void SetCompareDependencies(IdOrderType orderType, IReadOnlyList<uint> zoneCardsList, IReadOnlyList<DuelScene_CDC> cardViewList)
			{
				_orderType = orderType;
				for (int i = 0; i < zoneCardsList.Count; i++)
				{
					_indicesInZone[zoneCardsList[i]] = i;
				}
				for (int j = 0; j < cardViewList.Count; j++)
				{
					_indicesInCards[cardViewList[j].InstanceId] = j;
				}
			}

			public void ClearCompareDependencies()
			{
				_orderType = IdOrderType.None;
				_indicesInZone.Clear();
				_indicesInCards.Clear();
			}

			public int Compare(DuelScene_CDC lhs, DuelScene_CDC rhs)
			{
				int value = 0;
				int value2 = 0;
				_indicesInZone.TryGetValue(lhs.InstanceId, out value);
				_indicesInZone.TryGetValue(rhs.InstanceId, out value2);
				if (value < 0 || value2 < 0)
				{
					_indicesInCards.TryGetValue(lhs.InstanceId, out value);
					_indicesInCards.TryGetValue(rhs.InstanceId, out value2);
					return value.CompareTo(value2);
				}
				return _orderType switch
				{
					IdOrderType.Normal => value.CompareTo(value2), 
					IdOrderType.Reversed => value2.CompareTo(value), 
					_ => 0, 
				};
			}
		}

		private readonly CardViewComparer _comparer = new CardViewComparer();

		public void SortCardViewList(IdOrderType orderType, IReadOnlyList<uint> zoneCardsList, List<DuelScene_CDC> cardViewList)
		{
			if (orderType != IdOrderType.None && zoneCardsList != null && zoneCardsList.Count != 0 && cardViewList != null && cardViewList.Count != 0)
			{
				_comparer.SetCompareDependencies(orderType, zoneCardsList, cardViewList);
				cardViewList.Sort(_comparer);
				_comparer.ClearCompareDependencies();
			}
		}
	}

	protected MtgZone _zoneModel;

	protected IdOrderType _orderType;

	private readonly CardViewSorter _cardViewSorter = new CardViewSorter();

	private KeyValuePair<CardHolder_StaticVfx, GameObject> _activeZoneEffect;

	public ZoneType GetZoneType => _zoneModel?.Type ?? ZoneType.None;

	public MtgZone GetZone => _zoneModel;

	public void UpdateZoneModel(MtgZone zone)
	{
		_zoneModel = zone;
		_isDirty = true;
	}

	protected override void OnPreLayout()
	{
		base.OnPreLayout();
		_cardViewSorter.SortCardViewList(_orderType, _zoneModel?.CardIds, _cardViews);
	}

	protected override void OnPostLayout()
	{
		base.OnPostLayout();
		EvaluateForZoneVFX();
	}

	public void OnInteractionApplied(WorkflowBase workflow)
	{
		EvaluateForZoneVFX();
		if (ContainsWorkflowSecondaryId(workflow))
		{
			LayoutNow();
		}
	}

	private bool ContainsWorkflowSecondaryId(WorkflowBase workflow)
	{
		if (!UseSecondaryLayout)
		{
			return false;
		}
		foreach (uint workflowId in SecondaryLayoutContainer.GetWorkflowIds(workflow))
		{
			foreach (DuelScene_CDC cardView in _cardViews)
			{
				if (cardView.Model.InstanceId == workflowId)
				{
					return true;
				}
			}
		}
		return false;
	}

	public void OnInteractionCleared()
	{
		EvaluateForZoneVFX();
	}

	private void EvaluateForZoneVFX()
	{
		if (!_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<CardHolder_StaticVfx> loadedTree))
		{
			return;
		}
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.CardHolder = this;
		_assetLookupSystem.Blackboard.CardHolderType = m_cardHolderType;
		CardHolder_StaticVfx payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
		if (payload != _activeZoneEffect.Key)
		{
			GameObject gameObject = null;
			if ((bool)_activeZoneEffect.Value)
			{
				Object.Destroy(_activeZoneEffect.Value);
			}
			if (payload != null)
			{
				gameObject = AssetLoader.Instantiate(payload.VfxPrefabData.AllPrefabs[0], EffectsRoot);
				gameObject.transform.localPosition = payload.OffsetData.PositionOffset;
				gameObject.transform.localEulerAngles = payload.OffsetData.RotationOffset;
				gameObject.transform.localScale = payload.OffsetData.ScaleMultiplier;
				SelfCleanup component = gameObject.GetComponent<SelfCleanup>();
				if ((object)component != null)
				{
					Object.Destroy(component);
				}
			}
			_activeZoneEffect = new KeyValuePair<CardHolder_StaticVfx, GameObject>(payload, gameObject);
		}
		_assetLookupSystem.Blackboard.Clear();
	}
}
