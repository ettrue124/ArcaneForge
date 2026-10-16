using GreClient.Rules;

namespace Wotc.Mtga.DuelScene;

public class GameStatePlaybackController : IGameStatePlaybackController
{
	private readonly IGameStateController _gameStateController;

	private readonly IDuelSceneStateController _duelSceneStateController;

	private readonly ISignalDispatch<GameStatePlaybackStartedSignalArgs> _playbackStarted;

	private readonly ISignalDispatch<GameStatePlaybackCompleteSignalArgs> _playbackComplete;

	public GameStatePlaybackController(IGameStateController gameStateController, IDuelSceneStateController duelSceneStateController, ISignalDispatch<GameStatePlaybackStartedSignalArgs> playbackStarted, ISignalDispatch<GameStatePlaybackCompleteSignalArgs> playbackComplete)
	{
		_gameStateController = gameStateController;
		_duelSceneStateController = duelSceneStateController;
		_playbackStarted = playbackStarted;
		_playbackComplete = playbackComplete;
	}

	public void StartPlayback(MtgGameState gameState)
	{
		_gameStateController.SetCurrentGameState(gameState);
		_playbackStarted.Dispatch(new GameStatePlaybackStartedSignalArgs(this, gameState));
	}

	public void CompletePlayback(MtgGameState gameState)
	{
		IDuelSceneStateController duelSceneStateController = _duelSceneStateController;
		bool? firstGameStatePlaybackComplete = true;
		duelSceneStateController.SetState(null, firstGameStatePlaybackComplete);
		_playbackComplete.Dispatch(new GameStatePlaybackCompleteSignalArgs(this, gameState));
	}
}
