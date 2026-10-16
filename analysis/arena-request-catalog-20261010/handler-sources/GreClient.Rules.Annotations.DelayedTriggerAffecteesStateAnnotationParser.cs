using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class DelayedTriggerAffecteesStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_GRPID = "abilityGrpId";

	public const string DETAIL_REMOVES = "removesFromZone";

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
		if (!ai.Type.Contains(AnnotationType.DelayedTriggerAffectees))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		uint abilityGrpId = GetAbilityGrpId(ai);
		newState.DelayedTriggerAffectees.Add(new DelayedTriggerData(ai.AffectorId, abilityGrpId, ai.AffectedIds, ai.Details));
		if (!GetRemovesFromZone(ai))
		{
			return;
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetCard(affectedId, out var card))
			{
				card.IsTemporary = true;
			}
		}
	}

	private static uint GetAbilityGrpId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("abilityGrpId") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return 0u;
	}

	private static bool GetRemovesFromZone(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("removesFromZone") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32[0] == 1;
			}
		}
		return false;
	}
}
