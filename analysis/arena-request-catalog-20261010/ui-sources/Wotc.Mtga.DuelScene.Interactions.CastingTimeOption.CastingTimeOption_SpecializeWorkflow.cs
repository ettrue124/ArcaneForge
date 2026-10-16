using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.CastingTimeOption;

public class CastingTimeOption_SpecializeWorkflow : SelectCardsWorkflow<CastingTimeOption_SpecializeRequest>
{
	private string FAKE_CARD_KEY_FORMAT = "FAKE_SPECIALIZE_BROWSER_CARD_{0}";

	private string FAKE_SPECIALIZED_CARD_KEY_FORMAT = "FAKE_SPECIALIZED_BROWSER_CARD_{0}";

	private static List<CardColor> _specializableColors = new List<CardColor>
	{
		CardColor.White,
		CardColor.Blue,
		CardColor.Black,
		CardColor.Red,
		CardColor.Green
	};

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IFakeCardViewController _fakeCardController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly IBrowserController _browserController;

	private Dictionary<DuelScene_CDC, CardColor> _fakeCardToColorMapping = new Dictionary<DuelScene_CDC, CardColor>();

	private Dictionary<CardColor, uint> _colorToOptionGrpIdMapping = new Dictionary<CardColor, uint>();

	private CardColor _selectedColor;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetHeaderText()
	{
		return _header = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Specialize_BrowserHeader");
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public CastingTimeOption_SpecializeWorkflow(CastingTimeOption_SpecializeRequest request, ICardDatabaseAdapter cardDatabase, IFakeCardViewController fakeCardController, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider)
		: base(request)
	{
		_cardDatabase = cardDatabase;
		_fakeCardController = fakeCardController;
		_browserController = browserController;
		_headerTextProvider = headerTextProvider;
	}

	protected override void ApplyInteractionInternal()
	{
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, 1, 1, _request.CancellationType);
		SetHeaderAndSubheader();
		PopulateCardsInBrowser();
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax(1, 1u);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCards);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_prompt);
		_headerTextProvider.ClearParams();
	}

	private void PopulateCardsInBrowser()
	{
		_cardsToDisplay.Clear();
		selectable.Clear();
		nonSelectable.Clear();
		_fakeCardToColorMapping.Clear();
		_colorToOptionGrpIdMapping.Clear();
		for (int i = 0; i < _request.OptionGrpIds.Count; i++)
		{
			uint num = _request.OptionGrpIds[i];
			CardColor cardColor = _specializableColors[i];
			DuelScene_CDC duelScene_CDC = CreateFakeCard(cardColor, num);
			_colorToOptionGrpIdMapping[cardColor] = num;
			if (_request.SelectableColors.Contains(cardColor))
			{
				selectable.Add(duelScene_CDC);
				_fakeCardToColorMapping[duelScene_CDC] = cardColor;
			}
			else
			{
				nonSelectable.Add(duelScene_CDC);
			}
			_cardsToDisplay.Add(duelScene_CDC);
		}
		CardHoverController.OnHoveredCardUpdated += FlipCdcsOnHoveredCardUpdated;
	}

	private DuelScene_CDC CreateFakeCard(CardColor color, uint grpId)
	{
		ICardDataAdapter cardData = FakeCardDataForColor(color, grpId);
		return _fakeCardController.CreateFakeCard(string.Format(FAKE_CARD_KEY_FORMAT, color), cardData);
	}

	private ICardDataAdapter FakeCardDataForColor(CardColor color, uint grpId)
	{
		CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(grpId);
		MtgCardInstance parent = new MtgCardInstance
		{
			TitleId = cardPrintingById.TitleId,
			GrpId = cardPrintingById.LinkedFaceGrpIds[0],
			Colors = new List<CardColor>(cardPrintingById.Colors),
			PendingSpecializationColor = color
		};
		return CardDataExtensions.CreateWithDatabase(new MtgCardInstance
		{
			TitleId = cardPrintingById.TitleId,
			GrpId = _request.SourceAbilityId,
			ObjectSourceGrpId = grpId,
			ObjectType = GameObjectType.Ability,
			Parent = parent,
			PendingSpecializationColor = color
		}, _cardDatabase);
	}

	private DuelScene_CDC CreateFakeCardSpecialized(CardColor color, uint grpId)
	{
		ICardDataAdapter cardData = FakeCardDataSpecialized(grpId);
		return _fakeCardController.CreateFakeCard(string.Format(FAKE_SPECIALIZED_CARD_KEY_FORMAT, color), cardData);
	}

	private ICardDataAdapter FakeCardDataSpecialized(uint grpId)
	{
		CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(grpId);
		return new CardData(cardPrintingById.CreateInstance(), cardPrintingById);
	}

	private OptionalActionBrowserProvider_ClientSide Provider(CardColor color, IClientLocProvider locManager)
	{
		uint grpId = _colorToOptionGrpIdMapping[color];
		DuelScene_CDC item = CreateFakeCardSpecialized(color, grpId);
		_selectedColor = color;
		return new OptionalActionBrowserProvider_ClientSide(new OptionalActionBrowserProvider_ClientSide.OptionalActionBrowserData
		{
			CardViews = new List<DuelScene_CDC> { item },
			Header = locManager.GetLocalizedText("DuelScene/ClientPrompt/Are_You_Sure_Title"),
			SubHeader = locManager.GetLocalizedText("AbilityHanger/PlayWarning/Body_Legendary_Specialize"),
			NoText = "DuelScene/ClientPrompt/ClientPrompt_Button_No",
			OnNoAction = ApplyInteractionInternal,
			YesText = "DuelScene/ClientPrompt/ClientPrompt_Button_Yes",
			OnYesAction = ConfirmedSelectedSpecialization
		});
	}

	public override void CleanUp()
	{
		base.CleanUp();
		foreach (CardColor specializableColor in _specializableColors)
		{
			_fakeCardController.DeleteFakeCard(string.Format(FAKE_CARD_KEY_FORMAT, specializableColor));
			_fakeCardController.DeleteFakeCard(string.Format(FAKE_SPECIALIZED_CARD_KEY_FORMAT, specializableColor));
		}
		CardHoverController.OnHoveredCardUpdated -= FlipCdcsOnHoveredCardUpdated;
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (CanClick(cardView))
		{
			OnClick(cardView);
		}
	}

	private bool CanClick(DuelScene_CDC card)
	{
		return _fakeCardToColorMapping.ContainsKey(card);
	}

	private void OnClick(DuelScene_CDC card)
	{
		if (_fakeCardToColorMapping.TryGetValue(card, out var value))
		{
			if (_request.HotIds.Contains((uint)value))
			{
				SelectSpecialization(value);
			}
			else
			{
				OpenAreYouSureBrowser(value);
			}
		}
	}

	private void OpenAreYouSureBrowser(CardColor color)
	{
		_openedBrowser.Close();
		OptionalActionBrowserProvider_ClientSide optionalActionBrowserProvider_ClientSide = Provider(color, _cardDatabase.ClientLocProvider);
		CardHoverController.OnHoveredCardUpdated -= FlipCdcsOnHoveredCardUpdated;
		optionalActionBrowserProvider_ClientSide.SetOpenedBrowser(_browserController.OpenBrowser(optionalActionBrowserProvider_ClientSide));
	}

	private void ConfirmedSelectedSpecialization()
	{
		SelectSpecialization(_selectedColor);
	}

	private void SelectSpecialization(CardColor color)
	{
		_request.SubmitSpecialization(color);
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
		if (_buttonStateData == null)
		{
			_buttonStateData = new Dictionary<string, ButtonStateData>();
		}
		else
		{
			_buttonStateData.Clear();
		}
		if (_request.CanCancel)
		{
			ButtonStateData buttonStateData = new ButtonStateData();
			buttonStateData.LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel";
			buttonStateData.BrowserElementKey = "SingleButton";
			buttonStateData.Enabled = true;
			buttonStateData.StyleType = ButtonStyle.StyleType.Main;
			_buttonStateData.Add("CancelButton", buttonStateData);
		}
		return _buttonStateData;
	}

	private void FlipCdcsOnHoveredCardUpdated(DuelScene_CDC hoveredCardView)
	{
		foreach (DuelScene_CDC item in _cardsToDisplay)
		{
			if (item == hoveredCardView)
			{
				ICardDataAdapter overrideData = FakeCardDataSpecialized(item.Model.ObjectSourceGrpId);
				item.ModelOverride = new ModelOverride(overrideData);
			}
			else
			{
				item.ModelOverride = null;
			}
		}
	}
}
