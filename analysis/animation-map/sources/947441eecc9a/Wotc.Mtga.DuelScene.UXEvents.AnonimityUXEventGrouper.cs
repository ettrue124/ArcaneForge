using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AnonimityUXEventGrouper : IUXEventGrouper
{
	public void GroupEvents(in int startIdx, ref List<UXEvent> events)
	{
		int num = ToGroupCount(in startIdx, ref events);
		if (num != 0)
		{
			UXEvent[] array = new UXEvent[num];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = events[startIdx];
				events.RemoveAt(startIdx);
			}
			ParallelPlaybackUXEvent parallelPlaybackUXEvent = new ParallelPlaybackUXEvent(array);
			WaitForSecondsUXEvent waitForSecondsUXEvent = new WaitForSecondsUXEvent(float.Epsilon);
			events.Insert(startIdx, new UXEventGroup(new UXEvent[2] { parallelPlaybackUXEvent, waitForSecondsUXEvent }));
		}
	}

	private static int ToGroupCount(in int startIdx, ref List<UXEvent> events)
	{
		int num = 0;
		for (int i = startIdx; i < events.Count; i++)
		{
			if (events[i] is UpdateCardModelUXEvent { Property: PropertyType.Anonymity })
			{
				num++;
				continue;
			}
			return num;
		}
		return num;
	}

	void IUXEventGrouper.GroupEvents(in int startIdx, ref List<UXEvent> events)
	{
		GroupEvents(in startIdx, ref events);
	}
}
