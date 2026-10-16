using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class RevealedCardDeletedEventParser : IAnnotationEventParser
{
	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		yield return new CardRevealedEvent
		{
			DeleteId = ai.AffectorId,
			EventType = RevealEventType.Delete
		};
	}
}
