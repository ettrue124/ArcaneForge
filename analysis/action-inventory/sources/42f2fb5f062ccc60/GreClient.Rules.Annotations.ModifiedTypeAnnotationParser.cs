using System;
using System.Collections.Generic;
using GreClient.CardData;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ModifiedTypeAnnotationParser : IAnnotationStateParser
{
	private const string EFFECT_ID = "effect_id";

	private const string DETAIL_KEY_TEMPORARY_TYPE = "temporaryCardType";

	private const string DETAIL_KEY_SOURCE_ABILITY = "sourceAbilityGRPID";

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
		if (!ai.Type.Contains(AnnotationType.ModifiedType))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		if (ai.AffectorId == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		DecoratorType item = DecoratorType.Type;
		uint affectorId = ai.AffectorId;
		foreach (uint affectedId in ai.AffectedIds)
		{
			MtgCardInstance cardById = newState.GetCardById(affectedId);
			if (cardById != null)
			{
				AddRingBearerLayeredEffects(affectorId, affectedId, cardById, ai, newState);
				AddTemporaryTypeLayeredEffects(affectorId, affectedId, cardById, ai, oldState);
				HashSet<DecoratorType> value = null;
				if (!cardById.Decorators.TryGetValue(affectorId, out value))
				{
					value = (cardById.Decorators[affectorId] = new HashSet<DecoratorType>());
				}
				value.Add(item);
			}
		}
	}

	private void AddRingBearerLayeredEffects(uint affectorId, uint affectedId, MtgCardInstance affectedCard, AnnotationInfo ai, MtgGameState newState)
	{
		if (affectorId != 0 && newState.TryGetCard(affectorId, out var card) && card.Abilities.ContainsId(169427u))
		{
			uint promptId;
			if (card.Abilities.ContainsId(169430u))
			{
				promptId = 4u;
			}
			else
			{
				promptId = (card.Abilities.ContainsId(169429u) ? 3u : ((!card.Abilities.ContainsId(169428u)) ? 1u : 2u));
			}
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			AnnotationDataUtils.ExtractDetails(ai, dictionary);
			affectedCard.LayeredEffects.Add(new LayeredEffectData(ai.Type, affectorId, affectedId, "Ring-Bearer", promptId, (uint)ai.GetInt32Detail("effect_id"), dictionary));
		}
	}

	private void AddTemporaryTypeLayeredEffects(uint affectorId, uint affectedId, MtgCardInstance affectedCard, AnnotationInfo ai, MtgGameState oldState)
	{
		if (affectorId == 0 || !oldState.TryGetCard(affectedId, out var _))
		{
			return;
		}
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (!(detail.Key == "temporaryCardType") || detail == null)
			{
				continue;
			}
			KeyValuePairInfo keyValuePairInfo = detail;
			if (keyValuePairInfo.Type != KeyValuePairValueType.Int32 || keyValuePairInfo.ValueInt32.Count <= 0)
			{
				continue;
			}
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			AnnotationDataUtils.ExtractDetails(ai, dictionary);
			int sourceAbilityId = 0;
			foreach (KeyValuePairInfo detail2 in ai.Details)
			{
				if (detail2.Key == "sourceAbilityGRPID")
				{
					sourceAbilityId = detail2.ValueInt32[0];
					break;
				}
			}
			affectedCard.LayeredEffects.Add(new LayeredEffectData(ai.Type, affectorId, affectedId, "TemporaryType", 0u, (uint)ai.GetInt32Detail("effect_id"), dictionary, (uint)sourceAbilityId));
		}
	}
}
