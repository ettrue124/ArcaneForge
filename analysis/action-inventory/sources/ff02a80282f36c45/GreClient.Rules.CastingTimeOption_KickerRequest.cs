using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CastingTimeOption_KickerRequest : BaseUserRequest
{
	public readonly uint AffectorId;

	public readonly uint AffectedId;

	public readonly uint GrpId;

	public readonly List<ManaRequirement> ManaCost;

	public readonly AutoTapSolution Solution;

	public CastingTimeOption_KickerRequest(CastingTimeOptionReq option, GREToClientMessage inMsg)
		: base(inMsg)
	{
		SourceId = option.AffectorId;
		AffectorId = option.AffectorId;
		AffectedId = option.AffectedId;
		GrpId = option.GrpId;
		ManaCost = new List<ManaRequirement>(option.ManaCost);
		Solution = option.AutoTapSolution;
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

	public void SubmitKicked()
	{
		Submit();
	}
}
