using System;
using System.Collections.Generic;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UXEventPostProcessAggregate : IUXEventGrouper
{
	private IEnumerable<IUXEventGrouper> _elements;

	public UXEventPostProcessAggregate(params IUXEventGrouper[] elements)
	{
		_elements = elements ?? Array.Empty<IUXEventGrouper>();
	}

	public void GroupEvents(in int startIdx, ref List<UXEvent> events)
	{
		foreach (IUXEventGrouper element in _elements)
		{
			element.GroupEvents(in startIdx, ref events);
		}
	}

	void IUXEventGrouper.GroupEvents(in int startIdx, ref List<UXEvent> events)
	{
		GroupEvents(in startIdx, ref events);
	}
}
