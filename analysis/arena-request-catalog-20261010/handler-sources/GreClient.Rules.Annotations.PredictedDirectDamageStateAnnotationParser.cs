using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class PredictedDirectDamageStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_VALUE = "value";

	public const string DETAIL_MODIFIER = "modifierString";

	public const string ABILITY_WORD = "PredictiveDamage";

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
		if (!ai.Type.Contains(AnnotationType.PredictedDirectDamage))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (newState.TryGetCard(ai.AffectorId, out var card))
		{
			AbilityWordData item = new AbilityWordData
			{
				AbilityWord = "PredictiveDamage",
				AdditionalDetail = GetAdditionalDetail(ai),
				ModifierString = GetModifierString(ai),
				Colors = null,
				AbilityGrpId = 0u,
				ExcludedFromCount = false,
				Choice = null,
				IsActive = true
			};
			card.ActiveAbilityWords.RemoveAll((AbilityWordData x) => x.AbilityWord == "PredictiveDamage");
			card.ActiveAbilityWords.Add(item);
		}
	}

	private static string GetAdditionalDetail(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("value") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32[0].ToString();
			}
		}
		throw new ArgumentException("AnnotationInfo contains no additional detail", "ai");
	}

	private static string GetModifierString(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("modifierString") && detail.ValueString.Count > 0)
			{
				return detail.ValueString[0];
			}
		}
		return null;
	}
}
