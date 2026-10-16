using System;
using System.Collections.Concurrent;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class GetSettingsResponseTranslator : GreToClientMessageTranslatorBase
{
	private readonly Func<ConcurrentQueue<ClientUpdateBase>> _getClientUpdates;

	public GetSettingsResponseTranslator(Func<ConcurrentQueue<ClientUpdateBase>> getClientUpdates)
	{
		_getClientUpdates = getClientUpdates;
	}

	public override void Translate(GREToClientMessage msg)
	{
		_getClientUpdates().Enqueue(new SettingsNotification(msg.GetSettingsResp.Settings));
	}
}
