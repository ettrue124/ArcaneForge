using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class AutoTapActionsRequest : BaseUserRequest
{
	public readonly List<AutoTapSolution> Solutions;

	public readonly bool ShouldCancel;

	public AutoTapActionsRequest(PayCostsReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Solutions = new List<AutoTapSolution>(req.AutoTapActionsReq.AutoTapSolutions);
		ShouldCancel = req.ShouldCancel;
		_outboundMessage.Type = ClientMessageType.PerformAutoTapActionsResp;
		_outboundMessage.PerformAutoTapActionsResp = new PerformAutoTapActionsResp();
	}

	public void SubmitSolution(AutoTapSolution autoTapSolution)
	{
		int num = Solutions.IndexOf(autoTapSolution);
		if (num >= 0)
		{
			_outboundMessage.PerformAutoTapActionsResp.Index = (uint)num;
			Submit();
		}
	}
}
