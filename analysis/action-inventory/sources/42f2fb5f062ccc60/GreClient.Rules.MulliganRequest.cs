using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class MulliganRequest : BaseUserRequest
{
	public readonly MulliganType MulliganType;

	public readonly uint FreeMulliganCount;

	public readonly uint MulliganCount;

	public readonly bool FreeMulligan;

	public readonly uint StartingHandSize;

	public MulliganRequest(MulliganReq mulliganReq, GREToClientMessage inMsg)
		: base(inMsg)
	{
		MulliganType = mulliganReq.MulliganType;
		FreeMulliganCount = mulliganReq.FreeMulliganCount;
		MulliganCount = mulliganReq.MulliganCount;
		FreeMulligan = FreeMulliganCount > MulliganCount;
		StartingHandSize = (uint)inMsg.Prompt.Parameters[0].NumberValue;
		_outboundMessage.Type = ClientMessageType.MulliganResp;
		_outboundMessage.MulliganResp = new MulliganResp
		{
			Decision = MulliganOption.None
		};
	}

	public void KeepHand()
	{
		_outboundMessage.MulliganResp.Decision = MulliganOption.AcceptHand;
		Submit();
	}

	public void MulliganHand()
	{
		_outboundMessage.MulliganResp.Decision = MulliganOption.Mulligan;
		Submit();
	}
}
