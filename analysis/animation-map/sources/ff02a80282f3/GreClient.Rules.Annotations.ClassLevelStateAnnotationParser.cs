using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

internal class ClassLevelStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_LEVEL = "Level";

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
		if (!ai.Type.Contains(AnnotationType.ClassLevel))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds.Count <= 0 || ai.AffectedIds[0] != ai.AffectorId)
		{
			return;
		}
		int classLevel = 1;
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("Level") && detail.ValueInt32.Count > 0)
			{
				classLevel = detail.ValueInt32[0];
			}
		}
		if (newState.TryGetCard(ai.AffectorId, out var card))
		{
			card.ClassLevel = classLevel;
		}
	}
}
