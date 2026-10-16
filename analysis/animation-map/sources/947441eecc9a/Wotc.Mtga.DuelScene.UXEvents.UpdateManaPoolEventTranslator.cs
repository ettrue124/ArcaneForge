using System.Collections.Generic;
using GreClient.Rules;
using Pooling;

namespace Wotc.Mtga.DuelScene.UXEvents;

public class UpdateManaPoolEventTranslator : IEventTranslator
{
	private readonly IAvatarViewProvider _avatarViewProvider;

	private readonly IObjectPool _objectPool;

	private readonly IManaProducedAnimationPlayer _manaProducedAnimationPlayer;

	public UpdateManaPoolEventTranslator(IAvatarViewProvider avatarViewProvider, IObjectPool objectPool, IManaProducedAnimationPlayer manaProducedAnimationPlayer)
	{
		_avatarViewProvider = avatarViewProvider ?? NullAvatarViewProvider.Default;
		_objectPool = objectPool ?? NullObjectPool.Default;
		_manaProducedAnimationPlayer = manaProducedAnimationPlayer;
	}

	public void Translate(IReadOnlyList<GameRulesEvent> allChanges, int changeIndex, MtgGameState oldState, MtgGameState newState, List<UXEvent> events)
	{
		if (allChanges[changeIndex] is UpdateManaPoolEvent { OldPlayer: var oldPlayer, NewPlayer: var newPlayer })
		{
			List<UXEvent> manaProducedEvents = GenerateManaProducedEvents(newPlayer, oldPlayer);
			int insertIndex = GetInsertIndex(newPlayer.ManaPool, events);
			UXEvent item = new UpdateManaPoolUXEvent(newPlayer.InstanceId, newPlayer.ManaPool, manaProducedEvents, _objectPool, _avatarViewProvider);
			events.Insert(insertIndex, item);
		}
	}

	private List<UXEvent> GenerateManaProducedEvents(MtgPlayer newPlayer, MtgPlayer oldPlayer)
	{
		List<UXEvent> list = _objectPool.PopObject<List<UXEvent>>();
		foreach (MtgMana item in GetAllMana(newPlayer.ManaPool))
		{
			uint manaId = item.ManaId;
			if (!oldPlayer.IdToManaMap.ContainsKey(manaId))
			{
				list.Add(new ManaProducedUXEvent(item.SrcInstanceId, newPlayer.InstanceId, item, _manaProducedAnimationPlayer));
				if (list.Count >= 5)
				{
					break;
				}
			}
		}
		return list;
		static IEnumerable<MtgMana> GetAllMana(IEnumerable<MtgMana> manaPool)
		{
			foreach (MtgMana mana in manaPool)
			{
				for (int i = 0; i < mana.Count; i++)
				{
					yield return mana;
				}
			}
		}
	}

	private int GetInsertIndex(List<MtgMana> newMana, List<UXEvent> events)
	{
		int result = events.Count;
		HashSet<uint> hashSet = _objectPool.PopObject<HashSet<uint>>();
		foreach (MtgMana item in newMana)
		{
			hashSet.Add(item.SrcInstanceId);
		}
		for (int num = events.Count - 1; num >= 0; num--)
		{
			if (IsValidInsertIndex(num, events, hashSet))
			{
				result = num;
				break;
			}
		}
		hashSet.Clear();
		_objectPool.PushObject(hashSet);
		return result;
	}

	private bool IsValidInsertIndex(int idx, List<UXEvent> events, HashSet<uint> sourceIds)
	{
		if (!(events[idx] is ResolutionEventEndedUXEvent resolutionEventEndedUXEvent))
		{
			return false;
		}
		if (sourceIds.Contains(resolutionEventEndedUXEvent.InstigatorInstanceId))
		{
			return true;
		}
		MtgCardInstance instigator = resolutionEventEndedUXEvent.Instigator;
		if (instigator != null && instigator.ParentId != 0)
		{
			return sourceIds.Contains(instigator.ParentId);
		}
		return false;
	}
}
