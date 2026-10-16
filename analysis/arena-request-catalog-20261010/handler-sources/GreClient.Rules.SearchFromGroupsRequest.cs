using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SearchFromGroupsRequest : BaseUserRequest, IGroupRequest
{
	private readonly List<Group> _groups;

	private readonly uint _minFind;

	private readonly uint _maxFind;

	private readonly GroupingStyle _style;

	public readonly List<uint> ZonesToSearch;

	public readonly AllowFailToFind AllowFailToFind;

	public readonly List<uint> AdditionalZones;

	public IReadOnlyList<Group> Groups => _groups;

	public GroupingStyle Style => _style;

	public uint MinSelections => _minFind;

	public uint MaxSelections => _maxFind;

	public bool CanSubmit(int selected)
	{
		if (AllowFailToFind == AllowFailToFind.Zero)
		{
			if (selected != 0)
			{
				if (selected >= MinSelections)
				{
					return selected <= MaxSelections;
				}
				return false;
			}
			return true;
		}
		if (selected >= MinSelections)
		{
			return selected <= MaxSelections;
		}
		return false;
	}

	public SearchFromGroupsRequest(SearchFromGroupsReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		ZonesToSearch = new List<uint>(req.ZonesToSearch);
		_maxFind = req.MaxFind;
		_minFind = req.MinFind;
		_style = req.GroupingStyle;
		_groups = new List<Group>(req.Groups);
		AllowFailToFind = req.AllowFailToFind;
		AdditionalZones = new List<uint>(req.AdditionalZones);
		_outboundMessage.Type = ClientMessageType.SearchFromGroupsResp;
		_outboundMessage.SearchFromGroupsResp = new SearchFromGroupsResp();
	}

	public void SubmitSelection(IEnumerable<Group> groups)
	{
		_outboundMessage.SearchFromGroupsResp.Groups.AddRange(groups);
		Submit();
	}

	public void SubmitZone(uint zone)
	{
		_outboundMessage.SearchFromGroupsResp.AddZoneToSearchScope = zone;
		Submit();
	}
}
