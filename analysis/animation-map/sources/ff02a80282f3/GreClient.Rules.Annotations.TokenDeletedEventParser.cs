using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class TokenDeletedEventParser : IAnnotationEventParser
{
	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (!ai.Type.Contains(AnnotationType.TokenDeleted))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds == null || ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected IDs.", "ai");
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			yield return new CardDeletedEvent(affectedId);
		}
	}
}
