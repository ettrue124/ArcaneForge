using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class RemovedGamewideCountEventTranslator : IEventTranslator
{
	private readonly IGamewideCountController _controller;

	public RemovedGamewideCountEventTranslator(IGamewideCountController controller)
	{
		_controller = controller ?? NullGamewideController.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is RemovedGamewideCountEvent removedGamewideCountEvent)
		{
			events.Add(new RemoveGamewideCountUXEvent(removedGamewideCountEvent.RemovedGamewideCount, _controller));
		}
	}
}
