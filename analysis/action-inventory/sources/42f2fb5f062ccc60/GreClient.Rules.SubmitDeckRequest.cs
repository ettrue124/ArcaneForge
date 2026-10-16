using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SubmitDeckRequest : BaseUserRequest
{
	public readonly DeckMessage Deck;

	public SubmitDeckRequest(SubmitDeckReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Deck = req.Deck;
		_outboundMessage.Type = ClientMessageType.SubmitDeckResp;
		_outboundMessage.SubmitDeckResp = new SubmitDeckResp();
	}

	public void SubmitDeck(DeckMessage deck)
	{
		_outboundMessage.SubmitDeckResp.Deck = deck;
		Submit();
	}
}
