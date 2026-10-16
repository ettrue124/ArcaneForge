using System.Collections.Generic;
using AssetLookupTree.Blackboard;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

public class SurveilWorkflow : BrowserWorkflowBase<GroupRequest>
{
	private readonly ICardViewProvider _cardViewProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardDatabaseAdapter _cardDatabaseAdapter;

	private readonly IBrowserController _browserController;

	public SurveilWorkflow(GroupRequest request, IGameStateProvider gameStateProvider, ICardDatabaseAdapter cardDatabaseAdapter, IBrowserController browserController, ICardViewProvider cardViewProvider)
		: base(request)
	{
		_cardViewProvider = cardViewProvider;
		_gameStateProvider = gameStateProvider;
		_cardDatabaseAdapter = cardDatabaseAdapter;
		_browserController = browserController;
	}

	protected override void ApplyInteractionInternal()
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		_header = Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/Surveil_Browser_Title");
		_subHeader = Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/Surveil_Browser_Subtitle");
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.LocalizedString = "DuelScene/KeywordSelection/KeywordSelection_Done";
		buttonStateData.Enabled = true;
		buttonStateData.BrowserElementKey = "SubmitButton";
		buttonStateData.StyleType = ButtonStyle.StyleType.Main;
		_buttonStateData.Add("DoneButton", buttonStateData);
		_cardsToDisplay = _cardViewProvider.GetCardViews(_request.InstanceIds);
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
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
		SurveilBrowser surveilBrowser = _openedBrowser as SurveilBrowser;
		Group obj = new Group();
		obj.ZoneType = ZoneType.Graveyard;
		obj.SubZoneType = SubZoneType.None;
		foreach (DuelScene_CDC graveyardCard in surveilBrowser.GetGraveyardCards())
		{
			obj.Ids.Add(graveyardCard.InstanceId);
		}
		Group obj2 = new Group();
		obj2.ZoneType = ZoneType.Library;
		obj2.SubZoneType = SubZoneType.Top;
		foreach (DuelScene_CDC libraryCard in surveilBrowser.GetLibraryCards())
		{
			if (libraryCard.InstanceId != 0)
			{
				obj2.Ids.Add(libraryCard.InstanceId);
			}
		}
		List<Group> groups = new List<Group> { obj2, obj };
		_request.SubmitGroups(groups);
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.Surveil;
	}

	public override void SetFxBlackboardData(IBlackboard bb)
	{
		base.SetFxBlackboardData(bb);
		if (!_gameStateProvider.LatestGameState.Value.TryGetCard(_request.SourceId, out var card))
		{
			return;
		}
		bb.SetCardDataExtensive(CardDataExtensions.CreateWithDatabase(card, _cardDatabaseAdapter));
		AbilityPrintingData abilityPrintingById = _cardDatabaseAdapter.AbilityDataProvider.GetAbilityPrintingById(card.GrpId);
		if (abilityPrintingById != null)
		{
			bb.Ability = abilityPrintingById;
			return;
		}
		AbilityPrintingData abilityPrintingData = card.Abilities.Find((AbilityPrintingData x) => x.Category == AbilityCategory.Spell || x.Category == AbilityCategory.Chained);
		if (abilityPrintingData != null)
		{
			bb.Ability = abilityPrintingData;
		}
	}
}
