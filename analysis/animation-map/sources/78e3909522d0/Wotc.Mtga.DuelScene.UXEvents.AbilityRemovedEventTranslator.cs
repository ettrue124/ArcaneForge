using System;
using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AbilityRemovedEventTranslator : IEventTranslator
{
	private readonly IEntityViewProvider _viewProvider;

	private readonly IPlayerAbilityController _playerAbilityController;

	public AbilityRemovedEventTranslator(IEntityViewProvider viewProvider, IPlayerAbilityController playerAbilityController)
	{
		_viewProvider = viewProvider ?? NullEntityViewProvider.Default;
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
		if (!(allChanges[changeIndex] is AbilityRemovedEvent abilityRemovedEvent))
		{
			throw new ArgumentException("Event at given index does not implement AbilityRemovedEvent");
		}
		UXEvent item = new AbilityRemovedUXEvent(abilityRemovedEvent.InstanceId, abilityRemovedEvent.RemovedAbility, _viewProvider, _playerAbilityController);
		events.Add(item);
	}
}
