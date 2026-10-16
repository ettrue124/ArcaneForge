using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class DefaultTranslation : IWorkflowTranslation<ActionsAvailableRequest>
{
	private readonly GameManager _gameManager;

	private readonly ClientSideInteraction _clientSideInteraction;

	private readonly IContext _context;

	public DefaultTranslation(GameManager gameManager, ClientSideInteraction clientSideInteraction, IContext context)
	{
		_gameManager = gameManager;
		_clientSideInteraction = clientSideInteraction;
		_context = context ?? NullContext.Default;
	}

	public WorkflowBase Translate(ActionsAvailableRequest req)
	{
		return new ActionsAvailableWorkflow(req, _gameManager, _clientSideInteraction, _context);
	}
}
