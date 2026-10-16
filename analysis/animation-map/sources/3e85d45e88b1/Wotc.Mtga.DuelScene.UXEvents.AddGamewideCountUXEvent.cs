using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AddGamewideCountUXEvent : GamewideCountUXEvent
{
	public AddGamewideCountUXEvent(GamewideCountData data, IGamewideCountController gamewideCountController)
		: base(data, gamewideCountController)
	{
	}

	public override void Execute()
	{
		_gamewideCountController.AddGamewideCount(_data);
		Complete();
	}
}
