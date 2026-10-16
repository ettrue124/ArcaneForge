using System;
using System.Collections.Generic;
using EventPage.Components.NetworkModels;
using Wizards.Arena.Enums.Event;
using Wizards.MDN;
using Wizards.Mtga.FrontDoorModels;

namespace Wotc.Mtga.Events;

public class BasicEventInfo : IEventInfo
{
	private EventInfoV3 _eventInfoV3;

	public string EventId => _eventInfoV3.InternalEventName;

	public virtual string InternalEventName
	{
		get
		{
			return _eventInfoV3.InternalEventName;
		}
		set
		{
		}
	}

	public MDNEventState EventState => _eventInfoV3.EventState;

	public MDNEFormatType FormatType => _eventInfoV3.FormatType;

	public DateTime StartTime
	{
		get
		{
			return _eventInfoV3.StartTime;
		}
		set
		{
			_eventInfoV3.StartTime = value;
		}
	}

	public DateTime LockedTime
	{
		get
		{
			return _eventInfoV3.LockedTime;
		}
		set
		{
			_eventInfoV3.LockedTime = value;
		}
	}

	public DateTime ClosedTime
	{
		get
		{
			return _eventInfoV3.ClosedTime;
		}
		set
		{
			_eventInfoV3.ClosedTime = value;
		}
	}

	public bool IsRanked => _eventInfoV3.Flags.Contains(EventPage.Components.NetworkModels.EventInfoFlag.Ranked);

	public bool UpdateQuests => _eventInfoV3.Flags.Contains(EventPage.Components.NetworkModels.EventInfoFlag.UpdateQuests);

	public virtual bool UpdateDailyWeeklyRewards
	{
		get
		{
			return _eventInfoV3.Flags.Contains(EventPage.Components.NetworkModels.EventInfoFlag.UpdateDailyWeeklyRewards);
		}
		set
		{
		}
	}

	public bool IsArenaPlayModeEvent => _eventInfoV3.Flags.Contains(EventPage.Components.NetworkModels.EventInfoFlag.IsArenaPlayModeEvent);

	public virtual bool IsPreconEvent
	{
		get
		{
			return _eventInfoV3.Flags.Contains(EventPage.Components.NetworkModels.EventInfoFlag.IsPreconEvent);
		}
		set
		{
		}
	}

	public bool SkipDeckValidation => _eventInfoV3.Flags.Contains(EventPage.Components.NetworkModels.EventInfoFlag.SkipDeckValidation);

	public bool AllowUncollectedCards => _eventInfoV3.Flags.Contains(EventPage.Components.NetworkModels.EventInfoFlag.AllowUncollectedCards);

	public bool IsAiOpponent => _eventInfoV3.Flags.Contains(EventPage.Components.NetworkModels.EventInfoFlag.IsAiBotMatch);

	public List<EventEntryFeeInfo> EntryFees { get; private set; }

	public List<EventTag> EventTags => _eventInfoV3.EventTags;

	public BasicEventInfo(EventInfoV3 eventInfo)
	{
		_eventInfoV3 = eventInfo;
		List<EventEntryFeeInfo> list = new List<EventEntryFeeInfo>();
		if (eventInfo.EntryFees == null)
		{
			list.Add(new EventEntryFeeInfo());
		}
		else
		{
			foreach (EventEntryFee entryFee in eventInfo.EntryFees)
			{
				list.Add(new EventEntryFeeInfo(entryFee.CurrencyType, hasUsesRemaining(entryFee), entryFee.Quantity, entryFee.ReferenceId));
				bool hasUsesRemaining(EventEntryFee entryFee)
				{
					if (entryFee == null || !entryFee.MaxUses.HasValue || eventInfo.PastEntries == null)
					{
						return true;
					}
					eventInfo.PastEntries.TryGetValue(entryFee.CurrencyType, out var value);
					return value < entryFee.MaxUses;
				}
			}
		}
		EntryFees = list;
	}
}
