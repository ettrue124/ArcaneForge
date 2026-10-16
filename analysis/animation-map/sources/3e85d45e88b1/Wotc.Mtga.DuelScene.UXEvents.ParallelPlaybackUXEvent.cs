using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ParallelPlaybackUXEvent : UXEvent
{
	private const string TOSTRING_NO_EVENTS = "ParallelPlaybackUXEvent: No Events";

	private const string TOSTRING_HEADER = "ParallelPlaybackUXEvent: Event List";

	private const string TOSTRING_TAB = "     ";

	private const string TOSTRING_REGEX_PATTERN = "\r\n|\r|\n";

	public IReadOnlyList<UXEvent> Events { get; private set; }

	public override bool IsBlocking
	{
		get
		{
			foreach (UXEvent @event in Events)
			{
				if (@event.IsBlocking)
				{
					return true;
				}
			}
			return false;
		}
	}

	public ParallelPlaybackUXEvent(IReadOnlyList<UXEvent> events)
	{
		Events = events ?? Array.Empty<UXEvent>();
	}

	public override void Execute()
	{
		foreach (UXEvent @event in Events)
		{
			@event.Execute();
		}
	}

	public override void Update(float dt)
	{
		if (AllEventsAreComplete(Events))
		{
			Complete();
			return;
		}
		foreach (UXEvent @event in Events)
		{
			@event.Update(dt);
		}
		base.Update(dt);
	}

	public override bool CanExecute(List<UXEvent> currentlyRunningEvents)
	{
		foreach (UXEvent @event in Events)
		{
			if (!@event.CanExecute(currentlyRunningEvents))
			{
				return false;
			}
		}
		return true;
	}

	private static bool AllEventsAreComplete(IEnumerable<UXEvent> events)
	{
		foreach (UXEvent @event in events)
		{
			if (!@event.IsComplete)
			{
				return false;
			}
		}
		return true;
	}

	public override string ToString()
	{
		if (Events.Count == 0)
		{
			return "ParallelPlaybackUXEvent: No Events";
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("ParallelPlaybackUXEvent: Event List");
		foreach (UXEvent @event in Events)
		{
			string[] array = Regex.Split(@event.ToString(), "\r\n|\r|\n");
			foreach (string value in array)
			{
				stringBuilder.AppendLine();
				stringBuilder.Append("     ");
				stringBuilder.Append(value);
			}
		}
		return stringBuilder.ToString();
	}
}
