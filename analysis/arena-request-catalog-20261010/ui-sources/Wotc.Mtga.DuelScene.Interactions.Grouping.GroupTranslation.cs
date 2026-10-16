using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.Grouping;

public class GroupTranslation : IWorkflowTranslation<GroupRequest>
{
	private readonly GameManager _gameManager;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBrowserManager _browserManager;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IObjectPool _genericPool;

	private readonly IGreLocProvider _greLocProvider;

	private readonly IHighlightController _highlightController;

	private readonly ICardHoverController _cardHoverController;

	private readonly IWorkflowProvider _workflowProvider;

	public GroupTranslation(IContext context, GameManager gameManager)
		: this(context.Get<IObjectPool>(), context.Get<ICardHolderProvider>(), context.Get<ICardDatabaseAdapter>(), context.Get<IBrowserManager>(), context.Get<ICardViewProvider>(), context.Get<IHighlightController>(), context.Get<ICardHoverController>(), context.Get<IWorkflowProvider>(), context.Get<IGameStateProvider>(), gameManager)
	{
	}

	private GroupTranslation(IObjectPool genericPool, ICardHolderProvider cardHolderProvider, ICardDatabaseAdapter cardDatabase, IBrowserManager browserManager, ICardViewProvider cardViewProvider, IHighlightController highlightController, ICardHoverController cardHoverController, IWorkflowProvider workflowProvider, IGameStateProvider gameStateProvider, GameManager gameManager)
	{
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_browserManager = browserManager ?? NullBrowserManager.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_highlightController = highlightController ?? NullHighlightController.Default;
		_cardHoverController = cardHoverController;
		_workflowProvider = workflowProvider ?? NullWorkflowProvider.Default;
		_genericPool = genericPool;
		_gameManager = gameManager;
	}

	public WorkflowBase Translate(GroupRequest req)
	{
		if (req.Context == GroupingContext.Surveil)
		{
			return new SurveilWorkflow(req, _gameStateProvider, _cardDatabase, _browserManager, _cardViewProvider);
		}
		if (req.Context == GroupingContext.Scry)
		{
			return new ScryWorkflow(req, _cardDatabase.ClientLocProvider, _cardViewProvider, _browserManager);
		}
		if (req.Context == GroupingContext.LondonMulligan)
		{
			return new LondonWorkflow(req, _cardViewProvider, _cardDatabase, _browserManager, _gameManager.Logger);
		}
		if (IsLilianaOfTheVeilUltimate(_gameStateProvider.LatestGameState))
		{
			return new GroupWorkflow_BattlefieldPermanents(req, _cardHolderProvider, _cardDatabase.ClientLocProvider, _browserManager, _genericPool, _cardViewProvider, _cardDatabase.CardTitleProvider, _highlightController, _cardHoverController, _workflowProvider, _gameStateProvider, _gameManager.SpinnerController);
		}
		return new GroupWorkflow(req, _cardViewProvider, _browserManager);
	}

	private static bool IsLilianaOfTheVeilUltimate(MtgGameState state)
	{
		if (state.ResolvingCardInstance != null && state.ResolvingCardInstance.GrpId != 0)
		{
			return state.ResolvingCardInstance.GrpId == 99470;
		}
		return false;
	}
}
