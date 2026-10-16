using System;
using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Ability : BrowserWorkflowBase<SelectNRequest>, ISelectCardsBrowserProvider, IBasicBrowserProvider, ICardBrowserProvider, IDuelSceneBrowserProvider, IBrowserHeaderProvider
{
	private readonly Dictionary<DuelScene_CDC, uint> _abilitiesByCardView = new Dictionary<DuelScene_CDC, uint>(3);

	private readonly List<string> _fakeCardIds = new List<string>(3);

	private readonly Dictionary<DuelScene_CDC, HighlightType> _cardHighlights = new Dictionary<DuelScene_CDC, HighlightType>(3);

	private readonly ICardDatabaseAdapter _cardDatabaseAdapter;

	private readonly IAbilityDataProvider _abilityDataProvider;

	private readonly IFakeCardViewController _fakeCardViewController;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBrowserController _duelSceneBrowserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	public bool AllowKeyboardSelection => false;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public SelectNWorkflow_Ability(SelectNRequest request, ICardDatabaseAdapter cardDatabaseAdapter, IAbilityDataProvider abilityDataProvider, IFakeCardViewController fakeCardViewController, IGameStateProvider gameStateProvider, IBrowserController duelSceneBrowserController, IBrowserHeaderTextProvider headerTextProvider)
		: base(request)
	{
		_cardDatabaseAdapter = cardDatabaseAdapter;
		_abilityDataProvider = abilityDataProvider;
		_fakeCardViewController = fakeCardViewController;
		_gameStateProvider = gameStateProvider;
		_duelSceneBrowserController = duelSceneBrowserController;
		_headerTextProvider = headerTextProvider;
	}

	protected override void ApplyInteractionInternal()
	{
		ICardDataAdapter cardDataAdapter = CardDataExtensions.CreateWithDatabase(((MtgGameState)_gameStateProvider.CurrentGameState).GetCardById(_request.SourceId), _cardDatabaseAdapter);
		foreach (uint id in _request.Ids)
		{
			string text = $"SelectAbility_{id}";
			ICardDataAdapter cardData = CardDataExtensions.CreateAbilityCard(_abilityDataProvider.GetAbilityPrintingById(id), cardDataAdapter, _cardDatabaseAdapter);
			DuelScene_CDC duelScene_CDC = _fakeCardViewController.CreateFakeCard(text, cardData, isVisible: true);
			_cardsToDisplay.Add(duelScene_CDC);
			_fakeCardIds.Add(text);
			_abilitiesByCardView[duelScene_CDC] = id;
			_cardHighlights[duelScene_CDC] = HighlightType.Hot;
		}
		SetHeaderAndSubheader(cardDataAdapter);
		SetOpenedBrowser(_duelSceneBrowserController.OpenBrowser(this));
	}

	private void SetHeaderAndSubheader(ICardDataAdapter sourceModel)
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax(_request.MinSel, _request.MaxSel);
		_headerTextProvider.SetSourceModel(sourceModel);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCards);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_request.Prompt);
		_headerTextProvider.ClearParams();
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (_abilitiesByCardView.TryGetValue(cardView, out var value))
		{
			_request.SubmitSelection(value);
		}
	}

	public override void CleanUp()
	{
		foreach (string fakeCardId in _fakeCardIds)
		{
			_fakeCardViewController.DeleteFakeCard(fakeCardId);
		}
		_fakeCardIds.Clear();
		_abilitiesByCardView.Clear();
		_cardHighlights.Clear();
		base.CleanUp();
	}

	public IEnumerable<DuelScene_CDC> GetSelectableCdcs()
	{
		return _abilitiesByCardView.Keys;
	}

	public IEnumerable<DuelScene_CDC> GetNonSelectableCdcs()
	{
		return Array.Empty<DuelScene_CDC>();
	}

	public Dictionary<DuelScene_CDC, HighlightType> GetBrowserHighlights()
	{
		return _cardHighlights;
	}
}
