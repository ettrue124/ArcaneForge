using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class PayCostsRequestTranslator : RequestTranslatorBase
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ManaPaymentConditionAbilities _manaConditionAbilities;

	private readonly Func<GameStateUpdate> _getPendingGameStateUpdate;

	public PayCostsRequestTranslator(Func<MtgGameState> getCurrentGameState, Func<GameStateUpdate> getPendingGameStateUpdate, Action<GameStateUpdate> setPendingGameStateUpdate, ManaPaymentConditionAbilities manaConditionAbilities, ICardDatabaseAdapter cardDatabase, ConcurrentQueue<ClientToGREMessage> outgoingMessages)
		: base(getCurrentGameState, getPendingGameStateUpdate, setPendingGameStateUpdate, outgoingMessages)
	{
		_cardDatabase = cardDatabase;
		_manaConditionAbilities = manaConditionAbilities;
		_getPendingGameStateUpdate = getPendingGameStateUpdate;
	}

	public override void Translate(GREToClientMessage msg)
	{
		PayCostsRequest payCostsRequest = new PayCostsRequest(msg.PayCostsReq, msg);
		SetPendingInteraction(payCostsRequest);
		GameStateUpdate gameStateUpdate = _getPendingGameStateUpdate();
		SetupManaPaymentConditionAbilities(gameStateUpdate.NewState, gameStateUpdate.Changes, payCostsRequest);
	}

	private void SetupManaPaymentConditionAbilities(MtgGameState gamestate, List<GameRulesEvent> changes, PayCostsRequest payCost)
	{
		AutoTapActionsRequest autoTapActions = payCost.AutoTapActions;
		if (autoTapActions == null)
		{
			return;
		}
		MtgCardInstance topCardOnStack = gamestate.GetTopCardOnStack();
		foreach (AutoTapSolution solution in autoTapActions.Solutions)
		{
			ActionsAvailableRequestTranslator.CheckForAlternateConditionAbility(solution, topCardOnStack, _manaConditionAbilities, _cardDatabase, changes);
		}
	}
}
