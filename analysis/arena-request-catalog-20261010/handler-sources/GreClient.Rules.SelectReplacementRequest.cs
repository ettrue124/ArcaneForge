using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SelectReplacementRequest : BaseUserRequest
{
	public readonly IReadOnlyList<ReplacementEffect> Replacements;

	public readonly IReadOnlyList<ReplacementSelectionByZcid> ObjectSelections;

	public readonly bool IsOptional;

	public readonly SelectReplacementsType ReplacementsType;

	public SelectReplacementRequest(SelectReplacementReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Replacements = new List<ReplacementEffect>(req.Replacements);
		ObjectSelections = new List<ReplacementSelectionByZcid>(req.GameObjectSelections);
		IsOptional = req.IsOptional;
		ReplacementsType = req.ReplacementsType;
		_outboundMessage.Type = ClientMessageType.SelectReplacementResp;
		_outboundMessage.SelectReplacementResp = new SelectReplacementResp();
	}

	public void SubmitReplacement(ReplacementEffect replacementEffect)
	{
		_outboundMessage.SelectReplacementResp.Replacement = replacementEffect;
		Submit();
	}

	public void Decline()
	{
		if (IsOptional)
		{
			Submit();
		}
	}
}
