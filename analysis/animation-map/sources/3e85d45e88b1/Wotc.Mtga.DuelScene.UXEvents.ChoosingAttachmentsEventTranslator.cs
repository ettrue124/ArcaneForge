using System;
using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ChoosingAttachmentsEventTranslator : IEventTranslator
{
	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	public ChoosingAttachmentsEventTranslator(IGameStateProvider gameStateProvider, ICardHolderProvider cardHolderProvider)
	{
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
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
		if (!(allChanges[changeIndex] is ChoosingAttachmentsEvent choosingAttachmentsEvent))
		{
			throw new ArgumentException("Event at given index does not implement ChoosingAttachmentsEvent");
		}
		foreach (MtgZone relevantZone in GetRelevantZones(choosingAttachmentsEvent.AffectedIds, _gameStateProvider.CurrentGameState))
		{
			ZoneType type = relevantZone.Type;
			GREPlayerNum playerType = relevantZone.OwnerNum;
			if (type == ZoneType.Exile)
			{
				playerType = GREPlayerNum.LocalPlayer;
			}
			events.Add(new SetCardholderDirtyUXEvent(type, playerType, _cardHolderProvider));
		}
	}

	private IReadOnlyCollection<MtgZone> GetRelevantZones(IReadOnlyCollection<uint> affectedIds, MtgGameState gameState)
	{
		HashSet<MtgZone> hashSet = new HashSet<MtgZone>();
		foreach (uint affectedId in affectedIds)
		{
			if (gameState.TryGetCard(affectedId, out var card))
			{
				hashSet.Add(card.Zone);
			}
		}
		return hashSet;
	}
}
