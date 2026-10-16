using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateGamewideCountEventTranslator : IEventTranslator
{
	private readonly IGamewideCountController _controller;

	public UpdateGamewideCountEventTranslator(IGamewideCountController controller)
	{
		_controller = controller ?? NullGamewideController.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is UpdateGamewideCountEvent updateGamewideCountEvent)
		{
			events.Add(new UpdateGamewideCountUXEvent(updateGamewideCountEvent.UpdatedGamewideCount, _controller));
		}
	}
}
