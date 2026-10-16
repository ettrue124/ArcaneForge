using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions;

public class DistributionWorkflow : WorkflowBase<DistributionRequest>, ICardStackWorkflow, IAutoRespondWorkflow
{
	private class DistributionWorkflowHighlightsGenerator : IHighlightsGenerator
	{
		private readonly IReadOnlyList<uint> _targetIds;

		public DistributionWorkflowHighlightsGenerator(IReadOnlyList<uint> targetIds)
		{
			_targetIds = targetIds;
		}

		public Highlights GetHighlights()
		{
			Highlights highlights = new Highlights();
			foreach (uint targetId in _targetIds)
			{
				highlights.IdToHighlightType_Workflow[targetId] = HighlightType.Selected;
			}
			return highlights;
		}
	}

	public static class Utils
	{
		public static Dictionary<uint, uint> InitializeDistributions(IReadOnlyList<uint> targetIds, uint minPer)
		{
			Dictionary<uint, uint> dictionary = new Dictionary<uint, uint>();
			foreach (uint targetId in targetIds)
			{
				dictionary[targetId] = minPer;
			}
			return dictionary;
		}

		public static uint InitialDistributionTotal(uint targetIdCount, uint minPer)
		{
			return targetIdCount * minPer;
		}

		public static List<SpinnerData> CreateSpinnerData(IReadOnlyList<uint> targetIds, int min, int max)
		{
			List<SpinnerData> list = new List<SpinnerData>();
			foreach (uint targetId in targetIds)
			{
				list.Add(new SpinnerData(targetId, min, min, max));
			}
			return list;
		}

		public static bool CanAutoRespond(bool canCancel, int targetCount, uint min, uint max)
		{
			if (!canCancel && targetCount == 1)
			{
				return min == max;
			}
			return false;
		}
	}

	private readonly Dictionary<uint, uint> _distributions;

	private uint _currentDistribution;

	protected readonly SpinnerController _spinnerController;

	protected readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	private readonly CardHolderReference<StackCardHolder> _stack;

	protected readonly CardHolderReference<HandCardHolder> _localHand;

	private IReadOnlyList<uint> TargetIds => _request.TargetIds;

	public DistributionWorkflow(DistributionRequest request, ICardHolderProvider cardHolderProvider, SpinnerController spinnerController)
		: base(request)
	{
		_spinnerController = spinnerController;
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderProvider);
		_battlefield = CardHolderReference<IBattlefieldCardHolder>.Battlefield(cardHolderProvider);
		_localHand = new CardHolderReference<HandCardHolder>(cardHolderProvider, GREPlayerNum.LocalPlayer, CardHolderType.Hand);
		_distributions = Utils.InitializeDistributions(TargetIds, _request.MinPer);
		_currentDistribution = Utils.InitialDistributionTotal((uint)TargetIds.Count, _request.MinPer);
		_highlightsGenerator = new DistributionWorkflowHighlightsGenerator(TargetIds);
	}

	protected override void ApplyInteractionInternal()
	{
		List<SpinnerData> data = Utils.CreateSpinnerData(TargetIds, (int)_request.MinPer, (int)_request.MaxPer);
		_spinnerController.Open(data);
		_spinnerController.ValueChanged += OnValueChanged;
		_localHand.Get().SetHandCollapse(collapsed: true);
		_stack.Get().TryAutoDock(TargetIds);
		_battlefield.Get().LayoutNow();
		SetButtons();
	}

	private void OnValueChanged(uint id, uint value)
	{
		_distributions[id] = value;
		_currentDistribution = 0u;
		foreach (uint key in _distributions.Keys)
		{
			_currentDistribution += _distributions[key];
		}
		SetButtons();
		_battlefield.Get().LayoutNow();
	}

	public override void CleanUp()
	{
		if (_spinnerController.Active)
		{
			_spinnerController.Close();
		}
		StackCardHolder stackCardHolder = _stack.Get();
		if (stackCardHolder != null)
		{
			stackCardHolder.ResetAutoDock();
			stackCardHolder.TargetingSourceId = 0u;
		}
		if (_spinnerController != null)
		{
			_spinnerController.ValueChanged -= OnValueChanged;
		}
		_battlefield.ClearCache();
		_stack.ClearCache();
		_localHand.ClearCache();
		base.CleanUp();
	}

	protected override void SetButtons()
	{
		Buttons.Cleanup();
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = "DuelScene/ClientPrompt/ClientPrompt_Button_Submit",
			Style = ButtonStyle.StyleType.Main,
			ButtonCallback = () =>
			{
				_request.SubmitDistribution(_distributions);
			},
			Enabled = (_request.Min <= _currentDistribution && _request.Max >= _currentDistribution)
		});
		if (_request.CanCancel)
		{
			Buttons.CancelData = new PromptButtonData
			{
				ButtonText = Wotc.Mtga.Loc.Utils.GetCancelLocKey(_request.CancellationType),
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
		OnUpdateButtons(Buttons);
	}

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		bool flag = _distributions.TryGetValue(lhs.InstanceId, out var value);
		bool flag2 = _distributions.TryGetValue(rhs.InstanceId, out var value2);
		if (flag & flag2)
		{
			return value == value2;
		}
		return flag == flag2;
	}

	public bool TryAutoRespond()
	{
		if (Utils.CanAutoRespond(_request.CanCancel, TargetIds.Count, _request.Min, _request.Max))
		{
			_distributions[TargetIds[0]] = _request.Min;
			_request.SubmitDistribution(_distributions);
			return true;
		}
		return false;
	}
}
