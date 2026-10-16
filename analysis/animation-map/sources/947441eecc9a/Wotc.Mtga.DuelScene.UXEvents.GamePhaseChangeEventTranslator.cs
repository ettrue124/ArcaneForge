using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class GamePhaseChangeEventTranslator : IEventTranslator
{
	private readonly ITurnController _turnController;

	public GamePhaseChangeEventTranslator(ITurnController turnController)
	{
		_turnController = turnController ?? NullTurnController.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is GamePhaseChangeEvent gamePhaseChangeEvent)
		{
			events.Add(new UXEventUpdatePhase(gamePhaseChangeEvent.Phase, gamePhaseChangeEvent.Step, _turnController));
		}
	}
}
