using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectPrintingIndexWorkflow : BrowserWorkflowBase<SelectNRequest>, ISelectCardsBrowserProvider, IBasicBrowserProvider, ICardBrowserProvider, IDuelSceneBrowserProvider, IBrowserHeaderProvider
{
	private const string FAKE_CARD_ID_FORMAT = "SelectPrintingIndex_{0}";

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IFakeCardViewController _fakeCardController;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly List<string> _fakeCardIds = new List<string>(3);

	private readonly List<DuelScene_CDC> _selectableCdcs = new List<DuelScene_CDC>();

	private readonly List<DuelScene_CDC> _selectedCdcs = new List<DuelScene_CDC>();

	private readonly Dictionary<DuelScene_CDC, HighlightType> _cardHighlights = new Dictionary<DuelScene_CDC, HighlightType>(3);

	private Dictionary<DuelScene_CDC, uint> _fakeCardToIdMapping = new Dictionary<DuelScene_CDC, uint>();

	private List<uint> _selectedIds = new List<uint>();

	public bool AllowKeyboardSelection => false;

	private bool CanSubmit
	{
		get
		{
			if (_request.MinSel <= _selectedIds.Count)
			{
				return _selectedIds.Count <= _request.MaxSel;
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

	public SelectPrintingIndexWorkflow(SelectNRequest request, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IFakeCardViewController fakeCardViewController, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider)
		: base(request)
	{
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_fakeCardController = fakeCardViewController;
		_browserController = browserController;
		_headerTextProvider = headerTextProvider;
	}

	protected override void ApplyInteractionInternal()
	{
		GenerateFakeCards();
		SetUpCancelButtonData();
		SetHeaderAndSubheader(SourceCard(_request.SourceId, _gameStateProvider.LatestGameState, _cardDatabase));
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private void SetHeaderAndSubheader(ICardDataAdapter sourceModel)
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax(MinSelectForHeader(_request.CancellationType), _request.MaxSel);
		_headerTextProvider.SetSourceModel(sourceModel);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCards);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_request.Prompt);
		_headerTextProvider.ClearParams();
	}

	private int MinSelectForHeader(AllowCancel cancelType)
	{
		if (cancelType != AllowCancel.Continue)
		{
			return _request.MinSel;
		}
		return 0;
	}

	private void GenerateFakeCards()
	{
		foreach (var item3 in IdToGrpIdMappings(_request))
		{
			uint item = item3.Item1;
			uint item2 = item3.Item2;
			string text = $"SelectPrintingIndex_{item2}";
			CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(item2);
			DuelScene_CDC duelScene_CDC = _fakeCardController.CreateFakeCard(text, cardPrintingById.ConvertToCardModel(), isVisible: true);
			_cardsToDisplay.Add(duelScene_CDC);
			_selectableCdcs.Add(duelScene_CDC);
			_fakeCardIds.Add(text);
			_fakeCardToIdMapping[duelScene_CDC] = item;
		}
	}

	private void SetUpCancelButtonData()
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		if (_request.CanCancel)
		{
			string text = ((_request.CancellationType == AllowCancel.Continue) ? "DuelScene/ClientPrompt/Decline_Action" : "DuelScene/Browsers/Browser_CancelText");
			_buttonStateData["CancelButton"] = new ButtonStateData
			{
				Enabled = true,
				BrowserElementKey = "CancelButton",
				LocalizedString = text,
				StyleType = ButtonStyle.StyleType.Secondary
			};
		}
	}

	private static ICardDataAdapter SourceCard(uint sourceId, MtgGameState gameState, ICardDatabaseAdapter cardDatabase)
	{
		if (gameState.TryGetCard(sourceId, out var card))
		{
			return CardDataExtensions.CreateWithDatabase(card, cardDatabase);
		}
		return null;
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (_fakeCardToIdMapping.TryGetValue(cardView, out var value))
		{
			if (_selectedIds.Contains(value))
			{
				_selectedCdcs.Remove(cardView);
				_selectedIds.Remove(value);
			}
			else
			{
				_selectedCdcs.Add(cardView);
				_selectedIds.Add(value);
			}
			if (CanSubmit)
			{
				SubmitCurrentSelections();
			}
		}
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "CancelButton")
		{
			_request.Cancel();
		}
	}

	private void SubmitCurrentSelections()
	{
		_request.SubmitSelection(_selectedIds);
	}

	public override void CleanUp()
	{
		while (_fakeCardIds.Count > 0)
		{
			_fakeCardController.DeleteFakeCard(_fakeCardIds[0]);
			_fakeCardIds.RemoveAt(0);
		}
		_buttonStateData?.Clear();
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

	private static IEnumerable<(uint, uint)> IdToGrpIdMappings(SelectNRequest request)
	{
		if (request == null || request.ReqPrompt == null)
		{
			yield break;
		}
		foreach (var item in IdToGrpIdMappings(request.Ids, request.ReqPrompt.Parameters))
		{
			yield return item;
		}
	}

	private static IEnumerable<(uint, uint)> IdToGrpIdMappings(IReadOnlyList<uint> ids, IReadOnlyList<PromptParameter> promptParams)
	{
		if (!IdsAndPromptsAreMismatched(ids, promptParams))
		{
			for (int i = 0; i < ids.Count; i++)
			{
				yield return (ids[i], (uint)promptParams[i].PromptId);
			}
		}
	}

	private static bool IdsAndPromptsAreMismatched(IReadOnlyCollection<uint> ids, IReadOnlyCollection<PromptParameter> promptParams)
	{
		if (ids != null && promptParams != null)
		{
			return ids.Count != promptParams.Count;
		}
		return false;
	}
}
