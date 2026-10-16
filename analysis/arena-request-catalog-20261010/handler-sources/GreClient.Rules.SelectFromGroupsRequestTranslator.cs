using System;
using System.Collections.Concurrent;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SelectFromGroupsRequestTranslator : RequestTranslatorBase
{
	public SelectFromGroupsRequestTranslator(Func<MtgGameState> getCurrentGameState, Func<GameStateUpdate> getPendingGameStateUpdate, Action<GameStateUpdate> setPendingGameStateUpdate, ConcurrentQueue<ClientToGREMessage> outgoingMessages)
		: base(getCurrentGameState, getPendingGameStateUpdate, setPendingGameStateUpdate, outgoingMessages)
	{
	}

	public override void Translate(GREToClientMessage msg)
	{
		GroupingStyle groupingStyle = msg.SelectFromGroupsReq.GroupingStyle;
		if ((uint)(groupingStyle - 1) <= 2u)
		{
			SetPendingInteraction(new SelectFromGroupsRequest(msg.SelectFromGroupsReq, msg));
		}
	}
}
