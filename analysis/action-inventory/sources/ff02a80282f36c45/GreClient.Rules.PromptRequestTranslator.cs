using System;
using System.Collections.Concurrent;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class PromptRequestTranslator : GreToClientMessageTranslatorBase
{
	private readonly Func<ConcurrentQueue<ClientUpdateBase>> _getClientUpdates;

	public PromptRequestTranslator(Func<ConcurrentQueue<ClientUpdateBase>> getClientUpdates)
	{
		_getClientUpdates = getClientUpdates;
	}

	public override void Translate(GREToClientMessage msg)
	{
		_getClientUpdates().Enqueue(new PromptNotification(msg.Prompt));
	}
}
