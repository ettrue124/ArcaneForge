using System;
using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using MovementSystem;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CardChangedEventTranslator : IEventTranslator
{
	private readonly IObjectPool _objectPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly CardHolderReference<StackCardHolder> _stack;

	private readonly CardHolderReference<IBattlefieldCardHolder> _battlefield;

	private readonly ICardMovementController _cardMovementController;

	private readonly IVfxProvider _vfxProvider;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly IAssetCacheProvider<SplineMovementData> _splineCacheProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	public CardChangedEventTranslator(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<IObjectPool>(), context.Get<ICardDatabaseAdapter>(), context.Get<IGameStateProvider>(), context.Get<ICardViewProvider>(), context.Get<ICardHolderProvider>(), context.Get<IVfxProvider>(), context.Get<ICardMovementController>(), context.Get<ISplineMovementSystem>(), context.Get<IAssetCacheProvider<SplineMovementData>>(), assetLookupSystem)
	{
	}

	public CardChangedEventTranslator(IObjectPool objectPool, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, ICardHolderProvider cardHolderProvider, IVfxProvider vfxProvider, ICardMovementController cardMovementController, ISplineMovementSystem splineMovementSystem, IAssetCacheProvider<SplineMovementData> splineCacheProvider, AssetLookupSystem assetLookupSystem)
	{
		_objectPool = objectPool;
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_cardViewProvider = cardViewProvider;
		_cardHolderProvider = cardHolderProvider;
		_vfxProvider = vfxProvider;
		_cardMovementController = cardMovementController;
		_splineMovementSystem = splineMovementSystem;
		_splineCacheProvider = splineCacheProvider;
		_assetLookupSystem = assetLookupSystem;
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
		if (!(allChanges[changeIndex] is CardChangedEvent cardChangedEvent))
		{
			throw new ArgumentException("Event at given index does not implement CardChangedEvent");
		}
		if (((cardChangedEvent.OldInstance != null && cardChangedEvent.OldInstance.Zone.Type != ZoneType.Limbo) || (cardChangedEvent.NewInstance != null && cardChangedEvent.NewInstance.Zone.Type != ZoneType.Limbo)) && cardChangedEvent.OldInstance?.MutationParent == null)
		{
			UXEvent item = new UpdateCardModelUXEvent(cardChangedEvent, _objectPool, _cardDatabase, _gameStateProvider, _cardViewProvider, _cardHolderProvider, _vfxProvider, _cardMovementController, _splineMovementSystem, _splineCacheProvider, _assetLookupSystem);
			events.Add(item);
			if (cardChangedEvent.Property == PropertyType.DieRollResult)
			{
				events.Add(new WaitForSecondsUXEvent(0.75f));
			}
		}
	}
}
