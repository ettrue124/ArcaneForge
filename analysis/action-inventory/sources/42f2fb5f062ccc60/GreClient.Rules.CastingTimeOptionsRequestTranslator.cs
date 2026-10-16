using System;
using System.Collections.Concurrent;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class CastingTimeOptionsRequestTranslator : RequestTranslatorBase
{
	public CastingTimeOptionsRequestTranslator(Func<MtgGameState> getCurrentGameState, Func<GameStateUpdate> getPendingGameStateUpdate, Action<GameStateUpdate> setPendingGameStateUpdate, ConcurrentQueue<ClientToGREMessage> outgoingMessages)
		: base(getCurrentGameState, getPendingGameStateUpdate, setPendingGameStateUpdate, outgoingMessages)
	{
	}

	public override void Translate(GREToClientMessage msg)
	{
		bool flag = true;
		foreach (CastingTimeOptionReq item in msg.CastingTimeOptionsReq.CastingTimeOptionReq)
		{
			if (item.CastingTimeOptionType != CastingTimeOptionType.ManaType)
			{
				flag = false;
				break;
			}
		}
		if (flag)
		{
			SetPendingInteraction(new CastingTimeOption_ManaTypeRequest(msg.CastingTimeOptionsReq, msg));
		}
		else
		{
			SetPendingInteraction(new CastingTimeOptionRequest(msg.CastingTimeOptionsReq, msg));
		}
	}
}
