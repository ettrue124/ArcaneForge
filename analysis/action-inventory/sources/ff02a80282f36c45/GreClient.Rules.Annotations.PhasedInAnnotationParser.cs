using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class PhasedInAnnotationParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New, INeedList_Changes
{
	public MtgGameState oldState { private get; set; }

	public MtgGameState newState { private get; set; }

	public IReadOnlyList<GameRulesEvent> changes { get; set; }

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
		foreach (uint affectedId in ai.AffectedIds)
		{
			MtgEntity entityById = oldState.GetEntityById(affectedId);
			if (entityById != null)
			{
				MtgEntity entityById2 = newState.GetEntityById(affectedId);
				MtgEntity previousIncrementalEntity = GreInterface.GetPreviousIncrementalEntity(affectedId, changes);
				MtgEntity mtgEntity = GreInterface.CreateNewIncrementalEntity(entityById, previousIncrementalEntity, entityById2);
				MtgCardInstance mtgCardInstance = (MtgCardInstance)entityById;
				MtgCardInstance mtgCardInstance2 = (MtgCardInstance)entityById2;
				MtgCardInstance mtgCardInstance3 = (MtgCardInstance)mtgEntity;
				MtgCardInstance copy = ((MtgCardInstance)mtgEntity).GetCopy();
				copy.Zone = mtgCardInstance2.Zone;
				yield return new CardChangedEvent(mtgCardInstance3 ?? mtgCardInstance, copy, ai.AffectorId, PropertyType.PhaseState);
			}
		}
	}
}
