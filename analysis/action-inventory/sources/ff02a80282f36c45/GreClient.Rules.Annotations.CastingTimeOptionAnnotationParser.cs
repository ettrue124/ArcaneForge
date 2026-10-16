using System;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class CastingTimeOptionAnnotationParser : IAnnotationStateParser
{
	private const string DETAIL_ALTERNATE_COST_GRPID = "alternateCostGrpId";

	private const string DETAIL_CAST_ABILITY_GRPID = "castAbilityGrpId";

	private const string DETAIL_KICKER_ABILITY_GRPID = "kickerAbilityGrpId";

	private const string DETAIL_CASUALTY_ABILITY_GRPID = "casualtyAbilityGrpId";

	private const string DETAIL_ADDITIONAL_COST_GRPID = "additionalCostGrpId";

	private const string DETAIL_TYPE_STRING = "type";

	private const string DETAIL_COLOR = "color";

	private const string DETAIL_XVALUE = "value";

	private const string DETAIL_CHOSEN_COST_PROMPT_ID = "chosenCostPromptId";

	private const string DETAIL_COST_TYPE = "costType";

	private const string DETAIL_COST_GRPID = "costGrpId";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (!ai.Type.Contains(AnnotationType.CastingTimeOption))
		{
			throw new ArgumentException("Annotation type does not match parser type CastingTimeOption.");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		CastingTimeOptionType castingOptionTypeInfo = GetCastingOptionTypeInfo(ai);
		switch (castingOptionTypeInfo)
		{
		case CastingTimeOptionType.Kicker:
		case CastingTimeOptionType.AdditionalCost:
		case CastingTimeOptionType.ChooseOrCost:
		case CastingTimeOptionType.CastThroughAbility:
		case CastingTimeOptionType.Casualty:
		{
			uint abilityId = GetAbilityId(ai);
			{
				foreach (uint affectedId in ai.AffectedIds)
				{
					if (newState.TryGetCard(affectedId, out var card7))
					{
						card7.CastingTimeOptions.Add(new CastingTimeOption(castingOptionTypeInfo, abilityId));
					}
				}
				break;
			}
		}
		case CastingTimeOptionType.Bargain:
		{
			uint value = 303u;
			{
				foreach (uint affectedId2 in ai.AffectedIds)
				{
					if (newState.TryGetCard(affectedId2, out var card2))
					{
						card2.CastingTimeOptions.Add(new CastingTimeOption(castingOptionTypeInfo, value));
					}
				}
				break;
			}
		}
		case CastingTimeOptionType.Conspire:
		{
			uint value2 = 79u;
			{
				foreach (uint affectedId3 in ai.AffectedIds)
				{
					if (newState.TryGetCard(affectedId3, out var card6))
					{
						card6.CastingTimeOptions.Add(new CastingTimeOption(castingOptionTypeInfo, value2));
					}
				}
				break;
			}
		}
		case CastingTimeOptionType.ChooseX:
		{
			uint xValue = GetXValue(ai);
			{
				foreach (uint affectedId4 in ai.AffectedIds)
				{
					if (newState.TryGetCard(affectedId4, out var card3))
					{
						card3.ChooseXResult = xValue;
						card3.CastingTimeOptions.Add(new CastingTimeOption(castingOptionTypeInfo, xValue));
					}
				}
				break;
			}
		}
		case CastingTimeOptionType.Specialize:
		{
			if (!TryGetColorDetail(ai, out var color))
			{
				break;
			}
			if (newState.TryGetCard(ai.AffectorId, out var card4))
			{
				card4.PendingSpecializationColor = color;
				card4.CastingTimeOptions.Add(new CastingTimeOption(castingOptionTypeInfo, (uint)color));
			}
			{
				foreach (uint affectedId5 in ai.AffectedIds)
				{
					if (newState.TryGetCard(affectedId5, out var card5))
					{
						card5.PendingSpecializationColor = color;
						card5.CastingTimeOptions.Add(new CastingTimeOption(castingOptionTypeInfo, (uint)color));
					}
				}
				break;
			}
		}
		case CastingTimeOptionType.OptionalCostPaid:
		{
			if (newState.TryGetCard(ai.AffectorId, out var card) && TryGetCostType(ai, out var costType))
			{
				card.CastingTimeOptions.Add(new CastingTimeOption(castingOptionTypeInfo, GetAbilityId(ai), costType));
			}
			break;
		}
		case CastingTimeOptionType.Multikicker:
		case CastingTimeOptionType.OptionalCost:
		case CastingTimeOptionType.Replicate:
		case CastingTimeOptionType.ManaType:
		case CastingTimeOptionType.Modal:
		case CastingTimeOptionType.Selection:
		case CastingTimeOptionType.TimingPermission:
			break;
		}
	}

	private uint GetAbilityId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.ValueInt32.Count > 0)
			{
				switch (detail.Key)
				{
				case "castAbilityGrpId":
				case "alternateCostGrpId":
				case "kickerAbilityGrpId":
				case "casualtyAbilityGrpId":
				case "additionalCostGrpId":
				case "chosenCostPromptId":
				case "costGrpId":
					return (uint)detail.ValueInt32[0];
				}
			}
		}
		return 0u;
	}

	private uint GetXValue(AnnotationInfo ai)
	{
		return (uint)ai.Details.First((KeyValuePairInfo x) => x.Key == "value").ValueInt32.ElementAt(0);
	}

	private CastingTimeOptionType GetCastingOptionTypeInfo(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("type") && detail.ValueInt32.Count > 0)
			{
				return (CastingTimeOptionType)detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("Invalid CastingTimeOption annotation without a type detail", "ai");
	}

	private static bool TryGetColorDetail(AnnotationInfo ai, out CardColor color)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key == "color")
			{
				color = (CardColor)detail.ValueInt32[0];
				return true;
			}
		}
		color = CardColor.Colorless;
		return false;
	}

	private bool TryGetCostType(AnnotationInfo ai, out string costType)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("costType") && detail.ValueString.Count > 0)
			{
				costType = detail.ValueString[0];
				return true;
			}
		}
		costType = null;
		return false;
	}
}
