using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Wizards.Arena.Enums.Event;
using Wizards.Arena.Enums.Format;
using Wizards.Arena.Enums.Match;
using Wizards.Arena.Enums.Store;
using Wizards.Unification.Models.Event;

namespace Wizards.Unification.Models.Events;

public class EventInfoV3
{
	public string InternalEventName;

	[JsonConverter(typeof(StringEnumConverter))]
	public ActiveState EventState;

	[JsonConverter(typeof(StringEnumConverter))]
	public EFormatType FormatType;

	public DateTime StartTime;

	public DateTime LockedTime;

	public DateTime ClosedTime;

	[JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
	public List<EventInfoFlag> Flags;

	public List<EventTag> EventTags;

	public Dictionary<Wizards.Arena.Enums.Store.PurchaseCurrency, TokenTypeAndQuantity> PastEntries;

	public List<EventEntryFee> EntryFees;

	public EventUXInfo EventUXInfo;

	public MatchWinCondition WinCondition;

	public List<string> AllowedCountryCodes;

	public List<string> ExcludedCountryCodes;

	public ERankingClassType MinimumRank;
}
