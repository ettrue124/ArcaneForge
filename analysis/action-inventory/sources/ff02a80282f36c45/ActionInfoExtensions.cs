using System;
using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

public static class ActionInfoExtensions
{
	public static IReadOnlyCollection<ManaRequirement> GetManaCost(this ActionInfo actionInfo)
	{
		if (actionInfo == null || actionInfo.Action == null || actionInfo.Action.ManaCost == null)
		{
			return Array.Empty<ManaRequirement>();
		}
		return actionInfo.Action.ManaCost;
	}

	public static uint GetSourceId(this ActionInfo actionInfo)
	{
		if (actionInfo == null || actionInfo.Action == null)
		{
			return 0u;
		}
		return actionInfo.Action.SourceId;
	}

	public static bool IsActionType(this ActionInfo actionInfo, ActionType actionType)
	{
		return actionInfo?.Action.IsActionType(actionType) ?? false;
	}

	public static bool IsCategoricalAction(this ActionInfo actionInfo)
	{
		return actionInfo?.Action.IsCategoricalAction() ?? false;
	}

	public static bool IsSideboardAction(this ActionInfo actionInfo, MtgGameState gameState)
	{
		if (actionInfo.Action.SourceId != 0)
		{
			MtgCardInstance cardById = gameState.GetCardById(actionInfo.InstanceId());
			if (cardById != null)
			{
				return cardById.Zone == gameState.GetZone(ZoneType.Sideboard, GREPlayerNum.LocalPlayer);
			}
		}
		return false;
	}

	public static uint InstanceId(this ActionInfo actionInfo)
	{
		if (actionInfo == null)
		{
			return 0u;
		}
		if (actionInfo.Action == null)
		{
			return 0u;
		}
		return actionInfo.Action.InstanceId;
	}

	public static uint AbilityGrpId(this ActionInfo actionInfo)
	{
		if (actionInfo == null)
		{
			return 0u;
		}
		if (actionInfo.Action == null)
		{
			return 0u;
		}
		return actionInfo.Action.AbilityGrpId;
	}
}
