using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CastingTimeOption_DoneRequest : BaseUserRequest
{
	public readonly List<ManaRequirement> ManaCost;

	public readonly AutoTapSolution Solution;

	public CastingTimeOption_DoneRequest(CastingTimeOptionReq option, GREToClientMessage inMsg)
		: base(inMsg)
	{
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

	public void SubmitDone()
	{
		Submit();
	}
}
