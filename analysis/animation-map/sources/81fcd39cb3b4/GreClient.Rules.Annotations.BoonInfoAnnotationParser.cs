using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class BoonInfoAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_TOTAL_USES = "TotalUses";

	public const string DETAIL_USES_REMAINING = "UsesRemaining";

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
		if (!ai.Type.Contains(AnnotationType.BoonInfo))
		{
			throw new ArgumentException("Annotation type does not match parser type BoonInfo.");
		}
		if (!ai.TryGetInt32Detail("TotalUses", out var value))
		{
			throw new ArgumentNullException("Annotation missing detail for totalUses");
		}
		if (!ai.TryGetInt32Detail("UsesRemaining", out var value2))
		{
			throw new ArgumentNullException("Annotation missing detail for usesRemaining");
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			MtgCardInstance cardById = newState.GetCardById(affectedId);
			if (cardById != null)
			{
				cardById.BoonTriggersInitial = (uint)value;
				cardById.BoonTriggersRemaining = (uint)value2;
			}
		}
	}
}
