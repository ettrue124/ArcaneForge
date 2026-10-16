using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CastingTimeOption_SelectNRequest : BaseUserRequest
{
	public readonly uint Affector;

	public readonly uint Affected;

	public readonly uint GrpId;

	public readonly bool IsRequired;

	public readonly SelectNRequest SelectNRequest;

	public CastingTimeOption_SelectNRequest(CastingTimeOptionReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Affector = req.AffectorId;
		Affected = req.AffectedId;
		GrpId = req.GrpId;
		IsRequired = req.IsRequired;
		SelectNRequest = new SelectNRequest(req.SelectNReq, inMsg);
		SelectNRequest selectNRequest = SelectNRequest;
		selectNRequest.OnSubmit = (Action<ClientToGREMessage>)Delegate.Combine(selectNRequest.OnSubmit, new Action<ClientToGREMessage>(OnChildSubmit));
		SourceId = SelectNRequest.SourceId;
		_outboundMessage.Type = ClientMessageType.CastingTimeOptionsResp;
		_outboundMessage.CastingTimeOptionsResp = new CastingTimeOptionsResp
		{
			CastingTimeOptionResp = new CastingTimeOptionResp
			{
				CtoId = req.CtoId,
				CastingTimeOptionType = req.CastingTimeOptionType
			}
		};
	}

	private void OnChildSubmit(ClientToGREMessage outMsg)
	{
		switch (outMsg.Type)
		{
		case ClientMessageType.CancelActionReq:
			Cancel();
			break;
		case ClientMessageType.SelectNresp:
			_outboundMessage.CastingTimeOptionsResp.CastingTimeOptionResp.SelectNResp = outMsg.SelectNResp;
			Submit();
			break;
		}
	}
}
