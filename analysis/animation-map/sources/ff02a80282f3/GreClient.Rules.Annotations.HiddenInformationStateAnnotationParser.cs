using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class HiddenInformationStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_MANAVALUE = "ManaValue";

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
		if (!ai.Type.Contains(AnnotationType.HiddenInformation))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		if (!ai.TryGetInt32Detail("ManaValue", out var value))
		{
			return;
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetCard(affectedId, out var card) && !card.RevealedToOpponent)
			{
				card.FaceDownState.SetManaCost(value);
			}
		}
	}
}
