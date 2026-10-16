using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Interactions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class WaitingOnStartingPlayerUXEvent : UXEvent
{
	private readonly IClientLocProvider _clientLocProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IWorkflowProvider _workflowProvider;

	private readonly IBrowserController _browserController;

	private readonly UXEventQueue _eventQueue;

	private InformationalBrowser _informationalBrowser;

	public WaitingOnStartingPlayerUXEvent(IClientLocProvider locProvider, IGameStateProvider gameStateProvider, IWorkflowProvider workflowProvider, IBrowserController browserController, UXEventQueue uxEventQueue)
	{
		_clientLocProvider = locProvider;
		_browserController = browserController;
		_workflowProvider = workflowProvider;
		_gameStateProvider = gameStateProvider;
		_eventQueue = uxEventQueue;
		_canTimeOut = false;
	}

	public override void Execute()
	{
		if (_browserController.OpenBrowser(new InformationalBrowserProvider()) is InformationalBrowser informationalBrowser)
		{
			_informationalBrowser = informationalBrowser;
			informationalBrowser.SetHeaderText(_clientLocProvider.GetLocalizedText("DuelScene/Browsers/StartingPlayer_Waiting_Header"));
			informationalBrowser.SetSubHeaderText(_clientLocProvider.GetLocalizedText("DuelScene/Browsers/StartingPlayer_Waiting_SubHeader"));
			_eventQueue.EventExecutionCommenced += OnUxEventCommenced;
		}
		else
		{
			Complete();
		}
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if (CanComplete())
		{
			Complete();
		}
	}

	private bool CanComplete()
	{
		if (!CanCompleteViaWorkflow())
		{
			return CanCompleteViaGameState(_gameStateProvider.LatestGameState);
		}
		return true;
	}

	private bool CanCompleteViaWorkflow()
	{
		if (_workflowProvider.GetPendingWorkflow() == null)
		{
			return _workflowProvider.GetCurrentWorkflow() != null;
		}
		return true;
	}

	private bool CanCompleteViaGameState(MtgGameState gameState)
	{
		if (gameState.Stage == GameStage.Start)
		{
			return gameState.OpponentPendingMessageType != ClientMessageType.ChooseStartingPlayerResp;
		}
		return true;
	}

	protected override void Cleanup()
	{
		base.Cleanup();
		if (_informationalBrowser != null)
		{
			_informationalBrowser.Close();
			_informationalBrowser = null;
		}
		_eventQueue.EventExecutionCommenced -= OnUxEventCommenced;
	}

	private void OnUxEventCommenced(UXEvent uxEvent)
	{
		if (uxEvent is GameStatePlaybackCommencedUXEvent)
		{
			Complete();
		}
	}
}
