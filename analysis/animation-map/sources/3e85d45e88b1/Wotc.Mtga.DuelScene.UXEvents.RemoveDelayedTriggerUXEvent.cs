namespace Wotc.Mtga.DuelScene.UXEvents;

public class RemoveDelayedTriggerUXEvent : DelayedTriggerUXEvent
{
	private readonly uint _toRemoveId;

	public RemoveDelayedTriggerUXEvent(IDelayedTriggerController delayedTriggerController, uint toDelete)
		: base(delayedTriggerController)
	{
		_toRemoveId = toDelete;
	}

	public override void Execute()
	{
		_delayedTriggerController.RemoveDelayedTrigger(_toRemoveId);
		Complete();
	}
}
