using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AddDelayedTriggerUXEvent : DelayedTriggerUXEvent
{
	private readonly MtgCardInstance _delayedTrigger;

	public AddDelayedTriggerUXEvent(IDelayedTriggerController delayedTriggerController, MtgCardInstance delayedTrigger)
		: base(delayedTriggerController)
	{
		_delayedTrigger = delayedTrigger;
	}

	public override void Execute()
	{
		_delayedTriggerController.AddDelayedTrigger(_delayedTrigger);
		Complete();
	}
}
