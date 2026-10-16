using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class CrewedSaddledAnnotationParser : IAnnotationStateParser
{
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
		if (!ai.Type.Contains(AnnotationType.CrewedThisTurn) && !ai.Type.Contains(AnnotationType.SaddledThisTurn))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		if (ai.AffectorId == 0)
		{
			throw new ArgumentException("Annotation does not contain affector id.", "ai");
		}
		if (!newState.TryGetCard(ai.AffectorId, out var card))
		{
			return;
		}
		for (int i = 0; i < ai.AffectedIds.Count; i++)
		{
			card.CrewedAndSaddledByIds.Add(ai.AffectedIds[i]);
			if (newState.TryGetCard(ai.AffectedIds[i], out var card2))
			{
				card2.CrewedAndSaddledParentIds.Add(ai.AffectorId);
			}
		}
	}
}
