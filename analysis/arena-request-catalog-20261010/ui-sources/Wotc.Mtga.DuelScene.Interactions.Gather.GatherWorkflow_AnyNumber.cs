using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Interactions.Gather;

public class GatherWorkflow_AnyNumber : GatherWorkflow
{
	private readonly IEntityViewProvider _entityViewProvider;

	private Widget_TextBox _gatherAmountTextBox;

	public GatherWorkflow_AnyNumber(GatherRequest nRequest, ICardHolderProvider cardHolderProvider, IEntityViewProvider entityViewProvider, SpinnerController spinnerController)
		: base(nRequest, cardHolderProvider, spinnerController)
	{
		_entityViewProvider = entityViewProvider;
	}

	protected override IReadOnlyCollection<SpinnerData> GenerateSpinnerData()
	{
		List<SpinnerData> list = new List<SpinnerData>();
		foreach (uint key in _sourcesByInstanceId.Keys)
		{
			EntitySourceSink entitySourceSink = _sourcesByInstanceId[key];
			list.Add(new SpinnerData(key, (int)entitySourceSink.Max, (int)entitySourceSink.Min, (int)entitySourceSink.Max));
		}
		return list;
	}

	protected override void ApplyInteractionInternal()
	{
		base.ApplyInteractionInternal();
		if (_entityViewProvider.TryGetEntity(_destinationId, out var _))
		{
			_gatherAmountTextBox = _spinnerController.CreateWorkflowTextBox(_destinationId);
			_gatherAmountTextBox.SetValue((int)GetTotalGatherAmount());
		}
	}

	protected override void OnValueChanged(uint id, uint value)
	{
		EntitySourceSink entitySourceSink = _sourcesByInstanceId[id];
		_sourcesByInstanceId[id].Gathering.Amount = entitySourceSink.Max - value;
		if (_gatherAmountTextBox != null)
		{
			_gatherAmountTextBox.SetValue((int)GetTotalGatherAmount());
		}
		base.OnValueChanged(id, value);
	}
}
