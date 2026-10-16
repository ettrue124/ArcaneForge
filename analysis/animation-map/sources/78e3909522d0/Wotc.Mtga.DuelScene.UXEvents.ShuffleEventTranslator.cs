using System;
using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ShuffleEventTranslator : IEventTranslator
{
	private readonly GameManager _gameManager;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewManager _cardViewManager;

	private readonly ICardHolderProvider _cardHolderProvider;

	public ShuffleEventTranslator(ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardViewManager cardViewManager, ICardHolderProvider cardHolderProvider)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewManager = cardViewManager ?? NullCardViewManager.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
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
		if (!(allChanges[changeIndex] is ShuffleEvent shuffleEvent))
		{
			throw new ArgumentException("Event at given index does not implement ShuffleEvent");
		}
		UXEvent item = new DeckShuffleUXEvent(shuffleEvent.PlayerId, shuffleEvent.OldIds, shuffleEvent.NewIds, _cardDatabase, _gameStateProvider, _cardViewManager, _cardHolderProvider);
		events.Add(item);
	}
}
