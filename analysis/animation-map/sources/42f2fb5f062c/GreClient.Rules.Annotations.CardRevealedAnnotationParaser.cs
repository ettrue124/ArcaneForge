using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class CardRevealedAnnotationParaser : IAnnotationEventParser, INeedMtgGameState_New
{
	public const string DETAIL_KEY_SOURCE_ZONE = "source_zone";

	public MtgGameState newState { private get; set; }

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (ai == null)
		{
			throw new ArgumentNullException("ai");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (ai.ExcludedSeatIds.Contains(newState.LocalPlayer.InstanceId))
		{
			yield break;
		}
		uint affectorId = ai.AffectorId;
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.RevealedCards.TryGetValue(affectedId, out var value))
			{
				yield return new CardRevealedEvent
				{
					AffectorId = affectorId,
					RevealedInstance = value,
					EventType = RevealEventType.Reveal,
					OwnerId = DisplayFor(ExtractSourceZone(ai, newState), value)
				};
			}
		}
	}

	public static MtgZone ExtractSourceZone(AnnotationInfo ai, MtgGameState gameState)
	{
		return gameState.GetZoneById((uint)ai.GetInt32Detail("source_zone"));
	}

	public static uint DisplayFor(MtgZone zone, MtgCardInstance revealInstance)
	{
		if (zone != null && zone.Owner != null)
		{
			return zone.OwnerId;
		}
		return (revealInstance?.Controller?.InstanceId).GetValueOrDefault();
	}
}
