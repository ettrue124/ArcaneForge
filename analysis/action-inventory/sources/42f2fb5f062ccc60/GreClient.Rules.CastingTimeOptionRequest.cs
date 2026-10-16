using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CastingTimeOptionRequest : BaseUserRequest
{
	public readonly List<BaseUserRequest> ChildRequests = new List<BaseUserRequest>();

	public CastingTimeOptionRequest(CastingTimeOptionsReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		foreach (CastingTimeOptionReq item in req.CastingTimeOptionReq)
		{
			BaseUserRequest requestForOptionType = GetRequestForOptionType(item, inMsg);
			if (requestForOptionType != null)
			{
				requestForOptionType.ParentRequest = this;
				requestForOptionType.OnSubmit = (Action<ClientToGREMessage>)Delegate.Combine(requestForOptionType.OnSubmit, new Action<ClientToGREMessage>(OnChildSubmit));
				ChildRequests.Add(requestForOptionType);
				if (requestForOptionType.SourceId != 0)
				{
					SourceId = requestForOptionType.SourceId;
				}
			}
		}
	}

	private BaseUserRequest GetRequestForOptionType(CastingTimeOptionReq option, GREToClientMessage inMsg)
	{
		switch (option.CastingTimeOptionType)
		{
		case CastingTimeOptionType.Done:
			return new CastingTimeOption_DoneRequest(option, inMsg);
		case CastingTimeOptionType.ChooseX:
			return new CastingTimeOption_NumericInputRequest(option, inMsg);
		case CastingTimeOptionType.Modal:
			return new CastingTimeOption_ModalRequest(option, inMsg);
		case CastingTimeOptionType.ChooseOrCost:
			return new CastingTimeOption_ChooseOrCostRequest(option, inMsg);
		case CastingTimeOptionType.Selection:
			return new CastingTimeOption_SelectNRequest(option, inMsg);
		case CastingTimeOptionType.Kicker:
			return new CastingTimeOption_KickerRequest(option, inMsg);
		case CastingTimeOptionType.TimingPermission:
			return new CastingTimeOption_TimingPermissionRequest(option, inMsg);
		case CastingTimeOptionType.Conspire:
		case CastingTimeOptionType.Casualty:
		case CastingTimeOptionType.Bargain:
			return new CastingTimeOption_CostKeywordRequest(option, inMsg);
		case CastingTimeOptionType.Specialize:
			return new CastingTimeOption_SpecializeRequest(option, inMsg);
		case CastingTimeOptionType.AdditionalCost:
			return new CastingTimeOption_AdditionalCostRequest(option, inMsg);
		case CastingTimeOptionType.Replicate:
			return new CastingTimeOption_Replicate(option, inMsg);
		case CastingTimeOptionType.ManaType:
			return null;
		default:
			throw new NotImplementedException($"Unhandled type \"{option.CastingTimeOptionType}\"");
		}
	}

	private void OnChildSubmit(ClientToGREMessage outMsg)
	{
		switch (outMsg.Type)
		{
		case ClientMessageType.CancelActionReq:
			Cancel();
			return;
		case ClientMessageType.UndoReq:
			Undo();
			return;
		}
		_outboundMessage.Type = ClientMessageType.CastingTimeOptionsResp;
		_outboundMessage.CastingTimeOptionsResp = outMsg.CastingTimeOptionsResp;
		Submit();
	}
}
