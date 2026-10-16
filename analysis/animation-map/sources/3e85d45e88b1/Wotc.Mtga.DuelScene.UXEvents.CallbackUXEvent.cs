using System;
using Wizards.Arena.Client.Logging;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class CallbackUXEvent : UXEvent
{
	private readonly Action _callback;

	private ILogger _logger;

	public CallbackUXEvent(Action callback, ILogger logger = null)
	{
		_callback = callback;
		_logger = logger;
	}

	public override void Execute()
	{
		try
		{
			if (_callback != null)
			{
				_callback();
				Complete();
			}
			else
			{
				Fail();
			}
		}
		catch (Exception ex)
		{
			_logger?.Error(ex.ToString());
			Fail();
		}
	}
}
