using System.Collections.Generic;
using Wizards.Unification.Models.PlayBlade;

namespace Wizards.Mtga.PlayBlade;

public class BladeQueueInfo
{
	public string QueueId;

	public BladeEventInfo EventInfo_BO1;

	public BladeEventInfo EventInfo_BO3;

	public string LocTitle;

	public DeckConstraintInfo DeckConstraintInfo_B01;

	public DeckConstraintInfo DeckConstraintInfo_B03;

	public List<string> Tags;

	public PlayBladeQueueType PlayBladeQueueType;

	public bool CanBestOf3
	{
		get
		{
			if (!string.IsNullOrEmpty(EventInfo_BO1?.EventName))
			{
				return !string.IsNullOrEmpty(EventInfo_BO3?.EventName);
			}
			return false;
		}
	}
}
