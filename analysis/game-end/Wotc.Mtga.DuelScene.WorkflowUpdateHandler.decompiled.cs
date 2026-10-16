using GreClient.Rules;
using Wotc.Mtga.DuelScene.Interactions;

namespace Wotc.Mtga.DuelScene;

public class WorkflowUpdateHandler : IClientUpdateHandler<GameStateUpdate>
{
	private readonly WorkflowController _workflowController;

	public WorkflowUpdateHandler(WorkflowController workflowController)
	{
		_workflowController = workflowController;
	}

	public void Handle(GameStateUpdate clientUpdate)
	{
		BaseUserRequest interaction = clientUpdate.Interaction;
		if (interaction != null)
		{
			if (interaction is IntermissionRequest)
			{
				_workflowController.CleanUpCurrentWorkflow();
			}
			_workflowController.EnqueueRequest(interaction);
		}
	}
}
