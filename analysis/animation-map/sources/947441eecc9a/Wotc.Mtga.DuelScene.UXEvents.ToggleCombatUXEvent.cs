using System;
using System.Collections.Generic;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ToggleCombatUXEvent : NPEUXEvent
{
	private CombatState _CombatMode;

	public override bool HasWeight => true;

	public ToggleCombatUXEvent(Func<NPEDirector> getNpeDirector, CombatState combatMode)
		: base(getNpeDirector)
	{
		_CombatMode = combatMode;
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		return true;
	}

	public override void Execute()
	{
		NPEController nPEController = _getNpeDirector().NPEController;
		nPEController._SparkyCombatState = _CombatMode;
		if (_CombatMode == CombatState.CombatBegun)
		{
			nPEController.SetDynamicSparkyCombatXPosition();
		}
		Complete();
	}
}
