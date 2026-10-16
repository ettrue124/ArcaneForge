using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class GroupRequest : BaseUserRequest
{
	private GroupReq _groupRequest;

	public GroupingContext Context => _groupRequest.Context;

	public List<uint> InstanceIds => new List<uint>(_groupRequest.InstanceIds);

	public List<GroupSpecification> GroupSpecs => new List<GroupSpecification>(_groupRequest.GroupSpecs);

	public GroupRequest(GroupReq groupReq, GREToClientMessage inMsg)
		: base(inMsg)
	{
		_groupRequest = groupReq;
		_outboundMessage.Type = ClientMessageType.GroupResp;
		_outboundMessage.GroupResp = new GroupResp
		{
			GroupType = _groupRequest.GroupType
		};
	}

	public void SubmitGroups(IEnumerable<Group> groups)
	{
		GroupResp groupResp = _outboundMessage.GroupResp;
		groupResp.Groups.Clear();
		groupResp.Groups.AddRange(groups);
		Submit();
	}
}
