using System;
using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CardCreatedEventTranslator : IEventTranslator
{
	private readonly IContext _context;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly GameManager _gameManager;

	private readonly AssetLookupSystem _assetLookupSystem;

	public CardCreatedEventTranslator(IContext context, AssetLookupSystem assetLookupSystem, GameManager gameManager)
	{
		_context = context ?? (context = NullContext.Default);
		_cardViewProvider = context.Get<ICardViewProvider>() ?? NullCardViewProvider.Default;
		_assetLookupSystem = assetLookupSystem;
		_gameManager = gameManager;
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
		CardCreatedEvent obj = (allChanges[changeIndex] as CardCreatedEvent) ?? throw new ArgumentException("Event at given index does not implement CardCreatedEvent");
		MtgCardInstance card = obj.Card;
		uint instanceId = obj.Card.InstanceId;
		UXEvent item;
		if (TryingToCreatePendingToken(card, _cardViewProvider))
		{
			item = new UpdateCardModelUXEvent(new CardChangedEvent(card, card, instanceId, PropertyType.None), _context, _assetLookupSystem);
		}
		else
		{
			if (card.ObjectType == GameObjectType.Ability && card.Parent == null && TryFindParentIdFromInstigator(allChanges, instanceId, out var parentId))
			{
				card.Parent = newState.GetCardById(parentId);
			}
			item = new ZoneTransferUXEvent(_context, _assetLookupSystem, _gameManager, instanceId, null, instanceId, card, null, null, card.Zone, ZoneTransferReason.CardCreated);
		}
		events.Add(item);
	}

	private bool TryingToCreatePendingToken(MtgCardInstance cardInstance, ICardViewProvider cardViewProvider)
	{
		DuelScene_CDC cardView;
		if (cardInstance.ObjectType == GameObjectType.Token)
		{
			return cardViewProvider.TryGetCardView(cardInstance.InstanceId, out cardView);
		}
		return false;
	}

	private bool TryFindParentIdFromInstigator(IReadOnlyList<GameRulesEvent> allChanges, uint cardId, out uint parentId)
	{
		parentId = 0u;
		foreach (GameRulesEvent allChange in allChanges)
		{
			if (allChange is ZoneChangeEvent zoneChangeEvent && zoneChangeEvent.InstigatorId == cardId)
			{
				parentId = zoneChangeEvent.Id;
				return true;
			}
		}
		return false;
	}
}
