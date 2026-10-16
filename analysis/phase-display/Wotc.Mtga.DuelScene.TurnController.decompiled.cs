using System;
using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene;

public class TurnController : ITurnController, ITurnInfoProvider, IDisposable
{
	public ObservableValue<MtgTurnInfo> TurnInfo { get; } = new ObservableValue<MtgTurnInfo>();

	public ObservableReference<IReadOnlyList<ExtraTurn>> ExtraTurns { get; } = new ObservableReference<IReadOnlyList<ExtraTurn>>();

	public uint EventTranslationTurnNumber { get; private set; }

	public void SetExtraTurns(IReadOnlyList<ExtraTurn> extraTurns)
	{
		ExtraTurns.Value = extraTurns;
	}

	public void SetCurrentTurn(uint turnNumber, uint activePlayerId)
	{
		ObservableValue<MtgTurnInfo> turnInfo = TurnInfo;
		MtgTurnInfo value = TurnInfo.Value;
		uint? turnNumber2 = turnNumber;
		uint? activePlayerId2 = activePlayerId;
		turnInfo.Value = value.Modify(null, null, null, null, turnNumber2, activePlayerId2);
	}

	public void SetDecidingPlayer(uint decisionPlayerId)
	{
		ObservableValue<MtgTurnInfo> turnInfo = TurnInfo;
		MtgTurnInfo value = TurnInfo.Value;
		uint? decisionPlayerId2 = decisionPlayerId;
		turnInfo.Value = value.Modify(null, null, null, null, null, null, decisionPlayerId2);
	}

	public void SetPhase(Phase phase, Step step)
	{
		ObservableValue<MtgTurnInfo> turnInfo = TurnInfo;
		MtgTurnInfo value = TurnInfo.Value;
		Phase? phase2 = phase;
		Step? step2 = step;
		turnInfo.Value = value.Modify(phase2, null, step2);
	}

	public void SetEventTranslationTurnNumber(uint number)
	{
		EventTranslationTurnNumber = number;
	}

	public void Dispose()
	{
		TurnInfo.Dispose();
		ExtraTurns.Dispose();
	}
}
