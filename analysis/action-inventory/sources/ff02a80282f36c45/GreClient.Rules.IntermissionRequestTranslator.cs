using System;
using System.Collections.Concurrent;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class IntermissionRequestTranslator : RequestTranslatorBase
{
	private readonly Func<uint> _getSeatId;

	private readonly Func<GameStateUpdate> _getPendingGameStateUpdate;

	public IntermissionRequestTranslator(Func<uint> getSeatId, Func<MtgGameState> getCurrentGameState, Func<GameStateUpdate> getPendingGameStateUpdate, Action<GameStateUpdate> setPendingGameStateUpdate, ConcurrentQueue<ClientToGREMessage> outgoingMessages)
		: base(getCurrentGameState, getPendingGameStateUpdate, setPendingGameStateUpdate, outgoingMessages)
	{
		_getSeatId = getSeatId;
		_getPendingGameStateUpdate = getPendingGameStateUpdate;
	}

	public override void Translate(GREToClientMessage msg)
	{
		SetPendingInteraction(new IntermissionRequest(msg.IntermissionReq, msg));
		ResultSpec result = msg.IntermissionReq.Result;
		GREPlayerNum loser = GREPlayerNum.Invalid;
		if (result.Result == ResultType.WinLoss)
		{
			loser = ((result.WinningTeamId != _getSeatId()) ? GREPlayerNum.LocalPlayer : GREPlayerNum.Opponent);
		}
		_getPendingGameStateUpdate().Changes.Add(new GameEndEvent
		{
			Result = result.Result,
			Loser = loser,
			Scope = result.Scope,
			Reason = result.Reason
		});
	}
}
