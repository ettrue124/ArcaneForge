using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class SuspendLikeInfoCreatedUXEvent : UXEvent
{
	private readonly SuspendLikeData _suspendData;

	private readonly ISuspendLikeController _suspendLikeController;

	public SuspendLikeInfoCreatedUXEvent(SuspendLikeData data, ISuspendLikeController suspendLikeController)
	{
		_suspendData = data;
		_suspendLikeController = suspendLikeController;
	}

	public override void Execute()
	{
		_suspendLikeController.AddSuspendLikeData(_suspendData);
		Complete();
	}
}
