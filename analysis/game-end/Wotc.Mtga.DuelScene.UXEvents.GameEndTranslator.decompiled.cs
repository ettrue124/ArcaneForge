using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class GameEndTranslator : IEventTranslator
{
	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is GameEndEvent gameEndEvent)
		{
			events.Add(new GameEndUXEvent(gameEndEvent.Result, gameEndEvent.Loser, gameEndEvent.Reason));
		}
	}
}
