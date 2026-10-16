using System.Collections.Generic;
using GreClient.CardData;
using GreClient.CardData.RulesTextOverrider;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class ChoiceModalWorkflow : SelectCardsWorkflow<SelectNRequest>
{
	private readonly ChoiceOptions _choiceOptions;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private Dictionary<DuelScene_CDC, uint> _abilityCdcToIdMapping;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public ChoiceModalWorkflow(SelectNRequest request, ChoiceOptions choiceOptions, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardBuilder<DuelScene_CDC> cardBuilder, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider)
		: base(request)
	{
		_choiceOptions = choiceOptions;
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_cardBuilder = cardBuilder;
		_browserController = browserController;
		_headerTextProvider = headerTextProvider;
	}

	public override bool CanApply(List<UXEvent> events)
	{
		if (!((BrowserManager)_browserController).IsAnyBrowserOpen)
		{
			return events.Count == 0;
		}
		return false;
	}

	protected override void ApplyInteractionInternal()
	{
		MtgCardInstance cardById = _gameStateProvider.LatestGameState.Value.GetCardById(_request.SourceId);
		CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(cardById.GrpId);
		string imageAssetPath = cardPrintingById.ImageAssetPath;
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, 1, 1, _request.CancellationType);
		SetHeaderAndSubheader();
		_abilityCdcToIdMapping = new Dictionary<DuelScene_CDC, uint>();
		foreach (string sortedChoice in _choiceOptions.SortedChoices)
		{
			uint value = _choiceOptions.ChoiceToIdMap[sortedChoice];
			CardData cardData = new CardData(new MtgCardInstance
			{
				ObjectType = GameObjectType.Ability,
				TitleId = cardPrintingById.TitleId,
				ObjectSourceGrpId = cardById.GrpId,
				Visibility = Visibility.Public,
				Colors = cardById.Colors
			}, new CardPrintingData(new CardPrintingRecord(0u, 0u, imageAssetPath), NullCardDataProvider.Default, NullAbilityDataProvider.Default))
			{
				RulesTextOverride = new RawTextOverride(sortedChoice)
			};
			DuelScene_CDC duelScene_CDC = _cardBuilder.CreateCDC(cardData);
			_cardsToDisplay.Add(duelScene_CDC);
			selectable.Add(duelScene_CDC);
			_abilityCdcToIdMapping.Add(duelScene_CDC, value);
		}
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.SetMinMax(1, 1u);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCards);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_request.Prompt);
		_headerTextProvider.ClearParams();
	}

	public void SubmitResponse()
	{
		List<uint> list = new List<uint>();
		foreach (DuelScene_CDC currentSelection in currentSelections)
		{
			list.Add(_abilityCdcToIdMapping[currentSelection]);
		}
		_request.SubmitSelection(list);
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (currentSelections.Count == 0)
		{
			base.CardBrowser_OnCardViewSelected(cardView);
			if (currentSelections.Count == 1)
			{
				SubmitResponse();
			}
		}
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "CancelButton")
		{
			CancelRequest();
		}
	}

	private void CancelRequest()
	{
		if (_request.CanCancel)
		{
			_request.Cancel();
		}
	}

	protected override Dictionary<string, ButtonStateData> GenerateDefaultButtonStates(int currentSelectionCount, int minSelections, int maxSelections, AllowCancel cancelType)
	{
		Dictionary<string, ButtonStateData> dictionary = new Dictionary<string, ButtonStateData>();
		if (_request.CanCancel)
		{
			ButtonStateData buttonStateData = new ButtonStateData();
			buttonStateData.LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel";
			buttonStateData.BrowserElementKey = "SingleButton";
			buttonStateData.StyleType = ButtonStyle.StyleType.Main;
			dictionary.Add("CancelButton", buttonStateData);
		}
		return dictionary;
	}
}
