using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.CardData.RulesTextOverrider;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.OptionalAction;

public class NonMechanicCardButtonWorkflow : SelectCardsWorkflow<OptionalActionMessageRequest>
{
	private readonly MtgCardInstance _cardInstance;

	private readonly BrowserText _browserText;

	private readonly ICardBuilder<DuelScene_CDC> _cardViewBuilder;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IBrowserController _browserController;

	private readonly IPromptTextProvider _promptTextProvider;

	private DuelScene_CDC yesCardView;

	private DuelScene_CDC noCardView;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public NonMechanicCardButtonWorkflow(OptionalActionMessageRequest request, MtgCardInstance cardInstance, BrowserText browserText, ICardBuilder<DuelScene_CDC> cardViewProvider, IClientLocProvider locProvider, ICardDatabaseAdapter cardDatabase, IBrowserController browserController, IPromptTextProvider promptTextProvider)
		: base(request)
	{
		_cardInstance = cardInstance;
		_browserText = browserText;
		_cardViewBuilder = cardViewProvider;
		_clientLocProvider = locProvider;
		_cardDatabase = cardDatabase;
		_browserController = browserController;
		_promptTextProvider = promptTextProvider;
	}

	protected override void ApplyInteractionInternal()
	{
		uint grpId = _cardInstance.Parent.GrpId;
		_cardInstance.Colors = _cardInstance.Parent.Colors;
		_header = _clientLocProvider.GetLocalizedText(_browserText.Header, _browserText.Params);
		_subHeader = (string.IsNullOrEmpty(_browserText.Subheader) ? _promptTextProvider.GetPromptText(_request.Prompt) : _clientLocProvider.GetLocalizedText(_browserText.Subheader, _browserText.Params));
		_buttonStateData = GenerateDefaultButtonStates(0, 1, 1, _request.CancellationType);
		MtgCardInstance copy = _cardInstance.GetCopy();
		copy.InstanceId = 0u;
		copy.Abilities.Clear();
		CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(grpId);
		CardPrintingRecord record = cardPrintingById.Record;
		IReadOnlyList<(uint, uint)> abilityIds = Array.Empty<(uint, uint)>();
		IReadOnlyList<(uint, uint)> hiddenAbilityIds = Array.Empty<(uint, uint)>();
		IReadOnlyDictionary<uint, IReadOnlyList<uint>> abilityIdToLinkedTokenGrpId = DictionaryExtensions.Empty<uint, IReadOnlyList<uint>>();
		CardPrintingData printing = new CardPrintingData(cardPrintingById, new CardPrintingRecord(record, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, abilityIds, hiddenAbilityIds, null, null, abilityIdToLinkedTokenGrpId));
		CardData cardData = new CardData(copy, printing)
		{
			RulesTextOverride = new ClientLocTextOverride(_clientLocProvider, _browserText.YesText, _browserText.Params)
		};
		CardData cardData2 = new CardData(copy, printing);
		cardData2.RulesTextOverride = new ClientLocTextOverride(_clientLocProvider, _browserText.NoText, _browserText.Params);
		yesCardView = _cardViewBuilder.CreateCDC(cardData);
		noCardView = _cardViewBuilder.CreateCDC(cardData2);
		_cardsToDisplay = new List<DuelScene_CDC> { yesCardView, noCardView };
		selectable.Clear();
		selectable.AddRange(_cardsToDisplay);
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "SubmitButton")
		{
			SubmitResponse();
		}
		else if (buttonKey == "CancelButton")
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

	private void SubmitResponse()
	{
		OptionResponse response = ((currentSelections[0] == yesCardView) ? OptionResponse.AllowYes : OptionResponse.CancelNo);
		_request.SubmitResponse(response);
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

	protected override Dictionary<string, ButtonStateData> GenerateDefaultButtonStates(int currentSelectionCount, int minSelections, int maxSelections, AllowCancel cancelType)
	{
		Dictionary<string, ButtonStateData> dictionary = new Dictionary<string, ButtonStateData>();
		if (_request.CancellationType != AllowCancel.None && _request.CancellationType != AllowCancel.No)
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
