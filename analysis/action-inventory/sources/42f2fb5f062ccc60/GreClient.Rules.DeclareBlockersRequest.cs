using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class DeclareBlockersRequest : BaseUserRequest
{
	public readonly IReadOnlyList<Blocker> AllBlockers;

	public readonly IReadOnlyList<BlockWarning> BlockWarnings;

	public readonly IReadOnlyList<ManaRequirement> ManaCost;

	public readonly bool HasRestrictions;

	public readonly bool HasRequirements;

	public DeclareBlockersRequest(DeclareBlockersReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		AllBlockers = new List<Blocker>(req.Blockers);
		BlockWarnings = new List<BlockWarning>(req.BlockWarnings);
		ManaCost = req.GetNonEmptyManaCosts();
		HasRestrictions = req.HasRestrictions;
		HasRequirements = req.HasRequirements;
	}

	public void UpdateBlockers(params Blocker[] blockers)
	{
		_outboundMessage.Type = ClientMessageType.DeclareBlockersResp;
		_outboundMessage.DeclareBlockersResp = new DeclareBlockersResp();
		foreach (Blocker item in blockers)
		{
			_outboundMessage.DeclareBlockersResp.SelectedBlockers.Add(item);
		}
		Submit();
	}

	public void SubmitBlockers()
	{
		_outboundMessage.Type = ClientMessageType.SubmitBlockersReq;
		Submit();
	}
}
