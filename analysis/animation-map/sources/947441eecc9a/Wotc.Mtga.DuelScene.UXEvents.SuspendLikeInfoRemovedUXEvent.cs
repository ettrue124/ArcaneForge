namespace Wotc.Mtga.DuelScene.UXEvents;

public class SuspendLikeInfoRemovedUXEvent : UXEvent
{
	private readonly uint _id;

	private readonly ISuspendLikeController _suspendLikeController;

	public SuspendLikeInfoRemovedUXEvent(uint id, ISuspendLikeController suspendLikeController)
	{
		_id = id;
		_suspendLikeController = suspendLikeController;
	}

	public override void Execute()
	{
		_suspendLikeController.RemoveSuspendLikeData(_id);
		Complete();
	}
}
