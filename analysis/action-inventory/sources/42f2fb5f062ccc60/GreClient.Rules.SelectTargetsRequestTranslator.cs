using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class SelectTargetsRequestTranslator : RequestTranslatorBase
{
	private readonly Func<GameStateUpdate> _getPendingGameStateUpdate;

	public SelectTargetsRequestTranslator(Func<MtgGameState> getCurrentGameState, Func<GameStateUpdate> getPendingGameStateUpdate, Action<GameStateUpdate> setPendingGameStateUpdate, ConcurrentQueue<ClientToGREMessage> outgoingMessages)
		: base(getCurrentGameState, getPendingGameStateUpdate, setPendingGameStateUpdate, outgoingMessages)
	{
		_getPendingGameStateUpdate = getPendingGameStateUpdate;
	}

	public override void Translate(GREToClientMessage msg)
	{
		SelectTargetsRequest selectTargetsRequest = new SelectTargetsRequest(msg.SelectTargetsReq, msg);
		SetPendingInteraction(selectTargetsRequest);
		SetupTargetingInfoForAbilities(_getPendingGameStateUpdate().NewState, selectTargetsRequest);
	}

	private void SetupTargetingInfoForAbilities(MtgGameState gameState, SelectTargetsRequest selectTargetsRequest)
	{
		for (int i = 0; i < selectTargetsRequest.TargetSelections.Count; i++)
		{
			gameState.TargetInfo.Add(new TargetSpec(selectTargetsRequest.SourceId, new List<uint>(), selectTargetsRequest.TargetSelections[i].TargetingAbilityGrpId, (uint)i, new List<int>()));
		}
	}
}
