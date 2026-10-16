using System.Collections.Generic;
using UnityEngine;

namespace Wotc.Mtga.DuelScene;

public class AvatarLayout_Quadrant : IAvatarLayout
{
	private readonly IPlayerOffsetCalculator _playerOffsetCalculator;

	private readonly Dictionary<DuelScene_AvatarView, Vector3> _avatarToOriginalPosition = new Dictionary<DuelScene_AvatarView, Vector3>();

	public AvatarLayout_Quadrant(IPlayerOffsetCalculator playerOffsetCalculator)
	{
		_playerOffsetCalculator = playerOffsetCalculator;
	}

	public void LayoutAvatars(IEnumerable<DuelScene_AvatarView> avatars)
	{
		foreach (DuelScene_AvatarView avatar in avatars)
		{
			Vector3 originalPosition = GetOriginalPosition(avatar);
			Vector3 offset = _playerOffsetCalculator.GetOffset(avatar.InstanceId);
			avatar.transform.position = originalPosition + offset;
		}
	}

	private Vector3 GetOriginalPosition(DuelScene_AvatarView avatarView)
	{
		if (_avatarToOriginalPosition.TryGetValue(avatarView, out var value))
		{
			return value;
		}
		return _avatarToOriginalPosition[avatarView] = avatarView.transform.position;
	}
}
