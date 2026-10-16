using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class SuspendLikeInfoUpdatedUXEvent : UXEvent
{
	private readonly SuspendLikeData _suspendData;

	private readonly ISuspendLikeController _suspendLikeController;

	public SuspendLikeInfoUpdatedUXEvent(SuspendLikeData data, ISuspendLikeController suspendLikeController)
	{
		_suspendData = data;
		_suspendLikeController = suspendLikeController;
	}

	public override void Execute()
	{
		_suspendLikeController.UpdateSuspendLikeData(_suspendData);
		Complete();
	}
}
