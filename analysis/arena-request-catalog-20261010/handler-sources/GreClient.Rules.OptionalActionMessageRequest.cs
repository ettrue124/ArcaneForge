using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class OptionalActionMessageRequest : BaseUserRequest
{
	public readonly List<CardMechanicType> Mechanics;

	public readonly IReadOnlyList<uint> RecipientIds;

	public HighlightType HighlightType;

	public readonly IReadOnlyList<uint> HighlightIds;

	public OptionalActionMessageRequest(OptionalActionMessage req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Mechanics = new List<CardMechanicType>(req.OptionalActionTypes);
		RecipientIds = new List<uint>(req.RecipientIds);
		HighlightType = req.Highlight;
		SourceId = req.SourceId;
		HighlightIds = new List<uint>(req.HighlightIds);
		_outboundMessage.Type = ClientMessageType.OptionalActionResp;
		_outboundMessage.OptionalResp = new OptionalResp();
	}

	public void SubmitResponse(OptionResponse response)
	{
		_outboundMessage.OptionalResp.Response = response;
		Submit();
	}
}
