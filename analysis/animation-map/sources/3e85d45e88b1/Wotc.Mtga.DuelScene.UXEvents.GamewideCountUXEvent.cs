using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class GamewideCountUXEvent : UXEvent
{
	protected readonly GamewideCountData _data;

	protected readonly IGamewideCountController _gamewideCountController;

	public GamewideCountUXEvent(GamewideCountData data, IGamewideCountController gamewideCountController)
	{
		_data = data;
		_gamewideCountController = gamewideCountController ?? NullGamewideController.Default;
	}
}
