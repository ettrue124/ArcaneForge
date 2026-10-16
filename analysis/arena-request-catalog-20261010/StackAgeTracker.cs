using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;

namespace Wotc.Mtga.DuelScene.Battlefield;

public sealed class StackAgeTracker : IComparer<IBattlefieldStack>
{
	private readonly Dictionary<uint, uint> _oldestAgeByTitleId = new Dictionary<uint, uint>();

	private readonly List<uint> _titleIdsToRemove = new List<uint>();

	public void SyncTo(IReadOnlyList<IBattlefieldStack> stacks)
	{
		_titleIdsToRemove.Clear();
		foreach (uint key in _oldestAgeByTitleId.Keys)
		{
			if (!ContainsTitleId(stacks, key))
			{
				_titleIdsToRemove.Add(key);
			}
		}
		foreach (uint item in _titleIdsToRemove)
		{
			_oldestAgeByTitleId.Remove(item);
		}
		foreach (IBattlefieldStack stack in stacks)
		{
			uint titleId = stack.StackParentModel.TitleId;
			_oldestAgeByTitleId[titleId] = (_oldestAgeByTitleId.TryGetValue(titleId, out var value) ? Math.Min(value, stack.Age) : stack.Age);
		}
	}

	public void RemapAge(uint oldInstanceId, uint newInstanceId)
	{
		foreach (KeyValuePair<uint, uint> item in _oldestAgeByTitleId)
		{
			if (item.Value == oldInstanceId)
			{
				_oldestAgeByTitleId[item.Key] = newInstanceId;
				break;
			}
		}
	}

	public void RemapTitleId(uint oldTitleId, uint newTitleId)
	{
		if (_oldestAgeByTitleId.TryGetValue(oldTitleId, out var value))
		{
			_oldestAgeByTitleId[newTitleId] = value;
		}
	}

	public int Compare(IBattlefieldStack x, IBattlefieldStack y)
	{
		ICardDataAdapter stackParentModel = x.StackParentModel;
		ICardDataAdapter stackParentModel2 = y.StackParentModel;
		int num = CompareControllerIds(stackParentModel.Controller, stackParentModel2.Controller);
		if (num != 0)
		{
			return num;
		}
		if (stackParentModel.TitleId != stackParentModel2.TitleId)
		{
			num = -GetOldestAge(stackParentModel.TitleId).CompareTo(GetOldestAge(stackParentModel2.TitleId));
		}
		if (num == 0)
		{
			num = -x.Age.CompareTo(y.Age);
		}
		return num;
	}

	private uint GetOldestAge(uint titleId)
	{
		return _oldestAgeByTitleId[titleId];
	}

	private static int CompareControllerIds(MtgPlayer x, MtgPlayer y)
	{
		uint value = x?.InstanceId ?? 0;
		return (y?.InstanceId ?? 0).CompareTo(value);
	}

	private static bool ContainsTitleId(IReadOnlyList<IBattlefieldStack> stacks, uint titleId)
	{
		foreach (IBattlefieldStack stack in stacks)
		{
			if (stack.StackParentModel.TitleId == titleId)
			{
				return true;
			}
		}
		return false;
	}
}
