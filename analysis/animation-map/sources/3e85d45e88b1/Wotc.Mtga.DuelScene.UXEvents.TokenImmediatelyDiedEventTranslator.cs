using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class TokenImmediatelyDiedEventTranslator : IEventTranslator
{
	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		events.Add(new WaitForSecondsUXEvent(0.5f));
	}
}
