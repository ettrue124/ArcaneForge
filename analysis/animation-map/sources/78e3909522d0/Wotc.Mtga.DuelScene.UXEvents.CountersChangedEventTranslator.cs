using System.Collections.Generic;
using AssetLookupTree;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CountersChangedEventTranslator : IEventTranslator
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IEntityViewProvider _viewProvider;

	private readonly ICounterProducedAnimationPlayer _counterProducedAnimationPlayer;

	private readonly AssetLookupSystem _assetLookupSystem;

	public CountersChangedEventTranslator(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context.Get<ICardDatabaseAdapter>(), context.Get<IEntityViewProvider>(), context.Get<ICounterProducedAnimationPlayer>(), assetLookupSystem)
	{
	}

	public CountersChangedEventTranslator(ICardDatabaseAdapter cardDatabase, IEntityViewProvider viewProvider, ICounterProducedAnimationPlayer counterProducedAnimationPlayer, AssetLookupSystem assetLookupSystem)
	{
		_cardDatabase = cardDatabase;
		_viewProvider = viewProvider;
		_counterProducedAnimationPlayer = counterProducedAnimationPlayer;
		_assetLookupSystem = assetLookupSystem;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		CountersChangedEvent countersChangedEvent = allChanges[changeIndex] as CountersChangedEvent;
		uint affectorId = countersChangedEvent.AffectorId;
		MtgEntity entityById = newState.GetEntityById(affectorId);
		if (entityById == null)
		{
			entityById = oldState.GetEntityById(affectorId);
		}
		CounterType type = countersChangedEvent.Type;
		if (type == CounterType.Poison || type == CounterType.Energy)
		{
			if (countersChangedEvent.Change > 0)
			{
				events.Add(new CounterProducedUXEvent(affectorId, countersChangedEvent.NewInstance.InstanceId, countersChangedEvent.Type, _counterProducedAnimationPlayer));
			}
			else if (countersChangedEvent.Change < 0)
			{
				events.Add(new CounterProducedUXEvent(countersChangedEvent.NewInstance.InstanceId, affectorId, countersChangedEvent.Type, _counterProducedAnimationPlayer));
			}
		}
		events.Add(new CountersChangedUXEvent(affectorId, countersChangedEvent.NewInstance.InstanceId, entityById, countersChangedEvent.NewInstance, countersChangedEvent.OldCount, countersChangedEvent.Change, countersChangedEvent.Type, _cardDatabase, _viewProvider, _assetLookupSystem));
	}
}
