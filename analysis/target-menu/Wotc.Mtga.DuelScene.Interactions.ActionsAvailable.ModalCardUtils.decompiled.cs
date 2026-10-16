using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.CardData.RulesTextOverrider;
using GreClient.Rules;
using MovementSystem;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public static class ModalCardUtils
{
	private const float IdealPointScale = 0.33f;

	public static DuelScene_CDC GetModalCDC(ActionType actionType, CardData modalCardData, DuelScene_CDC cdc, ISplineMovementSystem splineMovementSystem, ICardBuilder<DuelScene_CDC> cardBuilder, ICardHolderProvider cardHolderProvider, ref bool cardAsButton)
	{
		DuelScene_CDC duelScene_CDC = null;
		if (actionType == ActionType.Cast || actionType == ActionType.Play)
		{
			if (!cardAsButton)
			{
				cardAsButton = true;
				cdc.SetModel(modalCardData);
				splineMovementSystem.MoveInstant(cdc.Root, new IdealPoint(cdc.Root));
				return cdc;
			}
			duelScene_CDC = cardBuilder.CreateCDC(modalCardData);
			duelScene_CDC.CurrentCardHolder = cardHolderProvider.GetCardHolder(GREPlayerNum.Invalid, CardHolderType.Invalid);
			splineMovementSystem.MoveInstant(duelScene_CDC.Root, new IdealPoint(cdc.Root)
			{
				Scale = Vector3.one * 0.33f
			});
		}
		else
		{
			duelScene_CDC = cardBuilder.CreateCDC(modalCardData, isVisible: true);
			splineMovementSystem.MoveInstant(duelScene_CDC.Root, new IdealPoint(cdc.Root)
			{
				Scale = Vector3.one * 0.33f
			});
		}
		return duelScene_CDC;
	}

	public static CardData GetNormalModalCardData(Wotc.Mtgo.Gre.External.Messaging.Action action, CardData originalCard, MtgCardInstance instanceCopy, CardPrintingData printingCopy, IReadOnlyCollection<ManaQuantity> interactionCost, ICardDatabaseAdapter cardDatabase, IReadOnlyList<GreInteraction> allInteractions)
	{
		switch (action.ActionType)
		{
		default:
			return CreateModalCard(action, instanceCopy, printingCopy, interactionCost);
		case ActionType.CastLeft:
		case ActionType.CastRight:
			return CreateModalSplitCard(action, originalCard, interactionCost, cardDatabase.CardDataProvider, allInteractions);
		case ActionType.CastAdventure:
			return CreateNoAltCostOmenOrAdventureSubCard(action, originalCard, interactionCost, cardDatabase.CardDataProvider, allInteractions, GameObjectType.Adventure);
		case ActionType.CastOmen:
			return CreateNoAltCostOmenOrAdventureSubCard(action, originalCard, interactionCost, cardDatabase.CardDataProvider, allInteractions, GameObjectType.Omen);
		case ActionType.CastLeftRoom:
		case ActionType.CastRightRoom:
			return CreateRoomSubCard(action, originalCard, cardDatabase, interactionCost);
		case ActionType.CastMdfc:
		case ActionType.PlayMdfc:
		case ActionType.CastPrototype:
			return CreateNoAltCostModalLinkedFace(action, originalCard, interactionCost, cardDatabase, allInteractions);
		}
	}

	public static CardData CreateModalCard(Wotc.Mtgo.Gre.External.Messaging.Action action, MtgCardInstance instance, CardPrintingData printing, IReadOnlyCollection<ManaQuantity> interactionCost)
	{
		return CreateModifiedModalCard(action, instance, printing, interactionCost);
	}

	public static CardData CreateOmenOrAdventureSubCard(Wotc.Mtgo.Gre.External.Messaging.Action action, CardData originalCard, IReadOnlyCollection<ManaQuantity> interactionCost, ICardDataProvider cardDatabase, GameObjectType objectType)
	{
		if (!CardUtilities.IsOmenOrAdventure(objectType))
		{
			SimpleLog.LogError($"Can't create sub-card for {objectType} because it's not considered an Omen or Adventure.");
			return null;
		}
		CardData cardData = (CardData)originalCard.GetLinkedFaceAtIndex(0, ignoreInstance: false, cardDatabase);
		CardData cardData2 = CreateModifiedModalCard(action, cardData.Instance, cardData.Printing, interactionCost);
		cardData2.Instance.ObjectType = objectType;
		cardData2.Instance.LinkedFaceInstances.Clear();
		cardData2.Instance.ActiveAbilityWords.AddRange(originalCard.ActiveAbilityWords);
		return cardData2;
	}

	private static CardData CreateNoAltCostOmenOrAdventureSubCard(Wotc.Mtgo.Gre.External.Messaging.Action action, CardData originalCard, IReadOnlyCollection<ManaQuantity> interactionCost, ICardDataProvider cardDatabase, IReadOnlyList<GreInteraction> allInteractions, GameObjectType cardObjectType)
	{
		CardData cardData = CreateOmenOrAdventureSubCard(action, originalCard, interactionCost, cardDatabase, cardObjectType);
		if (originalCard.HasPerpetualChanges())
		{
			RemoveAltCostPerpetualAbilities(action, cardData, allInteractions);
		}
		return cardData;
	}

	public static CardData CreateModalSplitCard(Wotc.Mtgo.Gre.External.Messaging.Action action, CardData originalCard, IReadOnlyCollection<ManaQuantity> interactionCost, ICardDataProvider cardDatabase, IReadOnlyList<GreInteraction> allInteractions = null)
	{
		CardData cardData = (CardData)originalCard.GetLinkedFaceAtIndex((action.ActionType != ActionType.CastLeft) ? 1 : 0, ignoreInstance: false, cardDatabase);
		CardData cardData2 = CreateModifiedModalCard(action, cardData.Instance, cardData.Printing, interactionCost);
		cardData2.Instance.ObjectType = GameObjectType.SplitCard;
		RemoveAltCastAbilitiesSplitCard(action, cardData2, allInteractions);
		return cardData2;
	}

	public static CardData CreateModalLinkedFace(Wotc.Mtgo.Gre.External.Messaging.Action action, CardData originalCard, IReadOnlyCollection<ManaQuantity> interactionCost, ICardDatabaseAdapter cardDatabase)
	{
		CardData cardData = (CardData)originalCard.GetLinkedFaceAtIndex(0, ignoreInstance: false, cardDatabase.CardDataProvider);
		MtgCardInstance copy = cardData.Instance.GetCopy();
		CardData cardData2 = CreateModalCard(action, copy, cardData.Printing, interactionCost);
		cardData2.Instance.ObjectType = GameObjectType.Card;
		if (originalCard.Instance != null)
		{
			foreach (DesignationData designation in originalCard.Instance.Designations)
			{
				Designation type = designation.Type;
				if (type == Designation.Commander || type == Designation.Companion)
				{
					cardData2.Instance.Designations.Add(designation);
				}
			}
		}
		if (originalCard.HasPerpetualChanges() && !cardData2.HasPerpetualChanges())
		{
			PerpetualChangeUtilities.CopyPerpetualEffects(originalCard, cardData2, cardDatabase.AbilityDataProvider);
		}
		return cardData2;
	}

	private static CardData CreateNoAltCostModalLinkedFace(Wotc.Mtgo.Gre.External.Messaging.Action action, CardData originalCard, IReadOnlyCollection<ManaQuantity> interactionCost, ICardDatabaseAdapter cardDatabase, IReadOnlyList<GreInteraction> allInteractions)
	{
		CardData cardData = CreateModalLinkedFace(action, originalCard, interactionCost, cardDatabase);
		if (originalCard.HasPerpetualChanges())
		{
			RemoveAltCostPerpetualAbilities(action, cardData, allInteractions);
		}
		return cardData;
	}

	private static CardData CreateModifiedModalCard(Wotc.Mtgo.Gre.External.Messaging.Action action, MtgCardInstance instance, CardPrintingData printing, IReadOnlyCollection<ManaQuantity> interactionCost)
	{
		MtgCardInstance copy = instance.GetCopy();
		CardPrintingRecord record = printing.Record;
		string oldSchoolManaText = ((interactionCost != null) ? ManaUtilities.ConvertToOldSchoolManaText(interactionCost) : null);
		IReadOnlyList<uint> linkedFaceGrpIds = Array.Empty<uint>();
		CardData cardData = new CardData(copy, new CardPrintingData(printing, new CardPrintingRecord(record, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, oldSchoolManaText, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, linkedFaceGrpIds)));
		cardData.Instance.LinkedFaceInstances.Clear();
		cardData.Instance.InstanceId = 0u;
		cardData.Instance.ParentId = 0u;
		cardData.Instance.Parent = null;
		cardData.Instance.ManaCostOverride = interactionCost;
		RemovePrototypeTextBasedOnActionType(action.ActionType, cardData);
		cardData.Instance.PlayWarnings = instance.PlayWarnings;
		return cardData;
	}

	public static CardData CreateRoomSubCard(Wotc.Mtgo.Gre.External.Messaging.Action action, CardData originalCard, ICardDatabaseAdapter cardDb, IReadOnlyCollection<ManaQuantity> interactionCost)
	{
		CardData cardData = (CardData)originalCard.GetLinkedFaceAtIndex((action.ActionType != ActionType.CastLeftRoom) ? 1 : 0, ignoreInstance: false, cardDb.CardDataProvider);
		return CreateModifiedModalCard(action, cardData.Instance, cardData.Printing, interactionCost);
	}

	public static CardData CreateUnlockRoomSubCard(Wotc.Mtgo.Gre.External.Messaging.Action action, CardData originalCard, ICardDatabaseAdapter cardDb, IReadOnlyCollection<ManaQuantity> interactionCost, IClientLocProvider loc)
	{
		CardData cardData = (CardData)originalCard.GetLinkedFaceAtIndex((action.AbilityGrpId != 349) ? 1 : 0, ignoreInstance: false, cardDb.CardDataProvider);
		CardData cardData2 = CreateModifiedModalCard(action, cardData.Instance, cardData.Printing, interactionCost);
		cardData2.Instance.ObjectType = GameObjectType.Ability;
		cardData2.Instance.Parent = cardData.Instance;
		AbilityTextOverride abilityTextOverride = new AbilityTextOverride(cardDb);
		abilityTextOverride.AddAbility(cardData2.Abilities);
		RawTextOverride rawTextOverride = new RawTextOverride(loc.GetLocalizedText("DuelScene/RuleText/UnlockRoomManaCost", ("cost", ManaUtilities.ConvertManaSymbols(ManaUtilities.ConvertToOldSchoolManaText(ManaUtilities.ConvertManaCostsToList(action.ManaCost))))) + "\n");
		cardData2.RulesTextOverride = new RulesTextOverrideAggregate(rawTextOverride, abilityTextOverride);
		return cardData2;
	}

	private static bool IsFaceDownReasonPrintedOnCard(ReasonFaceDown reason)
	{
		return reason switch
		{
			ReasonFaceDown.Cloak => false, 
			ReasonFaceDown.Manifest => false, 
			ReasonFaceDown.ManifestDread => false, 
			_ => true, 
		};
	}

	public static bool IsFaceDownCastId(uint grpId)
	{
		if (grpId != 307)
		{
			return grpId == 37;
		}
		return true;
	}

	private static ReasonFaceDown AbilityIdToReasonFaceDown(uint id)
	{
		return id switch
		{
			314u => ReasonFaceDown.Cloak, 
			307u => ReasonFaceDown.Disguise, 
			37u => ReasonFaceDown.Morph, 
			145u => ReasonFaceDown.Manifest, 
			351u => ReasonFaceDown.ManifestDread, 
			_ => ReasonFaceDown.None, 
		};
	}

	public static bool IsTurnFaceUpAbilityButNotKeyword(ActionType actionType, uint abilityId, in FaceDownState faceDownState)
	{
		bool flag = faceDownState.IsFaceDown && actionType == ActionType.SpecialTurnFaceUp;
		if (flag)
		{
			flag = abilityId switch
			{
				314u => true, 
				145u => true, 
				351u => true, 
				_ => false, 
			};
		}
		return flag;
	}

	private static CardPrintingData CreateFaceDownFakeCardPrinting(List<ManaRequirement> cost, ICardDatabaseAdapter db, CardArtSize cardArtSize)
	{
		string oldSchoolManaText = ManaUtilities.ConvertToOldSchoolManaText(ManaUtilities.ConvertManaCostsToList(cost));
		StringBackedInt power = new StringBackedInt(2);
		StringBackedInt toughness = new StringBackedInt(2);
		IReadOnlyList<CardType> types = new List<CardType> { CardType.Creature };
		return new CardPrintingData(new CardPrintingRecord(0u, 0u, null, 0u, 0u, 0u, 0u, 0u, 0u, 0u, null, cardArtSize, CardRarity.None, null, null, isToken: false, isPrimaryCard: true, isDigitalOnly: false, isRebalanced: false, 0u, 0u, 0u, null, null, null, null, usesSideboard: false, oldSchoolManaText, LinkedFace.None, null, null, default, power, toughness, null, null, null, null, types), db.CardDataProvider, db.AbilityDataProvider);
	}

	private static MtgCardInstance CreateFaceDownModalInstance(ICardDataAdapter sourceModal, GameObjectType gameObjectType, CardPrintingData printing, ReasonFaceDown reason, ICardDatabaseAdapter db, uint grpId)
	{
		MtgCardInstance mtgCardInstance = new MtgCardInstance
		{
			GrpId = grpId,
			CatalogId = WellKnownCatalogId.StandardCardBack,
			ObjectType = gameObjectType,
			Zone = sourceModal.Instance.Zone
		};
		mtgCardInstance.CopyFromPrinting(printing);
		if (reason == ReasonFaceDown.Disguise && db.AbilityDataProvider.TryGetAbilityPrintingById(141939u, out var ability))
		{
			mtgCardInstance.Abilities.Add(ability);
		}
		if (!mtgCardInstance.IsObjectCopy && !mtgCardInstance.IsCopy)
		{
			mtgCardInstance.FaceDownState.SetReasonFaceDown(reason);
		}
		return mtgCardInstance;
	}

	private static CardData CreateCardFromInstance(ICardDataAdapter sourceModal, CardPrintingData printing, MtgCardInstance instance, ICardDatabaseAdapter db)
	{
		CardData cardData = new CardData(instance, printing);
		if (sourceModal.HasPerpetualChanges())
		{
			PerpetualChangeUtilities.CopyPerpetualEffects(sourceModal, cardData, db.AbilityDataProvider);
		}
		return cardData;
	}

	public static (CardData, AbilityPrintingData) CreateTurnFaceUpAbilityModal(ICardDataAdapter sourceModal, List<ManaRequirement> cost, ICardDatabaseAdapter db, uint abilityId)
	{
		ReasonFaceDown reason = AbilityIdToReasonFaceDown(abilityId);
		CardPrintingData printing = CreateFaceDownFakeCardPrinting(cost, db, CardArtSize.Normal);
		MtgCardInstance instance = CreateFaceDownModalInstance(sourceModal, GameObjectType.Ability, printing, reason, db, abilityId);
		CardData cardData = CreateCardFromInstance(sourceModal, printing, instance, db);
		if (IsFaceDownReasonPrintedOnCard(reason))
		{
			SimpleLog.LogError("CreateTurnFaceUpAbilityModal only needs to be used for abilities not printed on the card that turn it face up. Calling this for abilities on the card might break assumptions made in the ALT.");
		}
		AbilityTextOverride abilityTextOverride = new AbilityTextOverride(db);
		abilityTextOverride.AddAbility(abilityId);
		abilityTextOverride.AddSubstitution("manaCost", ManaUtilities.ConvertToOldSchoolManaText(ManaUtilities.ConvertManaCostsToList(cost)));
		cardData.RulesTextOverride = abilityTextOverride;
		AbilityPrintingData abilityPrintingData = new AbilityPrintingData(new AbilityPrintingRecord(abilityId), db.AbilityDataProvider);
		cardData.Instance.Abilities.Add(abilityPrintingData);
		return (cardData, abilityPrintingData);
	}

	public static CardData CreateFaceDownCastingModal(ICardDataAdapter sourceModal, List<ManaRequirement> cost, ICardDatabaseAdapter db, uint abilityId)
	{
		ReasonFaceDown reasonFaceDown = AbilityIdToReasonFaceDown(abilityId);
		if (reasonFaceDown != ReasonFaceDown.Disguise && reasonFaceDown != ReasonFaceDown.Morph)
		{
			SimpleLog.LogError("CreateFaceDownCastingModal is only for CASTING face-down cards and " + $"does not support creating a modal for {reasonFaceDown}. This will cause downstream problems.");
		}
		CardPrintingData printing = CreateFaceDownFakeCardPrinting(cost, db, (!IsFaceDownReasonPrintedOnCard(reasonFaceDown)) ? CardArtSize.Full : CardArtSize.Normal);
		MtgCardInstance instance = CreateFaceDownModalInstance(sourceModal, GameObjectType.Card, printing, reasonFaceDown, db, 3u);
		return CreateCardFromInstance(sourceModal, printing, instance, db);
	}

	private static void RemovePrototypeTextBasedOnActionType(ActionType actionType, ICardDataAdapter modalCardData)
	{
		if (modalCardData != null && modalCardData.Instance != null && modalCardData.Instance.Abilities.ContainsId(263u) && actionType != ActionType.CastPrototype)
		{
			modalCardData.Instance.Abilities.RemoveAll((AbilityPrintingData x) => x.Id == 263);
		}
	}

	private static void RemoveAltCostPerpetualAbilities(Wotc.Mtgo.Gre.External.Messaging.Action action, ICardDataAdapter modalCard, IReadOnlyList<GreInteraction> allInteractions)
	{
		if (modalCard == null || modalCard.Instance == null || allInteractions == null || allInteractions.Count == 0)
		{
			return;
		}
		if (action.AlternativeGrpId == 0)
		{
			foreach (GreInteraction item in allInteractions.FindAll(action, (GreInteraction greInteraction, Wotc.Mtgo.Gre.External.Messaging.Action greAction) => greInteraction.GreAction.GrpId == greAction.GrpId && greInteraction.GreAction.AlternativeGrpId != 0))
			{
				int num = modalCard.Instance.Abilities.FindIndex(item.GreAction, (AbilityPrintingData ability, Wotc.Mtgo.Gre.External.Messaging.Action greAction) => ability.Id == greAction.AlternativeGrpId);
				if (num >= 0)
				{
					modalCard.Instance.Abilities.RemoveAt(num);
				}
			}
		}
		if (CardUtilities.IsLand(modalCard))
		{
			int num2 = modalCard.Instance.Abilities.FindIndex(240u, (AbilityPrintingData ability, uint blitzId) => ability.BaseId == blitzId);
			if (num2 >= 0)
			{
				modalCard.Instance.Abilities.RemoveAt(num2);
			}
		}
	}

	private static void RemoveAltCastAbilitiesSplitCard(Wotc.Mtgo.Gre.External.Messaging.Action action, ICardDataAdapter modalCard, IReadOnlyList<GreInteraction> allInteractions)
	{
		if (allInteractions == null)
		{
			return;
		}
		foreach (GreInteraction allInteraction in allInteractions)
		{
			if (allInteraction.GreAction.ActionType == action.ActionType && allInteraction.GreAction.AbilityGrpId != action.AbilityGrpId)
			{
				int num = modalCard.Instance.Abilities.FindIndex(allInteraction.GreAction, (AbilityPrintingData ability, Wotc.Mtgo.Gre.External.Messaging.Action greAction) => ability.Id == greAction.AbilityGrpId);
				if (num >= 0)
				{
					modalCard.Instance.Abilities.RemoveAt(num);
				}
			}
		}
	}
}
