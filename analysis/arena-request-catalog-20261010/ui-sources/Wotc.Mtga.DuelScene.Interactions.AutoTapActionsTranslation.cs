using AssetLookupTree;
using GreClient.Rules;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions;

public class AutoTapActionsTranslation : IWorkflowTranslation<AutoTapActionsRequest>
{
	private readonly IObjectPool _objectPool;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly IBrowserController _browserController;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IPlayWarningManager _playWarningManager;

	private readonly AutoTapActionsWorkflow.AutoTapButtonTextFetcher _buttonTextFetcher;

	private readonly AssetLookupSystem _assetLookupSystem;

	public AutoTapActionsTranslation(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<IObjectPool>(), context.Get<IGameStateProvider>(), context.Get<IPromptTextProvider>(), context.Get<IBrowserController>(), context.Get<ICardHolderProvider>(), context.Get<IPlayWarningManager>(), new AutoTapActionsWorkflow.AutoTapButtonTextFetcher(assetLookupSystem, context.Get<IAbilityDataProvider>(), context.Get<IObjectPool>()), assetLookupSystem)
	{
	}

	private AutoTapActionsTranslation(IObjectPool objectPool, IGameStateProvider gameStateProvider, IPromptTextProvider promptTextProvider, IBrowserController browserController, ICardHolderProvider cardHolderProvider, IPlayWarningManager playWarningManager, AutoTapActionsWorkflow.AutoTapButtonTextFetcher buttonTextFetcher, AssetLookupSystem assetLookupSystem)
	{
		_objectPool = objectPool ?? NullObjectPool.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_promptTextProvider = promptTextProvider ?? NullPromptTextProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_playWarningManager = playWarningManager ?? NullPlayWarningManager.Default;
		_buttonTextFetcher = buttonTextFetcher;
		_assetLookupSystem = assetLookupSystem;
	}

	public WorkflowBase Translate(AutoTapActionsRequest req)
	{
		return new AutoTapActionsWorkflow(req, _objectPool, _gameStateProvider, _promptTextProvider, _browserController, _cardHolderProvider, _playWarningManager, _buttonTextFetcher, _assetLookupSystem);
	}
}
