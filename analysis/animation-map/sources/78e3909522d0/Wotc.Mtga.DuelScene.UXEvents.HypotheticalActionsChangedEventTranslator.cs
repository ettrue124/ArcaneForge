using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class HypotheticalActionsChangedEventTranslator : IEventTranslator
{
	private readonly IContext _context;

	public HypotheticalActionsChangedEventTranslator(IContext context)
	{
		_context = context ?? NullContext.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is HypotheticalActionsChangedEvent hypotheticalActionsChangedEvent)
		{
			events.Add(new HypotheticalActionsUXChangedEvent(hypotheticalActionsChangedEvent.OldActions, hypotheticalActionsChangedEvent.NewActions, _context));
		}
	}
}
