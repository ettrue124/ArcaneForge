using System;
using System.Collections.Generic;
using System.Linq;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class TappedUntappedPermanentAnnotationParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New, INeedList_Changes
{
	public const string TAPPED_DETAIL = "tapped";

	public MtgGameState oldState { private get; set; }

	public MtgGameState newState { private get; set; }

	public IReadOnlyList<GameRulesEvent> changes { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (!ai.Type.Contains(AnnotationType.TappedUntappedPermanent))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectedIds == null || ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected ID.", "ai");
		}
		if (ai.Details == null || ai.Details.Count == 0)
		{
			throw new ArgumentException("Annotation contains no details");
		}
		if (oldState == null)
		{
			throw new ArgumentNullException("oldState");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		KeyValuePairInfo keyValuePairInfo = ai.Details.FirstOrDefault((KeyValuePairInfo x) => x.Key == "tapped");
		bool tapped = keyValuePairInfo != null && keyValuePairInfo.ValueInt32.FirstOrDefault() == 1;
		foreach (uint affectedId in ai.AffectedIds)
		{
			MtgCardInstance cardById = oldState.GetCardById(affectedId);
			if (cardById != null)
			{
				MtgCardInstance cardById2 = newState.GetCardById(affectedId);
				MtgCardInstance mtgCardInstance = GreInterface.GetPreviousIncrementalEntity(affectedId, changes) as MtgCardInstance;
				MtgCardInstance mtgCardInstance2 = GreInterface.CreateNewIncrementalEntity(cardById, mtgCardInstance, cardById2) as MtgCardInstance;
				mtgCardInstance2.IsTapped = tapped;
				yield return new CardChangedEvent(mtgCardInstance ?? cardById, mtgCardInstance2, ai.AffectorId, PropertyType.IsTapped);
			}
		}
	}
}
