using AssetLookupTree;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Interactions.SelectTargets;

namespace Wotc.Mtga.DuelScene.Interactions;

public class SelectTargetsTranslation : IWorkflowTranslation<SelectTargetsRequest>
{
	private readonly IObjectPool _objPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardTitleProvider _cardTitleProvider;

	private readonly IAbilityDataProvider _abilityProvider;

	private readonly IGameplaySettingsProvider _gameplaySettingsProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IPlayWarningManager _playWarningManager;

	private readonly IAutoTargetingSolution _autoTargetingSolution;

	private readonly AssetLookupSystem _assetLookupSystem;

	public SelectTargetsTranslation(IContext context, IAutoTargetingSolution autoTargetingSolution, AssetLookupSystem assetLookupSystem)
		: this(context.Get<IObjectPool>(), context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<IGameplaySettingsProvider>(), context.Get<ICardHolderProvider>(), context.Get<IBrowserManager>(), context.Get<IBrowserHeaderTextProvider>(), context.Get<ICardViewProvider>(), context.Get<IPromptTextProvider>(), context.Get<IPlayWarningManager>(), autoTargetingSolution, assetLookupSystem)
	{
	}

	private SelectTargetsTranslation(IObjectPool objectPool, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IGameplaySettingsProvider gameplaySettingsProvider, ICardHolderProvider cardHolderProvider, IBrowserManager browserManager, IBrowserHeaderTextProvider headerTextProvider, ICardViewProvider cardViewProvider, IPromptTextProvider promptTextProvider, IPlayWarningManager playWarningManager, IAutoTargetingSolution autoTargetingSolution, AssetLookupSystem assetLookupSystem)
	{
		_objPool = objectPool ?? NullObjectPool.Default;
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_gameplaySettingsProvider = gameplaySettingsProvider ?? NullGameplaySettingsProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_browserManager = browserManager ?? NullBrowserManager.Default;
		_headerTextProvider = headerTextProvider ?? NullBrowserHeaderTextProvider.Default;
		_promptTextProvider = promptTextProvider ?? NullPromptTextProvider.Default;
		_playWarningManager = playWarningManager ?? NullPlayWarningManager.Default;
		_autoTargetingSolution = autoTargetingSolution;
		_assetLookupSystem = assetLookupSystem;
	}

	public WorkflowBase Translate(SelectTargetsRequest req)
	{
		return new SelectTargetsWorkflow(req, _objPool, _cardDatabase, _gameStateProvider, _gameplaySettingsProvider, _cardHolderProvider, _browserManager, _headerTextProvider, _cardViewProvider, _promptTextProvider, _playWarningManager, _autoTargetingSolution, _assetLookupSystem);
	}
}
