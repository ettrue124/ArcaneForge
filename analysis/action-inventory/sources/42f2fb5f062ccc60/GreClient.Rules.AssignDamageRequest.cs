using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class AssignDamageRequest : BaseUserRequest
{
	public List<DamageAssigner> Assigners { get; private set; }

	public AssignDamageRequest(AssignDamageReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Assigners = new List<DamageAssigner>(req.DamageAssigners);
		_outboundMessage.Type = ClientMessageType.AssignDamageResp;
		_outboundMessage.AssignDamageResp = new AssignDamageResp();
	}

	public void SubmitAssignment(IEnumerable<DamageAssigner> damageAssigners)
	{
		AssignDamageResp assignDamageResp = _outboundMessage.AssignDamageResp;
		assignDamageResp.Assigners.Clear();
		assignDamageResp.Assigners.AddRange(damageAssigners);
		Submit();
	}
}
