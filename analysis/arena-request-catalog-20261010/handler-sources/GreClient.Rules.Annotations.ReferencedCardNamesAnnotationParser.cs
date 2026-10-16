using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ReferencedCardNamesAnnotationParser : IAnnotationStateParser
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
		if (!ai.Type.Contains(AnnotationType.ReferencedCardNames))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectorId == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		if (!newState.TryGetCard(ai.AffectorId, out var card))
		{
			return;
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			card.ReferencedCardTitleIds.Add(affectedId);
		}
	}
}
