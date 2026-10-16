using System;
using System.Collections.Generic;
using GreClient.CardData;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class LayeredEffectStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_EFFECT_ID = "effect_id";

	public const string DETAIL_ABILITY_ID = "abilityGrpId";

	public const string DETAIL_IS_TOP = "isTop";

	public const string DETAIL_DURATION = "Duration";

	public const string DETAIL_EFFECT_TYPE = "LayeredEffectType";

	public const string DETAIL_SOURCE_ABILITY_ID = "sourceAbilityGRPID";

	public const string EFFECT_TYPE_COPY_OBJ = "CopyObject";

	public const string PREVIOUS_FACEDOWN_REASON = "PreviousFacedownReason";

	public const string PREVIOUS_FACEDOWN_ABILITY_GRPID = "PreviousFacedownAbilityGrpId";

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		if (newState == null)
		{
			throw new ArgumentNullException("newState");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected id.", "ai");
		}
		if (IsMutate(ai, newState, out var mutateParent, out var mutateChild))
		{
			ProcessMutate(ai, mutateParent, mutateChild);
		}
		else
		{
			ProcessNormal(ai, newState);
		}
	}

	private void ProcessNormal(AnnotationInfo ai, MtgGameState newState)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		AnnotationDataUtils.ExtractDetails(ai, dictionary);
		foreach (uint affectedId in ai.AffectedIds)
		{
			LayeredEffectData item = new LayeredEffectData(ai.Type, ai.AffectorId, affectedId, ai.GetStringDetail("LayeredEffectType"), (uint)ai.GetInt32Detail("Duration"), (uint)ai.GetInt32Detail("effect_id"), dictionary, (uint)ai.GetInt32Detail("sourceAbilityGRPID"));
			MtgPlayer player;
			if (TryGetCard(affectedId, newState, out var card))
			{
				card.LayeredEffects.Add(item);
			}
			else if (TryGetPlayer(affectedId, newState, out player))
			{
				player.LayeredEffects.Add(item);
			}
		}
	}

	private static bool TryGetCard(uint instanceId, MtgGameState state, out MtgCardInstance card)
	{
		if (state.VisibleCards.TryGetValue(instanceId, out card))
		{
			return true;
		}
		if (state.RevealedCards.TryGetValue(instanceId, out card))
		{
			return true;
		}
		card = null;
		return false;
	}

	private static bool TryGetPlayer(uint instanceId, MtgGameState state, out MtgPlayer player)
	{
		if (state.TryGetPlayer(instanceId, out player))
		{
			return true;
		}
		player = null;
		return false;
	}

	private void ProcessMutate(AnnotationInfo ai, MtgCardInstance mutateParent, MtgCardInstance mutateChild)
	{
		mutateParent.MutationChildren.Add(mutateChild);
		mutateParent.MutationChildrenIds.Add(mutateChild.InstanceId);
		mutateChild.MutationParent = mutateParent;
		mutateChild.MutationParentId = mutateParent.InstanceId;
		ReasonFaceDown int32Detail = (ReasonFaceDown)ai.GetInt32Detail("PreviousFacedownReason");
		uint int32Detail2 = (uint)ai.GetInt32Detail("PreviousFacedownAbilityGrpId");
		bool flag = ai.GetInt32Detail("isTop") == 1;
		if (flag && mutateParent.BaseGrpId == 3 && (int32Detail > ReasonFaceDown.None || int32Detail2 != 0))
		{
			mutateParent.FaceDownState.SetMutatedOverFaceDownReasonFaceDown(int32Detail);
			mutateParent.FaceDownState.SetMutatedOverFaceDownSourceAbilityGrpId(int32Detail2);
		}
		uint int32Detail3 = (uint)ai.GetInt32Detail("effect_id");
		foreach (AbilityPrintingData ability in mutateChild.Abilities)
		{
			mutateParent.AbilityAdders.Add(new AddedAbilityData(ability.Id, 0u, addedById: mutateChild.InstanceId, addedByGrpId: mutateChild.GrpId, sourceId: mutateChild.InstanceId, soureGrpId: mutateChild.GrpId, layeredEffectId: int32Detail3));
			mutateParent.AbilityModifications.Add(AbilityModification.Added(ability.Id));
		}
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		AnnotationDataUtils.ExtractDetails(ai, dictionary);
		mutateParent.LayeredEffects.Add(new LayeredEffectData(ai.Type, mutateChild.InstanceId, mutateParent.InstanceId, flag ? "MutateOver" : "MutateUnder", 0u, int32Detail3, dictionary));
	}

	private static bool IsMutate(AnnotationInfo ai, MtgGameState newState, out MtgCardInstance mutateParent, out MtgCardInstance mutateChild)
	{
		mutateChild = (mutateParent = null);
		if ((long)ai.GetInt32Detail("abilityGrpId") == 203 && ai.AffectedIds.Count == 1 && newState.VisibleCards.TryGetValue(ai.AffectedIds[0], out mutateParent) && (mutateParent.ObjectType == GameObjectType.Card || mutateParent.ObjectType == GameObjectType.Token) && newState.VisibleCards.TryGetValue(ai.AffectorId, out mutateChild) && (mutateChild.ObjectType == GameObjectType.Card || mutateChild.ObjectType == GameObjectType.Token))
		{
			return mutateChild.CardTypes.Contains(CardType.Creature);
		}
		return false;
	}
}
