using System;
using System.Collections.Generic;
using GreClient.Rules;
using Wizards.Arena.Client.Logging;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Loop;

public class PlaybackState : ILoopModelState, ILoopHandler
{
	private readonly ILoopData loopData;

	private readonly System.Action endPlayback;

	private readonly UXEventQueue uxEventQueue;

	private readonly ILogger logger;

	private int playbackIndex;

	private readonly Queue<ILoopPlaybackCommand> commands;

	public LoopModelStateType Type => LoopModelStateType.Playback;

	public PlaybackState(ILoopData loopData, System.Action endPlaybackCallback, UXEventQueue uxEventQueue, ILogger logger)
	{
		this.loopData = loopData;
		endPlayback = endPlaybackCallback;
		this.uxEventQueue = uxEventQueue;
		this.logger = logger;
		this.uxEventQueue.EventExecutionCompleted += ExecuteCommandWhenUxEventQueueEmpty;
		commands = new Queue<ILoopPlaybackCommand>();
	}

	public void OnEnterState()
	{
		logger?.Debug("PlaybackState.OnEnterState");
		playbackIndex = 0;
		if (loopData.Request == null)
		{
			logger?.Debug("ending playback because there is no open request");
			endPlayback();
		}
		else
		{
			HandleRequest(loopData.Request);
		}
	}

	public void OnExitState()
	{
		logger?.Debug("PlaybackState.OnExitState");
		playbackIndex = 0;
	}

	public bool HandleRequest(BaseUserRequest request)
	{
		logger?.Debug("PlaybackState: setting loopData.request " + loopData.Request.MessageIdAndTypeStr() + " => " + request.MessageIdAndTypeStr());
		loopData.Request = request;
		if (loopData.MessagePairs.Count < 1)
		{
			logger?.Debug("PlaybackState: ending playback when handling request " + request.MessageIdAndTypeStr() + " because there are 0 message pairs.");
			endPlayback();
			return false;
		}
		foreach (IRequestHandler requestHandler in loopData.RequestHandlers)
		{
			if (requestHandler.HandleRequest(request, loopData.MessagePairs[playbackIndex], out var command))
			{
				commands.Enqueue(command);
				playbackIndex = (playbackIndex + 1) % loopData.MessagePairs.Count;
				ExecuteCommandWhenUxEventQueueEmpty();
				return true;
			}
		}
		logger?.Debug("PlaybackState: unhandled request, ending playback. Awaiting recorded request " + loopData.MessagePairs[playbackIndex].Request.MessageIdAndTypeStr() + " but got " + request.MessageIdAndTypeStr());
		endPlayback();
		return false;
	}

	private void ExecuteCommandWhenUxEventQueueEmpty(UXEvent uxEvent = null)
	{
		if (!uxEventQueue.IsRunning && commands.Count > 0)
		{
			commands.Dequeue()?.Execute();
		}
	}

	public void HandleResponse(ClientToGREMessage response)
	{
		if (loopData.RequestHandlers.ValidateResponse(loopData.Request.OriginalMessage, response))
		{
			logger?.Debug("PlaybackState: response valid. " + response.ResponseIdAndTypeStr());
			loopData.Request = null;
		}
		else
		{
			logger?.Debug("PlaybackState: response invalid. " + response.ResponseIdAndTypeStr() + ". Ending playback.");
			endPlayback();
		}
	}

	public void Debug_HandleInput()
	{
		endPlayback();
	}
}
