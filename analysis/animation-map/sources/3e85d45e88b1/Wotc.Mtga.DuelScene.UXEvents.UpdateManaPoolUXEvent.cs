using System.Collections.Generic;
using GreClient.Rules;
using Pooling;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateManaPoolUXEvent : UXEvent
{
	private static ManaProducedStaggerPostProcess _eventPostProcess = new ManaProducedStaggerPostProcess();

	private readonly uint _affectorId;

	private List<MtgMana> _newManaPool;

	private List<UXEvent> _manaProducedEvents;

	private readonly IAvatarViewProvider _avatarViewProvider;

	private readonly IObjectPool _objPool;

	private UXEventGroup _eventGroup;

	public UpdateManaPoolUXEvent(uint affectorId, List<MtgMana> newManaPool, List<UXEvent> manaProducedEvents, IObjectPool objectPool, IAvatarViewProvider avatarViewProvider)
	{
		_affectorId = affectorId;
		_newManaPool = newManaPool ?? new List<MtgMana>();
		_manaProducedEvents = manaProducedEvents ?? new List<UXEvent>();
		_objPool = objectPool ?? NullObjectPool.Default;
		_avatarViewProvider = avatarViewProvider ?? NullAvatarViewProvider.Default;
	}

	public override void Update(float dt)
	{
		base.Update(dt);
		if (!IsComplete && _eventGroup != null)
		{
			_eventGroup.Update(dt);
			TryComplete();
		}
	}

	public override void Execute()
	{
		if (_avatarViewProvider.TryGetAvatarById(_affectorId, out var avatar))
		{
			avatar.UpdateManaPool(_newManaPool);
		}
		if (_manaProducedEvents.Count > 0)
		{
			_timeOutTarget += _manaProducedEvents.Count;
			_eventPostProcess.GroupEvents(0, ref _manaProducedEvents);
			_eventGroup = new UXEventGroup(_manaProducedEvents);
			_eventGroup.Execute();
			TryComplete();
		}
		else
		{
			Complete();
		}
	}

	private void TryComplete()
	{
		if (_eventGroup.IsComplete)
		{
			Complete();
		}
	}

	protected override void Cleanup()
	{
		base.Cleanup();
		_manaProducedEvents.Clear();
		_objPool.PushObject(_manaProducedEvents);
	}
}
