using System;
using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateDelayedTriggerTranslator : IEventTranslator
{
	private const string DOES_NOT_IMPL_ERROR = "Event at given index does not implement DelayedTriggerUpdated";

	private readonly IDelayedTriggerController _delayedTriggerController;

	public UpdateDelayedTriggerTranslator(IDelayedTriggerController delayedTriggerController)
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
		if (allChanges[changeIndex] is DelayedTriggerUpdated delayedTriggerUpdated)
		{
			events.Add(new UpdateDelayedTriggerUXEvent(_delayedTriggerController, delayedTriggerUpdated.DelayedTrigger));
			return;
		}
		throw new ArgumentException("Event at given index does not implement DelayedTriggerUpdated");
	}
}
