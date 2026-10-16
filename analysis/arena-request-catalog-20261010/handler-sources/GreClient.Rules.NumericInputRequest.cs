using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class NumericInputRequest : BaseUserRequest, INumericInputRequest
{
	public uint Min { get; }

	public uint Max { get; }

	public NumericInputType InputType { get; }

	public IReadOnlyList<uint> DisallowedValues { get; }

	public IReadOnlyList<uint> SuggestedValues { get; }

	public bool DisallowEven { get; }

	public bool DisallowOdd { get; }

	public NumericInputRequest(NumericInputReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Min = req.MinValue;
		Max = req.MaxValue;
		InputType = req.NumericInputType;
		SourceId = req.SourceId;
		DisallowedValues = new List<uint>(req.DisallowedValues);
		SuggestedValues = new List<uint>(req.SuggestedValues);
		DisallowEven = req.DisallowEven;
		DisallowOdd = req.DisallowOdd;
		_outboundMessage.Type = ClientMessageType.NumericInputResp;
		_outboundMessage.NumericInputResp = new NumericInputResp
		{
			NumericInputValue = req.MinValue
		};
	}

	public void SubmitValue(uint value)
	{
		_outboundMessage.NumericInputResp.NumericInputValue = value;
		Submit();
	}
}
