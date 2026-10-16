using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RemovePlayerNumericAidEventTranslator : IEventTranslator
{
	private readonly IPlayerNumericAidController _controller;

	public RemovePlayerNumericAidEventTranslator(IPlayerNumericAidController controller)
	{
		_controller = controller ?? NullPlayerNumericAidController.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is RemovePlayerNumericAid removePlayerNumericAid)
		{
			events.Add(new RemovePlayerNumericAidUXEvent(_controller, removePlayerNumericAid.PlayerId, removePlayerNumericAid.PlayerNumericAid));
		}
	}
}
