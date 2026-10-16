using System;
using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtgo.Gre.External.Messaging;

namespace GreClient.Rules.Annotations;

public class LayeredEffectCreatedEventParser : IAnnotationEventParser, INeedMtgGameState_Old, INeedMtgGameState_New, INeedList_Changes
{
	private readonly IAbilityDataProvider _abilityProvider;

	public MtgGameState oldState { private get; set; }

	public MtgGameState newState { private get; set; }

	public IReadOnlyList<GameRulesEvent> changes { private get; set; }

	public LayeredEffectCreatedEventParser(IAbilityDataProvider abilityDataProvider)
	{
		_abilityProvider = abilityDataProvider ?? NullAbilityDataProvider.Default;
	}

	public IEnumerable<GameRulesEvent> Process(AnnotationInfo ai)
	{
		if (oldState == null)
		{
			throw new ArgumentNullException("old gamestate state not set");
		}
		if (newState == null)
		{
			throw new ArgumentNullException("new gamestate state not set");
		}
		if (changes == null)
		{
			throw new ArgumentNullException("changelist not set");
		}
		return ai.Details.SelectMany((KeyValuePairInfo detail) => GenerateEventsFromDetail(detail));
	}

	private IEnumerable<GameRulesEvent> GenerateEventsFromDetail(KeyValuePairInfo detail)
	{
		uint affector = (uint)detail.ValueInt32[0];
		IEnumerable<uint> valueUint = detail.ValueUint32;
		return detail.Key switch
		{
			"Effect_ModifiedType" => GenerateTypeModificationEvents(affector, valueUint), 
			"Effect_ModifiedColor" => GenerateColorModifiedEvents(affector, valueUint), 
			"Effect_ModifiedPower" => GenerateModifiedPowerEvents(affector, valueUint), 
			"Effect_ModifiedToughness" => GenerateModifiedToughnessEvents(affector, valueUint), 
			"Effect_ModifiedPowerAndToughness" => GeneratePowerToughnessModifiedEvents(affector, valueUint), 
			"Effect_ControllerChanged" => GenerateControllerChangedEvents(affector, valueUint), 
			"Effect_AddedAbility" => GenerateAddedAbilityEvents(affector, valueUint, (uint)detail.ValueInt32[1], detail.ValueInt64), 
			_ => Array.Empty<GameRulesEvent>(), 
		};
	}

	private IEnumerable<GameRulesEvent> GenerateTypeModificationEvents(uint affector, IEnumerable<uint> affected)
	{
		foreach (uint item in affected)
		{
			if (newState.TryGetCard(item, out var card))
			{
				MtgCardInstance mtgCardInstance = GreInterface.GetPreviousIncrementalEntity(item, changes) as MtgCardInstance;
				MtgCardInstance oldInstance = mtgCardInstance ?? oldState.GetCardById(item);
				if (oldInstance != null)
				{
					MtgCardInstance modified = GreInterface.CreateNewIncrementalEntity(oldInstance, mtgCardInstance, card) as MtgCardInstance;
					if (!oldInstance.Supertypes.SequenceEqual(card.Supertypes))
					{
						modified.Supertypes.Clear();
						modified.Supertypes.AddRange(card.Supertypes);
						yield return new CardChangedEvent(oldInstance, modified, affector, PropertyType.SuperType);
					}
					if (!oldInstance.Subtypes.SequenceEqual(card.Subtypes))
					{
						modified.Subtypes.Clear();
						modified.Subtypes.AddRange(card.Subtypes);
						yield return new CardChangedEvent(oldInstance, modified, affector, PropertyType.SubType);
					}
					if (!oldInstance.CardTypes.SequenceEqual(card.CardTypes))
					{
						modified.CardTypes.Clear();
						modified.CardTypes.AddRange(card.CardTypes);
						yield return new CardChangedEvent(oldInstance, modified, affector, PropertyType.CardType);
					}
				}
			}
			card = null;
		}
	}

