using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class GameStatePlaybackCommencedUXEvent : UXEvent
{
	public readonly MtgGameState GameState;

	private readonly IGameStatePlaybackController _playbackController;

	public GameStatePlaybackCommencedUXEvent()
	{
	}

	public GameStatePlaybackCommencedUXEvent(MtgGameState gameState, IGameStatePlaybackController playbackController)
	{
		GameState = gameState;
		_playbackController = playbackController ?? NullGameStatePlaybackController.Default;
	}

	public override void Execute()
	{
		_playbackController.StartPlayback(GameState);
		Complete();
	}
}
