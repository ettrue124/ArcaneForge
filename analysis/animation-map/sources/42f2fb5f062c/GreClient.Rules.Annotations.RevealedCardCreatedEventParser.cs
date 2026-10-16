using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class RevealedCardCreatedEventParser : IAnnotationEventParser, INeedMtgGameState_New
{
	public MtgGameState newState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (newState == null)
		{
			throw new ArgumentNullException("Null NewState parameter");
		}
		if (newState.TryGetCard(ai.AffectorId, out var card) && !card.Owner.IsLocalPlayer)
		{
			MtgZone zone = card.Zone;
			if (!zone.Owner.IsLocalPlayer && card.Zone.Type == ZoneType.Hand)
			{
				yield return new CardRevealedEvent
				{
					CreateInstance = card,
					EventType = RevealEventType.Create,
					OwnerId = zone.OwnerId
				};
			}
		}
	}
}
