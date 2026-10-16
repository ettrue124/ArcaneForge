using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class TokenImmediatelyDiedAnnotationParser : IAnnotationEventParser, INeedMtgGameState_New
{
	public MtgGameState newState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		uint num = ai.AffectedIds[0];
		if (newState.TryGetCard(num, out var card))
		{
			card.Zone = newState.Battlefield;
			yield return new TokenImmediatelyDiedEvent(num);
		}
	}
}
