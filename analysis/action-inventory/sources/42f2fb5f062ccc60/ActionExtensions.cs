using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

public static class ActionExtensions
{
	private static readonly IReadOnlyList<ManaQuantity> _zeroManaCostList = new List<ManaQuantity> { ManaQuantity.MakeGeneric(0u) };

	public static ICollection<AutoTapAction> AutoTapActions(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		AutoTapSolution autoTapSolution = action.AutoTapSolution;
		if (autoTapSolution == null)
		{
			return Array.Empty<AutoTapAction>();
		}
		return autoTapSolution.AutoTapActions;
	}

	public static bool ContainsXCost(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		foreach (ManaRequirement item in action.ManaCost)
		{
			foreach (ManaColor item2 in item.Color)
			{
				if (item2 == ManaColor.X)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static bool AreAllManaCostsColorX(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action.ManaCost.Count == 0)
		{
			return false;
		}
		foreach (ManaRequirement item in action.ManaCost)
		{
			foreach (ManaColor item2 in item.Color)
			{
				if (item2 != ManaColor.X)
				{
					return false;
				}
			}
		}
		return true;
	}

	public static bool AreAllManaCostsPhyrexian(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		foreach (ManaRequirement item in action.ManaCost)
		{
			if (!item.Color.Contains(ManaColor.Phyrexian))
			{
				return false;
			}
		}
		return action.ManaCost.Count > 0;
	}

	public static bool CanAffordToCast(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (!action.IsPlayAction() && action.AutoTapActions().Count <= 0 && action.ManaCost.Count != 0 && !action.AreAllManaCostsColorX())
		{
			return action.AreAllManaCostsPhyrexian();
		}
		return true;
	}

	public static IEnumerable<uint> AutoTapInstanceIds(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		foreach (AutoTapAction item in action.AutoTapActions())
		{
			if (item.ManaId != 0)
			{
				yield return item.ManaId;
			}
			else if (item.InstanceId != 0)
			{
				yield return item.InstanceId;
			}
		}
	}

	public static bool IsActionType(this Wotc.Mtgo.Gre.External.Messaging.Action action, ActionType actionType)
	{
		if (action != null)
		{
			return action.ActionType == actionType;
		}
		return false;
	}

	public static bool IsCastAction(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action != null)
		{
			return IsCastAction(action.ActionType);
		}
		return false;
	}

	public static bool IsCastAction(ActionType actionType)
	{
		return actionType switch
		{
			ActionType.Cast => true, 
			ActionType.CastAdventure => true, 
			ActionType.CastOmen => true, 
			ActionType.CastLeft => true, 
			ActionType.CastRight => true, 
			ActionType.CastMdfc => true, 
			ActionType.CastPrototype => true, 
			ActionType.CastLeftRoom => true, 
			ActionType.CastRightRoom => true, 
			_ => false, 
		};
	}

	public static bool IsFreeCast(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action.IsCastAction() && action.AlternativeGrpId == 149 && action.AlternativeSourceZcid == 0)
		{
			return !action.ManaSpentMatters;
		}
		return false;
	}

