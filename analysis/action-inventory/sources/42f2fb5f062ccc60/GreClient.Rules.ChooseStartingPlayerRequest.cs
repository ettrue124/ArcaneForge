using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class ChooseStartingPlayerRequest : BaseUserRequest
{
	public IReadOnlyList<uint> SeatIds { get; private set; }

	public ChooseStartingPlayerRequest(ChooseStartingPlayerReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		SeatIds = new List<uint>(req.SystemSeatIds);
		_outboundMessage.Type = ClientMessageType.ChooseStartingPlayerResp;
		_outboundMessage.ChooseStartingPlayerResp = new ChooseStartingPlayerResp
		{
			TeamType = req.TeamType
		};
	}

	public void ChooseStartingPlayer(uint playerId)
	{
		ChooseStartingPlayerResp chooseStartingPlayerResp = _outboundMessage.ChooseStartingPlayerResp;
		chooseStartingPlayerResp.TeamId = playerId;
		chooseStartingPlayerResp.SystemSeatId = playerId;
		Submit();
	}
}
