using System;
using System.Collections.Generic;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AttackDecrementUXEvent : NPEUXEvent
{
	private uint _quarryId;

	public override bool HasWeight => true;

	public AttackDecrementUXEvent(Func<NPEDirector> getNpeDirector, uint quarryId)
		: base(getNpeDirector)
	{
		_quarryId = quarryId;
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		return true;
	}

	public override void Execute()
	{
		_getNpeDirector().AttackHaloTabulation(_quarryId, -1);
		Complete();
	}
}
