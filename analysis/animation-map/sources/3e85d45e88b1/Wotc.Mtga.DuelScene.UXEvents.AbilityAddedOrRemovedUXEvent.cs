using System.Collections.Generic;

namespace Wotc.Mtga.DuelScene.UXEvents;

public abstract class AbilityAddedOrRemovedUXEvent : UXEvent
{
	public readonly uint InstanceId;

	public readonly uint AbilityId;

	public readonly uint AffectorId;

	protected readonly IEntityViewProvider _viewProvider;

	protected readonly IPlayerAbilityController _playerAbilityController;

	protected AbilityAddedOrRemovedUXEvent(uint instanceId, uint abilityId, uint affectorId)
	{
		InstanceId = instanceId;
		AbilityId = abilityId;
		AffectorId = affectorId;
	}

	protected AbilityAddedOrRemovedUXEvent(uint instanceId, uint abilityId, uint affectorId, IEntityViewProvider viewProvider, IPlayerAbilityController playerAbilityController)
		: this(instanceId, abilityId, affectorId)
	{
		_viewProvider = viewProvider ?? NullEntityViewManager.Default;
		_playerAbilityController = playerAbilityController ?? NullPlayerAbilityController.Default;
	}

	public override IEnumerable<uint> GetInvolvedIds()
	{
		yield return InstanceId;
	}
}
