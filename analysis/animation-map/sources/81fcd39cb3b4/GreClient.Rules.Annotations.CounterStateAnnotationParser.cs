using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class CounterStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_COUNT = "count";

	public const string DETAIL_COUNTER = "counter_type";

	public const string DETAIL_PERPETUAL_COUNT = "perpetual_count";

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
		if (!ai.Type.Contains(AnnotationType.Counter))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		uint instanceId = ai.AffectedIds[0];
		CounterType counterType = GetCounterType(ai);
		int count = GetCount(ai);
		if (count != 0 && newState.TryGetEntity(instanceId, out var mtgEntity))
		{
			mtgEntity.Counters[counterType] = count;
			mtgEntity.CounterDatas.Add(new CounterData(counterType, count));
		}
	}

	private static int GetCount(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("count") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("AnnotationInfo contains no counter count", "ai");
	}

	private static CounterType GetCounterType(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("counter_type") && detail.ValueInt32.Count > 0)
			{
				return (CounterType)detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("AnnotationInfo contains no counter type", "ai");
	}
}
