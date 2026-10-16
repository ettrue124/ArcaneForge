using System;
using System.Collections.Generic;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class QuarryHaloUXEvent : NPEUXEvent
{
	private bool _topActive;

	private bool _bottomActive;

	public override bool HasWeight => true;

	public QuarryHaloUXEvent(Func<NPEDirector> getNpeDirector, bool topActive, bool bottomActive)
		: base(getNpeDirector)
	{
		_topActive = topActive;
		_bottomActive = bottomActive;
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		return true;
	}

	public override void Execute()
	{
		NPEController nPEController = _getNpeDirector().NPEController;
		nPEController.TopQuarryHalo.SetActive(_topActive);
		nPEController.BottomQuarryHalo.SetActive(_bottomActive);
		Complete();
	}
}
