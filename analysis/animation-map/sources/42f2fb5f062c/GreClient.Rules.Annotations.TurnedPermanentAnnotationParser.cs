using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class TurnedPermanentAnnotationParser : IAnnotationStateParser
{
	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (oldState == null)
		{
			throw new ArgumentNullException("oldState");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (!ai.Type.Contains(AnnotationType.TurnPermanent))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (oldState.TryGetCard(affectedId, out var card) && newState.TryGetCard(affectedId, out var card2))
			{
				card2.FaceDownState.SetWasTurnedFaceUpFrom(card.FaceDownState.ReasonFaceDown);
			}
		}
	}
}
