using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class SuspendLikeInfoRemovedTranslator : IEventTranslator
{
	private readonly ISuspendLikeController _controller;

	public SuspendLikeInfoRemovedTranslator(ISuspendLikeController controller)
	{
		_controller = controller;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is SuspendLikeInfoRemoved suspendLikeInfoRemoved)
		{
			events.Add(new SuspendLikeInfoRemovedUXEvent(suspendLikeInfoRemoved.Id, _controller));
		}
	}
}
