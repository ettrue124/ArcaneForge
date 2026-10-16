using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

public class ScryWorkflow : BrowserWorkflowBase<GroupRequest>
{
	private readonly IClientLocProvider _locProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserController _browserController;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.Scry;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Scry";
	}

	public ScryWorkflow(GroupRequest request, IClientLocProvider clientLocProvider, ICardViewProvider cardViewProvider, IBrowserController browserController)
		: base(request)
	{
		_locProvider = clientLocProvider;
		_cardViewProvider = cardViewProvider;
		_browserController = browserController;
	}

	protected override void ApplyInteractionInternal()
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		_header = _locProvider.GetLocalizedText("DuelScene/Browsers/ScryBrowser_Header");
		_subHeader = _locProvider.GetLocalizedText("DuelScene/Browsers/ScryBrowser_SubHeader");
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.LocalizedString = "DuelScene/KeywordSelection/KeywordSelection_Done";
		buttonStateData.Enabled = true;
		buttonStateData.BrowserElementKey = "SubmitButton";
		buttonStateData.StyleType = ButtonStyle.StyleType.Main;
		_buttonStateData.Add("DoneButton", buttonStateData);
		_cardsToDisplay = _cardViewProvider.GetCardViews(_request.InstanceIds);
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton")
		{
			Submit();
		}
	}

	private void Submit()
	{
		Group obj = new Group();
		obj.SubZoneType = SubZoneType.Top;
		obj.ZoneType = ZoneType.Library;
		Group obj2 = new Group();
		obj2.SubZoneType = SubZoneType.Bottom;
		obj2.ZoneType = ZoneType.Library;
		Group obj3 = obj;
		foreach (DuelScene_CDC cardView in (_openedBrowser as CardBrowserBase).GetCardViews())
		{
			if (cardView.InstanceId == 0)
			{
				obj3 = obj2;
			}
			else
			{
				obj3.Ids.Add(cardView.InstanceId);
			}
		}
		List<Group> groups = new List<Group> { obj, obj2 };
		_request.SubmitGroups(groups);
	}
}
