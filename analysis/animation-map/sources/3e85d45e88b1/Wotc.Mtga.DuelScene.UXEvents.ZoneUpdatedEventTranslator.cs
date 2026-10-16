using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ZoneUpdatedEventTranslator : IEventTranslator
{
	private readonly ICardHolderProvider _cardHolderProvider;

	public ZoneUpdatedEventTranslator(ICardHolderProvider cardHolderProvider)
	{
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is ZoneUpdatedEvent zoneUpdatedEvent)
		{
			events.Add(new UpdateZoneUXEvent(zoneUpdatedEvent.Zone, _cardHolderProvider));
		}
	}
}
