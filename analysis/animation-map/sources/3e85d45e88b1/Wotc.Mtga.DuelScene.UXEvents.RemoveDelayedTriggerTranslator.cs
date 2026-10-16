using System;
using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RemoveDelayedTriggerTranslator : IEventTranslator
{
	private const string DOES_NOT_IMPL_ERROR = "Event at given index does not implement DelayedTriggerDeleted";

	private readonly IDelayedTriggerController _delayedTriggerController;

	public RemoveDelayedTriggerTranslator(IDelayedTriggerController delayedTriggerController)
	{
		_delayedTriggerController = delayedTriggerController ?? NullDelayedTriggerController.Default;
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
		if (allChanges[changeIndex] is DelayedTriggerDeleted delayedTriggerDeleted)
		{
			events.Add(new RemoveDelayedTriggerUXEvent(_delayedTriggerController, delayedTriggerDeleted.DelayedTriggerId));
			return;
		}
		throw new ArgumentException("Event at given index does not implement DelayedTriggerDeleted");
	}
}
