using System;
using System.Collections.Generic;
using ReferenceMap;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class PendingEffectEventParser : IAnnotationEventParser, INeedMtgGameState_New
{
	private const string DETAIL_EFFECT_TYPE = "effect_type";

	private const string DETAIL_COUNTER_TYPE = "counter_type";

	private const string DETAIL_COUNT = "count";

	private const string EFFECT_TYPE_TRANSFER_COUNTER = "TransferCounter";

	public MtgGameState newState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (newState == null)
		{
			throw new ArgumentNullException("new gamestate state not set");
		}
		if (!ai.GetStringDetail("effect_type").Equals("TransferCounter"))
		{
			yield break;
		}
		uint affectorId = ai.AffectorId;
		CounterType counterType = (CounterType)ai.GetInt32Detail("counter_type");
		int count = ai.GetInt32Detail("count");
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetEntity(affectedId, out var mtgEntity))
			{
				newState.ReferenceMap.Add(affectorId, ReferenceMap.ReferenceType.PendingAffector, affectedId);
				if (mtgEntity is MtgCardInstance mtgCardInstance)
				{
					SetOverrideData(mtgCardInstance, counterType, count);
					yield return new CountersChangedEvent(mtgCardInstance, mtgCardInstance, affectorId, count, counterType);
				}
			}
		}
	}

	private static void SetOverrideData(MtgCardInstance cardInstance, CounterType counterType, int count)
	{
		MtgCardInstance.PendingEffectOverrideData overrideDataForCard = GetOverrideDataForCard(cardInstance);
		if (counterType == CounterType.P1P1)
		{
			overrideDataForCard.Power = new StringBackedInt(overrideDataForCard.Power.Value + count);
			overrideDataForCard.Toughness = new StringBackedInt(overrideDataForCard.Toughness.Value + count);
		}
	}

	private static MtgCardInstance.PendingEffectOverrideData GetOverrideDataForCard(MtgCardInstance cardInstance)
	{
		if (cardInstance.PendingEffectOverrides != null)
		{
			return cardInstance.PendingEffectOverrides;
		}
		MtgCardInstance.PendingEffectOverrideData pendingEffectOverrideData = new MtgCardInstance.PendingEffectOverrideData
		{
			Power = cardInstance.Power,
			Toughness = cardInstance.Toughness
		};
		foreach (KeyValuePair<CounterType, int> counter in cardInstance.Counters)
		{
			pendingEffectOverrideData.Counters.Add(counter.Key, counter.Value);
		}
		cardInstance.PendingEffectOverrides = pendingEffectOverrideData;
		return pendingEffectOverrideData;
	}
}
