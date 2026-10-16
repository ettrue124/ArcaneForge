using System.Collections.Generic;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class NullUXEventGrouper : IUXEventGrouper
{
	public static readonly IUXEventGrouper Default = new NullUXEventGrouper();

	public void GroupEvents(in int startIdx, ref List<UXEvent> events)
	{
	}

	void IUXEventGrouper.GroupEvents(in int startIdx, ref List<UXEvent> events)
	{
		GroupEvents(in startIdx, ref events);
	}
}
