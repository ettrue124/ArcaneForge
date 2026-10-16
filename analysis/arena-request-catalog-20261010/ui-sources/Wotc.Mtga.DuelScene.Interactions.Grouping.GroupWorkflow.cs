using System;
using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.Grouping;

public class GroupWorkflow : GroupedBrowserWorkflow<GroupRequest>
{
	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserController _browserController;

	private SplitCardsBrowser _splitCardsBrowser;

	public int GroupCount => _request.GroupSpecs.Count;

	public GroupWorkflow(GroupRequest request, ICardViewProvider cardViewProvider, IBrowserController browserController)
		: base(request)
	{
		_cardViewProvider = cardViewProvider;
		_browserController = browserController;
		_prompt = null;
	}

	protected override void ApplyInteractionInternal()
	{
		_cardsToDisplay = new List<List<DuelScene_CDC>>();
		List<DuelScene_CDC> cardViews = _cardViewProvider.GetCardViews(_request.InstanceIds);
		_cardsToDisplay.Add(cardViews);
		InitializeButtonStateData();
		if (_request.GroupSpecs.Count > 1)
		{
			SetOpenedBrowser(_browserController.OpenBrowser(this));
			return;
		}
		throw new NotImplementedException("Attempting to handle group request with 1 or less groups (this used to fall back to an order cards browser)");
	}

	private void InitializeButtonStateData()
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.Enabled = true;
		buttonStateData.LocalizedString = "MainNav/DeckManager/DeckManager_DoneButton";
		buttonStateData.BrowserElementKey = "SubmitButton";
		buttonStateData.StyleType = ButtonStyle.StyleType.Main;
		_buttonStateData.Add("SubmitButton", buttonStateData);
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.Split;
	}

	private void Submit()
	{
		List<List<DuelScene_CDC>> cardGroups = _splitCardsBrowser.GetCardGroups();
		List<Group> list = new List<Group>();
		for (int i = 0; i < cardGroups.Count; i++)
		{
			Group obj = new Group();
			obj.SubZoneType = _request.GroupSpecs[i].SubZoneType;
			obj.ZoneType = _request.GroupSpecs[i].ZoneType;
			obj.IsFacedown = _request.GroupSpecs[i].IsFacedown;
			foreach (DuelScene_CDC item in cardGroups[i])
			{
				obj.Ids.Add(item.InstanceId);
			}
			list.Add(obj);
		}
		_request.SubmitGroups(list);
	}

	public override void Browser_OnButtonPressed(string buttonKey)
	{
		if (buttonKey == "SubmitButton")
		{
			Submit();
		}
	}

	protected override void Browser_OnClosed()
	{
		base.Browser_OnClosed();
		_splitCardsBrowser = null;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "SplitGroup";
	}

	public override void SetOpenedBrowser(IBrowser browser)
	{
		_splitCardsBrowser = (SplitCardsBrowser)browser;
		base.SetOpenedBrowser(browser);
	}

	public bool IsGroupFaceDown(int groupId)
	{
		return _request.GroupSpecs[groupId].IsFacedown;
	}
}
