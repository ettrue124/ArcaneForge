using System;
using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CardReactionEventTranslator : IEventTranslator
{
	private readonly ICardViewProvider _cardViewProvider;

	public CardReactionEventTranslator(ICardViewProvider cardViewProvider)
	{
		_cardViewProvider = cardViewProvider;
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
		if (!(allChanges[changeIndex] is CardReactionEvent cardReactionEvent))
		{
			throw new ArgumentException("Event at given index does not implement CardReactionEvent");
		}
		events.Add(new CardReactionUXEvent(_cardViewProvider, cardReactionEvent.ReactionType, cardReactionEvent.InstanceId));
	}
}
