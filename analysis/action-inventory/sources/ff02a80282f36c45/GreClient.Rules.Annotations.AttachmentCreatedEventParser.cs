using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class AttachmentCreatedEventParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New, INeedList_Changes
{
	public MtgGameState oldState { private get; set; }

	public MtgGameState newState { private get; set; }

	public IReadOnlyList<GameRulesEvent> changes { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (oldState == null)
		{
			throw new ArgumentNullException("old gamestate state not set");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("new gamestate state not set");
		}
		if (changes == null)
		{
			throw new ArgumentNullException("changelist not set");
		}
		if (ai.AffectedIds.Count != 1)
		{
			yield break;
		}
		uint attachmentId = ai.AffectorId;
		uint attachedToId = ai.AffectedIds[0];
		if (attachmentId == attachedToId)
		{
			yield break;
		}
		if (newState.TryGetCard(attachmentId, out var card))
		{
			MtgCardInstance mtgCardInstance = GreInterface.GetPreviousIncrementalEntity(attachmentId, changes) as MtgCardInstance;
			MtgCardInstance mtgCardInstance2 = mtgCardInstance ?? oldState.GetCardById(attachmentId);
			if (mtgCardInstance2 != null)
			{
				MtgCardInstance mtgCardInstance3 = GreInterface.CreateNewIncrementalEntity(mtgCardInstance2, mtgCardInstance, card) as MtgCardInstance;
				if (mtgCardInstance3.AttachedToId != attachedToId)
				{
					mtgCardInstance3.AttachedToId = attachedToId;
					yield return new CardChangedEvent(mtgCardInstance2, mtgCardInstance3, attachedToId, PropertyType.AttachedTo);
				}
			}
		}
		if (!newState.TryGetCard(attachedToId, out var card2))
		{
			yield break;
		}
		MtgCardInstance mtgCardInstance4 = GreInterface.GetPreviousIncrementalEntity(attachedToId, changes) as MtgCardInstance;
		MtgCardInstance mtgCardInstance5 = mtgCardInstance4 ?? oldState.GetCardById(attachedToId);
		if (mtgCardInstance5 != null)
		{
			MtgCardInstance mtgCardInstance6 = GreInterface.CreateNewIncrementalEntity(mtgCardInstance5, mtgCardInstance4, card2) as MtgCardInstance;
			if (mtgCardInstance6.AttachedWithIds.Add(attachmentId))
			{
				yield return new CardChangedEvent(mtgCardInstance5, mtgCardInstance6, attachmentId, PropertyType.AttachedWith);
			}
		}
	}
}
