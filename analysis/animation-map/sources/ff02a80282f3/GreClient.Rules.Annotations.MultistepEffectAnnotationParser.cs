using System;
using System.Collections.Generic;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public abstract class MultistepEffectAnnotationParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New
{
	public const string SUBCATEGORY_KEY = "SubCategory";

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
			yield return GetEffect(GetAffectedPlayer(ai, oldState, newState), GetAffectorCard(ai, oldState, newState), GetAbilitySubCategory(ai));
		}
	}

	protected abstract bool AnnotationTypeIsValid(AnnotationInfo ai);

	protected abstract MultistepEffectEvent GetEffect(MtgPlayer affected, MtgCardInstance affector, AbilitySubCategory category);

	private static AbilitySubCategory GetAbilitySubCategory(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("SubCategory") && detail.ValueInt32.Count > 0)
			{
				return (AbilitySubCategory)detail.ValueInt32[0];
			}
		}
		throw new ArgumentException("Annotation contained no detail for the Ability SubCategory", "ai");
	}

	private static MtgPlayer GetAffectedPlayer(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		foreach (uint affectedId in ai.AffectedIds)
		{
			if (newState.TryGetPlayer(affectedId, out var player) || oldState.TryGetPlayer(affectedId, out player))
			{
				return player;
			}
		}
		return null;
	}

	private static MtgCardInstance GetAffectorCard(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		return newState.GetCardById(ai.AffectorId) ?? oldState.GetCardById(ai.AffectorId);
	}

	private static bool AnnotationAffectorIsValid(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		return GetAffectorCard(ai, oldState, newState) != null;
	}
}
