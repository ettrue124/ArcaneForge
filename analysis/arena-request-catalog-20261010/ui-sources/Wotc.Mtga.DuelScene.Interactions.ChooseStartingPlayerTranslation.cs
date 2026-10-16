using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions;

public class ChooseStartingPlayerTranslation : IWorkflowTranslation<ChooseStartingPlayerRequest>
{
	private readonly IClientLocProvider _locProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBrowserController _browserController;

	public ChooseStartingPlayerTranslation(IContext context)
		: this(context.Get<IClientLocProvider>(), context.Get<IGameStateProvider>(), context.Get<IBrowserController>())
	{
	}

	private ChooseStartingPlayerTranslation(IClientLocProvider locProvider, IGameStateProvider gameStateProvider, IBrowserController browserController)
	{
		_locProvider = locProvider;
		_gameStateProvider = gameStateProvider;
		_browserController = browserController;
	}

	public WorkflowBase Translate(ChooseStartingPlayerRequest req)
	{
		return new ChooseStartingPlayerWorkflow(req, _locProvider, _gameStateProvider, _browserController);
	}
}
