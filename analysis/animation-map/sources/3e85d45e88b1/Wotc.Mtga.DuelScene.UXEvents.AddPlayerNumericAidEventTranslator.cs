using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AddPlayerNumericAidEventTranslator : IEventTranslator
{
	private readonly IPlayerNumericAidController _controller;

	public AddPlayerNumericAidEventTranslator(IPlayerNumericAidController controller)
	{
		_controller = controller ?? NullPlayerNumericAidController.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is AddPlayerNumericAid addPlayerNumericAid)
		{
			events.Add(new AddPlayerNumericAidUXEvent(_controller, addPlayerNumericAid.PlayerId, addPlayerNumericAid.PlayerNumericAid));
		}
	}
}
