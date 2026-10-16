using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Notifications;

public class EdictalMessageNotificationParser : INotificationParser, IDisposable
{
	public ClientUpdateBase Translate(GREToClientMessage msg)
	{
		return new EdictalUpdate(msg.EdictalMessage);
	}

	public void Dispose()
	{
	}
}
