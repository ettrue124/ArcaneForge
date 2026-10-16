using System;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class NPEReminderUXEvent : NPEUXEvent
{
	private NPEReminder _reminder;

	public NPEReminderUXEvent(Func<NPEDirector> getNpeDirector, NPEReminder reminder)
		: base(getNpeDirector)
	{
		_reminder = reminder;
	}

	public override void Execute()
	{
		_getNpeDirector().NPEController.BeginQueuedReminder(_reminder);
		Complete();
	}
}
