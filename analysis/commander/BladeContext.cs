using System;
using AssetLookupTree;

namespace Wizards.Mtga.PlayBlade;

[Serializable]
internal class BladeContext
{
	public CustomTab bladeTabView;

	public BladeView bladeView;

	public BladeContentView bladeContentView;

	public void Hide()
	{
		bladeTabView.SetTabActiveVisuals(show: false);
		bladeContentView.UbSubscribeToSelectionUpdates();
		bladeView.UbSubscribeToSelectionUpdates();
		bladeView.Hide();
		bladeContentView.Hide();
	}

	public void UpdateSelectionInfo(BladeSelectionInfo selectionInfo)
	{
		bladeView.OnSelectionInfoUpdated(selectionInfo);
		bladeContentView.OnSelectionInfoUpdated(selectionInfo);
	}

	public void Show(Action<BladeSelectionInfo> updateSelectionInfo)
	{
		bladeTabView.SetTabActiveVisuals(show: true);
		bladeView.Show();
		bladeContentView.Show();
		bladeView.SubscribeToSelectionUpdates(updateSelectionInfo);
		bladeContentView.SubscribeToSelectionUpdates(updateSelectionInfo);
	}

	public void SetData(IBladeModel data, BladeSelectionInfo selectionInfo, AssetLookupSystem assetLookupSystem)
	{
		bladeView.SetModel(data, selectionInfo, assetLookupSystem);
		bladeContentView.SetModel(data, selectionInfo, assetLookupSystem);
	}

	public void SetNotificationPip(bool setPip)
	{
		bladeTabView.SetPipVisible(setPip);
	}
}
