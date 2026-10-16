using System;
using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectCounters;

public class SelectCountersWorkflow : WorkflowBase<SelectCountersRequest>, ICardStackWorkflow, IAutoRespondWorkflow
{
	private readonly IObjectPool _pool;

	private readonly CounterDistribution _counterDistribution;

	private readonly CounterSelection _counterSelection;

	private readonly List<CounterPair> _selectedCounters;

	private readonly SortedDictionary<uint, List<CounterPair>> _pairsById;

	private readonly SortedDictionary<uint, uint> _distributionsById;

	private readonly Queue<(uint, uint)> _remainingCounterSelections;

	private (uint, uint) _currentCounterSelection = (0u, 0u);

	public SelectCountersWorkflow(SelectCountersRequest req, IObjectPool pool, CounterDistribution counterDistribution, CounterSelection counterSelection)
		: base(req)
	{
		_pool = pool ?? NullObjectPool.Default;
		_counterDistribution = counterDistribution;
		_counterSelection = counterSelection;
		_selectedCounters = _pool.PopObject<List<CounterPair>>();
		_pairsById = _pool.PopObject<SortedDictionary<uint, List<CounterPair>>>();
		_distributionsById = _pool.PopObject<SortedDictionary<uint, uint>>();
		_remainingCounterSelections = _pool.PopObject<Queue<(uint, uint)>>();
		_highlightsGenerator = new HighlightsGenerator(_pool, GetCurrentCounterSelection);
	}

	protected override void ApplyInteractionInternal()
	{
		PopulatePairMappings(_request.CounterPairs);
		_counterSelection.Submitted += OnCounterSelectionSubmitted;
		if (_pairsById.Keys.Count == 1 && _request.MinSelect == _request.MaxSelect)
		{
			_distributionsById[_pairsById.Keys.First()] = _request.MaxSelect;
			SubmitDistributions();
			return;
		}
		_counterDistribution.ValueChanged += OnDistributionValueChanged;
		_counterDistribution.SetMax(_request.MaxSelect);
		_counterDistribution.Apply(_pairsById);
		SetButtons();
	}

	private void PopulatePairMappings(IEnumerable<CounterPair> counterPairs)
	{
		_pairsById.Clear();
		foreach (CounterPair counterPair in counterPairs)
		{
			uint instanceId = counterPair.InstanceId;
			if (_pairsById.TryGetValue(instanceId, out var value))
			{
				value.Add(counterPair);
				continue;
			}
			List<CounterPair> list = _pool.PopObject<List<CounterPair>>();
			list.Add(counterPair);
			_pairsById[counterPair.InstanceId] = list;
		}
	}

	private void OnDistributionValueChanged(uint id, uint value)
	{
		_distributionsById[id] = value;
		SetButtons();
	}

	private void SubmitDistributions()
	{
		_counterDistribution.CleanUp();
		foreach (KeyValuePair<uint, uint> item in _distributionsById)
		{
			uint key = item.Key;
			uint value = item.Value;
			if (!_pairsById.TryGetValue(key, out var value2))
			{
				continue;
			}
			if (value2.Count > 1 && (uint)value2.Sum((CounterPair x) => x.Count) > value)
			{
				_remainingCounterSelections.Enqueue((key, value));
				continue;
			}
			foreach (CounterPair item2 in value2)
			{
				uint count = ((item2.Count > value) ? value : item2.Count);
				_selectedCounters.Add(new CounterPair
				{
					InstanceId = item2.InstanceId,
					CounterType = item2.CounterType,
					Count = count
				});
			}
		}
		if (_remainingCounterSelections.Count > 0)
		{
			DequeueAndApplyCounterSelection();
		}
		else
		{
			SubmitResults();
		}
	}

	private void OnCounterSelectionSubmitted(IEnumerable<uint> results)
	{
		AddSelectedCounters(results);
		if (_remainingCounterSelections.Count > 0)
		{
			DequeueAndApplyCounterSelection();
		}
		else
		{
			SubmitResults();
		}
	}

	private void AddSelectedCounters(IEnumerable<uint> selectedCounters)
	{
		Dictionary<uint, uint> dictionary = _pool.PopObject<Dictionary<uint, uint>>();
		foreach (uint selectedCounter in selectedCounters)
		{
			if (dictionary.ContainsKey(selectedCounter))
			{
				dictionary[selectedCounter]++;
			}
			else
			{
				dictionary[selectedCounter] = 1u;
			}
		}
		uint item = _currentCounterSelection.Item1;
		foreach (KeyValuePair<uint, uint> item2 in dictionary)
		{
			_selectedCounters.Add(new CounterPair
			{
				InstanceId = item,
				CounterType = (CounterType)item2.Key,
				Count = item2.Value
			});
		}
		dictionary.Clear();
		_pool.PushObject(dictionary);
	}

