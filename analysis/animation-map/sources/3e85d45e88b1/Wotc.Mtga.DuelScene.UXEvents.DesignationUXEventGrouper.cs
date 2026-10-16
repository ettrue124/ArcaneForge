using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class DesignationUXEventGrouper : IUXEventGrouper
{
	public void GroupEvents(in int startIdx, ref List<UXEvent> events)
	{
		int num = FindIndexOfFirstZoneTransfer(events);
		if (num < 0)
		{
			return;
		}
		List<UXEvent> list = new List<UXEvent> { events[num] };
		for (int i = 0; i < events.Count; i++)
		{
			if (events[i] is AddDesignationUXEvent addDesignationUXEvent && addDesignationUXEvent.Designation.Type == Designation.PlayerSpeed)
			{
				list.Add(addDesignationUXEvent);
				events.RemoveAt(i);
				i--;
			}
		}
		if (list.Count > 0)
		{
			events[num] = new ParallelPlaybackUXEvent(list);
		}
	}

	private int FindIndexOfFirstZoneTransfer(List<UXEvent> events)
	{
		for (int i = 0; i < events.Count; i++)
		{
			if (!(events[i] is ZoneTransferGroup zoneTransferGroup))
			{
				continue;
			}
			foreach (ZoneTransferUXEvent zoneTransfer in zoneTransferGroup._zoneTransfers)
			{
				if (zoneTransfer.Reason == ZoneTransferReason.Resolve && zoneTransfer.ToZoneType == ZoneType.Battlefield)
				{
					return i;
				}
			}
		}
		return -1;
	}

	void IUXEventGrouper.GroupEvents(in int startIdx, ref List<UXEvent> events)
	{
		GroupEvents(in startIdx, ref events);
	}
}
