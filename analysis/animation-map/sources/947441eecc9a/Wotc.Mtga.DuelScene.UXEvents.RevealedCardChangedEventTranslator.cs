using System;
using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RevealedCardChangedEventTranslator : IEventTranslator
{
	private readonly IRevealedCardsController _revealedCardsController;

	public RevealedCardChangedEventTranslator(IRevealedCardsController revealedCardsController)
	{
		_revealedCardsController = revealedCardsController;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges == null)
		{
			throw new ArgumentNullException("allChanges");
		}
		if (changeIndex < 0 || changeIndex >= allChanges.Count)
		{
			throw new ArgumentOutOfRangeException("changeIndex");
		}
		if (events == null)
		{
			throw new ArgumentNullException("events");
		}
		if (!(allChanges[changeIndex] is RevealedCardChangedEvent revealedCardChangedEvent))
		{
			throw new ArgumentException("Event at given index does not implement RevealedCardChangedEvent");
		}
		if (newState.RevealedCards.TryGetValue(revealedCardChangedEvent.InstanceId, out var value))
		{
			events.Add(new UpdateRevealedCardUXEvent(value, _revealedCardsController));
		}
	}
}
