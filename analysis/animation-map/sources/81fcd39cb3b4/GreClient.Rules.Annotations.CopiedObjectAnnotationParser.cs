using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class CopiedObjectAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_ABILITY_ID = "abilityGrpId";

	public const string DETAIL_EFFECT_TYPE = "LayeredEffectType";

	public const string EFFECT_TYPE_COPY_OBJ = "CopyObject";

	public const string DETAIL_REASON = "REASON";

	public const string DETAIL_COPY_FROM_GRPID = "copyFromGrpid";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		if (!ai.Type.Contains(AnnotationType.CopiedObject))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		ReasonFaceDown reasonFaceDown = GetReasonFaceDown(ai);
		uint abilityGrpId = GetAbilityGrpId(ai);
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (TryGetCard(affectedId, newState, out var card))
			{
				if (string.IsNullOrEmpty(ai.GetStringDetail("LayeredEffectType")))
				{
					card.IsCopy = true;
				}
				else if (ai.GetStringDetail("LayeredEffectType").Equals("CopyObject"))
				{
					card.IsObjectCopy = true;
				}
				if (ai.TryGetInt32Detail("copyFromGrpid", out var value))
				{
					card.CopyObjectGrpId = (uint)value;
				}
				if (reasonFaceDown != ReasonFaceDown.None)
				{
					card.FaceDownState.SetCopiedCardsReasonFaceDown(reasonFaceDown);
				}
				if (abilityGrpId != 0)
				{
					card.FaceDownState.SetCopiedCardsReasonFaceDownSourceAbilityGrpId(abilityGrpId);
				}
			}
		}
	}

	private bool TryGetCard(uint instanceId, MtgGameState state, out MtgCardInstance card)
	{
		if (state.VisibleCards.TryGetValue(instanceId, out card))
		{
			return true;
		}
		if (state.RevealedCards.TryGetValue(instanceId, out card))
		{
			return true;
		}
		card = null;
		return false;
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
