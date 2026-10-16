using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Wizards.Arena.Enums.Event;
using Wizards.MDN;
using Wizards.MDN.Services.Models.Event;
using Wizards.Mtga.FrontDoorModels;

namespace EventPage.Components.NetworkModels;

public class EventInfoV3
{
	public string InternalEventName;

	[JsonConverter(typeof(StringEnumConverter))]
	public MDNEventState EventState;

	[JsonConverter(typeof(StringEnumConverter))]
	public MDNEFormatType FormatType;

	public DateTime StartTime;

	public DateTime LockedTime;

	public DateTime ClosedTime;

	[JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
	public List<EventInfoFlag> Flags;

	public Dictionary<EventEntryCurrencyType, int> PastEntries;

	public List<EventEntryFee> EntryFees;

	public List<EventTag> EventTags;

	public EventUXInfo EventUXInfo;

	public MatchWinCondition WinCondition;

	public List<string> AllowedCountryCodes;

	public List<string> ExcludedCountryCodes;

	public ERankingClassType MinimumRank = ERankingClassType.None;
}
