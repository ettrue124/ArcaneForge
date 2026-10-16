using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class EffectCostRequest : BaseUserRequest
{
	public readonly SelectNRequest CostSelection;

	public readonly SelectFromGroupsRequest SelectFromGroups;

	public readonly SelectCountersRequest SelectCountersRequest;

	public readonly GatherRequest GatherReq;

	public readonly List<BaseUserRequest> ChildRequests = new List<BaseUserRequest>();

	public readonly EffectCostType EffectCostType;

	public EffectCostRequest(EffectCostReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		EffectCostType = req.EffectCostType;
		if (req.CostSelection != null)
		{
			CostSelection = new SelectNRequest(req.CostSelection, inMsg);
			CostSelection.ParentRequest = this;
			SelectNRequest costSelection = CostSelection;
			costSelection.OnSubmit = (Action<ClientToGREMessage>)Delegate.Combine(costSelection.OnSubmit, new Action<ClientToGREMessage>(OnChildSubmit));
			ChildRequests.Add(CostSelection);
		}
		if (req.GroupSelection != null)
		{
			SelectFromGroups = new SelectFromGroupsRequest(req.GroupSelection, inMsg);
			SelectFromGroups.ParentRequest = this;
			SelectFromGroupsRequest selectFromGroups = SelectFromGroups;
			selectFromGroups.OnSubmit = (Action<ClientToGREMessage>)Delegate.Combine(selectFromGroups.OnSubmit, new Action<ClientToGREMessage>(OnChildSubmit));
			ChildRequests.Add(SelectFromGroups);
		}
		if (req.GatherReq != null)
		{
			GatherReq = new GatherRequest(req.GatherReq, inMsg);
			GatherReq.ParentRequest = this;
			GatherRequest gatherReq = GatherReq;
			gatherReq.OnSubmit = (Action<ClientToGREMessage>)Delegate.Combine(gatherReq.OnSubmit, new Action<ClientToGREMessage>(OnChildSubmit));
			ChildRequests.Add(GatherReq);
		}
		if (req.CounterSelection != null)
		{
			SelectCountersRequest = new SelectCountersRequest(req.CounterSelection, inMsg);
			SelectCountersRequest.ParentRequest = this;
			SelectCountersRequest selectCountersRequest = SelectCountersRequest;
			selectCountersRequest.OnSubmit = (Action<ClientToGREMessage>)Delegate.Combine(selectCountersRequest.OnSubmit, new Action<ClientToGREMessage>(OnChildSubmit));
			ChildRequests.Add(SelectCountersRequest);
		}
		foreach (BaseUserRequest childRequest in ChildRequests)
		{
			if (childRequest.SourceId != 0)
			{
				SourceId = childRequest.SourceId;
				break;
			}
		}
		_outboundMessage.Type = ClientMessageType.EffectCostResp;
		_outboundMessage.EffectCostResp = new EffectCostResp
		{
			EffectCostType = req.EffectCostType
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
			_outboundMessage.EffectCostResp.CostSelection = outMsg.SelectNResp;
			Submit();
			break;
		case ClientMessageType.SelectFromGroupsResp:
			_outboundMessage.EffectCostResp.GroupSelection = outMsg.SelectFromGroupsResp;
			Submit();
			break;
		case ClientMessageType.GatherResp:
			_outboundMessage.EffectCostResp.GatherResp = outMsg.GatherResp;
			Submit();
			break;
		case ClientMessageType.SelectCountersResp:
			_outboundMessage.EffectCostResp.CounterSelection = outMsg.SelectCountersResp;
			Submit();
			break;
		case ClientMessageType.UndoReq:
			Undo();
			break;
		}
	}
}
