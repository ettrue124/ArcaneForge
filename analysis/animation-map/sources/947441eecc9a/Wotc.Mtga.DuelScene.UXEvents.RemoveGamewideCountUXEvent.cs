using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RemoveGamewideCountUXEvent : GamewideCountUXEvent
{
	public RemoveGamewideCountUXEvent(GamewideCountData data, IGamewideCountController gamewideCountController)
		: base(data, gamewideCountController)
	{
	}

	public override void Execute()
	{
		_gamewideCountController.RemoveGamewideCount(_data);
		Complete();
	}
}
