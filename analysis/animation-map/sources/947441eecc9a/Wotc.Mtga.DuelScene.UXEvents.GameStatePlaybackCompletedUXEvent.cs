using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class GameStatePlaybackCompletedUXEvent : UXEvent
{
	public readonly MtgGameState GameState;

	private readonly IGameStatePlaybackController _playbackController;

	public GameStatePlaybackCompletedUXEvent()
	{
	}

	public GameStatePlaybackCompletedUXEvent(MtgGameState gameState, IGameStatePlaybackController playbackController)
	{
		GameState = gameState;
		_playbackController = playbackController ?? NullGameStatePlaybackController.Default;
	}

	public override void Execute()
	{
		_playbackController.CompletePlayback(GameState);
		Complete();
	}
}
