using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class DeleteZoneEventTranslator : IEventTranslator
{
	private readonly ICardHolderController _cardHolderController;

	public DeleteZoneEventTranslator(ICardHolderController cardHolderController)
	{
		_cardHolderController = cardHolderController ?? NullCardHolderController.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is DeleteZoneEvent deleteZoneEvent)
		{
			events.Add(new DeleteZoneUXEvent(deleteZoneEvent.ZoneId, _cardHolderController));
		}
	}
}
