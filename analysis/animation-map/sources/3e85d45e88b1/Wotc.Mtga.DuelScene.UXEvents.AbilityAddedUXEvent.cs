using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.Cards.Database;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AbilityAddedUXEvent : AbilityAddedOrRemovedUXEvent
{
	private readonly AddedAbilityData _addedData;

	private readonly IAbilityDataProvider _abilityDataProvider;

	private readonly IGameStateProvider _gameStateProvider;

	public AbilityAddedUXEvent(uint instanceId, AddedAbilityData addedData, uint affectorId, IEntityViewProvider viewProvider, IAbilityDataProvider abilityDataProvider, IGameStateProvider gameStateProvider, IPlayerAbilityController playerAbilityController)
		: base(instanceId, addedData.AbilityId, affectorId, viewProvider, playerAbilityController)
	{
		_addedData = addedData;
		_abilityDataProvider = abilityDataProvider ?? NullAbilityDataProvider.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
	}

	public AbilityAddedUXEvent(uint instanceId, uint abilityId, uint affectorId)
		: base(instanceId, abilityId, affectorId)
	{
	}

	public override string ToString()
	{
		return $"Ability #{AbilityId} added to entity #{InstanceId} by entity #{AffectorId}.";
	}

	public override void Execute()
	{
		DuelScene_AvatarView avatar;
		DuelScene_CDC cardView;
		if (_abilityDataProvider.GetAbilityPrintingById(AbilityId) == null)
		{
			Debug.LogErrorFormat("AbilityAddedUXEvent of unknown ability #{0} for entity #{1}", AbilityId, InstanceId);
		}
		else if (_viewProvider.TryGetAvatarById(InstanceId, out avatar))
		{
			avatar.HandleAbilityAdded(AbilityId, AffectorId, _gameStateProvider.CurrentGameState);
		}
		else if (_viewProvider.TryGetCardView(InstanceId, out cardView))
		{
			cardView.HandleAddedAbility(AbilityId);
		}
		_playerAbilityController.AddAbility(InstanceId, _addedData);
		Complete();
	}
}
