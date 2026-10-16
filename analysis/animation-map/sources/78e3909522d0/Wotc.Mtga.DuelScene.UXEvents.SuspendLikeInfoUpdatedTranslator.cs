using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class SuspendLikeInfoUpdatedTranslator : IEventTranslator
{
	private readonly ISuspendLikeController _controller;

	public SuspendLikeInfoUpdatedTranslator(ISuspendLikeController controller)
	{
		_controller = controller;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is SuspendLikeInfoUpdated suspendLikeInfoUpdated)
		{
			events.Add(new SuspendLikeInfoUpdatedUXEvent(suspendLikeInfoUpdated.Data, _controller));
		}
	}
}
