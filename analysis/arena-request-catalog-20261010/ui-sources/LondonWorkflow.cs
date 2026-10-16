using System.Collections.Generic;
using System.Linq;
using System.Text;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.Interactions.Mulligan;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

internal class LondonWorkflow : BrowserWorkflowBase<GroupRequest>
{
	private const int LIBRARY_GROUP_INDEX = 1;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly ICardDatabaseAdapter _cardDatabaseAdapter;

	private readonly IBrowserController _browserController;

	private readonly DuelSceneLogger _logger;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.London;
	}

	public LondonWorkflow(GroupRequest request, ICardViewProvider cardViewProvider, ICardDatabaseAdapter cardDatabaseAdapter, IBrowserController browserController, DuelSceneLogger duelSceneLogger)
		: base(request)
	{
		_cardViewProvider = cardViewProvider;
		_cardDatabaseAdapter = cardDatabaseAdapter;
		_browserController = browserController;
		_logger = duelSceneLogger;
	}

	protected override void OnBrowserOpened()
	{
		uint lowerBound = _request.GroupSpecs[1].LowerBound;
		LondonBrowser londonBrowser = (LondonBrowser)_openedBrowser;
		londonBrowser.RequiredPutbackCount = lowerBound;
		londonBrowser.HeaderText = Languages.ActiveLocProvider.GetLocalizedText((lowerBound == 1) ? "DuelScene/StartingPlayer/LondonReturnTitleSingular" : "DuelScene/StartingPlayer/LondonReturnTitlePlural", ("count", lowerBound.ToString()));
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<i>");
		stringBuilder.Append(Languages.ActiveLocProvider.GetLocalizedText((lowerBound == 1) ? "DuelScene/StartingPlayer/LondonReturnSubheaderSingular" : "DuelScene/StartingPlayer/LondonReturnSubheaderPlural", ("count", lowerBound.ToString())));
		stringBuilder.Append("</i>");
		londonBrowser.SubheaderText = stringBuilder.ToString();
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (!(buttonKey == "DoneButton"))
		{
			return;
		}
		LondonBrowser londonBrowser = _openedBrowser as LondonBrowser;
		Group obj = new Group();
		obj.ZoneType = ZoneType.Hand;
		obj.SubZoneType = SubZoneType.Top;
		List<DuelScene_CDC> handCards = londonBrowser.GetHandCards();
		foreach (DuelScene_CDC item in handCards)
		{
			obj.Ids.Add(item.InstanceId);
		}
		_logger.UpdateStartingHand(handCards.Select((DuelScene_CDC card) => card.Model.GrpId).ToList());
		Group obj2 = new Group();
		obj2.ZoneType = ZoneType.Library;
		obj2.SubZoneType = SubZoneType.Bottom;
		foreach (DuelScene_CDC libraryCard in londonBrowser.GetLibraryCards())
		{
			if (libraryCard.InstanceId != 0)
			{
				obj2.Ids.Add(libraryCard.InstanceId);
			}
		}
		List<Group> groups = new List<Group> { obj, obj2 };
		_request.SubmitGroups(groups);
	}

	public void OnGroupsUpdated()
	{
		LondonBrowser obj = _openedBrowser as LondonBrowser;
		uint num = (uint)(obj.GetLibraryCards().Count - 1);
		_buttonStateData["DoneButton"].Enabled = num == _request.GroupSpecs[1].LowerBound;
		obj.UpdateButtons();
	}

	protected override void ApplyInteractionInternal()
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.LocalizedString = "DuelScene/KeywordSelection/KeywordSelection_Done";
		buttonStateData.Enabled = false;
		buttonStateData.BrowserElementKey = "SubmitButton";
		buttonStateData.StyleType = ButtonStyle.StyleType.Main;
		_buttonStateData.Add("DoneButton", buttonStateData);
		_cardsToDisplay = _cardViewProvider.GetCardViews(_request.InstanceIds);
		MulliganWorkflow.SortCards(_cardsToDisplay, _cardDatabaseAdapter.CardTitleProvider);
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}
}
