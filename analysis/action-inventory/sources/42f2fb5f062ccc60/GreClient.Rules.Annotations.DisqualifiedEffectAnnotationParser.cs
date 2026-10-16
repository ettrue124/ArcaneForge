using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class DisqualifiedEffectAnnotationParser : IAnnotationEventParser
{
	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (!ai.Type.Contains(AnnotationType.DisqualifiedEffect))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		yield return new DisqualifiedEffectEvent
		{
			AffectorId = ai.AffectorId
		};
	}
}
