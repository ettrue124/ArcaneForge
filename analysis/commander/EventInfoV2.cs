using System;
using System.Collections.Generic;
using System.Text;
using Wizards.Mtga.FrontDoorModels;

namespace Wizards.MDN;

public class EventInfoV2
{
	public string PublicEventName;

	public string InternalEventName;

	public MDNEventState EventState;

	public MDNEFormatType FormatType;

	public ModuleInfo ModuleGlobalData;

	public DateTime StartTime;

	public DateTime LockedTime;

	public DateTime ClosedTime;

	public Dictionary<EventEntryCurrencyType, int> PastEntries;

	public int DisplayPriority;

	public bool IsArenaPlayModeEvent;

	public List<uint> Emblems;

	public UILayoutInfo UILayoutOptions;

	public bool SkipValidation;

	public bool DoesUpdateQuests = true;

	public bool DoesUpdateDailyWeeklyRewards = true;

	public bool AllowUncollectedCards;

	public Dictionary<string, string> Parameters;

	public string Group = "";

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("InternalEventName = " + InternalEventName);
		stringBuilder.AppendLine("EventState        = " + EventState);
		stringBuilder.AppendLine("EventType         = " + FormatType);
		stringBuilder.AppendLine("PublicEventName   = " + PublicEventName);
		return stringBuilder.ToString();
	}
}
