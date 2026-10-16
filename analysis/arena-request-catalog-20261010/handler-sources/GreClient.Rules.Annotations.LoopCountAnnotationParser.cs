using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class LoopCountAnnotationParser : IAnnotationStateParser
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
		if (!ai.Type.Contains(AnnotationType.LoopCount))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.TryGetInt32Detail("LoopCount", out var value) && ai.TryGetInt32Detail("LoopLimit", out var value2))
		{
			newState.LoopCounts.Add(new LoopCountData(ai.AffectorId, value, value2));
		}
	}
}
