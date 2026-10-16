using System;
using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AbilityAddedEventTranslator : IEventTranslator
{
	private readonly IGameStateProvider _gameStateProvider;

	private readonly IEntityViewProvider _viewProvider;

	private readonly IAbilityDataProvider _abilityDataProvider;

	private readonly IPlayerAbilityController _playerAbilityController;

	public AbilityAddedEventTranslator(IContext context)
		: this(context.Get<IGameStateProvider>(), context.Get<IEntityViewProvider>(), context.Get<IAbilityDataProvider>(), context.Get<IPlayerAbilityController>())
	{
	}

	public AbilityAddedEventTranslator(IGameStateProvider gameStateProvider, IEntityViewProvider viewProvider, IAbilityDataProvider abilityDataProvider, IPlayerAbilityController playerAbilityController)
	{
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_viewProvider = viewProvider ?? NullEntityViewProvider.Default;
		_abilityDataProvider = abilityDataProvider ?? NullAbilityDataProvider.Default;
		_playerAbilityController = playerAbilityController ?? NullPlayerAbilityController.Default;
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
		if (!(allChanges[changeIndex] is AbilityAddedEvent abilityAddedEvent))
		{
			throw new ArgumentException("Event at given index does not implement AbilityAddedEvent");
		}
		events.Add(new AbilityAddedUXEvent(abilityAddedEvent.InstanceId, abilityAddedEvent.AddedData, abilityAddedEvent.AffectorId, _viewProvider, _abilityDataProvider, _gameStateProvider, _playerAbilityController));
	}
}
