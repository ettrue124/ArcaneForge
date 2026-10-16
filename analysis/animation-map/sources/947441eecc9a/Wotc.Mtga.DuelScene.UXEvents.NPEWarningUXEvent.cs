using System;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class NPEWarningUXEvent : NPEUXEventWithDuration
{
	protected MTGALocalizedString _displayText;

	public NPEWarningUXEvent(Func<NPEDirector> getNpeDirector, MTGALocalizedString displayText, float duration)
		: base(getNpeDirector, duration)
	{
		_displayText = displayText;
	}

	public override void Execute()
	{
		_getNpeDirector().NPEController.ShowWarning(_displayText);
	}

	protected override void Cleanup()
	{
		_getNpeDirector().NPEController.StopShowingWarning();
		base.Cleanup();
	}
}
