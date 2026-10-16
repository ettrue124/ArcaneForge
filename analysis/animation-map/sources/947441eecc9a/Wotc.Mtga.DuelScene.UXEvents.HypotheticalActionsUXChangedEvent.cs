using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class HypotheticalActionsUXChangedEvent : UXEvent
{
	private List<ActionInfo> _oldActions;

	private List<ActionInfo> _newActions;

	private readonly IActionEffectController _actionEffectController;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly ICardMovementController _cardMovementController;

	public HypotheticalActionsUXChangedEvent(List<ActionInfo> oldActions, List<ActionInfo> newActions, IContext context)
	{
		_oldActions = oldActions;
		_newActions = newActions;
		_actionEffectController = context.Get<IActionEffectController>() ?? NullActionEffectController.Default;
		_cardViewProvider = context.Get<ICardViewProvider>() ?? NullCardViewProvider.Default;
		_cardMovementController = context.Get<ICardMovementController>() ?? NullCardMovementController.Default;
	}

	public override void Execute()
	{
		foreach (ActionInfo oldAction in _oldActions)
		{
			if (!DoesAnyActionInfoMatch(_newActions, oldAction) && !_actionEffectController.RemoveActionEffect(oldAction))
			{
				DoMoveCardOperation(_cardViewProvider.GetCardView(oldAction.Action.InstanceId));
			}
		}
		foreach (ActionInfo newAction in _newActions)
		{
			if (!DoesAnyActionInfoMatch(_oldActions, newAction) && !_actionEffectController.AddActionEffect(newAction))
			{
				DoMoveCardOperation(_cardViewProvider.GetCardView(newAction.Action.InstanceId));
			}
		}
		Complete();
	}

	private bool DoesAnyActionInfoMatch(List<ActionInfo> actionsA, ActionInfo actionB)
	{
		foreach (ActionInfo item in actionsA)
		{
			if (item.Action.InstanceId == actionB.Action.InstanceId && item.SeatId == actionB.SeatId && item.Action.SourceId == actionB.Action.SourceId && item.Action.Selection == actionB.Action.Selection && item.Action.SelectionType == actionB.Action.SelectionType)
			{
				return true;
			}
		}
		return false;
	}

	private void DoMoveCardOperation(DuelScene_CDC cardView)
	{
		if (!(cardView == null))
		{
			ZoneType type = cardView.Model.Instance.Zone.Type;
			if (type == ZoneType.Graveyard || type == ZoneType.Exile)
			{
				_cardMovementController.MoveCard(cardView, cardView.Model.Zone);
			}
		}
	}
}
