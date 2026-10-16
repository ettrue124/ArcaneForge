using System;
using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_AnchorWord : BrowserWorkflowBase<SelectNRequest>, ISelectCardsBrowserProvider, IBasicBrowserProvider, ICardBrowserProvider, IDuelSceneBrowserProvider, IBrowserHeaderProvider
{
	private readonly Dictionary<DuelScene_CDC, uint> _anchorWordByCardView = new Dictionary<DuelScene_CDC, uint>();

	private readonly List<string> _fakeCardIds = new List<string>();

	private readonly Dictionary<DuelScene_CDC, HighlightType> _cardHighlights = new Dictionary<DuelScene_CDC, HighlightType>();

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IAbilityDataProvider _abilityDataProvider;

	private readonly IFakeCardViewController _fakeCardViewController;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IBrowserController _browserController;

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

	public SelectNWorkflow_AnchorWord(SelectNRequest request, IContext context)
		: base(request)
	{
		_cardDatabase = context.Get<ICardDatabaseAdapter>() ?? NullCardDatabaseAdapter.Default;
		_abilityDataProvider = context.Get<IAbilityDataProvider>() ?? NullAbilityDataProvider.Default;
		_fakeCardViewController = context.Get<IFakeCardViewController>() ?? NullFakeCardViewController.Default;
		_gameStateProvider = context.Get<IGameStateProvider>() ?? NullGameStateProvider.Default;
		_browserController = context.Get<IBrowserController>() ?? NullBrowserController.Default;
		_headerTextProvider = context.Get<IBrowserHeaderTextProvider>() ?? NullBrowserHeaderTextProvider.Default;
	}

	public override BrowserCardHeader.BrowserCardHeaderData GetCardHeaderData(DuelScene_CDC cardView)
	{
		if (_anchorWordByCardView.TryGetValue(cardView, out var value))
		{
			string localizedText = _cardDatabase.GreLocProvider.GetLocalizedText(value);
			return new BrowserCardHeader.BrowserCardHeaderData(string.Empty, localizedText);
		}
		return base.GetCardHeaderData(cardView);
	}

	protected override void ApplyInteractionInternal()
	{
		ICardDataAdapter cardDataAdapter = CardDataExtensions.CreateWithDatabase(((MtgGameState)_gameStateProvider.LatestGameState).GetCardById(_request.SourceId), _cardDatabase);
		List<uint> ids = _request.Ids;
		List<uint> parameterPromptIds = _request.ReqPrompt.Parameters.Select((PromptParameter x) => (uint)x.PromptId).ToList();
		ids = ids.OrderBy((uint x) => parameterPromptIds.IndexOf(x)).ToList();
		for (int num = 0; num < ids.Count; num++)
		{
			uint value = ids[num];
			int index = ((cardDataAdapter.Abilities.Count > num) ? (num + 1) : num);
			uint id = cardDataAdapter.Abilities[index].Id;
			string text = $"SelectAnchorWord_{id}";
			ICardDataAdapter cardData = CardDataExtensions.CreateAbilityCard(_abilityDataProvider.GetAbilityPrintingById(id), cardDataAdapter, _cardDatabase);
			DuelScene_CDC duelScene_CDC = _fakeCardViewController.CreateFakeCard(text, cardData, isVisible: true);
			_cardsToDisplay.Add(duelScene_CDC);
			_fakeCardIds.Add(text);
			_anchorWordByCardView[duelScene_CDC] = value;
			_cardHighlights[duelScene_CDC] = HighlightType.Hot;
		}
		SetHeaderAndSubheader(cardDataAdapter);
		SetOpenedBrowser(_browserController.OpenBrowser(this));
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
		if (_anchorWordByCardView.TryGetValue(cardView, out var value))
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
		_anchorWordByCardView.Clear();
		_cardHighlights.Clear();
		base.CleanUp();
	}

	public IEnumerable<DuelScene_CDC> GetSelectableCdcs()
	{
		return _anchorWordByCardView.Keys;
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
