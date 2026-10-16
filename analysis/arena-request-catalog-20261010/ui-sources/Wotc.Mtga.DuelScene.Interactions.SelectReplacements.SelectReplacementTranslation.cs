using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions.SelectReplacements;

public class SelectReplacementTranslation : IWorkflowTranslation<SelectReplacementRequest>
{
	private readonly IObjectPool _objectPool;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IFakeCardViewController _fakeCardViewController;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	public SelectReplacementTranslation(IContext context)
	{
		_objectPool = context.Get<IObjectPool>() ?? NullObjectPool.Default;
		_cardViewProvider = context.Get<ICardViewProvider>() ?? NullCardViewProvider.Default;
		_gameStateProvider = context.Get<IGameStateProvider>() ?? NullGameStateProvider.Default;
		_fakeCardViewController = context.Get<IFakeCardViewController>() ?? NullFakeCardViewController.Default;
		_cardDatabase = context.Get<ICardDatabaseAdapter>() ?? NullCardDatabaseAdapter.Default;
		_browserController = context.Get<IBrowserController>() ?? NullBrowserController.Default;
		_headerTextProvider = context.Get<IBrowserHeaderTextProvider>() ?? NullBrowserHeaderTextProvider.Default;
	}

	public WorkflowBase Translate(SelectReplacementRequest req)
	{
		if (!PickWhichTurtleToHurtle(req))
		{
			return new SelectReplacementWorkflow(req, _cardDatabase, _gameStateProvider, _fakeCardViewController, _browserController, _headerTextProvider);
		}
		return new TurtleHurtleWorkflow(req, _objectPool, _cardViewProvider);
	}

	private static bool PickWhichTurtleToHurtle(SelectReplacementRequest req)
	{
		return req.ObjectSelections.Count > 0;
	}
}
