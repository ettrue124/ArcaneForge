using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CastingTimeOption_CostKeywordRequest : BaseUserRequest
{
	public readonly uint GrpId;

	public readonly CastingTimeOptionType OptionType;

	public readonly AutoTapSolution Solution;

	public CastingTimeOption_CostKeywordRequest(CastingTimeOptionReq option, GREToClientMessage inMsg)
		: base(inMsg)
	{
		SourceId = option.AffectorId;
		GrpId = option.GrpId;
		OptionType = option.CastingTimeOptionType;
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

	public void SubmitKeywordAction()
	{
		Submit();
	}
}
