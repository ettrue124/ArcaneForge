using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ObjectsToDistinguishAnnotationParser : IAnnotationStateParser
{
	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		new List<GameRulesEvent>();
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (!ai.Type.Contains(AnnotationType.ObjectsToDistinguish))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectorId == 0)
		{
			throw new ArgumentException("Annotation does not contain affector id.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			newState.GetCardById(affectedId)?.DistinguishedByIds.Add(ai.AffectorId);
		}
	}
}
