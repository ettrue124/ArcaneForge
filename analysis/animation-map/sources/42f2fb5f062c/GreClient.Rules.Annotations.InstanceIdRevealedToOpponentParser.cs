using System;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class InstanceIdRevealedToOpponentParser : IAnnotationStateParser
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
		if (!ai.Type.Contains(AnnotationType.InstanceRevealedToOpponent))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetCard(affectedId, out var card) && OwnerIsLocalPlayer(card))
			{
				card.RevealedToOpponent = true;
			}
		}
	}

	private bool OwnerIsLocalPlayer(MtgCardInstance card)
	{
		if (card == null || card.Owner == null)
		{
			return false;
		}
		return card.Owner.IsLocalPlayer;
	}
}
