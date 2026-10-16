using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class SuppressedPowerAndToughnessStateParser : IAnnotationStateParser
{
	private const string DETAIL_POWER = "power";

	private const string DETAIL_TOUGHNESS = "toughness";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (newState == null)
		{
			return;
		}
		StringBackedInt valueFromAnnotation = GetValueFromAnnotation(ai, "power");
		StringBackedInt valueFromAnnotation2 = GetValueFromAnnotation(ai, "toughness");
		if (valueFromAnnotation.Equals(StringBackedInt.UNDEFINED) && valueFromAnnotation2.Equals(StringBackedInt.UNDEFINED))
		{
			return;
		}
		foreach (MtgCardInstance item in AffectedCards(ai.AffectedIds, newState))
		{
			item.SuppressedPower = valueFromAnnotation;
			item.SuppressedToughness = valueFromAnnotation2;
		}
	}

	private static StringBackedInt GetValueFromAnnotation(AnnotationInfo ai, string detailName)
	{
		if (!ai.TryGetInt32Detail(detailName, out var value))
		{
			return StringBackedInt.UNDEFINED;
		}
		return new StringBackedInt(value);
	}

	private static IEnumerable<MtgCardInstance> AffectedCards(IReadOnlyList<uint> ids, MtgGameState gameState)
	{
		foreach (uint id in ids)
		{
			if (gameState.TryGetCard(id, out var card))
			{
				yield return card;
			}
		}
	}
}
