using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class AbilityCreatedEventParser : IAnnotationEventParser, INeedMtgGameState_New
{
	public MtgGameState newState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetCard(affectedId, out var card))
			{
				yield return new CardCreatedEvent(card);
			}
		}
	}
}
