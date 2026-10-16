using System;
using ReferenceMap;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ObjectsSelectedStateAnnotationParser : IAnnotationStateParser
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
		if (!ai.Type.Contains(AnnotationType.ObjectsSelected))
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
		if (!newState.TryGetEntity(ai.AffectorId, out var mtgEntity))
		{
			return;
		}
		if (mtgEntity is MtgCardInstance mtgCardInstance)
		{
			mtgCardInstance.SelectedObjects.UnionWith(ai.AffectedIds);
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (affectedId != 0)
			{
				newState.ReferenceMap.Add(ai.AffectorId, ReferenceMap.ReferenceType.Selecting, affectedId);
				if (newState.TryGetCard(affectedId, out var card))
				{
					card.SelectedBy.Add(ai.AffectorId);
				}
			}
		}
	}
}