	private IEnumerable<GameRulesEvent> GenerateColorModifiedEvents(uint affector, IEnumerable<uint> affected)
	{
		foreach (uint item in affected)
		{
			if (!newState.TryGetCard(item, out var card))
			{
				continue;
			}
			MtgCardInstance mtgCardInstance = GreInterface.GetPreviousIncrementalEntity(item, changes) as MtgCardInstance;
			MtgCardInstance mtgCardInstance2 = mtgCardInstance ?? oldState.GetCardById(item);
			if (mtgCardInstance2 != null)
			{
				MtgCardInstance mtgCardInstance3 = GreInterface.CreateNewIncrementalEntity(mtgCardInstance2, mtgCardInstance, card) as MtgCardInstance;
				if (!mtgCardInstance2.Colors.SequenceEqual(card.Colors))
				{
					mtgCardInstance3.Colors.Clear();
					mtgCardInstance3.Colors.AddRange(card.Colors);
					yield return new CardChangedEvent(mtgCardInstance2, mtgCardInstance3, affector, PropertyType.Colors);
				}
			}
		}
	}

	private IEnumerable<GameRulesEvent> GenerateModifiedPowerEvents(uint affector, IEnumerable<uint> affected)
	{
		foreach (uint item in affected)
		{
			if (!newState.TryGetCard(item, out var card))
			{
				continue;
			}
			MtgCardInstance mtgCardInstance = GreInterface.GetPreviousIncrementalEntity(item, changes) as MtgCardInstance;
			MtgCardInstance mtgCardInstance2 = mtgCardInstance ?? oldState.GetCardById(item);
			if (mtgCardInstance2 != null)
			{
				MtgCardInstance mtgCardInstance3 = GreInterface.CreateNewIncrementalEntity(mtgCardInstance2, mtgCardInstance, card) as MtgCardInstance;
				if (!card.Power.Equals(mtgCardInstance3.Power))
				{
					PropertyType property = ((card.Power.Value > mtgCardInstance3.Power.Value) ? PropertyType.PowerUp : PropertyType.PowerDown);
					mtgCardInstance3.Power = new StringBackedInt(card.Power.Value);
					yield return new CardChangedEvent(mtgCardInstance2, mtgCardInstance3, affector, property);
				}
			}
		}
	}

	private IEnumerable<GameRulesEvent> GenerateModifiedToughnessEvents(uint affector, IEnumerable<uint> affected)
	{
		foreach (uint item in affected)
		{
			if (!newState.TryGetCard(item, out var card))
			{
				continue;
			}
			MtgCardInstance mtgCardInstance = GreInterface.GetPreviousIncrementalEntity(item, changes) as MtgCardInstance;
			MtgCardInstance mtgCardInstance2 = mtgCardInstance ?? oldState.GetCardById(item);
			if (mtgCardInstance2 != null)
			{
				MtgCardInstance mtgCardInstance3 = GreInterface.CreateNewIncrementalEntity(mtgCardInstance2, mtgCardInstance, card) as MtgCardInstance;
				if (!card.Toughness.Equals(mtgCardInstance3.Toughness))
				{
					PropertyType property = ((card.Toughness.Value > mtgCardInstance3.Toughness.Value) ? PropertyType.ToughnessUp : PropertyType.ToughnessDown);
					mtgCardInstance3.Toughness = new StringBackedInt(card.Toughness.Value);
					yield return new CardChangedEvent(mtgCardInstance2, mtgCardInstance3, affector, property);
				}
			}
		}
	}

