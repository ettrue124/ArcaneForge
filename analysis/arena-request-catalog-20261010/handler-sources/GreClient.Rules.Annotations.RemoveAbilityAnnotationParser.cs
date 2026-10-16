using System;
using System.Linq;
using GreClient.CardData;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class RemoveAbilityAnnotationParser : IAnnotationStateParser
{
	public const string DETAIL_EFFECT_ID = "effect_id";

	private readonly ICardDatabaseAdapter _cardDatabase;

	public RemoveAbilityAnnotationParser(ICardDatabaseAdapter cardDatabase)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
	}

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
		if (!ai.Type.Contains(AnnotationType.RemoveAbility))
		{
			throw new ArgumentException("Annotation type does not match parser type.", "ai");
		}
		if (ai.AffectorId == 0)
		{
			throw new ArgumentException("Annotation does not contain affector id.", "ai");
		}
		if (ai.AffectedIds.Count == 0)
		{
			throw new ArgumentException("Annotation does not contain affected ids.", "ai");
		}
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			uint result = 0u;
			if (uint.TryParse(detail.Key, out result) && TryGetEntity(newState, result, out var mtgEntity))
			{
				if (detail.Type == KeyValuePairValueType.String)
				{
					processStringKvpi(mtgEntity, detail, ai);
				}
				else if (detail.Type == KeyValuePairValueType.Int32)
				{
					processInt32Kvpi(mtgEntity, detail, ai);
				}
			}
		}
	}

	private static bool TryGetEntity(MtgGameState state, uint instanceId, out MtgEntity mtgEntity)
	{
		if (state.TryGetPlayer(instanceId, out var player))
		{
			mtgEntity = player;
			return true;
		}
		if (state.VisibleCards.TryGetValue(instanceId, out var value))
		{
			mtgEntity = value;
			return true;
		}
		if (state.RevealedCards.TryGetValue(instanceId, out var value2))
		{
			mtgEntity = value2;
			return true;
		}
		mtgEntity = null;
		return false;
	}

	private void processStringKvpi(MtgEntity entity, KeyValuePairInfo kvpi, AnnotationInfo ai)
	{
		if (!(entity is MtgCardInstance mtgCardInstance) || !(kvpi.ValueString[0] == "all") || (!_cardDatabase.CardDataProvider.TryGetCardPrintingById(mtgCardInstance.GrpId, out var card) && !mtgCardInstance.FaceDownState.IsFaceDown))
		{
			return;
		}
		if (ai.Type.Contains(AnnotationType.AddAbility) && ai.Type.IndexOf(AnnotationType.AddAbility) < ai.Type.IndexOf(AnnotationType.RemoveAbility))
		{
			AbilityModification item = entity.AbilityModifications.LastOrDefault();
			if (item.Type == AbilityModification.ModType.Added)
			{
				int index = entity.AbilityModifications.IndexOf(item);
				entity.AbilityModifications.Insert(index, AbilityModification.AllRemoved());
			}
		}
		else
		{
			entity.AbilityModifications.Add(AbilityModification.AllRemoved());
		}
		foreach (AddedAbilityData addedAbility in entity.AbilityAdders)
		{
			if (!entity.Abilities.Exists((AbilityPrintingData x) => x.Id == addedAbility.AbilityId))
			{
				entity.AbilityRemovers.Add(new RemovedAbilityData
				{
					AbilityId = addedAbility.AbilityId,
					RemovedById = ai.AffectorId,
					LayeredEffectId = GetEffectId(ai)
				});
			}
		}
		if (card == null)
		{
			return;
		}
		foreach (AbilityPrintingData ability in card.Abilities)
		{
			if (!TryRemoveClassAbilityGranting(ability, entity, ai))
			{
				entity.AbilityRemovers.Add(new RemovedAbilityData
				{
					AbilityId = ability.Id,
					RemovedById = ai.AffectorId,
					LayeredEffectId = GetEffectId(ai)
				});
			}
		}
	}

	private void processInt32Kvpi(MtgEntity entity, KeyValuePairInfo kvpi, AnnotationInfo ai)
	{
		foreach (int item in kvpi.ValueInt32)
		{
			if (!IgnoreRoomChildAbilityRemoval(entity, (uint)item) || !ai.Type.Contains(AnnotationType.TextChange))
			{
				entity.AbilityModifications.Add(AbilityModification.Removed((uint)item));
				AbilityPrintingData abilityPrintingById = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById((uint)item);
				if (abilityPrintingById == null || !TryRemoveClassAbilityGranting(abilityPrintingById, entity, ai))
				{
					entity.AbilityModifications.Add(AbilityModification.Removed((uint)item));
					entity.AbilityRemovers.Add(new RemovedAbilityData
					{
						AbilityId = (uint)item,
						RemovedById = ai.AffectorId,
						LayeredEffectId = GetEffectId(ai)
					});
				}
			}
		}
	}

	private bool IgnoreRoomChildAbilityRemoval(MtgEntity entity, uint abilityId)
	{
		if (entity is MtgCardInstance mtgCardInstance && _cardDatabase.CardDataProvider.TryGetCardPrintingById(mtgCardInstance.GrpId, out var card) && card.LinkedFaceType == LinkedFace.RoomHalf)
		{
			return card.LinkedFacePrintings.Exists(abilityId, (CardPrintingData childPrinting, uint x) => childPrinting.Abilities.Exists(x, (AbilityPrintingData childAbility, uint y) => childAbility.Id == y));
		}
		return false;
	}

	private bool TryRemoveClassAbilityGranting(AbilityPrintingData ability, MtgEntity entity, AnnotationInfo ai)
	{
		if (ability != null && ability.SubCategory == AbilitySubCategory.ClassAbilityGranting)
		{
			foreach (AbilityPrintingData hiddenAbility in ability.HiddenAbilities)
			{
				entity.AbilityModifications.Add(AbilityModification.Removed(hiddenAbility.Id));
				entity.AbilityRemovers.Add(new RemovedAbilityData
				{
					AbilityId = hiddenAbility.Id,
					RemovedById = ai.AffectorId,
					LayeredEffectId = GetEffectId(ai)
				});
			}
			return true;
		}
		return false;
	}

	private static uint GetEffectId(AnnotationInfo ai)
	{
		foreach (KeyValuePairInfo detail in ai.Details)
		{
			if (detail.Key.Equals("effect_id") && detail.ValueInt32.Count > 0)
			{
				return (uint)detail.ValueInt32[0];
			}
		}
		return 0u;
	}
}
