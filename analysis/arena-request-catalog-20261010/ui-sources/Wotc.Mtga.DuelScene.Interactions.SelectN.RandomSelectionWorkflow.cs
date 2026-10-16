using System;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class RandomSelectionWorkflow : WorkflowBase<SelectNRequest>
{
	public RandomSelectionWorkflow(SelectNRequest request)
		: base(request)
	{
	}

	protected override void ApplyInteractionInternal()
	{
		int index = new Random().Next(0, _request.Ids.Count);
		_request.SubmitSelection(_request.Ids[index]);
	}
}
