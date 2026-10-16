using System;

namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class NPEUXEventWithDuration : NPEUXEvent
{
	protected float _duration;

	protected NPEUXEventWithDuration(Func<NPEDirector> getNpeDirector, float duration)
		: base(getNpeDirector)
	{
		_duration = duration;
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if (_duration < _timeRunning)
		{
			Complete();
		}
	}
}
