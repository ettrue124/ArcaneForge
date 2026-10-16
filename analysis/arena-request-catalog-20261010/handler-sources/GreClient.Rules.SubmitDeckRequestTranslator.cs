using System;
using System.Collections.Concurrent;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SubmitDeckRequestTranslator : RequestTranslatorBase
{
	public SubmitDeckRequestTranslator(Func<MtgGameState> getCurrentGameState, Func<GameStateUpdate> getPendingGameStateUpdate, Action<GameStateUpdate> setPendingGameStateUpdate, ConcurrentQueue<ClientToGREMessage> outgoingMessages)
		: base(getCurrentGameState, getPendingGameStateUpdate, setPendingGameStateUpdate, outgoingMessages)
	{
	}

	public override void Translate(GREToClientMessage msg)
	{
		SetPendingInteraction(new SubmitDeckRequest(msg.SubmitDeckReq, msg));
	}
}
