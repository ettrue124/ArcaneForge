using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Interactions.AssignDamage;

public class AutoAssignDamageTranslation : IWorkflowTranslation<AssignDamageRequest>
{
	public WorkflowBase Translate(AssignDamageRequest req)
	{
		return new AutoAssignDamageWorkflow(req);
	}
}
