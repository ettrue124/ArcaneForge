using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class NPEActionsAvailableWorkflowTranslation : IWorkflowTranslation<ActionsAvailableRequest>
{
	private readonly GameManager _gameManager;

	private readonly ClientSideInteraction _clientSideInteraction;

	private readonly IContext _context;

	private readonly NPEDirector _director;

	public NPEActionsAvailableWorkflowTranslation(GameManager gameManager, ClientSideInteraction clientSideInteraction, IContext context, NPEDirector director)
	{
		_gameManager = gameManager;
		_clientSideInteraction = clientSideInteraction;
		_context = context ?? NullContext.Default;
		_director = director;
	}

	public WorkflowBase Translate(ActionsAvailableRequest req)
	{
		return new ActionsAvailableWorkflow_NPE(req, _gameManager, _clientSideInteraction, _context, _director);
	}
}
