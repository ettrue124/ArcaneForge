using System.Collections.Generic;
using GreClient.Rules;
using ReferenceMap;
using Wotc.Mtga.DuelScene.Interactions;
using Wotc.Mtga.DuelScene.UXEvents;

namespace Wotc.Mtga.DuelScene;

public static class GameStateUpdateHandlerFactory
{
	public static IClientUpdateHandler<GameStateUpdate> Create(NPEDirector npeDirector, ICardViewProvider cardViewProvider, MapAggregate mapAggregate, IGameStateController gameStateController, IPlayerTimerController playerTimerController, UXEventQueue eventQueue, IEventTranslation eventTranslation, IPromptTextController promptTextController, WorkflowController workflowController)
	{
		List<IClientUpdateHandler<GameStateUpdate>> list = new List<IClientUpdateHandler<GameStateUpdate>>();
		if (npeDirector != null)
		{
			list.Add(new NPEDirectorUpdateHandler(npeDirector, cardViewProvider));
		}
		list.Add(new MapAggregateUpdateHandler(mapAggregate));
		list.Add(new SetLatestGameStateUpdateHandler(gameStateController));
		list.Add(new UpdatePlayerTimersHandler(playerTimerController));
		list.Add(new GenerateUXEventsUpdateHandler(eventQueue, eventTranslation));
		list.Add(new SetPromptTextUpdateHandler(promptTextController));
		list.Add(new WorkflowUpdateHandler(workflowController));
		return new GameStateUpdateHandler(list);
	}
}
