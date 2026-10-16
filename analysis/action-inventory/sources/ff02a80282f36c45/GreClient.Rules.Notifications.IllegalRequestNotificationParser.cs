using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Notifications;

public class IllegalRequestNotificationParser : INotificationParser, IDisposable
{
	public ClientUpdateBase Translate(GREToClientMessage msg)
	{
		return new IllegalRequestNotification(msg.IllegalRequestMessage);
	}

	public void Dispose()
	{
	}
}
