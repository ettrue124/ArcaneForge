using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class LoseDesignationAnnotationParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New
{
	public MtgGameState oldState { private get; set; }

	public MtgGameState newState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (oldState == null)
		{
			throw new ArgumentNullException("oldState");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (!ai.Type.Contains(AnnotationType.LoseDesignation))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		DesignationUtils.GetDesignationType(ai);
		uint affectorId = ai.AffectorId;
		uint num = ai.AffectedIds[0];
		if (newState.TryGetCard(num, out var card))
		{
			yield return new CardChangedEvent(oldState.GetCardById(num), card, affectorId, PropertyType.LoseDesignation);
		}
	}
}
