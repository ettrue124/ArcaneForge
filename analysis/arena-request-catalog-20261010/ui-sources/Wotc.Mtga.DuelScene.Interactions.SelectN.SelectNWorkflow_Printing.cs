using System;
using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Printing : BrowserWorkflowBase<SelectNRequest>, ISelectCardsBrowserProvider, IBasicBrowserProvider, ICardBrowserProvider, IDuelSceneBrowserProvider, IBrowserHeaderProvider
{
	private const string FAKE_CARD_ID_FORMAT = "SelectPrinting_{0}";

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IGameplaySettingsProvider _gameplaySettings;

	private readonly IFakeCardViewController _fakeCardViewController;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly List<string> _fakeCardIds = new List<string>(3);

	private readonly List<DuelScene_CDC> _selectableCdcs = new List<DuelScene_CDC>();

	private readonly List<DuelScene_CDC> _selectedCdcs = new List<DuelScene_CDC>();

	private readonly Dictionary<DuelScene_CDC, HighlightType> _cardHighlights = new Dictionary<DuelScene_CDC, HighlightType>(3);

	public bool AllowKeyboardSelection => false;

	private bool CanSubmit
	{
		get
		{
			if (_request.MinSel <= _selectedCdcs.Count)
			{
				return _selectedCdcs.Count <= _request.MaxSel;
			}
			return false;
		}
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public SelectNWorkflow_Printing(SelectNRequest request, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IGameplaySettingsProvider gameplaySettings, IFakeCardViewController fakeCardViewController, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider)
		: base(request)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_gameplaySettings = gameplaySettings ?? NullGameplaySettingsProvider.Default;
		_fakeCardViewController = fakeCardViewController ?? NullFakeCardViewController.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_headerTextProvider = headerTextProvider ?? NullBrowserHeaderTextProvider.Default;
		_buttonStateData = new Dictionary<string, ButtonStateData> { ["DoneButton"] = new ButtonStateData
		{
			Enabled = false,
			BrowserElementKey = "DoneButton",
			LocalizedString = "DuelScene/Browsers/ViewDismiss_Done",
			StyleType = ButtonStyle.StyleType.Main
		} };
	}

	private void UpdateDoneButton()
	{
		_buttonStateData["DoneButton"].Enabled = CanSubmit;
		_openedBrowser?.UpdateButtons();
	}

	protected override void ApplyInteractionInternal()
	{
		MtgCardInstance cardById = ((MtgGameState)_gameStateProvider.LatestGameState).GetCardById(_request.SourceId);
		ICardDataAdapter headerAndSubheader = CardDataExtensions.CreateWithDatabase(cardById, _cardDatabase);
		foreach (uint id in _request.Ids)
		{
			string text = $"SelectPrinting_{id}";
			CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(id);
			ICardDataAdapter cardDataAdapter = cardPrintingById.ConvertToCardModel();
			if (cardPrintingById.KnownSupportedStyles.Contains(cardById.SkinCode))
			{
				cardDataAdapter.Instance.SkinCode = cardById.SkinCode;
			}
			if (cardPrintingById.KnownSupportedStyles.Contains(cardById.BaseSkinCode))
			{
				cardDataAdapter.Instance.BaseSkinCode = cardById.BaseSkinCode;
			}
			DuelScene_CDC item = _fakeCardViewController.CreateFakeCard(text, cardDataAdapter, isVisible: true);
			_cardsToDisplay.Add(item);
			_selectableCdcs.Add(item);
			_fakeCardIds.Add(text);
		}
		SetHeaderAndSubheader(headerAndSubheader);
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private void SetHeaderAndSubheader(ICardDataAdapter sourceModel)
	{
		_headerTextProvider.SetParams(_request.MinSel, _request.MaxSel, sourceModel, this, _request, DuelSceneBrowserType.SelectCards);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_request.Prompt);
		_headerTextProvider.ClearParams();
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (_selectedCdcs.Contains(cardView))
		{
			_selectedCdcs.Remove(cardView);
		}
		else if (_selectableCdcs.Contains(cardView))
		{
			_selectedCdcs.Add(cardView);
		}
		if (_gameplaySettings.FullControlEnabled)
		{
			UpdateDoneButton();
		}
		else if (CanSubmit)
		{
			SubmitCurrentSelections();
		}
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton")
		{
			if (CanSubmit)
			{
				SubmitCurrentSelections();
			}
			else
			{
				UpdateDoneButton();
			}
		}
	}

	private void SubmitCurrentSelections()
	{
		_request.SubmitSelection(_selectedCdcs.Select((DuelScene_CDC x) => x.Model.GrpId));
	}

	public override void CleanUp()
	{
		foreach (string fakeCardId in _fakeCardIds)
		{
			_fakeCardViewController.DeleteFakeCard(fakeCardId);
		}
		_fakeCardIds.Clear();
		_selectableCdcs.Clear();
		_selectedCdcs.Clear();
		base.CleanUp();
	}

	public IEnumerable<DuelScene_CDC> GetSelectableCdcs()
	{
		return _selectableCdcs;
	}

	public IEnumerable<DuelScene_CDC> GetNonSelectableCdcs()
	{
		return Array.Empty<DuelScene_CDC>();
	}

	public Dictionary<DuelScene_CDC, HighlightType> GetBrowserHighlights()
	{
		_cardHighlights.Clear();
		foreach (DuelScene_CDC selectableCdc in _selectableCdcs)
		{
			_cardHighlights[selectableCdc] = HighlightType.Hot;
		}
		foreach (DuelScene_CDC selectedCdc in _selectedCdcs)
		{
			_cardHighlights[selectedCdc] = HighlightType.Selected;
		}
		return _cardHighlights;
	}
}
