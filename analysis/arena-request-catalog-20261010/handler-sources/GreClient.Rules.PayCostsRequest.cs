using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class PayCostsRequest : BaseUserRequest
{
	public readonly List<ManaRequirement> ManaRequirements;

	public readonly SelectNRequest PaymentSelection;

	public readonly ActionsAvailableRequest PaymentActions;

	public readonly EffectCostRequest EffectCost;

	public readonly AutoTapActionsRequest AutoTapActions;

	public readonly List<BaseUserRequest> ChildRequests = new List<BaseUserRequest>();

	public PayCostsRequest(PayCostsReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		ManaRequirements = new List<ManaRequirement>(req.ManaCost);
		SelectNReq paymentSelection = req.PaymentSelection;
		if (paymentSelection != null && paymentSelection.Ids.Count > 0)
		{
			PaymentSelection = new SelectNRequest(paymentSelection, inMsg);
			PaymentSelection.ParentRequest = this;
			SelectNRequest paymentSelection2 = PaymentSelection;
			paymentSelection2.OnSubmit = (Action<ClientToGREMessage>)Delegate.Combine(paymentSelection2.OnSubmit, new Action<ClientToGREMessage>(OnChildSubmit));
			ChildRequests.Add(PaymentSelection);
			if (PaymentSelection.SourceId != 0)
			{
				SourceId = PaymentSelection.SourceId;
			}
		}
		ActionsAvailableReq paymentActions = req.PaymentActions;
		if (paymentActions != null && (paymentActions.Actions.Count > 0 || paymentActions.InactiveActions.Count > 0))
		{
			PaymentActions = new ActionsAvailableRequest(paymentActions, inMsg);
			PaymentActions.ParentRequest = this;
			ActionsAvailableRequest paymentActions2 = PaymentActions;
			paymentActions2.OnSubmit = (Action<ClientToGREMessage>)Delegate.Combine(paymentActions2.OnSubmit, new Action<ClientToGREMessage>(OnChildSubmit));
			ChildRequests.Add(PaymentActions);
			if (PaymentActions.SourceId != 0)
			{
				SourceId = PaymentActions.SourceId;
			}
		}
		if (req.EffectCostReq != null)
		{
			EffectCost = new EffectCostRequest(req.EffectCostReq, inMsg);
			EffectCost.ParentRequest = this;
			EffectCostRequest effectCost = EffectCost;
			effectCost.OnSubmit = (Action<ClientToGREMessage>)Delegate.Combine(effectCost.OnSubmit, new Action<ClientToGREMessage>(OnChildSubmit));
			ChildRequests.Add(EffectCost);
			if (EffectCost.SourceId != 0)
			{
				SourceId = EffectCost.SourceId;
			}
		}
		if (req.AutoTapActionsReq != null)
		{
			AutoTapActions = new AutoTapActionsRequest(req, inMsg);
			AutoTapActions.ParentRequest = this;
			AutoTapActionsRequest autoTapActions = AutoTapActions;
			autoTapActions.OnSubmit = (Action<ClientToGREMessage>)Delegate.Combine(autoTapActions.OnSubmit, new Action<ClientToGREMessage>(OnChildSubmit));
			ChildRequests.Add(AutoTapActions);
			if (AutoTapActions.SourceId != 0)
			{
				SourceId = AutoTapActions.SourceId;
			}
		}
	}

	private void OnChildSubmit(ClientToGREMessage outMsg)
	{
		switch (outMsg.Type)
		{
		case ClientMessageType.CancelActionReq:
			Cancel();
			break;
		case ClientMessageType.SelectNresp:
			_outboundMessage.Type = ClientMessageType.SelectNresp;
			_outboundMessage.SelectNResp = outMsg.SelectNResp;
			Submit();
			break;
		case ClientMessageType.PerformActionResp:
			_outboundMessage.Type = ClientMessageType.PerformActionResp;
			_outboundMessage.PerformActionResp = outMsg.PerformActionResp;
			Submit();
			break;
		case ClientMessageType.EffectCostResp:
			_outboundMessage.Type = ClientMessageType.EffectCostResp;
			_outboundMessage.EffectCostResp = outMsg.EffectCostResp;
			Submit();
			break;
		case ClientMessageType.PerformAutoTapActionsResp:
			_outboundMessage.Type = ClientMessageType.PerformAutoTapActionsResp;
			_outboundMessage.PerformAutoTapActionsResp = outMsg.PerformAutoTapActionsResp;
			Submit();
			break;
		case ClientMessageType.UndoReq:
			Undo();
			break;
		}
	}
}
