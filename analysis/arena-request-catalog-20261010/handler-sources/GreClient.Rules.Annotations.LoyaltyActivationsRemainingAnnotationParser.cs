using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class LoyaltyActivationsRemainingAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_ACTIVATIONS_REMAINING = "ActivationsRemaining";

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
		if (!ai.Type.Contains(AnnotationType.LoyaltyActivationsRemaining))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		uint activationsRemaining = GetActivationsRemaining(ai);
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetCard(affectedId, out var card))
			{
				card.LoyaltyActivationsRemaining = activationsRemaining;
			}
		}
	}

	private static uint GetActivationsRemaining(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("ActivationsRemaining"))
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("AnnotationInfo does not contain activations remaining", "ai");
	}
}
