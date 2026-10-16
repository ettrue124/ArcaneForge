using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AttachmentAddedUXEventGrouper : IUXEventGrouper
{
	public void GroupEvents(in int idx, ref List<UXEvent> events)
	{
		if (!(events[idx] is ZoneTransferGroup zoneTransferGroup) || !HasAttachmentEvents(zoneTransferGroup, idx + 1, events))
		{
			return;
		}
		List<UXEvent> list = new List<UXEvent> { zoneTransferGroup };
		events.RemoveAt(idx);
		foreach (ZoneTransferUXEvent zoneTransfer in zoneTransferGroup._zoneTransfers)
		{
			for (int i = idx; i < events.Count; i++)
			{
				if (!(events[i] is UpdateCardModelUXEvent updateCardModelUXEvent))
				{
					continue;
				}
				if (AttachmentAddedViaZoneTransfer(zoneTransfer, updateCardModelUXEvent))
				{
					if (zoneTransfer.NewInstance != null && updateCardModelUXEvent.NewInstance != null)
					{
						zoneTransfer.AttachmentAddedViaZoneTransfer(updateCardModelUXEvent.NewInstance.AttachedToId);
					}
					events.RemoveAt(i);
					i--;
				}
				else if (AttachesWithZoneTransfer(zoneTransfer, updateCardModelUXEvent))
				{
					list.Add(updateCardModelUXEvent);
					events.RemoveAt(i);
					i--;
				}
			}
		}
		ParallelPlaybackUXEvent item = new ParallelPlaybackUXEvent(list);
		events.Insert(idx, item);
	}

	public static bool HasAttachmentEvents(ZoneTransferGroup ztg, int idx, IReadOnlyList<UXEvent> events)
	{
		for (int i = idx; i < events.Count; i++)
		{
			if (events[i] is UpdateCardModelUXEvent updateCardModel && (AttachesToZoneTransferGroup(ztg, updateCardModel) || AttachesWithZoneTransferGroup(ztg, updateCardModel)))
			{
				return true;
			}
		}
		return false;
	}

	private static bool AttachesToZoneTransferGroup(ZoneTransferGroup ztg, UpdateCardModelUXEvent updateCardModel)
	{
		if (updateCardModel == null || ztg == null)
		{
			return false;
		}
		foreach (ZoneTransferUXEvent zoneTransfer in ztg._zoneTransfers)
		{
			if (AttachmentAddedViaZoneTransfer(zoneTransfer, updateCardModel))
			{
				return true;
			}
		}
		return false;
	}

	public static bool AttachmentAddedViaZoneTransfer(ZoneTransferUXEvent zte, UpdateCardModelUXEvent updateCardModel)
	{
		if (updateCardModel.Property == PropertyType.AttachedTo && updateCardModel.NewInstance != null && updateCardModel.NewInstance.InstanceId == zte.NewId)
		{
			return updateCardModel.NewInstance.AttachedToId != 0;
		}
		return false;
	}

	public static bool AttachesWithZoneTransferGroup(ZoneTransferGroup ztg, UpdateCardModelUXEvent updateCardModel)
	{
		if (updateCardModel == null || ztg == null)
		{
			return false;
		}
		foreach (ZoneTransferUXEvent zoneTransfer in ztg._zoneTransfers)
		{
			if (AttachesWithZoneTransfer(zoneTransfer, updateCardModel))
			{
				return true;
			}
		}
		return false;
	}

	public static bool AttachesWithZoneTransfer(ZoneTransferUXEvent zte, UpdateCardModelUXEvent updateCardModel)
	{
		if (updateCardModel.Property == PropertyType.AttachedWith && updateCardModel.NewInstance != null)
		{
			return updateCardModel.NewInstance.AttachedWithIds.Contains(zte.NewId);
		}
		return false;
	}

	void IUXEventGrouper.GroupEvents(in int startIdx, ref List<UXEvent> events)
	{
		GroupEvents(in startIdx, ref events);
	}
}
