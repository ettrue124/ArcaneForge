using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

internal class CopyExceptionAnnotationParser : IAnnotationStateParser
{
	private const string MANA_COST_STRING = "manaCost";

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
		if (ai.Details == null || ai.Details.Count == 0)
		{
			throw new ArgumentException("Annotation contains no details.", "ai");
		}
		if (!ai.Type.Contains(AnnotationType.CopyException))
		{
			throw new ArgumentException("Annotation type does not match parser type CopyException.");
		}
		_ = ai.AffectorId;
		foreach (uint affectedId in ai.AffectedIds)
		{
			MtgCardInstance cardById = newState.GetCardById(affectedId);
			if (cardById != null)
			{
				cardById.CopyExceptionManaString = ai.GetStringDetail("manaCost");
			}
		}
	}
}
