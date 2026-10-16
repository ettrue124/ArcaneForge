using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ModifiedPowerAnnotationParser : IAnnotationStateParser
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
		if (!ai.Type.Contains(AnnotationType.ModifiedPower))
		{
			throw new ArgumentException("Annotation type does not match parser type ModifiedPower.");
		}
		uint affectorId = ai.AffectorId;
		foreach (uint affectedId in ai.AffectedIds)
		{
			MtgCardInstance cardById = newState.GetCardById(affectedId);
			if (cardById != null)
			{
				if (!cardById.Decorators.TryGetValue(affectorId, out var value))
				{
					value = (cardById.Decorators[affectorId] = new HashSet<DecoratorType>());
				}
				value.Add(DecoratorType.Power);
			}
		}
	}
}
