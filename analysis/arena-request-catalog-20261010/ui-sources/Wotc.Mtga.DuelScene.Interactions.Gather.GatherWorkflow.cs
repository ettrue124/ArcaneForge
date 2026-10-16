using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using WorkflowVisuals;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.Gather;

public abstract class GatherWorkflow : WorkflowBase<GatherRequest>, ICardStackWorkflow
{
	protected class EntitySourceSink
	{
		public readonly uint Id;

		public readonly uint Min;

		public readonly uint Max;

		public Gathering Gathering;

		public EntitySourceSink(uint id, GatherSource source)
		{
			Id = id;
			Min = source.MinAmount;
			Max = source.MaxAmount;
			Gathering = new Gathering
			{
				InstanceId = Id,
				Amount = Min
			};
		}
	}

	private class GatherHighlightsGenerator : IHighlightsGenerator
	{
		private readonly IReadOnlyCollection<uint> _sourceIds;

		private readonly Func<uint> _getDestinationId;

		public GatherHighlightsGenerator(IReadOnlyCollection<uint> sourceIds, Func<uint> getDestinationId)
		{
			_sourceIds = sourceIds;
			_getDestinationId = getDestinationId;
		}

		public Highlights GetHighlights()
		{
			Highlights highlights = new Highlights();
			foreach (uint sourceId in _sourceIds)
			{
				highlights.IdToHighlightType_Workflow[sourceId] = HighlightType.Selected;
			}
			if (_getDestinationId != null)
			{
				highlights.IdToHighlightType_Workflow[_getDestinationId()] = HighlightType.Hot;
			}
			return highlights;
		}
	}

	protected readonly SpinnerController _spinnerController;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	protected readonly Dictionary<uint, EntitySourceSink> _sourcesByInstanceId = new Dictionary<uint, EntitySourceSink>();

	protected readonly uint _destinationId;

	private readonly HashSet<uint> _sourceIds;

	public GatherWorkflow(GatherRequest nRequest, ICardHolderProvider cardHolderProvider, SpinnerController spinnerController)
		: base(nRequest)
	{
		_spinnerController = spinnerController;
		_stack = CardHolderReference<StackCardHolder>.Stack(cardHolderProvider);
		_battlefield = CardHolderReference<IBattlefieldCardHolder>.Battlefield(cardHolderProvider);
		_destinationId = nRequest.DestinationId;
		_sourceIds = new HashSet<uint>();
		foreach (GatherSource source in _request.Sources)
		{
			uint sourceId = source.SourceId;
			_sourceIds.Add(sourceId);
			_sourcesByInstanceId.Add(sourceId, new EntitySourceSink(sourceId, source));
		}
		_highlightsGenerator = new GatherHighlightsGenerator(_sourceIds, () => _destinationId);
	}

	protected abstract IReadOnlyCollection<SpinnerData> GenerateSpinnerData();

	protected override void ApplyInteractionInternal()
	{
		_spinnerController.Open(GenerateSpinnerData());
		_spinnerController.ValueChanged += OnValueChanged;
		SetButtons();
		_battlefield.Get().LayoutNow();
		_stack.Get().TryAutoDock(_sourceIds);
	}

	protected virtual void OnValueChanged(uint id, uint value)
	{
		SetButtons();
	}

	public override void CleanUp()
	{
		_sourceIds.Clear();
		_battlefield.Get().LayoutNow();
		_battlefield.ClearCache();
		_stack.Get().LayoutNow();
		_stack.ClearCache();
		if (_spinnerController != null)
		{
			_spinnerController.ValueChanged -= OnValueChanged;
			if (_spinnerController.Active)
			{
				_spinnerController.Close();
			}
		}
		base.CleanUp();
	}

	protected virtual bool CanSubmitGathering()
	{
		foreach (uint key in _sourcesByInstanceId.Keys)
		{
			EntitySourceSink entitySourceSink = _sourcesByInstanceId[key];
			uint amount = entitySourceSink.Gathering.Amount;
			if (entitySourceSink.Min > amount || entitySourceSink.Max < amount)
			{
				return false;
			}
		}
		return true;
	}

	protected uint GetTotalGatherAmount()
	{
		uint num = 0u;
		foreach (uint key in _sourcesByInstanceId.Keys)
		{
			EntitySourceSink entitySourceSink = _sourcesByInstanceId[key];
			num += entitySourceSink.Gathering.Amount;
		}
		return num;
	}

	protected override void SetButtons()
	{
		int totalGatherAmount = (int)GetTotalGatherAmount();
		Buttons = new Buttons();
		Buttons.WorkflowButtons.Add(new PromptButtonData
		{
			ButtonText = new MTGALocalizedString
			{
				Key = "DuelScene/ClientPrompt/Submit_N",
				Parameters = new Dictionary<string, string> { 
				{
					"submitCount",
					totalGatherAmount.ToString()
				} }
			},
			Style = ((totalGatherAmount != 0) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary),
			ButtonCallback = () =>
			{
				List<Gathering> list = new List<Gathering>();
				foreach (uint key in _sourcesByInstanceId.Keys)
				{
					list.Add(_sourcesByInstanceId[key].Gathering);
				}
				_request.SubmitGathering(list);
			},
			Enabled = CanSubmitGathering()
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
		OnUpdateButtons(Buttons);
	}

	protected override void SetDimming()
	{
		Dimming.IdToIsDimmed = new Dictionary<uint, bool>(_sourceIds.Count + 1);
		foreach (uint sourceId in _sourceIds)
		{
			Dimming.IdToIsDimmed[sourceId] = false;
		}
		Dimming.IdToIsDimmed[_destinationId] = false;
		OnUpdateDimming(Dimming);
	}

	public bool CanStack(ICardDataAdapter lhs, ICardDataAdapter rhs)
	{
		uint instanceId = lhs.InstanceId;
		uint instanceId2 = rhs.InstanceId;
		bool num = _sourceIds.Contains(instanceId) || _destinationId == instanceId;
		bool flag = _sourceIds.Contains(instanceId2) || _destinationId == instanceId2;
		if (num | flag)
		{
			return false;
		}
		return true;
	}
}
