using System;
using System.Collections.Generic;
using Pooling;
using UnityEngine;

namespace Wotc.Mtga.DuelScene;

public class AvatarLayout : IAvatarLayout
{
	private const int PLAYER_WIDTH = 15;

	private readonly IObjectPool _objectPool;

	private readonly IComparer<IAvatarView> _comparer;

	public AvatarLayout(IObjectPool objectPool, IComparer<IAvatarView> comparer)
	{
		_objectPool = objectPool;
		_comparer = comparer;
	}

	public void LayoutAvatars(IEnumerable<DuelScene_AvatarView> avatars)
	{
		List<DuelScene_AvatarView> list = _objectPool.PopObject<List<DuelScene_AvatarView>>();
		List<DuelScene_AvatarView> list2 = _objectPool.PopObject<List<DuelScene_AvatarView>>();
		foreach (DuelScene_AvatarView item in avatars ?? Array.Empty<DuelScene_AvatarView>())
		{
			((item.Model.ClientPlayerEnum == GREPlayerNum.Opponent) ? list : list2).Add(item);
		}
		list2.Sort(_comparer);
		PositionAvatars(list2);
		list.Sort(_comparer);
		PositionAvatars(list);
		list.Clear();
		_objectPool.PushObject(list, tryClear: false);
		list2.Clear();
		_objectPool.PushObject(list2, tryClear: false);
	}

	private static void PositionAvatars(IReadOnlyList<DuelScene_AvatarView> avatars)
	{
		int count = avatars.Count;
		float num = (float)((count - 1) * 15) * 0.5f;
		for (int i = 0; i < count; i++)
		{
			Transform transform = avatars[i].transform;
			Vector3 localPosition = transform.localPosition;
			localPosition.x = (float)(i * 15) - num;
			transform.localPosition = localPosition;
		}
	}
}
