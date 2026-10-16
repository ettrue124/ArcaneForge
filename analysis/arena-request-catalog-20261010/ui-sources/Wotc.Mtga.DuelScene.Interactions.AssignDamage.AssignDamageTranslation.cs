using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Interactions.AssignDamage;

public class AssignDamageTranslation : IWorkflowTranslation<AssignDamageRequest>
{
	private readonly IContext _context;

	public AssignDamageTranslation(IContext context)
	{
		_context = context;
	}

	public WorkflowBase Translate(AssignDamageRequest req)
	{
		return new AssignDamageWorkflow(req, _context);
	}
}
