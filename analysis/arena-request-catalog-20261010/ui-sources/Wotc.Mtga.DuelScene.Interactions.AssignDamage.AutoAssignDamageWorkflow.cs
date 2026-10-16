using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Interactions.AssignDamage;

public class AutoAssignDamageWorkflow : WorkflowBase<AssignDamageRequest>
{
	public AutoAssignDamageWorkflow(AssignDamageRequest req)
		: base(req)
	{
	}

	protected override void ApplyInteractionInternal()
	{
		_request.SubmitAssignment(_request.Assigners);
	}
}
