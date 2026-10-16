using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class SyntheticEventAnnotationParser : IAnnotationEventParser
{
	public const string TYPE_DETAIL = "type";

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (!ai.Type.Contains(AnnotationType.SyntheticEvent))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds == null || ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected ID.", "ai");
		}
		if (ai.Details == null || ai.Details.Count == 0)
		{
			throw new ArgumentException("Annotation contains no details.");
		}
		SyntheticEventType int32Detail = (SyntheticEventType)ai.GetInt32Detail("type");
		if (int32Detail != SyntheticEventType.None)
		{
			yield return new SyntheticEvent
			{
				Affector = ai.AffectorId,
				Affected = ai.AffectedIds.ToList(),
				Type = int32Detail
			};
		}
	}
}
