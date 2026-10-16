using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public abstract class PlayerSelectingOrSubmittedTargetsAnnotationParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New
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
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		if (!AnnotationTypeIsValid(ai))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (AnnotationAffectorIsValid(ai, oldState, newState))
		{
			yield return GetEvent(GetAffectedCard(ai, oldState, newState), GetAffectorPlayer(ai, oldState, newState));
		}
	}

	protected abstract bool AnnotationTypeIsValid(AnnotationInfo ai);

	protected abstract GameRulesEvent GetEvent(MtgCardInstance affected, MtgPlayer affector);

	private static MtgPlayer GetAffectorPlayer(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (newState.TryGetPlayer(ai.AffectorId, out var player) || oldState.TryGetPlayer(ai.AffectorId, out player))
		{
			return player;
		}
		return null;
	}

	private static MtgCardInstance GetAffectedCard(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.GetCardById(affectedId) != null)
			{
				return newState.GetCardById(affectedId);
			}
			if (oldState.GetCardById(ai.AffectorId) != null)
			{
				return oldState.GetCardById(ai.AffectorId);
			}
		}
		return null;
	}

	private static bool AnnotationAffectorIsValid(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		return GetAffectorPlayer(ai, oldState, newState) != null;
	}
}
