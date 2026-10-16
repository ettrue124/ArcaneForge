using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Notifications;

public class TimerStateMessageNotificationParser : INotificationParser, IDisposable
{
	private Func<MtgGameState> _getCurrentGamestate;

	public TimerStateMessageNotificationParser(Func<MtgGameState> gameState)
	{
		_getCurrentGamestate = gameState;
	}

	public ClientUpdateBase Translate(GREToClientMessage msg)
	{
		TimerStateMessage timerStateMessage = msg.TimerStateMessage;
		if (timerStateMessage != null)
		{
			MtgGameState mtgGameState = _getCurrentGamestate();
			if (mtgGameState != null && mtgGameState.TryGetPlayer(timerStateMessage.SeatId, out var player))
			{
				List<MtgTimer> list = new List<MtgTimer>();
				if (timerStateMessage.Timers != null)
				{
					foreach (TimerInfo timer in timerStateMessage.Timers)
					{
						list.Add(new MtgTimer(timer));
					}
				}
				player.Timers = list;
				return new TimerUpdate(player);
			}
		}
		return null;
	}

	public void Dispose()
	{
		_getCurrentGamestate = null;
	}
}
