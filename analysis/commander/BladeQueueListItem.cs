using System;
using System.Collections.Generic;

namespace Wizards.Mtga.PlayBlade;

public class BladeQueueListItem : BladeListItem
{
	private BladeQueueInfo _bladeQueueInfo;

	private Action<BladeQueueInfo> _clicked;

	private const string NEW_TAG = "NEW";

	public void SetModel(BladeQueueInfo bladeQueueInfo)
	{
		_bladeQueueInfo = bladeQueueInfo;
		ResetToggle();
		SetText(_bladeQueueInfo.LocTitle);
		SetUpTags(_bladeQueueInfo.Tags);
	}

	public void Cleanup()
	{
		ResetToggle();
		_bladeQueueInfo = null;
		_clicked = null;
	}

	public void SetOnClick(Action<BladeQueueInfo> onClick)
	{
		_clicked = onClick;
		SetOnClick(OnClick);
	}

	private void SetUpTags(List<string> tags)
	{
		bool attract = tags?.Contains("NEW") ?? false;
		SetAttract(attract);
	}

	private void OnClick()
	{
		_clicked?.Invoke(_bladeQueueInfo);
	}
}