	private IEnumerable<GameRulesEvent> GeneratePowerToughnessModifiedEvents(uint affector, IEnumerable<uint> affected)
	{
		foreach (uint item in affected)
		{
			if (newState.TryGetCard(item, out var card))
			{
				MtgCardInstance mtgCardInstance = GreInterface.GetPreviousIncrementalEntity(item, changes) as MtgCardInstance;
				MtgCardInstance oldInstance = mtgCardInstance ?? oldState.GetCardById(item);
				if (oldInstance != null)
				{
					MtgCardInstance modified = GreInterface.CreateNewIncrementalEntity(oldInstance, mtgCardInstance, card) as MtgCardInstance;
					if (!card.Power.Equals(modified.Power))
					{
						PropertyType property = ((card.Power.Value > modified.Power.Value) ? PropertyType.PowerUp : PropertyType.PowerDown);
						modified.Power = new StringBackedInt(card.Power.Value);
						yield return new CardChangedEvent(oldInstance, modified, affector, property);
					}
					if (!card.Toughness.Equals(modified.Toughness))
					{
						PropertyType property2 = ((card.Toughness.Value > modified.Toughness.Value) ? PropertyType.ToughnessUp : PropertyType.ToughnessDown);
						modified.Toughness = new StringBackedInt(card.Toughness.Value);
						yield return new CardChangedEvent(oldInstance, modified, affector, property2);
					}
				}
			}
			card = null;
		}
	}

	private IEnumerable<GameRulesEvent> GenerateControllerChangedEvents(uint affector, IEnumerable<uint> affected)
	{
		foreach (uint item in affected)
		{
			if (!newState.TryGetCard(item, out var card))
			{
				continue;
			}
			MtgCardInstance mtgCardInstance = GreInterface.GetPreviousIncrementalEntity(item, changes) as MtgCardInstance;
			MtgCardInstance mtgCardInstance2 = mtgCardInstance ?? oldState.GetCardById(item);
			if (mtgCardInstance2 != null)
			{
				MtgCardInstance mtgCardInstance3 = GreInterface.CreateNewIncrementalEntity(mtgCardInstance2, mtgCardInstance, card) as MtgCardInstance;
				if (card.Controller?.InstanceId != mtgCardInstance3.Controller?.InstanceId)
				{
					mtgCardInstance3.Controller = card.Controller;
					yield return new CardChangedEvent(mtgCardInstance2, mtgCardInstance3, affector, PropertyType.Controller);
				}
			}
		}
	}

	private IEnumerable<GameRulesEvent> GenerateAddedAbilityEvents(uint affector, IEnumerable<uint> affected, uint layeredEffectId, IEnumerable<long> abilityIds)
	{
		foreach (uint affectedId in affected)
		{
			foreach (long abilityId in abilityIds)
			{
				_ = abilityId;
				if (newState.TryGetCard(affectedId, out var card))
				{
					MtgEntity old = oldState.GetEntityById(affectedId);
					foreach (AddedAbilityData addedAbility in card.AbilityAdders)
					{
						if (addedAbility.LayeredEffectId != layeredEffectId)
						{
							continue;
						}
						MtgCardInstance prv = GreInterface.GetPreviousIncrementalEntity(affectedId, changes) as MtgCardInstance;
						MtgCardInstance modified = GreInterface.CreateNewIncrementalEntity(old, prv, card) as MtgCardInstance;
						if (modified.AbilityAdders.Exists((AddedAbilityData x) => x.AbilityId == addedAbility.AbilityId && x.SourceId == addedAbility.SourceId))
						{
							break;
						}
						modified.AbilityModifications.Add(AbilityModification.Added(addedAbility.AbilityId));
						modified.AbilityAdders.Add(addedAbility);
						if (!modified.Abilities.Exists((AbilityPrintingData x) => x.Id == addedAbility.AbilityId))
						{
							AbilityPrintingData item = card.Abilities.Find((AbilityPrintingData x) => x.Id == addedAbility.AbilityId) ?? _abilityProvider.GetAbilityPrintingById(addedAbility.AbilityId);
							modified.Abilities.Add(item);
						}
						yield return new AbilityAddedEvent(prv, modified, affector, addedAbility);
						yield return new CardChangedEvent(prv, modified, affector, PropertyType.Abilities);
					}
				}
				card = null;
			}
		}
	}
}
