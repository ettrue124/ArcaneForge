using System;
using System.Collections.Generic;
using AssetLookupTree.Blackboard;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectZonesWorkflow : WorkflowBase<SelectNRequest>, IDuelSceneBrowserProvider, IBrowserHeaderProvider, IButtonScrollListBrowserProvider
{
	private const string BUTTONKEY_DECLINE = "Decline";

	private BrowserBase _openedBrowser;

	private readonly Dictionary<string, ButtonStateData> _browserButtonStateData = new Dictionary<string, ButtonStateData>();

	private readonly Dictionary<string, ButtonStateData> _scrollListButtonStateData = new Dictionary<string, ButtonStateData>();

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IBrowserController _browserController;

	public SelectZonesWorkflow(SelectNRequest request, IGameStateProvider gameStateProvider, IClientLocProvider clientLocProvider, IBrowserController browserController)
		: base(request)
	{
		_gameStateProvider = gameStateProvider;
		_clientLocProvider = clientLocProvider;
		_browserController = browserController;
	}

	protected override void ApplyInteractionInternal()
	{
		foreach (uint id in _request.Ids)
		{
			MtgZone zoneById = _gameStateProvider.LatestGameState.Value.GetZoneById(id);
			string localizedZoneKey = Utils.GetLocalizedZoneKey(zoneById.Type, zoneById.Owner);
			string localizedText = _clientLocProvider.GetLocalizedText("DuelScene/Browsers/ZoneSearch", ("zone", _clientLocProvider.GetLocalizedText(localizedZoneKey)));
			ButtonStateData value = new ButtonStateData
			{
				LocalizedString = new UnlocalizedMTGAString(localizedText),
				StyleType = ButtonStyle.StyleType.Secondary
			};
			_scrollListButtonStateData.Add(zoneById.Id.ToString(), value);
		}
		if (_request.MinSel == 0)
		{
			string localizedText2 = _clientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/ClientPrompt_Button_Decline");
			ButtonStateData value2 = new ButtonStateData
			{
				LocalizedString = new UnlocalizedMTGAString(localizedText2),
				StyleType = ButtonStyle.StyleType.Secondary
			};
			_scrollListButtonStateData.Add("Decline", value2);
		}
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}

	public DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.ButtonScrollList;
	}

	public Dictionary<string, ButtonStateData> GetButtonStateData()
	{
		return _browserButtonStateData;
	}

	public Dictionary<string, ButtonStateData> GetScrollListButtonDataByKey()
	{
		return _scrollListButtonStateData;
	}

	private void Browser_OnButtonPressed(string buttonKey)
	{
		if (uint.TryParse(buttonKey, out var result))
		{
			_request.SubmitSelection(result);
		}
		else if (buttonKey == "Decline")
		{
			_request.SubmitSelection(Array.Empty<uint>());
		}
	}

	private void Browser_OnClosed()
	{
		_openedBrowser.ButtonPressedHandlers -= Browser_OnButtonPressed;
		_openedBrowser.ClosedHandlers -= Browser_OnClosed;
		_openedBrowser = null;
	}

	private void SetOpenedBrowser(IBrowser browser)
	{
		_openedBrowser = (BrowserBase)browser;
		_openedBrowser.ButtonPressedHandlers += Browser_OnButtonPressed;
		_openedBrowser.ClosedHandlers += Browser_OnClosed;
	}

	public override void CleanUp()
	{
		_openedBrowser?.Close();
	}

	public virtual string GetHeaderText()
	{
		return Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Option_One");
	}

	public virtual string GetSubHeaderText()
	{
		return Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/SelectZones_SubHeader");
	}

	public void SetFxBlackboardData(IBlackboard bb)
	{
	}
}
