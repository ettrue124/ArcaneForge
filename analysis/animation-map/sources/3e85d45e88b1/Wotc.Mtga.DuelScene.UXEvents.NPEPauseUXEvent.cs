using System;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class NPEPauseUXEvent : NPEUXEventWithDuration
{
	public override bool IsBlocking => true;

	public NPEPauseUXEvent(Func<NPEDirector> getNpeDirector, float duration)
		: base(getNpeDirector, duration)
	{
	}

	protected override void Cleanup()
	{
		_getNpeDirector().NPEController.CurrentPause = null;
		base.Cleanup();
	}

	public override void Execute()
	{
		_getNpeDirector().NPEController.CurrentPause = this;
	}
}
