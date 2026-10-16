using System.Collections.Generic;
using AssetLookupTree.Blackboard;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Interactions;

namespace AssetLookupTree.Indirectors.Interaction.ActionsAvailable;

public class Interaction_ChildWorkflow : IIndirector
{
	private BaseUserRequest _cahcedRequest;

	private WorkflowBase _cachedInteraction;

	public void SetCache(IBlackboard bb)
	{
		_cahcedRequest = bb.Request;
		_cachedInteraction = bb.Interaction;
	}

	public void ClearCache(IBlackboard bb)
	{
		bb.Request = _cahcedRequest;
		bb.Interaction = _cachedInteraction;
	}

	public IEnumerable<IBlackboard> Execute(IBlackboard bb)
	{
		foreach (WorkflowBase childWorkflow in GetChildWorkflows(_cachedInteraction))
		{
			bb.Request = childWorkflow.BaseRequest;
			bb.Interaction = childWorkflow;
			yield return bb;
		}
	}

	private IEnumerable<WorkflowBase> GetChildWorkflows(WorkflowBase workflowBase)
	{
		if (!(workflowBase is IParentWorkflow parentWorkflow))
		{
			yield break;
		}
		foreach (WorkflowBase childWorkflow in parentWorkflow.ChildWorkflows)
		{
			yield return childWorkflow;
			foreach (WorkflowBase childWorkflow2 in GetChildWorkflows(childWorkflow))
			{
				yield return childWorkflow2;
			}
		}
	}
}
