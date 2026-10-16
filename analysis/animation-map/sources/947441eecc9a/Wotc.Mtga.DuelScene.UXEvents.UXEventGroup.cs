using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UXEventGroup : UXEvent
{
	public readonly IReadOnlyList<UXEvent> Events;

	private readonly UXEventQueue _eventQueue = new UXEventQueue();

	private const string TOSTRING_NO_EVENTS = "UXEvent Group: No Events";

	private const string TOSTRING_TAB = "     ";

	private const string TOSTRING_REGEX_PATTERN = "\r\n|\r|\n";

	public UXEventGroup(IReadOnlyList<UXEvent> events)
	{
		Events = events ?? Array.Empty<UXEvent>();
	}

	public override void Execute()
	{
		_eventQueue.EnqueuePending(Events);
	}

	public override void Update(float dt)
	{
		if (_eventQueue.Events.Count == 0)
		{
			Complete();
		}
		base.Update(dt);
		_eventQueue.Update(dt);
	}

	public override string ToString()
	{
		if (Events.Count == 0)
		{
			return "UXEvent Group: No Events";
		}
		if (!IsComplete && !_eventQueue.IsRunning)
		{
			StringBuilder stringBuilder = new StringBuilder("UXEventGroup");
			stringBuilder.Append("     ");
			appendEventsToSB(stringBuilder, "Events:", Events);
			return stringBuilder.ToString();
		}
		StringBuilder stringBuilder2 = new StringBuilder("UXEventGroup");
		appendEventsToSB(stringBuilder2, "Pending:", _eventQueue.PendingEvents);
		appendEventsToSB(stringBuilder2, "Running:", _eventQueue.RunningEvents);
		return stringBuilder2.ToString();
		static void appendEventsToSB(StringBuilder sb, string header, IReadOnlyList<UXEvent> events)
		{
			sb.AppendLine();
			sb.Append("     ");
			sb.Append(header);
			foreach (UXEvent @event in events)
			{
				string[] array = Regex.Split(@event.ToString(), "\r\n|\r|\n");
				foreach (string value in array)
				{
					sb.AppendLine();
					sb.Append("     ");
					sb.Append("     ");
					sb.Append(value);
				}
			}
		}
	}
}
