using System.Collections.Generic;

namespace Wizards.Unification.Models.PlayBlade;

public class PlayBladeQueueEntry
{
	public string Id { get; set; }

	public PlayBladeQueueType QueueType { get; set; }

	public string LocTitle { get; set; }

	public string EventNameBO1 { get; set; }

	public string EventNameBO3 { get; set; }

	public string DeckSizeBO1 { get; set; }

	public string DeckSizeBO3 { get; set; }

	public string SideBoardBO1 { get; set; }

	public string SideBoardBO3 { get; set; }

	public List<string> Tags { get; set; }
}
