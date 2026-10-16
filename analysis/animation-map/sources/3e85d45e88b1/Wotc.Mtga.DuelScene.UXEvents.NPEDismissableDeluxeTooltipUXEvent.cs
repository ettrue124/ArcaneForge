using System;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class NPEDismissableDeluxeTooltipUXEvent : NPEUXEvent
{
	private DeluxeTooltipType _type;

	public override bool HasWeight => true;

	public override bool IsBlocking => true;

	public NPEDismissableDeluxeTooltipUXEvent(Func<NPEDirector> getNpeDirector, DeluxeTooltipType tooltipType)
		: base(getNpeDirector)
	{
		_canTimeOut = false;
		_type = tooltipType;
	}

	public override void Execute()
	{
		_getNpeDirector().NPEController.LaunchDismissableDeluxeTooltip(_type, Complete);
	}
}