	public static bool IsPlayAction(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action != null)
		{
			return IsPlayAction(action.ActionType);
		}
		return false;
	}

	public static bool IsPlayAction(ActionType actionType)
	{
		return actionType switch
		{
			ActionType.Play => true, 
			ActionType.PlayMdfc => true, 
			_ => false, 
		};
	}

	public static bool IsActivateAction(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action != null)
		{
			return IsActivateAction(action.ActionType);
		}
		return false;
	}

	public static bool IsActivateAction(ActionType actionType)
	{
		return actionType switch
		{
			ActionType.Activate => true, 
			ActionType.ActivateMana => true, 
			ActionType.ActivateTest => true, 
			_ => false, 
		};
	}

	public static bool IsAbilityAction(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action != null)
		{
			return IsAbilityAction(action.ActionType);
		}
		return false;
	}

	public static bool IsAbilityAction(ActionType actionType)
	{
		return actionType switch
		{
			ActionType.Activate => true, 
			ActionType.ActivateMana => true, 
			ActionType.ActivateTest => true, 
			ActionType.SpecialTurnFaceUp => true, 
			ActionType.MakePayment => true, 
			_ => false, 
		};
	}

	public static bool IsMDFCAction(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action != null)
		{
			return IsMDFCAction(action.ActionType);
		}
		return false;
	}

	public static bool IsMDFCAction(ActionType actionType)
	{
		return actionType switch
		{
			ActionType.CastMdfc => true, 
			ActionType.PlayMdfc => true, 
			_ => false, 
		};
	}

	public static bool IsCastSplitCardAction(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action != null)
		{
			return IsCastSplitCardAction(action.ActionType);
		}
		return false;
	}

	public static bool IsCastSplitCardAction(ActionType actionType)
	{
		return actionType switch
		{
			ActionType.CastLeft => true, 
			ActionType.CastRight => true, 
			_ => false, 
		};
	}

	public static bool IsCastAdventureAction(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action != null)
		{
			return action.ActionType == ActionType.CastAdventure;
		}
		return false;
	}

	public static bool IsCastOmenAction(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action != null)
		{
			return action.ActionType == ActionType.CastOmen;
		}
		return false;
	}

	public static bool IsCastPrototypeAction(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action != null)
		{
			return action.ActionType == ActionType.CastPrototype;
		}
		return false;
	}

	public static bool IsCastRoomAction(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action != null)
		{
			return IsCastRoomAction(action.ActionType);
		}
		return false;
	}

	public static bool IsCastRoomAction(ActionType actionType)
	{
		return actionType switch
		{
			ActionType.CastLeftRoom => true, 
			ActionType.CastRightRoom => true, 
			_ => false, 
		};
	}

	public static bool IsCastMultiFacetAction(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action == null)
		{
			return false;
		}
		if (!action.IsMDFCAction() && !action.IsCastAdventureAction() && !action.IsCastOmenAction() && !action.IsCastSplitCardAction() && !action.IsCastPrototypeAction())
		{
			return action.IsCastRoomAction();
		}
		return true;
	}

	public static bool IsEmbalmAbilityAction(ICardDataAdapter model, Wotc.Mtgo.Gre.External.Messaging.Action costAction)
	{
		bool result = false;
		if (model != null && model.AbilityIds.ContainsId(costAction.AbilityGrpId))
		{
			foreach (AbilityPrintingData ability in model.Abilities)
			{
				if (ability.Id == costAction.AbilityGrpId && ability.BaseId == 160)
				{
					result = true;
					break;
				}
			}
		}
		return result;
	}

	public static IReadOnlyList<ManaQuantity> ConvertedActionManaCost(this Wotc.Mtgo.Gre.External.Messaging.Action action, ICardDataAdapter model = null)
	{
		if ((action.IsCastAction() || IsEmbalmAbilityAction(model, action)) && action.ManaCost.Count == 0)
		{
			if (model != null && model.IsDisplayedFaceDown)
			{
				return Array.Empty<ManaQuantity>();
			}
			return _zeroManaCostList;
		}
		return ManaUtilities.ConvertManaCostsToList(action.ManaCost);
	}

	public static IReadOnlyList<ManaQuantity> ConvertedActionManaCost(this Wotc.Mtgo.Gre.External.Messaging.Action action, AbilityPrintingData ability)
	{
		if (AbilityHasACost(ability) && action.ActionType == ActionType.Activate && action.ManaCost.Count == 0)
		{
			return _zeroManaCostList;
		}
		return action.ConvertedActionManaCost();
	}

	private static bool AbilityHasACost(AbilityPrintingData ability)
	{
		if (ability != null)
		{
			return ability.ManaCost.Count > 0;
		}
		return false;
	}

	public static uint? SourceGrpId(this Wotc.Mtgo.Gre.External.Messaging.Action action, MtgGameState gameState)
	{
		if (action == null || gameState == null)
		{
			return null;
		}
		if (action.AlternativeGrpId == 0)
		{
			return SourceGrpId(action.SourceId, gameState);
		}
		return SourceGrpId(action.SourceInstance(gameState));
	}

	public static uint? SourceGrpId(uint sourceId, MtgGameState gameState)
	{
		if (sourceId == 0 || gameState == null)
		{
			return null;
		}
		if (gameState.TryGetCard(sourceId, out var card))
		{
			return SourceGrpId(card);
		}
		foreach (QualificationData qualification in gameState.Qualifications)
		{
			if (qualification.AffectorId == sourceId)
			{
				return qualification.AbilityParentGrpId;
			}
		}
		return null;
	}

	private static uint? SourceGrpId(MtgCardInstance srcInstance)
	{
		if (srcInstance == null)
		{
			return 0u;
		}
		if (srcInstance.ObjectType == GameObjectType.Ability)
		{
			return SourceGrpId(srcInstance.Parent);
		}
		if (srcInstance.GrpId == 2 && srcInstance.ObjectSourceGrpId != 0)
		{
			return srcInstance.ObjectSourceGrpId;
		}
		return srcInstance.GrpId;
	}

	public static MtgCardInstance SourceInstance(this Wotc.Mtgo.Gre.External.Messaging.Action action, MtgGameState gameState)
	{
		if (action == null || gameState == null)
		{
			return null;
		}
		if (action.InstanceId != action.AlternativeSourceZcid && gameState.TryGetCard(action.AlternativeSourceZcid, out var card))
		{
			return card;
		}
		if (gameState.TryGetCard(action.SourceId, out var card2))
		{
			return card2;
		}
		return null;
	}

	public static bool TryGetSourceInstance(this Wotc.Mtgo.Gre.External.Messaging.Action action, MtgGameState gameState, out MtgCardInstance sourceInstance)
	{
		sourceInstance = action.SourceInstance(gameState);
		return sourceInstance != null;
	}

	public static bool IsCategoricalAction(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		if (action == null)
		{
			return false;
		}
		if (action.Selection != 0)
		{
			return action.SelectionType != 0;
		}
		return false;
	}

	public static bool IsDuplicateCastActionOf(this Wotc.Mtgo.Gre.External.Messaging.Action action, Wotc.Mtgo.Gre.External.Messaging.Action secondAction, MtgGameState gameState, IAbilityDataProvider abilityProvider)
	{
		if (action.AbilityGrpId != secondAction.AbilityGrpId)
		{
			return false;
		}
		if (action.AlternativeGrpId != secondAction.AlternativeGrpId)
		{
			return false;
		}
		if (action.GrpId != secondAction.GrpId)
		{
			return false;
		}
		if (action.ActionType != secondAction.ActionType)
		{
			return false;
		}
		if (!action.IsCastAction())
		{
			return false;
		}
		if (!secondAction.IsCastAction())
		{
			return false;
		}
		if (action.Selection != secondAction.Selection)
		{
			return false;
		}
		if (action.SelectionType != secondAction.SelectionType)
		{
			return false;
		}
		MtgCardInstance mtgCardInstance = action.SourceInstance(gameState);
		MtgCardInstance mtgCardInstance2 = secondAction.SourceInstance(gameState);
		if (mtgCardInstance == null || mtgCardInstance2 == null)
		{
			return false;
		}
		if (mtgCardInstance.TitleId != mtgCardInstance2.TitleId)
		{
			return false;
		}
		if (action.AlternativeGrpId != 0)
		{
			AbilityPrintingData abilityPrintingById = abilityProvider.GetAbilityPrintingById(action.AlternativeGrpId);
			if (abilityPrintingById.Category != AbilityCategory.Static)
			{
				return abilityPrintingById.Category == AbilityCategory.AlternativeCost;
			}
			return true;
		}
		if (action.AbilityGrpId != 0)
		{
			AbilityPrintingData abilityPrintingById2 = abilityProvider.GetAbilityPrintingById(action.AbilityGrpId);
			if (abilityPrintingById2.Category != AbilityCategory.Static)
			{
				return abilityPrintingById2.Category == AbilityCategory.AlternativeCost;
			}
			return true;
		}
		return false;
	}

	public static bool PropertiesMatch(this Wotc.Mtgo.Gre.External.Messaging.Action action, Wotc.Mtgo.Gre.External.Messaging.Action other)
	{
		if (action != null && other != null && action.ActionType == other.ActionType && action.InstanceId == other.InstanceId && action.AbilityGrpId == other.AbilityGrpId && action.SourceId == other.SourceId && action.UniqueAbilityId == other.UniqueAbilityId && action.AlternativeGrpId == other.AlternativeGrpId && action.AlternativeSourceZcid == other.AlternativeSourceZcid && action.SelectionType == other.SelectionType)
		{
			return action.Selection == other.Selection;
		}
		return false;
	}

	public static bool IsRedundantDuplicateActivatedAbility(this Wotc.Mtgo.Gre.External.Messaging.Action action, Wotc.Mtgo.Gre.External.Messaging.Action secondAction, IAbilityDataProvider abilityDataProvider)
	{
		if (action == null || secondAction == null)
		{
			return false;
		}
		if (abilityDataProvider == null)
		{
			return false;
		}
		if (action.AbilityGrpId != secondAction.AbilityGrpId)
		{
			return false;
		}
		if (action.Highlight != secondAction.Highlight)
		{
			return false;
		}
		if (action.Selection != secondAction.Selection)
		{
			return false;
		}
		if (action.MaxActivations != secondAction.MaxActivations)
		{
			return false;
		}
		if (action.ActionType != secondAction.ActionType)
		{
			return false;
		}
		if (action.AlternativeGrpId != secondAction.AlternativeGrpId)
		{
			return false;
		}
		ActionType actionType = action.ActionType;
		if (actionType != ActionType.Activate && actionType != ActionType.ActivateMana)
		{
			return false;
		}
		AbilityPrintingData abilityPrintingById = abilityDataProvider.GetAbilityPrintingById(action.AbilityGrpId);
		if (abilityPrintingById == null)
		{
			return false;
		}
		return abilityPrintingById.SubCategory != AbilitySubCategory.NonRedundantActivatedAbility;
	}

	public static bool IsRedundantManaPayment(this Wotc.Mtgo.Gre.External.Messaging.Action action, Wotc.Mtgo.Gre.External.Messaging.Action secondAction)
	{
		if (action == null || secondAction == null)
		{
			return false;
		}
		if (action.ActionType != secondAction.ActionType)
		{
			return false;
		}
		if (action.ActionType != ActionType.MakePayment)
		{
			return false;
		}
		if (action.GrpId != secondAction.GrpId)
		{
			return false;
		}
		if (action.InstanceId != secondAction.InstanceId)
		{
			return false;
		}
		if (action.FacetId != secondAction.FacetId)
		{
			return false;
		}
		if (action.AbilityGrpId != secondAction.AbilityGrpId)
		{
			return false;
		}
		if (action.SourceId != secondAction.SourceId)
		{
			return false;
		}
		if (action.ManaPaymentOptions.Count != secondAction.ManaPaymentOptions.Count)
		{
			return false;
		}
		return true;
	}

	public static bool IsAltCostAction(this Wotc.Mtgo.Gre.External.Messaging.Action action, IAbilityDataProvider abilityDataProvider)
	{
		if (action != null)
		{
			return IsAltCostAbility(action.AlternativeGrpId, abilityDataProvider);
		}
		return false;
		static bool IsAltCostAbility(uint abilityId, IAbilityDataProvider abilityDataProvider2)
		{
			if (!abilityDataProvider2.TryGetAbilityRecordById(abilityId, out var record))
			{
				return false;
			}
			if (record.Tags.Contains(MetaDataTag.IsCostReductionAbility))
			{
				return false;
			}
			if (record.Category == AbilityCategory.AlternativeCost || record.Tags.Contains(MetaDataTag.IsAlternativeCostAbility))
			{
				return true;
			}
			return IsAltCostAbility(record.BaseId, abilityDataProvider2);
		}
	}

	public static DisqualificationType GetDisqualificationType(this Wotc.Mtgo.Gre.External.Messaging.Action action)
	{
		return action.ActionType switch
		{
			ActionType.Cast => DisqualificationType.Cast, 
			ActionType.CastLeft => DisqualificationType.Cast, 
			ActionType.CastRight => DisqualificationType.Cast, 
			ActionType.Play => DisqualificationType.Play, 
			ActionType.Activate => DisqualificationType.Activate, 
			ActionType.ActivateTest => DisqualificationType.Activate, 
			ActionType.ActivateMana => DisqualificationType.Activate, 
			ActionType.SpecialTurnFaceUp => DisqualificationType.TurnFaceUp, 
			_ => DisqualificationType.None, 
		};
	}
}
