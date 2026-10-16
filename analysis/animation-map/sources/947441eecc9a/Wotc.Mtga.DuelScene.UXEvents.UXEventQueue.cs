using System;
using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UXEventQueue : IDisposable
{
	private readonly List<UXEvent> evtCache = new List<UXEvent>();

	private readonly List<UXEvent> _pendingEvents = new List<UXEvent>();

	private readonly List<UXEvent> _runningEvents = new List<UXEvent>();

	private bool _pauseExecution;

	public List<UXEvent> Events
	{
		get
		{
			evtCache.Clear();
			evtCache.AddRange(_pendingEvents);
			evtCache.AddRange(_runningEvents);
			return evtCache;
		}
	}

	public IReadOnlyList<UXEvent> PendingEvents => _pendingEvents;

	public IReadOnlyList<UXEvent> RunningEvents => _runningEvents;

	public bool IsRunning
	{
		get
		{
			if (_runningEvents.Count <= 0)
			{
				return _pendingEvents.Count > 0;
			}
			return true;
		}
	}

	public event Action<UXEvent> EventExecutionCommenced;

	public event Action<UXEvent> EventExecutionCompleted;

	public void EnqueuePending(UXEvent evt)
	{
		_pendingEvents.Add(evt);
	}

	public void EnqueuePending(IEnumerable<UXEvent> evts)
	{
		_pendingEvents.AddRange(evts);
	}

	public void Update(float dt)
	{
		if (!IsRunning || _pauseExecution)
		{
			return;
		}
		bool flag = false;
		for (int i = 0; i < _runningEvents.Count; i++)
		{
			UXEvent uXEvent = _runningEvents[i];
			if (uXEvent.IsComplete)
			{
				_runningEvents.RemoveAt(i);
				i--;
				EventExecutionCompleted?.Invoke(uXEvent);
			}
			else
			{
				uXEvent.Update(dt);
				flag |= uXEvent.IsBlocking;
			}
		}
		if (flag)
		{
			return;
		}
		while (!_pauseExecution && _pendingEvents.Count > 0 && _pendingEvents[0].CanExecute(_runningEvents))
		{
			UXEvent uXEvent2 = _pendingEvents[0];
			_pendingEvents.RemoveAt(0);
			uXEvent2.Execute();
			EventExecutionCommenced?.Invoke(uXEvent2);
			if (!uXEvent2.IsComplete)
			{
				_runningEvents.Add(uXEvent2);
				if (uXEvent2.IsBlocking)
				{
					break;
				}
			}
			else
			{
				EventExecutionCompleted?.Invoke(uXEvent2);
			}
		}
	}

	public void Pause()
	{
		_pauseExecution = true;
	}

	public void Resume()
	{
		_pauseExecution = false;
	}

	public void RemoveNpePauseUxEventsInPending()
	{
		_pendingEvents.RemoveAll((UXEvent x) => x is NPEPauseUXEvent);
	}

	public void RemovaNpeUxEventsInPending()
	{
		_pendingEvents.RemoveAll((UXEvent x) => x is NPEUXEvent);
	}

	public void ClearPendingAndRunningIfNotInitialGameState()
	{
		clearEvents(_pendingEvents);
		clearEvents(_runningEvents);
		static void clearEvents(List<UXEvent> events)
		{
			int num = 0;
			while (num < events.Count)
			{
				if (events[num] is GameStatePlaybackCommencedUXEvent gameStatePlaybackCommencedUXEvent)
				{
					int num2 = events.FindIndex(num + 1, (UXEvent x) => x is GameStatePlaybackCompletedUXEvent);
					if (num2 == -1)
					{
						num2 = events.Count - 1;
					}
					int num3 = num2 - num;
					if (num3 > 0 && clearEventsForGameState(gameStatePlaybackCommencedUXEvent.GameState))
					{
						events.RemoveRange(num, num3);
					}
					num = num2 + 1;
				}
				else
				{
					events.RemoveAt(num);
					num++;
				}
			}
		}
		static bool clearEventsForGameState(MtgGameState gameState)
		{
			if (gameState.Type != MtgGameState.StateType.InitialGameState)
			{
				return gameState.Stage != GameStage.GameOver;
			}
			return false;
		}
	}

	public void Dispose()
	{
		EventExecutionCommenced = null;
		EventExecutionCompleted = null;
	}
}
