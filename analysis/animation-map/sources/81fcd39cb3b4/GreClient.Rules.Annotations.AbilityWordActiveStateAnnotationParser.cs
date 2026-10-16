using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class AbilityWordActiveStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_ABILITY_WORD = "AbilityWordName";

	public const string DETAIL_VALUE = "value";

	public const string DETAIL_COLORS = "colors";

	public const string DETAIL_ABILITY_GRPID = "AbilityGrpId";

	public const string DETAIL_MINI_CDC = "ShowOnMiniCDC";

	public const string DETAIL_EXCLUDED_FROM_COUNT = "ExcludedFromCount";

	public const string DETAIL_CHOICE = "choice";

	public const string DETAIL_ACTIVE = "isActive";

	public const string DETAIL_MANA_SPEC = "manaSpec";

	public const string DETAIL_THRESHOLD = "threshold";

	public const string DETAIL_TYPES = "types";

	public const string DETAIL_TRIGGER_CONDITION_MET = "trigger_condition_met";

	public const string DETAIL_FOR_MODE = "for-mode";

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
		if (!ai.Type.Contains(AnnotationType.AbilityWordActive) && !ai.Type.Contains(AnnotationType.SupplementalText))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		string abilityWord = GetAbilityWord(ai);
		string valueText = GetValueText(ai);
		int[] valueArray = GetValueArray(ai);
		bool showOnMiniCdc = GetShowOnMiniCdc(ai);
		List<int> colors = GetColors(ai);
		bool excludedFromCount = GetExcludedFromCount(ai);
		string choice = GetChoice(ai);
		bool isActive = GetIsActive(ai);
		IReadOnlyList<ManaSpecType> manaSpecTypes = GetManaSpecTypes(ai);
		int? thresholdValue = GetThresholdValue(ai);
		List<int> typesValue = GetTypesValue(ai);
		int triggerConditionValue = GetTriggerConditionValue(ai);
		int forModeValue = GetForModeValue(ai);
		if (IsSpecialPartyCase(abilityWord, valueText))
		{
			isActive = true;
		}
		IEnumerator<uint> enumerator = GetAbilityGrpIds(ai).GetEnumerator();
		int num = 0;
		while (true)
		{
			bool flag = enumerator.MoveNext();
			if ((1u & (flag ? 1u : 0u)) == 0 && num != 0)
			{
				break;
			}
			num++;
			AbilityWordData abilityWordData = new AbilityWordData
			{
				AbilityWord = abilityWord,
				AdditionalDetail = valueText,
				Colors = colors,
				AbilityGrpId = (flag ? enumerator.Current : 0u),
				ShowOnMiniCDC = showOnMiniCdc,
				ExcludedFromCount = excludedFromCount,
				Choice = choice,
				IsActive = isActive,
				Values = valueArray,
				ManaSpecTypes = manaSpecTypes,
				Threshold = thresholdValue,
				CardTypes = typesValue,
				TriggerConditionMet = triggerConditionValue,
				ForModeId = forModeValue
			};
			if (ShouldApplyToAffectorInsteadOfAffected(abilityWord))
			{
				if (newState.TryGetEntity(ai.AffectorId, out var mtgEntity))
				{
					mtgEntity.ActiveAbilityWords.Add(abilityWordData);
				}
				continue;
			}
			foreach (uint affectedId in ai.AffectedIds)
			{
				if (newState.TryGetEntity(affectedId, out var mtgEntity2))
				{
					AddAbilityWordToEntity(mtgEntity2, abilityWordData);
					AddAbilityWordToGameState(mtgEntity2, newState, abilityWordData);
				}
			}
		}
	}

	private static void AddAbilityWordToEntity(MtgEntity entity, AbilityWordData abilityWordData)
	{
		entity.ActiveAbilityWords.Add(abilityWordData);
	}

	private static void AddAbilityWordToGameState(MtgEntity entity, MtgGameState newState, AbilityWordData abilityWordData)
	{
		if (!(entity is MtgCardInstance mtgCardInstance))
		{
			return;
		}
		string abilityWord = abilityWordData.AbilityWord;
		int[] values = abilityWordData.Values;
		int value = ((values != null && values.Length != 0) ? abilityWordData.Values[0] : 0);
		if (mtgCardInstance.Controller != null)
		{
			uint instanceId = mtgCardInstance.Controller.InstanceId;
			if (newState.PlayerToActiveAbilityWords.TryGetValue(instanceId, out var value2))
			{
				value2[abilityWord] = value;
				return;
			}
			newState.PlayerToActiveAbilityWords[instanceId] = new Dictionary<string, int> { { abilityWord, value } };
		}
	}

	private static string GetAbilityWord(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("AbilityWordName") && detail.ValueString.Count > 0)
			{
				return detail.ValueString[0];
			}
		}
		throw new ArgumentException("Annotation contained no detail for Ability Word", "ai");
	}

	private static string GetValueText(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("value") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32[0].ToString();
			}
		}
		return null;
	}

	private static int[] GetValueArray(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("value") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32.ToArray();
			}
		}
		return null;
	}

	private static bool GetShowOnMiniCdc(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("ShowOnMiniCDC") && detail.ValueString.Count > 0)
			{
				return bool.Parse(detail.ValueString[0]);
			}
		}
		return true;
	}

	private static List<int> GetColors(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("colors") && detail.ValueInt32.Count > 0)
			{
				return new List<int>(detail.ValueInt32);
			}
		}
		return null;
	}

	private static IEnumerable<uint> GetAbilityGrpIds(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (!detail.Key.Equals("AbilityGrpId") || detail.ValueInt32.Count <= 0)
			{
				continue;
			}
			foreach (int item in detail.ValueInt32)
			{
				yield return (uint)item;
			}
		}
	}

	private static bool GetExcludedFromCount(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("ExcludedFromCount") && detail.ValueString.Count > 0)
			{
				return bool.Parse(detail.ValueString[0]);
			}
		}
		return false;
	}

	private static string GetChoice(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("choice") && detail.ValueString.Count > 0)
			{
				return detail.ValueString[0];
			}
		}
		return string.Empty;
	}

	private static bool GetIsActive(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("isActive") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32[0] == 1;
			}
		}
		return false;
	}

	private static IReadOnlyList<ManaSpecType> GetManaSpecTypes(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("manaSpec") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32.Cast<ManaSpecType>().ToArray();
			}
		}
		return Array.Empty<ManaSpecType>();
	}

	private static bool IsSpecialPartyCase(string abilityWord, string additionalDetail)
	{
		if (abilityWord.Equals("PartySize"))
		{
			return additionalDetail.Equals("4");
		}
		return false;
	}

	private static bool ShouldApplyToAffectorInsteadOfAffected(string abilityWord)
	{
		if (abilityWord == "Mentor" || abilityWord == "LifeGainedThisTurn")
		{
			return true;
		}
		return false;
	}

	private static int? GetThresholdValue(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("threshold") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32[0];
			}
		}
		return null;
	}

	private static List<int> GetTypesValue(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("types") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32.ToList();
			}
		}
		return null;
	}

	private static int GetTriggerConditionValue(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("trigger_condition_met") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32.FirstOrDefault();
			}
		}
		return 0;
	}

	private static int GetForModeValue(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("for-mode") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32.FirstOrDefault();
			}
		}
		return 0;
	}
}
