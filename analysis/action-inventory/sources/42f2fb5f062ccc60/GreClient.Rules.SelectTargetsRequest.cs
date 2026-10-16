using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SelectTargetsRequest : BaseUserRequest
{
	public readonly List<TargetSelection> TargetSelections;

	public readonly uint AbilityGrpId;

	public SelectTargetsRequest(SelectTargetsReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		SourceId = req.SourceId;
		TargetSelections = new List<TargetSelection>(req.Targets);
		AbilityGrpId = req.AbilityGrpId;
	}

	public void UpdateTarget(Target target, uint targetIdx)
	{
		_outboundMessage.Type = ClientMessageType.SelectTargetsResp;
		_outboundMessage.SelectTargetsResp = new SelectTargetsResp
		{
			Target = new TargetSelection
			{
				TargetIdx = targetIdx,
				Targets = { target }
			}
		};
		Submit();
	}

	public void SubmitTargets()
	{
		_outboundMessage.Type = ClientMessageType.SubmitTargetsReq;
		Submit();
	}
}
