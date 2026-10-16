using System;
using System.Collections.Concurrent;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SetSettingsResponseTranslator : GreToClientMessageTranslatorBase
{
	private readonly Func<ConcurrentQueue<ClientUpdateBase>> _getClientUpdates;

	public SetSettingsResponseTranslator(Func<ConcurrentQueue<ClientUpdateBase>> getClientUpdates)
	{
		_getClientUpdates = getClientUpdates;
	}

	public override void Translate(GREToClientMessage msg)
	{
		_getClientUpdates().Enqueue(new SettingsNotification(msg.SetSettingsResp.Settings));
	}
}
