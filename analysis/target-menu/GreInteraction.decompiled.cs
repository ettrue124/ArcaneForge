using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

public class GreInteraction
{
	public readonly ActionType Type;

	public readonly List<uint> AutoTapInstanceIds = new List<uint>();

	public readonly Dictionary<uint, uint> AutoTapManaIds = new Dictionary<uint, uint>();

	public readonly bool CanAffordToCast;

	public Action GreAction { get; private set; }

	public bool IsActive { get; private set; }

	public GreInteraction(Action greAction, bool isActive = true)
	{
		GreAction = greAction;
		IsActive = isActive;
		Type = greAction.ActionType;
		foreach (AutoTapAction item in greAction.AutoTapActions())
		{
			if (item.ManaId != 0 && item.ManaPaymentOption != null)
			{
				foreach (ManaInfo item2 in item.ManaPaymentOption.Mana)
				{
					AutoTapManaIds.TryAdd(item2.ManaId, 0u);
					AutoTapManaIds[item2.ManaId] += item2.Count;
				}
			}
			if (item.InstanceId != 0)
			{
				AutoTapInstanceIds.Add(item.InstanceId);
			}
		}
		CanAffordToCast = greAction.CanAffordToCast();
	}
}
