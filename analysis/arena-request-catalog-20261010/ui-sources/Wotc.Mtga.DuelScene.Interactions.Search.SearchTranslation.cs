using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions.Search;

public class SearchTranslation : IWorkflowTranslation<SearchRequest>
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IGameplaySettingsProvider _gameplaySettings;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	public SearchTranslation(IContext context)
		: this(context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<IGameplaySettingsProvider>(), context.Get<ICardViewProvider>(), context.Get<IBrowserManager>(), context.Get<IBrowserHeaderTextProvider>())
	{
	}

	private SearchTranslation(ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IGameplaySettingsProvider gameplaySettings, ICardViewProvider cardViewProvider, IBrowserManager browserManager, IBrowserHeaderTextProvider headerTextProvider)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_gameplaySettings = gameplaySettings ?? NullGameplaySettingsProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_browserManager = browserManager ?? NullBrowserManager.Default;
		_headerTextProvider = headerTextProvider ?? NullBrowserHeaderTextProvider.Default;
	}

	public WorkflowBase Translate(SearchRequest req)
	{
		if (req.IsMultiZoneSearch)
		{
			return new SearchWorkflow_MultiZone(req, _cardDatabase, _gameStateProvider, _gameplaySettings, _cardViewProvider, _browserManager);
		}
		return new SearchWorkflow(req, _cardDatabase.CardTitleProvider, _gameStateProvider, _gameplaySettings, _cardViewProvider, _browserManager, _headerTextProvider);
	}
}
