using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SelectNGroupRequest : BaseUserRequest
{
	public readonly uint MinSelect;

	public readonly uint MaxSelect;

	public readonly List<Group> Groups;

	public SelectNGroupRequest(SelectNGroupReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		MinSelect = req.MinSel;
		MaxSelect = req.MaxSel;
		Groups = new List<Group>(req.Groups);
		SourceId = req.SourceId;
		_outboundMessage.Type = ClientMessageType.SelectNgroupResp;
		_outboundMessage.SelectNGroupResp = new SelectNGroupResp();
	}

	public void SubmitGroupSelection(uint id)
	{
		_outboundMessage.SelectNGroupResp.Selectedgroupids.Add(id);
		Submit();
	}

	public void SubmitGroupSelection(IEnumerable<uint> ids)
	{
		_outboundMessage.SelectNGroupResp.Selectedgroupids.AddRange(ids);
		Submit();
	}
}
