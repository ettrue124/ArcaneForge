namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class DelayedTriggerUXEvent : UXEvent
{
	protected readonly IDelayedTriggerController _delayedTriggerController;

	public DelayedTriggerUXEvent(IDelayedTriggerController delayedTriggerController)
	{
		_delayedTriggerController = delayedTriggerController ?? NullDelayedTriggerController.Default;
	}
}
