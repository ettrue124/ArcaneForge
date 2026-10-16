using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Interactions;

public class EffectCostTranslation : IWorkflowTranslation<EffectCostRequest>
{
	private readonly IWorkflowTranslator _workflowTranslator;

	public EffectCostTranslation(IWorkflowTranslator workflowTranslator)
	{
		_workflowTranslator = workflowTranslator;
	}

	public WorkflowBase Translate(EffectCostRequest req)
	{
		return new EffectCostWorkflow(req, _workflowTranslator);
	}
}
