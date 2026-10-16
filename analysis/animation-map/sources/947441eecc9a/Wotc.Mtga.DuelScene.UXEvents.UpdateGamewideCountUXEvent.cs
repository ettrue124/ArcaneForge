using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateGamewideCountUXEvent : GamewideCountUXEvent
{
	public UpdateGamewideCountUXEvent(GamewideCountData data, IGamewideCountController gamewideCountController)
		: base(data, gamewideCountController)
	{
	}

	public override void Execute()
	{
		_gamewideCountController.UpdateGamewideCount(_data);
		Complete();
	}
}
