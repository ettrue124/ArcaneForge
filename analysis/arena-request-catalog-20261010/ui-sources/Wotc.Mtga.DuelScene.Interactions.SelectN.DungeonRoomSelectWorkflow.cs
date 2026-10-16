using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class DungeonRoomSelectWorkflow : BrowserWorkflowBase<SelectNRequest>
{
	public readonly DungeonData CurrentDungeonData;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IBrowserController _browserController;

	public readonly List<uint> SelectableIds = new List<uint>(2);

	public readonly List<uint> SelectedIds = new List<uint>(1);

	public readonly List<uint> ActiveIds = new List<uint>(1);

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.DungeonRoomSelection;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "DungeonRoomSelect";
	}

	public DungeonRoomSelectWorkflow(SelectNRequest request, DungeonData dungeonData, IClientLocProvider clientLocProvider, IBrowserController browserController)
		: base(request)
	{
		CurrentDungeonData = dungeonData;
		_clientLocProvider = clientLocProvider;
		_browserController = browserController;
		SelectableIds.AddRange(request.Ids);
		ActiveIds.Add(CurrentDungeonData.CurrentRoomGrpId);
	}

	protected override void ApplyInteractionInternal()
	{
		ResetButtonData();
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	public override string GetHeaderText()
	{
		return _header = _clientLocProvider.GetLocalizedText("DuelScene/Browsers/DungeonRoomSelect_BrowserHeader");
	}

	public override string GetSubHeaderText()
	{
		return _subHeader = _clientLocProvider.GetLocalizedText("DuelScene/Browsers/DungeonRoomSelect_BrowserSubHeader");
	}

	public void ResetButtonData()
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>(1);
		_buttonStateData["SubmitButton"] = new ButtonStateData
		{
			BrowserElementKey = "SubmitButton",
			Enabled = (SelectedIds.Count == 1),
			LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Submit",
			StyleType = ButtonStyle.StyleType.Main
		};
		SetButtons();
	}

	public void OnDungeonRoomSelected(uint roomId)
	{
		if (SelectableIds.Contains(roomId))
		{
			if (SelectedIds.Contains(roomId))
			{
				SelectedIds.Remove(roomId);
			}
			else
			{
				SelectedIds.Clear();
				SelectedIds.Add(roomId);
			}
		}
		ResetButtonData();
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "SubmitButton")
		{
			_request.SubmitSelection(SelectedIds);
		}
	}

	public static DungeonData DecidingPlayerDungeon(MtgGameState gameState)
	{
		if (gameState == null || gameState.DecidingPlayer == null)
		{
			return default;
		}
		return gameState.DecidingPlayer.DungeonState;
	}
}
