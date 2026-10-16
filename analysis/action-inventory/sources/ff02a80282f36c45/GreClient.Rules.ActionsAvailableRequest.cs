using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class ActionsAvailableRequest : BaseUserRequest
{
	public const uint TOC_GRPID = 2u;

	public readonly List<Action> Actions;

	public readonly List<Action> InactiveActions;

	private readonly Action _passAction;

	public bool CanPass => _passAction != null;

	public ActionsAvailableRequest(ActionsAvailableReq req, GREToClientMessage inMsg)
		: base(inMsg)
	{
		Actions = new List<Action>(req.Actions);
		InactiveActions = new List<Action>(req.InactiveActions);
		foreach (Action action in req.Actions)
		{
			if (action.ActionType == ActionType.Pass)
			{
				_passAction = action;
				break;
			}
		}
		_outboundMessage.Type = ClientMessageType.PerformActionResp;
		_outboundMessage.PerformActionResp = new PerformActionResp();
	}

	public void SubmitAction(Action action, bool autoPassPriorty = false)
	{
		PerformActionResp performActionResp = _outboundMessage.PerformActionResp;
		performActionResp.AutoPassPriority = ((!autoPassPriorty) ? AutoPassPriority.No : AutoPassPriority.Yes);
		performActionResp.Actions.Add(action);
		Submit();
	}

	public void SubmitActions(IEnumerable<Action> actions, bool autoPassPriorty = false)
	{
		PerformActionResp performActionResp = _outboundMessage.PerformActionResp;
		performActionResp.AutoPassPriority = ((!autoPassPriorty) ? AutoPassPriority.No : AutoPassPriority.Yes);
		performActionResp.Actions.AddRange(actions);
		Submit();
	}

	public void SubmitPass()
	{
		PerformActionResp performActionResp = _outboundMessage.PerformActionResp;
		performActionResp.AutoPassPriority = AutoPassPriority.None;
		performActionResp.Actions.Add(_passAction);
		Submit();
	}

	public static bool IsCastAction(Action action)
	{
		if (action == null)
		{
			return false;
		}
		ActionType actionType = action.ActionType;
		if (actionType == ActionType.Cast || actionType == ActionType.Play || (uint)(actionType - 10) <= 1u)
		{
			return true;
		}
		return false;
	}

	public static bool IsAbilityAction(Action action)
	{
		if (action == null)
		{
			return false;
		}
		switch (action.ActionType)
		{
		case ActionType.Activate:
		case ActionType.ActivateMana:
		case ActionType.ActivateTest:
		case ActionType.SpecialTurnFaceUp:
		case ActionType.MakePayment:
			return true;
		default:
			return false;
		}
	}
}
