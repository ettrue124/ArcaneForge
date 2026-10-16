using System;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class WaitUntilUXEvent : UXEvent
{
	private readonly Func<bool> _condition;

	public override bool IsBlocking => !_condition();

	public WaitUntilUXEvent(Func<bool> condition, float timeOutOverride = 10f)
	{
		_condition = condition;
		_timeOutTarget = timeOutOverride;
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
		try
		{
			if (_condition())
			{
				Complete();
			}
		}
		catch
		{
			Fail();
		}
	}
}
