using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Wizards.Arena.Enums.Event;

namespace Wizards.Unification.Models.Events;

public class EventUXInfo
{
	public string PublicEventName;

	public int DisplayPriority;

	public List<DisplayPriorityMilestone> DisplayPriorityMilestoneChanges;

	public bool PrioritizeBannerIfPlayerHasToken;

	public Dictionary<string, string> Parameters;

	public string Group = "";

	[JsonConverter(typeof(StringEnumConverter))]
	public EventBladeBehavior EventBladeBehavior;

	public List<string> DynamicFilterTagIds;

	[JsonConverter(typeof(StringEnumConverter))]
	public JoinType Joinability;

	public List<FactionSealedUXInfo> FactionSealedUXInfo;

	public string DeckSelectFormat;

	public Dictionary<int, Guid> Prizes;

	public EventComponentData EventComponentData;
}
