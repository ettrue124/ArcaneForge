using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.UXEvents;

namespace Wotc.Mtga.DuelScene.Interactions;

public class SearchFromGroupsTranslation : IWorkflowTranslation<SearchFromGroupsRequest>
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly IBrowserController _browserController;

	private readonly UXEventQueue _eventQueue;

	public SearchFromGroupsTranslation(IContext context)
		: this(context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<IEntityViewProvider>(), context.Get<IPromptTextProvider>(), context.Get<IBrowserController>(), context.Get<UXEventQueue>())
	{
	}

	public SearchFromGroupsTranslation(ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IEntityViewProvider entityViewProvider, IPromptTextProvider promptTextProvider, IBrowserController browserController, UXEventQueue eventQueue)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_entityViewProvider = entityViewProvider ?? NullEntityViewProvider.Default;
		_promptTextProvider = promptTextProvider ?? NullPromptTextProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_eventQueue = eventQueue ?? new UXEventQueue();
	}

	public WorkflowBase Translate(SearchFromGroupsRequest req)
	{
		return new SearchFromGroupsWorkflow(req, _cardDatabase, _gameStateProvider, _entityViewProvider, _promptTextProvider, _browserController, _eventQueue);
	}
}
