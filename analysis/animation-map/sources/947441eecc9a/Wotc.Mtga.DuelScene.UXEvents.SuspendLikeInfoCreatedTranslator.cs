using System.Collections.Generic;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class SuspendLikeInfoCreatedTranslator : IEventTranslator
{
	private readonly ISuspendLikeController _controller;

	public SuspendLikeInfoCreatedTranslator(ISuspendLikeController controller)
	{
		_controller = controller;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is SuspendLikeInfoCreated suspendLikeInfoCreated)
		{
			events.Add(new SuspendLikeInfoCreatedUXEvent(suspendLikeInfoCreated.Data, _controller));
		}
	}
}
