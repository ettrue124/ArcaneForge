using AssetLookupTree;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class SubmitDeckTranslation : IWorkflowTranslation<SubmitDeckRequest>
{
	private readonly GameManager _gameManager;

	private readonly Camera _camera;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IAccountClient _accountClient;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IDuelSceneStateController _duelSceneStateController;

	private readonly MatchManager _matchManager;

	private readonly AssetLookupSystem _assetLookupSystem;

	public SubmitDeckTranslation(IContext context, AssetLookupSystem assetLookupSystem, Camera camera)
		: this(context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<IDuelSceneStateController>(), context.Get<IAccountClient>(), context.Get<MatchManager>(), assetLookupSystem, camera)
	{
	}

	private SubmitDeckTranslation(ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IDuelSceneStateController duelSceneStateController, IAccountClient accountClient, MatchManager matchManager, AssetLookupSystem assetLookupSystem, Camera camera)
	{
		_camera = camera;
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_accountClient = accountClient;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_duelSceneStateController = duelSceneStateController ?? NullDuelSceneStateController.Default;
		_assetLookupSystem = assetLookupSystem;
		_matchManager = matchManager;
	}

	public WorkflowBase Translate(SubmitDeckRequest req)
	{
		return new SubmitDeckWorkflow(req, _camera, _cardDatabase, _accountClient, _gameStateProvider, _duelSceneStateController, _matchManager, _assetLookupSystem, GetSideboardTimer(_gameStateProvider.LatestGameState));
	}

	private static MtgTimer GetSideboardTimer(MtgGameState gameState)
	{
		return gameState?.LocalPlayer?.Timers.Find((MtgTimer x) => x.TimerType == TimerType.Epilogue);
	}
}
