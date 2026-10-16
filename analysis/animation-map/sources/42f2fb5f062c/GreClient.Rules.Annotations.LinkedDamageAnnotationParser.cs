using System;
using ReferenceMap;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class LinkedDamageAnnotationParser : IAnnotationStateParser
{
	public static string DETAIL_ABILITY_GRPID = "AbilityGrpId";

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
		if (!ai.Type.Contains(AnnotationType.LinkedDamage))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectorId == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		uint abilityGrpId = GetAbilityGrpId(ai);
		foreach (uint affectedId in ai.AffectedIds)
		{
			newState.ReferenceMap.Add(ai.AffectorId, ReferenceMap.ReferenceType.LinkedDamage, affectedId, abilityGrpId);
		}
	}

	private static uint GetAbilityGrpId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals(DETAIL_ABILITY_GRPID) && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("Annotation contained no detail for Ability GrpId", "ai");
	}
}
