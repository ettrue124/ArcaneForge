using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class ObjectDeselectedEventTranslator : IEventTranslator
{
	private readonly ICardViewProvider _cardViewProvider;

	public ObjectDeselectedEventTranslator(ICardViewProvider cardViewProvider)
	{
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is ObjectDeselectedEvent ode)
		{
			events.Add(new ObjectDeselectedUXEvent(ode, _cardViewProvider));
		}
	}
}
