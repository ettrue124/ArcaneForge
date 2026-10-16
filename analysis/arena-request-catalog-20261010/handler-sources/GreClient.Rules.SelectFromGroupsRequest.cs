using System.Collections.Generic;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SelectFromGroupsRequest : BaseUserRequest, IGroupRequest
{
	private readonly List<Group> _groups;

	private readonly uint _minSelections;

	private readonly uint _maxSelections;

	private readonly GroupingStyle _style;

	public readonly List<uint> UnfilteredIds;

	public readonly List<uint> ZoneIds;

	public IReadOnlyList<Group> Groups => _groups;

	public GroupingStyle Style => _style;

	public uint MinSelections => _minSelections;

	public uint MaxSelections => _maxSelections;

	public bool CanSubmit(int groupCount)
	{
		if (groupCount >= MinSelections)
		{
			return groupCount <= MaxSelections;
		}
		return false;
	}

	public SelectFromGroupsRequest(SelectFromGroupsReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		_groups = new List<Group>(req.Groups);
		UnfilteredIds = new List<uint>(req.UnfilteredIds);
		ZoneIds = new List<uint>(req.ZoneIds);
		_style = req.GroupingStyle;
		_minSelections = req.MinTotalSel;
		_maxSelections = req.MaxTotalSel;
		SourceId = req.SourceId;
		_outboundMessage.Type = ClientMessageType.SelectFromGroupsResp;
		_outboundMessage.SelectFromGroupsResp = new SelectFromGroupsResp();
	}

	public void Submit(IEnumerable<Group> groups)
	{
		_outboundMessage.SelectFromGroupsResp.Groups.AddRange(groups);
		Submit();
	}

	public static List<uint> GetUniqueGroupIds(IReadOnlyList<Group> groups)
	{
		List<uint> list = new List<uint>();
		for (int i = 0; i < groups.Count; i++)
		{
			Group obj = groups[i];
			int count = obj.Ids.Count;
			if (count != 0 && obj.MinSelect == count && obj.MaxSelect == count && GroupIsUnique(obj, groups))
			{
				list.AddRange(obj.Ids);
			}
		}
		return list;
	}

	public static bool GroupIsUnique(Group group, IReadOnlyList<Group> groups)
	{
		foreach (uint id in group.Ids)
		{
			if (groups.Exists((Group anotherGroup) => anotherGroup != group && anotherGroup.Ids.Contains(id)))
			{
				return false;
			}
		}
		return true;
	}
}
