using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Notifications;

public class ConnectResponseNotificationParser : INotificationParser, IDisposable
{
	public ClientUpdateBase Translate(GREToClientMessage msg)
	{
		return new SettingsNotification(msg.ConnectResp.Settings);
	}

	public void Dispose()
	{
	}
}
