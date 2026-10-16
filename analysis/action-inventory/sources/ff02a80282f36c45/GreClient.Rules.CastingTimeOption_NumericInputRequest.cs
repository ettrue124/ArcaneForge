using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CastingTimeOption_NumericInputRequest : BaseUserRequest, INumericInputRequest
{
	public readonly uint StepSize;

	public readonly uint GrpId;

	public uint Min { get; }

	public uint Max { get; }

	public NumericInputType InputType { get; }

	public IReadOnlyList<uint> DisallowedValues { get; }

	public IReadOnlyList<uint> SuggestedValues { get; }

	public bool DisallowEven { get; }

	public bool DisallowOdd { get; }

	public CastingTimeOption_NumericInputRequest(CastingTimeOptionReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		NumericInputReq numericInputReq = req.NumericInputReq;
		Min = numericInputReq.MinValue;
		Max = numericInputReq.MaxValue;
		StepSize = numericInputReq.StepSize;
		GrpId = req.GrpId;
		InputType = numericInputReq.NumericInputType;
		DisallowedValues = new List<uint>(numericInputReq.DisallowedValues);
		SuggestedValues = new List<uint>(numericInputReq.SuggestedValues);
		DisallowEven = numericInputReq.DisallowEven;
		DisallowOdd = numericInputReq.DisallowOdd;
		SourceId = numericInputReq.SourceId;
		_outboundMessage.Type = ClientMessageType.CastingTimeOptionsResp;
		_outboundMessage.CastingTimeOptionsResp = new CastingTimeOptionsResp
		{
			CastingTimeOptionResp = new CastingTimeOptionResp
			{
				CtoId = req.CtoId,
				CastingTimeOptionType = req.CastingTimeOptionType,
				NumericInputResp = new NumericInputResp
				{
					NumericInputValue = numericInputReq.MinValue
				}
			}
		};
	}

	public void SubmitX(uint value)
	{
		_outboundMessage.CastingTimeOptionsResp.CastingTimeOptionResp.NumericInputResp.NumericInputValue = value;
		Submit();
	}
}
