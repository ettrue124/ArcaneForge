using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions;

public class OrderTranslation : IWorkflowTranslation<OrderRequest>
{
	private readonly IClientLocProvider _locProvider;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IGameplaySettingsProvider _settingsProvider;

	private readonly IResolutionEffectProvider _resolutionEffectProvider;

	private readonly IBrowserController _browserController;

	private readonly AssetLookupSystem _assetLookupSystem;

	public OrderTranslation(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<IClientLocProvider>(), context.Get<IPromptTextProvider>(), context.Get<IGameStateProvider>(), context.Get<ICardViewProvider>(), context.Get<IGameplaySettingsProvider>(), context.Get<IResolutionEffectProvider>(), context.Get<IBrowserController>(), assetLookupSystem)
	{
	}

	private OrderTranslation(IClientLocProvider locProvider, IPromptTextProvider promptTextProvider, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IGameplaySettingsProvider settingsProvider, IResolutionEffectProvider resolutionEffectProvider, IBrowserController browserController, AssetLookupSystem assetLookupSystem)
	{
		_locProvider = locProvider ?? NullLocProvider.Default;
		_promptTextProvider = promptTextProvider ?? NullPromptTextProvider.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_settingsProvider = settingsProvider ?? NullGameplaySettingsProvider.Default;
		_resolutionEffectProvider = resolutionEffectProvider ?? NullResolutionEffectProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_assetLookupSystem = assetLookupSystem;
	}

	public WorkflowBase Translate(OrderRequest req)
	{
		return new OrderWorkflow(req, _locProvider, _promptTextProvider, _gameStateProvider, _cardViewProvider, _settingsProvider, _resolutionEffectProvider, _browserController, _assetLookupSystem);
	}
}
