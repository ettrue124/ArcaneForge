using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class ControllerChangedAnnotationParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New, INeedList_Changes
{
	public MtgGameState oldState { private get; set; }

	public MtgGameState newState { private get; set; }

	public IReadOnlyList<GameRulesEvent> changes { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (!ai.Type.Contains(AnnotationType.ControllerChanged))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds == null || ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected ID.", "ai");
		}
		if (oldState == null)
		{
			throw new ArgumentNullException("oldState");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			MtgCardInstance cardById = oldState.GetCardById(affectedId);
			if (cardById != null)
			{
				MtgCardInstance cardById2 = newState.GetCardById(affectedId);
				MtgCardInstance mtgCardInstance = GreInterface.GetPreviousIncrementalEntity(affectedId, changes) as MtgCardInstance;
				MtgCardInstance mtgCardInstance2 = GreInterface.CreateNewIncrementalEntity(cardById, mtgCardInstance, cardById2) as MtgCardInstance;
				mtgCardInstance2.Controller = cardById2.Controller;
				yield return new CardChangedEvent(mtgCardInstance ?? cardById, mtgCardInstance2, ai.AffectorId, PropertyType.Controller);
			}
		}
	}
}
