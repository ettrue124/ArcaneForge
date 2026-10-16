using GreClient.Rules;
using Wotc.Mtga.DuelScene.UXEvents;

namespace Wotc.Mtga.DuelScene;

public class GenerateUXEventsUpdateHandler : IClientUpdateHandler<GameStateUpdate>
{
	private readonly UXEventQueue _eventQueue;

	private readonly IEventTranslation _eventTranslation;

	public GenerateUXEventsUpdateHandler(UXEventQueue eventQueue, IEventTranslation eventTranslation)
	{
		_eventQueue = eventQueue;
		_eventTranslation = eventTranslation;
	}

	public void Handle(GameStateUpdate clientUpdate)
	{
		_eventQueue.EnqueuePending(_eventTranslation.GenerateEvents(clientUpdate));
	}
}