	private void DequeueAndApplyCounterSelection()
	{
		if (_remainingCounterSelections.Count > 0)
		{
			_currentCounterSelection = _remainingCounterSelections.Dequeue();
			uint item = _currentCounterSelection.Item1;
			uint item2 = _currentCounterSelection.Item2;
			if (_pairsById.TryGetValue(item, out var value))
			{
				_counterSelection.Apply(item, value, item2);
				SetHighlights();
			}
		}
	}

	private void SubmitResults()
	{
		_request.SubmitCountersResponse(_selectedCounters);
	}

	private bool IsDistributingCounters()
	{
		if (_currentCounterSelection.Item1 == 0)
		{
			return _currentCounterSelection.Item2 == 0;
		}
		return false;
	}

	private (uint, uint) GetCurrentCounterSelection()
	{
		return _currentCounterSelection;
	}

	protected override void SetButtons()
	{
		Buttons.Cleanup();
		if (IsDistributingCounters())
		{
			uint num = 0u;
			foreach (KeyValuePair<uint, uint> item in _distributionsById)
			{
				num += item.Value;
			}
			Buttons.WorkflowButtons.Add(new PromptButtonData
			{
				ButtonText = new MTGALocalizedString
				{
					Key = "DuelScene/ClientPrompt/Submit_N",
					Parameters = new Dictionary<string, string> { 
					{
						"submitCount",
						num.ToString()
					} }
				},
				Style = ButtonStyle.StyleType.Main,
				ButtonCallback = SubmitDistributions,
				Enabled = (_request.MinSelect <= num && _request.MaxSelect >= num)
			});
			if (_request.CanCancel)
			{
				Buttons.CancelData = new PromptButtonData
				{
					ButtonText = Utils.GetCancelLocKey(_request.CancellationType),
					Style = ButtonStyle.StyleType.Secondary,
					ButtonCallback = () =>
					{
						_request.Cancel();
					},
					ButtonSFX = WwiseEvents.sfx_ui_cancel.EventName
				};
			}
			if (_request.AllowUndo)
			{
				Buttons.UndoData = new PromptButtonData
				{
					ButtonCallback = () =>
					{
						_request.Undo();
					}
				};
			}
		}
		base.SetButtons();
	}

	public override void CleanUp()
	{
		_remainingCounterSelections.Clear();
		_pool.PushObject(_remainingCounterSelections);
		_distributionsById.Clear();
		_pool.PushObject(_distributionsById);
		foreach (KeyValuePair<uint, List<CounterPair>> item in _pairsById)
		{
			List<CounterPair> value = item.Value;
			value.Clear();
			_pool.PushObject(value);
		}
		_pairsById.Clear();
		_pool.PushObject(_pairsById);
		_selectedCounters.Clear();
		_pool.PushObject(_selectedCounters);
		_counterDistribution.ValueChanged -= OnDistributionValueChanged;
		_counterDistribution.CleanUp();
		_counterSelection.Submitted -= OnCounterSelectionSubmitted;
		_counterSelection.CleanUp();
		if (_highlightsGenerator is IDisposable disposable)
		{
			disposable.Dispose();
		}
		base.CleanUp();
	}

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		bool flag = _distributionsById.TryGetValue(lhs.InstanceId, out var value);
		bool flag2 = _distributionsById.TryGetValue(rhs.InstanceId, out var value2);
		if (flag & flag2)
		{
			return value == value2;
		}
		return flag == flag2;
	}

	public bool TryAutoRespond()
	{
		if (_request.MinSelect != _request.MaxSelect)
		{
			return false;
		}
		PopulatePairMappings(_request.CounterPairs);
		if (_pairsById.Keys.Count > 1)
		{
			return false;
		}
		_distributionsById[_pairsById.Keys.First()] = _request.MaxSelect;
		foreach (KeyValuePair<uint, uint> item in _distributionsById)
		{
			uint key = item.Key;
			uint value = item.Value;
			if (!_pairsById.TryGetValue(key, out var value2))
			{
				continue;
			}
			if (value2.Count > 1 && (uint)value2.Sum((CounterPair x) => x.Count) > value)
			{
				_distributionsById.Clear();
				_pairsById.Clear();
				_selectedCounters.Clear();
				return false;
			}
			foreach (CounterPair item2 in value2)
			{
				uint count = ((item2.Count > value) ? value : item2.Count);
				_selectedCounters.Add(new CounterPair
				{
					InstanceId = item2.InstanceId,
					CounterType = item2.CounterType,
					Count = count
				});
			}
		}
		SubmitResults();
		return true;
	}
}
