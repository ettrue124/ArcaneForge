using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class IntermissionRequest : BaseUserRequest
{
	public readonly List<UserOption> Options;

	public readonly ResultType Result;

	public readonly MatchScope Scope;

	public readonly ResultReason Reason;

	public readonly uint WinningTeamId;

	public IntermissionRequest(IntermissionReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Options = new List<UserOption>(req.Options);
		ResultSpec result = req.Result;
		Result = result.Result;
		Scope = result.Scope;
		Reason = result.Reason;
		WinningTeamId = result.WinningTeamId;
	}

	public void SubmitOption(ClientMessageType option)
	{
		_outboundMessage.Type = option;
		Submit();
	}
}
