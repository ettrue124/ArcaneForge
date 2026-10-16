using System;

namespace Wotc.Mtga.DuelScene.Interactions;

public class MutableWorkflowProvider : IWorkflowProvider, IClickableWorkflowProvider, IDisposable
{
	public WorkflowBase Workflow;

	public WorkflowBase GetCurrentWorkflow()
	{
		if (Workflow == null || Workflow.AppliedState != InteractionAppliedState.Applied)
		{
			return null;
		}
		return Workflow;
	}

	public WorkflowBase GetPendingWorkflow()
	{
		if (Workflow == null || Workflow.AppliedState == InteractionAppliedState.Applied)
		{
			return null;
		}
		return Workflow;
	}

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		return GetCurrentWorkflow().CanClick(entity, clickType);
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		GetCurrentWorkflow().OnClick(entity, clickType);
	}

	public void Dispose()
	{
		Workflow = null;
	}
}
