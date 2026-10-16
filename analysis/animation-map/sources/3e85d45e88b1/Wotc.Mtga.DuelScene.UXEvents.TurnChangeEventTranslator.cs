using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class TurnChangeEventTranslator : IEventTranslator
{
	private readonly ITurnController _turnController;

	private readonly IPlayerFocusController _playerFocusController;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly UIManager _uiManager;

	public TurnChangeEventTranslator(ITurnController turnController, IPlayerFocusController playerFocusController, ICardHolderProvider cardHolderProvider, UIManager uiManager)
	{
		_turnController = turnController ?? NullTurnController.Default;
		_playerFocusController = playerFocusController ?? NullPlayerFocusController.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_uiManager = uiManager;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is TurnChangeEvent turnChangeEvent)
		{
			uint gameWideTurn = newState.GameWideTurn;
			UpdateTurnUXEvent item = new UpdateTurnUXEvent(turnChangeEvent.ActivePlayer, gameWideTurn, _turnController, _playerFocusController, _cardHolderProvider, _uiManager);
			UXEventUpdatePhase uXEventUpdatePhase = ((events.Count == 0) ? null : (events[events.Count - 1] as UXEventUpdatePhase));
			if (uXEventUpdatePhase != null && uXEventUpdatePhase.Phase == Phase.Beginning && uXEventUpdatePhase.Step == Step.None)
			{
				events.Insert(events.Count - 1, item);
			}
			else
			{
				events.Add(item);
			}
			_turnController.SetEventTranslationTurnNumber(gameWideTurn);
		}
	}
}
