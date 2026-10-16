using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Stacking : WorkflowBase<SelectNRequest>, IAutoRespondWorkflow
{
	public SelectNWorkflow_Stacking(SelectNRequest request)
		: base(request)
	{
	}

	protected override void ApplyInteractionInternal()
	{
	}

	public bool TryAutoRespond()
	{
		_request.SubmitSelection(_request.Ids[0]);
		return true;
	}
}
