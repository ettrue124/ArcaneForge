using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Interactions.Gather;

public class GatherWorkflow_RemoveExact : GatherWorkflow
{
	private readonly uint _exactAmount;

	public GatherWorkflow_RemoveExact(GatherRequest nRequest, ICardHolderProvider cardHolderProvider, SpinnerController spinnerController)
		: base(nRequest, cardHolderProvider, spinnerController)
	{
		_exactAmount = nRequest.AmountToGather;
	}

	protected override IReadOnlyCollection<SpinnerData> GenerateSpinnerData()
	{
		List<SpinnerData> list = new List<SpinnerData>();
		foreach (uint key in _sourcesByInstanceId.Keys)
		{
			EntitySourceSink entitySourceSink = _sourcesByInstanceId[key];
			list.Add(new SpinnerData(key, 0, (int)entitySourceSink.Min, (int)entitySourceSink.Max));
		}
		return list;
	}

	protected override void OnValueChanged(uint id, uint value)
	{
		_sourcesByInstanceId[id].Gathering.Amount = value;
		base.OnValueChanged(id, value);
	}

	protected override bool CanSubmitGathering()
	{
		if (base.CanSubmitGathering())
		{
			return GetTotalGatherAmount() == _exactAmount;
		}
		return false;
	}
}
