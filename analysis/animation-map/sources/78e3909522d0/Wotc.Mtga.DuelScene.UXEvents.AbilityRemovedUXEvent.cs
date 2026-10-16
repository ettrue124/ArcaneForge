using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class AbilityRemovedUXEvent : AbilityAddedOrRemovedUXEvent
{
	private readonly AddedAbilityData _removedAbilityData;

	public AbilityRemovedUXEvent(uint instanceId, AddedAbilityData removedAbility, IEntityViewProvider viewProvider, IPlayerAbilityController playerAbilityController)
		: base(instanceId, removedAbility.AbilityId, removedAbility.AddedById, viewProvider, playerAbilityController)
	{
		_removedAbilityData = removedAbility;
	}

	public override string ToString()
	{
		return $"Ability #{AbilityId} removed from entity #{InstanceId} by entity #{AffectorId}.";
	}

	public override void Execute()
	{
		if (_viewProvider.TryGetAvatarById(InstanceId, out var avatar))
		{
			avatar.HandleAbilityRemoved(_removedAbilityData.AbilityId);
		}
		_playerAbilityController.RemoveAbility(InstanceId, _removedAbilityData);
		Complete();
	}
}
