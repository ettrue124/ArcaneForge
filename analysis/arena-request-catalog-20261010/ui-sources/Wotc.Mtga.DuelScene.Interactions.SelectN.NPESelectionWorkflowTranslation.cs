using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class NPESelectionWorkflowTranslation : IWorkflowTranslation<SelectNRequest>
{
	private readonly NPEDirector _director;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IResolutionEffectProvider _resolutionEffectProvider;

	private readonly IGameplaySettingsProvider _gameplaySettingsProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IBrowserManager _browserManager;

	private readonly ISelectionConfirmation _selectionConfirmation;

	private readonly IPlayWarningManager _playWarningManager;

	private readonly AssetLookupSystem _assetLookupSystem;

	public NPESelectionWorkflowTranslation(IContext context, AssetLookupSystem assetLookupSystem, NPEDirector director)
	{
		_cardDatabase = context.Get<ICardDatabaseAdapter>() ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = context.Get<IGameStateProvider>() ?? NullGameStateProvider.Default;
		_resolutionEffectProvider = context.Get<IResolutionEffectProvider>() ?? NullResolutionEffectProvider.Default;
		_gameplaySettingsProvider = context.Get<IGameplaySettingsProvider>() ?? NullGameplaySettingsProvider.Default;
		_cardViewProvider = context.Get<ICardViewProvider>() ?? NullCardViewProvider.Default;
		_cardHolderProvider = context.Get<ICardHolderProvider>() ?? NullCardHolderProvider.Default;
		_browserManager = context.Get<IBrowserManager>() ?? NullBrowserManager.Default;
		_selectionConfirmation = SelectionConfirmation.CreateDefault(context);
		_playWarningManager = context.Get<IPlayWarningManager>() ?? NullPlayWarningManager.Default;
		_assetLookupSystem = assetLookupSystem;
		_director = director;
	}

	public WorkflowBase Translate(SelectNRequest req)
	{
		return new SelectionWorkflow_NPE(req, _cardDatabase, _gameStateProvider, _resolutionEffectProvider, _gameplaySettingsProvider, _cardViewProvider, _cardHolderProvider, _browserManager, _selectionConfirmation, _playWarningManager, _assetLookupSystem, _director);
	}
}
