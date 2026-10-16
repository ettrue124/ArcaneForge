using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class GamewideHistoryCountStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_GRPID = "AbilityGrpId";

	public const string DETAIL_COUNT = "count";

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
		if (!ai.Type.Contains(AnnotationType.GamewideHistoryCount))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		newState.TrackHistoricEntity(ai.AffectorId, oldState);
		uint affectorId = ai.AffectorId;
		uint abilityGrpId = GetAbilityGrpId(ai);
		uint count = GetCount(ai);
		if (!newState.TryGetEntity(affectorId, out var mtgEntity) && newState.TrackedHistoricCards.TryGetValue(affectorId, out var value))
		{
			mtgEntity = value;
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			GamewideCountData item = new GamewideCountData(ai.Id, abilityGrpId, affectorId, affectedId, count);
			if (newState.TryGetEntity(affectedId, out var mtgEntity2))
			{
				mtgEntity2.GamewideCounts.Add(item);
			}
			mtgEntity?.GamewideCounts.Add(item);
		}
	}

	private static uint GetAbilityGrpId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("AbilityGrpId") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("Annotation did not contain a valid AbilityGrpId detail.", "ai");
	}

	private static uint GetCount(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("count") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("Annotation did not contain a valid Count detail.", "ai");
	}
}
