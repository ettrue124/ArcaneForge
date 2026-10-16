using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class StringInputRequest : BaseUserRequest
{
	public readonly StringInputReq Request;

	public StringInputRequest(StringInputReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Request = req;
		_outboundMessage.Type = ClientMessageType.StringInputResp;
		_outboundMessage.StringInputResp = new StringInputResp
		{
			InputValue = string.Empty
		};
	}

	public void SubmitValue(string value)
	{
		if (!string.IsNullOrWhiteSpace(value))
		{
			_outboundMessage.StringInputResp.InputValue = value;
		}
		Submit();
	}
}
