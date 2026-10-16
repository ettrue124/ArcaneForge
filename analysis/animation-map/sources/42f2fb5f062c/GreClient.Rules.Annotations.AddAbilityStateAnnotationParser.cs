using System;
using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class AddAbilityStateAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_GRPID = "grpid";

	public const string DETAIL_EFFECT_ID = "effect_id";

	public const string DETAIL_ABILITY_ZCID = "originalAbilityObjectZcid";

	public const string DETAIL_COUNT = "count";

	private const string DETAIL_SOURCE_ABILITY_ID = "sourceAbilityGRPID";

	private const string DETAIL_UNIQUE_ABILITY_ID = "UniqueAbilityId";

	private const string DETAIL_KEY_PERPETUAL_AFFECTEDID = "perpetualAffectedId";

	private readonly IAbilityDataProvider _abilityProvider;

	public AddAbilityStateAnnotationParser(IAbilityDataProvider abilityProvider)
	{
		_abilityProvider = abilityProvider;
	}

	public void Process(AnnotationInfo ai, MtgGameState oldState, MtgGameState newState)
	{
		uint id = ai.Id;
		uint affectorId = ai.AffectorId;
		uint layeredEffectId = (ai.TryGetInt32Detail("effect_id", out var value) ? ((uint)value) : 0u);
		IReadOnlyList<uint> abilityGrpIds = GetAbilityGrpIds(ai);
		IReadOnlyList<uint> abilitySourceId = GetAbilitySourceId(ai);
		uint sourceAbilityId = (ai.TryGetInt32Detail("sourceAbilityGRPID", out var value2) ? ((uint)value2) : 0u);
		uint num = ((!ai.TryGetInt32Detail("count", out var value3)) ? 1u : ((uint)value3));
		uint perpetualAffectedId = (ai.TryGetInt32Detail("perpetualAffectedId", out var value4) ? ((uint)value4) : 0u);
		for (int i = 0; i < abilityGrpIds.Count; i++)
		{
			uint num2 = abilityGrpIds[i];
			uint num3 = ((abilitySourceId.Count > i) ? abilitySourceId[i] : 0u);
			uint addedByGrpId = ((TryGetCard(newState, affectorId, out var card) && card.GrpId > 11) ? card.GrpId : 0u);
			uint soureGrpId = ((TryGetCard(newState, num3, out var card2) && card2.GrpId > 11) ? card2.GrpId : 0u);
			foreach (uint affectedId in ai.AffectedIds)
			{
				if (newState.TryGetPlayer(affectedId, out var player))
				{
					AbilityPrintingData abilityPrintingData = _abilityProvider.GetAbilityPrintingById(num2);
					if (abilityPrintingData == null)
					{
						abilityPrintingData = AbilityPrintingData.InvalidAbility(num2);
					}
					player.Abilities.Add(abilityPrintingData);
				}
				if (TryGetEntityById(newState, affectedId, out var entity))
				{
					for (int j = 0; j < num; j++)
					{
						entity.AbilityModifications.Add(AbilityModification.Added(num2));
						entity.AbilityAdders.Add(new AddedAbilityData(num2, sourceAbilityId, num3, soureGrpId, affectorId, addedByGrpId, layeredEffectId));
					}
				}
			}
		}
		foreach (int @int in ai.GetIntList("UniqueAbilityId"))
		{
			if (newState.AbilityInstances.TryGetValue((uint)@int, out var value5))
			{
				value5.AbilityAddedDetails = new AbilityAddedDetails(id, affectorId, layeredEffectId, sourceAbilityId, perpetualAffectedId);
			}
		}
	}

	private static bool TryGetEntityById(MtgGameState gameState, uint instanceId, out MtgEntity entity)
	{
		if (gameState.TryGetPlayer(instanceId, out var player))
		{
			entity = player;
			return true;
		}
		if (TryGetCard(gameState, instanceId, out var card))
		{
			entity = card;
			return true;
		}
		entity = null;
		return false;
	}

	private static bool TryGetCard(MtgGameState gameState, uint instanceId, out MtgCardInstance card)
	{
		if (gameState.VisibleCards.TryGetValue(instanceId, out card))
		{
			return true;
		}
		if (gameState.RevealedCards.TryGetValue(instanceId, out card))
		{
			return true;
		}
		card = null;
		return false;
	}

	private static IReadOnlyList<uint> GetAbilityGrpIds(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("grpid") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32.Select((int x) => (uint)x).ToArray();
			}
		}
		return Array.Empty<uint>();
	}

	private static IReadOnlyList<uint> GetAbilitySourceId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("originalAbilityObjectZcid") && detail.ValueInt32.Count > 0)
			{
				return detail.ValueInt32.Select((int x) => (uint)x).ToArray();
			}
		}
		return Array.Empty<uint>();
	}
}
