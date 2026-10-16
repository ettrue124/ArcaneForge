using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CastingTimeOption_AdditionalCostRequest : BaseUserRequest
{
	public readonly uint AffectorId;

	public readonly uint AffectedId;

	public readonly uint GrpId;

	public readonly AutoTapSolution Solution;

	public readonly List<ManaRequirement> ManaCost;

	public CastingTimeOption_AdditionalCostRequest(CastingTimeOptionReq option, GREToClientMessage inMsg)
		: base(inMsg)
	{
		SourceId = option.AffectorId;
		AffectorId = option.AffectorId;
		AffectedId = option.AffectedId;
		GrpId = option.GrpId;
		Solution = option.AutoTapSolution;
		ManaCost = new List<ManaRequirement>(option.ManaCost);
		_outboundMessage.Type = ClientMessageType.CastingTimeOptionsResp;
		_outboundMessage.CastingTimeOptionsResp = new CastingTimeOptionsResp
		{
			CastingTimeOptionResp = new CastingTimeOptionResp
			{
				CtoId = option.CtoId,
				CastingTimeOptionType = option.CastingTimeOptionType
			}
		};
	}

	public void SubmitAdditionalCost()
	{
		Submit();
	}
}
