using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ReplacementEffectAppliedStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_GRPID = "grpid";

	public const string DETAIL_DAMAGE_REPLACEMENT = "IsDamageReplacement";

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
		if (!ai.Type.Contains(AnnotationType.ReplacementEffectApplied))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		uint affectorId = ai.AffectorId;
		uint grpId = GetGrpId(ai);
		bool isDamageReplacement = GetIsDamageReplacement(ai);
		foreach (uint affectedId in ai.AffectedIds)
		{
			AppliedReplacementEffectData item = new AppliedReplacementEffectData
			{
				AffectorId = affectorId,
				AffectedId = affectedId,
				AbilityId = grpId,
				IsDamageReplacement = isDamageReplacement
			};
			if (oldState.TryGetCard(affectedId, out var card))
			{
				card.AppliedReplacementEffects.Add(item);
			}
			if (newState.TryGetCard(affectedId, out var card2))
			{
				card2.AppliedReplacementEffects.Add(item);
			}
		}
	}

	public static uint GetGrpId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("grpid") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("AnnotationInfo contains no grpId", "ai");
	}

	public static bool GetIsDamageReplacement(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("IsDamageReplacement") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32[0] == 1;
			}
		}
		return false;
	}
}
