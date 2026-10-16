using AssetLookupTree;
using GreClient.Rules;
using MovementSystem;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions.Mulligan;

public class MulliganTranslation : IWorkflowTranslation<MulliganRequest>
{
	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly ICardViewManager _cardViewManager;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IDuelSceneStateProvider _stateProvider;

	private readonly IBrowserController _browserController;

	private readonly ICanvasRootProvider _canvasRootProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly GameManager _gameManager;

	public MulliganTranslation(IContext context, AssetLookupSystem assetLookupSystem, GameManager gameManager)
		: this(context.Get<ISplineMovementSystem>(), context.Get<ICardViewManager>(), context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<ICardHolderProvider>(), context.Get<IDuelSceneStateProvider>(), context.Get<IBrowserController>(), context.Get<ICanvasRootProvider>(), assetLookupSystem, gameManager)
	{
	}

	private MulliganTranslation(ISplineMovementSystem splineMovementSystem, ICardViewManager cardViewManager, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardHolderProvider cardHolderProvider, IDuelSceneStateProvider stateProvider, IBrowserController browserController, ICanvasRootProvider canvasRootProvider, AssetLookupSystem assetLookupSystem, GameManager gameManager)
	{
		_splineMovementSystem = splineMovementSystem;
		_cardViewManager = cardViewManager;
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_cardHolderProvider = cardHolderProvider;
		_stateProvider = stateProvider;
		_browserController = browserController;
		_canvasRootProvider = canvasRootProvider;
		_assetLookupSystem = assetLookupSystem;
		_gameManager = gameManager;
	}

	public WorkflowBase Translate(MulliganRequest req)
	{
		return new MulliganWorkflow(req, _cardViewManager, _cardDatabase, _gameStateProvider, _browserController, _canvasRootProvider, _assetLookupSystem, new CardPreviewAnimation(_splineMovementSystem, _cardViewManager, _cardDatabase.CardDataProvider, _gameStateProvider, _cardHolderProvider, _stateProvider), _gameManager);
	}
}
