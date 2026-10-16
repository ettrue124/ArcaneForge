using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AddedGamewideCountEventTranslator : IEventTranslator
{
	private readonly IGamewideCountController _controller;

	public AddedGamewideCountEventTranslator(IGamewideCountController controller)
	{
		_controller = controller ?? NullGamewideController.Default;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is AddedGamewideCountEvent addedGamewideCountEvent)
		{
			events.Add(new AddGamewideCountUXEvent(addedGamewideCountEvent.AddedGamewideCount, _controller));
		}
	}
}
