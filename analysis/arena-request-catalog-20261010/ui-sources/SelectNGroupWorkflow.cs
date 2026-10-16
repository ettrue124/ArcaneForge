using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;

public class SelectNGroupWorkflow : GroupedBrowserWorkflow<SelectNGroupRequest>
{
	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _browserHeaderTextProvider;

	public SelectNGroupWorkflow(SelectNGroupRequest request, ICardViewProvider cardViewProvider, IBrowserController browserController, IBrowserHeaderTextProvider browserHeaderTextProvider)
		: base(request)
	{
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_browserHeaderTextProvider = browserHeaderTextProvider ?? NullBrowserHeaderTextProvider.Default;
	}

	protected override void ApplyInteractionInternal()
	{
		_cardsToDisplay = new List<List<DuelScene_CDC>>();
		InitializeButtonStateData();
		for (int i = 0; i < _request.Groups.Count; i++)
		{
			_cardsToDisplay.Add(_cardViewProvider.GetCardViews(_request.Groups[i].Ids));
		}
		ICardDataAdapter headerAndSubheader = null;
		if (_cardViewProvider.TryGetCardView(_request.SourceId, out var cardView))
		{
			headerAndSubheader = cardView.Model;
		}
		SetHeaderAndSubheader(headerAndSubheader);
		SetOpenedBrowser(OpenBrowser());
	}

	private IBrowser OpenBrowser()
	{
		IBrowser browser = _browserController.OpenBrowser(this);
		if (browser is SelectGroupBrowser selectGroupBrowser)
		{
			selectGroupBrowser.SetHeader(_header);
			selectGroupBrowser.SetSubheader(_subHeader);
		}
		return browser;
	}

	private void SetHeaderAndSubheader(ICardDataAdapter sourceModel)
	{
		_browserHeaderTextProvider.ClearParams();
		_browserHeaderTextProvider.SetMinMax((int)_request.MinSelect, _request.MaxSelect);
		_browserHeaderTextProvider.SetSourceModel(sourceModel);
		_browserHeaderTextProvider.SetWorkflow(this);
		_browserHeaderTextProvider.SetRequest(_request);
		_browserHeaderTextProvider.SetBrowserType(DuelSceneBrowserType.SelectGroup);
		_header = _browserHeaderTextProvider.GetHeaderText("DuelScene/ClientPrompt/Select_Pile_Title");
		_subHeader = _browserHeaderTextProvider.GetSubHeaderText(_prompt);
		_browserHeaderTextProvider.ClearParams();
	}

	private void InitializeButtonStateData()
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.Enabled = true;
		buttonStateData.LocalizedString = "DuelScene/Browsers/Select_Title";
		buttonStateData.BrowserElementKey = "GroupAButton";
		buttonStateData.StyleType = ButtonStyle.StyleType.Secondary;
		_buttonStateData.Add("GroupAButton", buttonStateData);
		ButtonStateData buttonStateData2 = new ButtonStateData();
		buttonStateData2.Enabled = true;
		buttonStateData2.LocalizedString = "DuelScene/Browsers/Select_Title";
		buttonStateData2.BrowserElementKey = "GroupBButton";
		buttonStateData2.StyleType = ButtonStyle.StyleType.Secondary;
		_buttonStateData.Add("GroupBButton", buttonStateData2);
	}

	public void OnGroupSelected(int groupIndex)
	{
		_request.SubmitGroupSelection((uint)_request.Groups[groupIndex].GroupId);
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectGroup;
	}

	public override void Browser_OnButtonPressed(string buttonKey)
	{
		OnGroupSelected((!(buttonKey == "GroupAButton")) ? 1 : 0);
	}

	public override string GetCardHolderLayoutKey()
	{
		return "SelectGroup";
	}
}
