using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateDelayedTriggerUXEvent : DelayedTriggerUXEvent
{
	private readonly MtgCardInstance _delayedTrigger;

	public UpdateDelayedTriggerUXEvent(IDelayedTriggerController delayedTriggerController, MtgCardInstance delayedTrigger)
		: base(delayedTriggerController)
	{
		_delayedTrigger = delayedTrigger;
	}

	public override void Execute()
	{
		_delayedTriggerController.UpdateDelayedTrigger(_delayedTrigger);
		Complete();
	}
}
