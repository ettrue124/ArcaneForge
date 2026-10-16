using System;
using System.Collections.Concurrent;
using System.Threading;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class GreInterfaceThreadedCallbacks : IGreInterfaceCallbacks
{
	private Thread _activeProcessThread;

	private ManualResetEvent _resetEvent;

	private volatile bool _isShuttingDown;

	private GREToClientMessage _currentMsg;

	public void ProcessQueues(ConcurrentQueue<GREToClientMessage> incomingMessages, Action<GREToClientMessage> processMessage)
	{
		if (_resetEvent == null)
		{
			_resetEvent = new ManualResetEvent(initialState: false);
		}
		if (_activeProcessThread == null)
		{
			_activeProcessThread = new Thread(ProcessMessageThread(incomingMessages, processMessage));
			_activeProcessThread.Name = "GreInterface ProcessMessageThread";
			_activeProcessThread.IsBackground = true;
			_activeProcessThread.Start();
		}
		if (incomingMessages.Count != 0)
		{
			_resetEvent.Set();
		}
	}

	private ThreadStart ProcessMessageThread(ConcurrentQueue<GREToClientMessage> incomingMessages, Action<GREToClientMessage> processMessage)
	{
		return ProcessMessageThreadStart;
		void ProcessMessageThreadStart()
		{
			while (!_isShuttingDown)
			{
				_resetEvent.WaitOne();
				while (incomingMessages.TryDequeue(out _currentMsg))
				{
					processMessage(_currentMsg);
				}
				if (!_isShuttingDown)
				{
					_resetEvent.Reset();
				}
			}
		}
	}

	public void OnManualDispose()
	{
		_isShuttingDown = true;
		if (_resetEvent != null)
		{
			_resetEvent.Set();
		}
	}
}
