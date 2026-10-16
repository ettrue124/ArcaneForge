using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Notifications;

public class TimeoutMessageNotificationParser : INotificationParser, IDisposable
{
	public ClientUpdateBase Translate(GREToClientMessage msg)
	{
		uint seatId = msg.TimeoutMessage.SeatId;
		uint timeoutCount = msg.TimeoutMessage.TimeoutCount;
		return new TimeoutNotification(new MtgTimer(msg.TimeoutMessage.Timer), seatId, timeoutCount);
	}

	public void Dispose()
	{
	}
}
