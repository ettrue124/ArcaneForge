using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GreClient.CardData;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules;

public class ActionsAvailableRequestTranslator : RequestTranslatorBase
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ManaPaymentConditionAbilities _manaConditionAbilities;

	private readonly Func<GameStateUpdate> _getPendingGameStateUpdate;

	public ActionsAvailableRequestTranslator(Func<MtgGameState> getCurrentGameState, Func<GameStateUpdate> getPendingGameStateUpdate, Action<GameStateUpdate> setPendingGameStateUpdate, ManaPaymentConditionAbilities manaConditionAbilities, ICardDatabaseAdapter cardDatabase, ConcurrentQueue<ClientToGREMessage> outgoingMessages)
		: base(getCurrentGameState, getPendingGameStateUpdate, setPendingGameStateUpdate, outgoingMessages)
	{
		_cardDatabase = cardDatabase;
		_manaConditionAbilities = manaConditionAbilities;
		_getPendingGameStateUpdate = getPendingGameStateUpdate;
	}

	public override void Translate(GREToClientMessage msg)
	{
		ActionsAvailableRequest actionsAvailableRequest = new ActionsAvailableRequest(msg.ActionsAvailableReq, msg);
		SetPendingInteraction(actionsAvailableRequest);
		GameStateUpdate gameStateUpdate = _getPendingGameStateUpdate();
		SetupManaPaymentConditionAbilities(gameStateUpdate.NewState, gameStateUpdate.Changes, actionsAvailableRequest);
	}

	private void SetupManaPaymentConditionAbilities(MtgGameState gamestate, List<GameRulesEvent> changes, ActionsAvailableRequest actionsAvailable)
	{
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action action in actionsAvailable.Actions)
		{
			CheckForAlternateConditionAbility(action.AutoTapSolution, gamestate.GetCardById(action.InstanceId), _manaConditionAbilities, _cardDatabase, changes);
		}
	}

	public static void CheckForAlternateConditionAbility(AutoTapSolution autoTapSolution, MtgCardInstance card, ManaPaymentConditionAbilities manaConditionAbilities, ICardDatabaseAdapter cardDatabse, List<GameRulesEvent> changes)
	{
		if (autoTapSolution == null || card == null)
		{
			return;
		}
		ICollection<ManaPaymentCondition> manaPaymentConditions = autoTapSolution.ManaPaymentConditions;
		if (manaPaymentConditions == null || card.ActiveAbilityWords.Exists((AbilityWordData x) => x.AbilityWord == "ManaPaymentCondition"))
		{
			return;
		}
		foreach (ManaPaymentCondition item in manaPaymentConditions)
		{
			bool flag = manaConditionAbilities.IsManaConditionAbility(item.AbilityGrpId);
			if (!flag)
			{
				AbilityPrintingData abilityPrintingById = cardDatabse.AbilityDataProvider.GetAbilityPrintingById(item.AbilityGrpId);
				if (abilityPrintingById == null)
				{
					continue;
				}
				flag = manaConditionAbilities.IsManaConditionAbility(abilityPrintingById.AbilityWord);
			}
			if (flag)
			{
				card.ActiveAbilityWords.Add(new AbilityWordData
				{
					AbilityWord = "ManaPaymentCondition",
					IsActive = true
				});
				changes.Add(new CardChangedEvent(card, card, 0u, PropertyType.ActiveAbilityWord));
			}
		}
	}
}
