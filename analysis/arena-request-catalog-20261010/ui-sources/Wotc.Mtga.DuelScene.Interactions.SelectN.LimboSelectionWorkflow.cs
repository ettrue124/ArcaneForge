using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class LimboSelectionWorkflow : SelectCardsWorkflow<SelectNRequest>
{
	private string FAKE_CARD_KEY_FORMAT = "FAKE_LIMBO_SELECT_BROWSER_CARD_{0}";

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IGameplaySettingsProvider _gameplaySettings;

	private readonly IFakeCardViewController _fakeCardController;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly Dictionary<DuelScene_CDC, uint> _fakeCardMappings = new Dictionary<DuelScene_CDC, uint>();

	private readonly Dictionary<DuelScene_CDC, uint> _cardMappings = new Dictionary<DuelScene_CDC, uint>();

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public LimboSelectionWorkflow(SelectNRequest request, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IGameplaySettingsProvider gameplaySettings, IFakeCardViewController fakeCardController, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider, ICardViewProvider cardViewProvider)
		: base(request)
	{
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_gameplaySettings = gameplaySettings;
		_fakeCardController = fakeCardController;
		_browserController = browserController;
		_headerTextProvider = headerTextProvider;
		_cardViewProvider = cardViewProvider;
	}

	protected override void ApplyInteractionInternal()
	{
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, 1, 1, _request.CancellationType);
		SetHeaderAndSubheader();
		PopulateCardsInBrowser(_request.Ids, _gameStateProvider.LatestGameState);
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

	private void PopulateCardsInBrowser(IEnumerable<uint> ids, MtgGameState gameState)
	{
		ClearCardCollections();
		foreach (uint id in ids)
		{
			MtgCardInstance card;
			if (_cardViewProvider.TryGetCardView(id, out var cardView))
			{
				selectable.Add(cardView);
				_cardsToDisplay.Add(cardView);
				_cardMappings[cardView] = id;
			}
			else if (gameState.TryGetCard(id, out card))
			{
				DuelScene_CDC duelScene_CDC = CreateFakeCard(card);
				selectable.Add(duelScene_CDC);
				_cardsToDisplay.Add(duelScene_CDC);
				_fakeCardMappings[duelScene_CDC] = id;
			}
		}
	}

	private DuelScene_CDC CreateFakeCard(MtgCardInstance cardInstance)
	{
		ICardDataAdapter cardData = CardDataExtensions.CreateWithDatabase(cardInstance, _cardDatabase);
		return _fakeCardController.CreateFakeCard(string.Format(FAKE_CARD_KEY_FORMAT, cardInstance.InstanceId), cardData);
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
		return selectable.Contains(card);
	}

	private void OnClick(DuelScene_CDC card)
	{
		base.CardBrowser_OnCardViewSelected(card);
		if (ShouldSubmit())
		{
			_request.SubmitSelection(GetSelectedIds());
			return;
		}
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, _request.MinSel, (int)_request.MaxSel, _request.CancellationType);
		_openedBrowser.UpdateButtons();
	}

	private bool ShouldSubmit()
	{
		if (_request.MinSel == 1 && _request.MaxSel == 1 && currentSelections.Count == 1)
		{
			return true;
		}
		if (_request.MaxSel == currentSelections.Count && _gameplaySettings.FullControlDisabled)
		{
			return true;
		}
		return false;
	}

	private IEnumerable<uint> GetSelectedIds()
	{
		foreach (DuelScene_CDC currentSelection in currentSelections)
		{
			if (_fakeCardMappings.TryGetValue(currentSelection, out var value))
			{
				yield return value;
			}
			else if (_cardMappings.TryGetValue(currentSelection, out value))
			{
				yield return value;
			}
		}
	}

	private void ClearCardCollections()
	{
		selectable.Clear();
		nonSelectable.Clear();
		currentSelections.Clear();
		_cardsToDisplay.Clear();
		foreach (KeyValuePair<DuelScene_CDC, uint> fakeCardMapping in _fakeCardMappings)
		{
			_fakeCardController.DeleteFakeCard(string.Format(FAKE_CARD_KEY_FORMAT, fakeCardMapping.Value));
		}
		_fakeCardMappings.Clear();
		_cardMappings.Clear();
	}

	protected override Dictionary<string, ButtonStateData> GenerateDefaultButtonStates(int currentSelectionCount, int minSelections, int maxSelections, AllowCancel cancelType)
	{
		if (displayButtons(_request))
		{
			return base.GenerateDefaultButtonStates(currentSelectionCount, minSelections, maxSelections, cancelType);
		}
		return new Dictionary<string, ButtonStateData>();
		static bool displayButtons(SelectNRequest selectN)
		{
			if (selectN.CanCancel)
			{
				return true;
			}
			if (selectN.MinSel == 1)
			{
				return selectN.MaxSel != 1;
			}
			return true;
		}
	}

	public override void CleanUp()
	{
		ClearCardCollections();
	}
}
