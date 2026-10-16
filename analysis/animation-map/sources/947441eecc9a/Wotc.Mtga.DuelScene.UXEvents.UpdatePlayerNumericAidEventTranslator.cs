using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdatePlayerNumericAidEventTranslator : IEventTranslator
{
	private readonly IPlayerNumericAidController _controller;

	public UpdatePlayerNumericAidEventTranslator(IPlayerNumericAidController controller)
	{
		_controller = controller;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is UpdatePlayerNumericAid updatePlayerNumericAid)
		{
			events.Add(new UpdatePlayerNumericAidUXEvent(_controller, updatePlayerNumericAid.PlayerId, updatePlayerNumericAid.PlayerNumericAid));
		}
	}
}
