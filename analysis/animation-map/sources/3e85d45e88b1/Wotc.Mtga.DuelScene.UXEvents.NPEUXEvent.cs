using System;
using System.Collections.Generic;

namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class NPEUXEvent : UXEvent
{
	protected Func<NPEDirector> _getNpeDirector;

	public override bool HasWeight => false;

	protected NPEUXEvent(Func<NPEDirector> getNpeDirector)
	{
		_getNpeDirector = getNpeDirector;
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		foreach (UXEvent currentlyRunningEvent in currentlyRunningEvents)
		{
			if (currentlyRunningEvent is NPEUXEventWithDuration || currentlyRunningEvent is NPEDismissableDeluxeTooltipUXEvent)
			{
				return false;
			}
		}
		return true;
	}

	protected override void Cleanup()
	{
		_getNpeDirector = null;
		base.Cleanup();
	}
}
