namespace Wotc.Mtga.DuelScene.UXEvents;

public class WaitForSecondsUXEvent : UXEvent
{
	private readonly float _secondsToWait;

	public override bool IsBlocking => true;

	public WaitForSecondsUXEvent(float seconds = 0f)
	{
		_secondsToWait = seconds;
		_timeOutTarget = 1f + seconds;
	}

	public override void Execute()
	{
		TryComplete();
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		TryComplete();
	}

	private void TryComplete()
	{
		if (_secondsToWait <= _timeRunning)
		{
			Complete();
		}
	}
}
