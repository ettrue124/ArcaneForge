using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Interactions;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class WaitingOnStartingPlayerEventTranslator : IEventTranslator
{
	private readonly IClientLocProvider _clientLocProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IWorkflowProvider _workflowProvider;

	private readonly IBrowserController _browserController;

	private readonly UXEventQueue _eventQueue;

	public WaitingOnStartingPlayerEventTranslator(IContext context, UXEventQueue eventQueue)
		: this(context.Get<IClientLocProvider>(), context.Get<IGameStateProvider>(), context.Get<IWorkflowProvider>(), context.Get<IBrowserController>(), eventQueue)
	{
	}

	private WaitingOnStartingPlayerEventTranslator(IClientLocProvider locProvider, IGameStateProvider gameStateProvider, IWorkflowProvider workflowProvider, IBrowserController browserController, UXEventQueue uxEventQueue)
	{
		_clientLocProvider = locProvider ?? NullLocProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_workflowProvider = workflowProvider ?? NullWorkflowProvider.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_eventQueue = uxEventQueue;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is WaitingOnStartingPlayerEvent)
		{
			events.Add(new WaitingOnStartingPlayerUXEvent(_clientLocProvider, _gameStateProvider, _workflowProvider, _browserController, _eventQueue));
		}
	}
}
