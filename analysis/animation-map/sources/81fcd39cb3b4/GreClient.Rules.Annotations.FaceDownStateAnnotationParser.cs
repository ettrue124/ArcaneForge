using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class FaceDownStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_GRPID = "abilityGrpId";

	public const string DETAIL_REASON = "REASON";

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
		if (!ai.Type.Contains(AnnotationType.FaceDown))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		ReasonFaceDown reasonFaceDown = GetReasonFaceDown(ai);
		uint abilityGrpId = GetAbilityGrpId(ai);
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetCard(affectedId, out var card))
			{
				if (reasonFaceDown != ReasonFaceDown.None)
				{
					card.FaceDownState.SetReasonFaceDown(reasonFaceDown);
				}
				if (abilityGrpId != 0)
				{
					card.FaceDownState.SetReasonFaceDownSourceAbilityGrpId(abilityGrpId);
				}
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

	private ReasonFaceDown GetReasonFaceDown(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("REASON") && detail.ValueInt32.Count > 0)
			{
				return (ReasonFaceDown)detail.ValueInt32[0];
			}
		}
		return ReasonFaceDown.None;
	}
}
