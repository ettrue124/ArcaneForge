using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.Search;

public class SearchWorkflow : SelectCardsWorkflow<SearchRequest>
{
	private readonly ICardTitleProvider _cardTitleProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IGameplaySettingsProvider _gameplaySettings;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private List<uint> _selected = new List<uint>();

	private List<uint> _contextOptions = new List<uint>();

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Search";
	}

	public SearchWorkflow(SearchRequest request, ICardTitleProvider cardTitleProvider, IGameStateProvider gameStateProvider, IGameplaySettingsProvider gameplaySettings, ICardViewProvider cardViewProvider, IBrowserManager browserManager, IBrowserHeaderTextProvider headerTextProvider)
		: base(request)
	{
		_cardTitleProvider = cardTitleProvider;
		_gameStateProvider = gameStateProvider;
		_gameplaySettings = gameplaySettings;
		_cardViewProvider = cardViewProvider;
		_browserManager = browserManager;
		_headerTextProvider = headerTextProvider;
	}

	protected override void ApplyInteractionInternal()
	{
		SetHeaderAndSubheader();
		_buttonStateData = GenerateDefaultButtonStates(_selected.Count, _request.Min, (int)_request.Max, _request.CancellationType);
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_contextOptions = new List<uint>(_request.ContextOptions);
		foreach (uint item in _request.ZonesToSearch)
		{
			foreach (uint cardId in mtgGameState.GetZoneById(item).CardIds)
			{
				if (!_contextOptions.Contains(cardId))
				{
					_contextOptions.Add(cardId);
				}
			}
		}
		foreach (uint option in _request.Options)
		{
			if (_cardViewProvider.TryGetCardView(option, out var cardView))
			{
				selectable.Add(cardView);
			}
		}
		foreach (uint contextOption in _contextOptions)
		{
			if (!_request.Options.Contains(contextOption) && _cardViewProvider.TryGetCardView(contextOption, out var cardView2))
			{
				nonSelectable.Add(cardView2);
			}
		}
		_cardsToDisplay.AddRange(selectable);
		_cardsToDisplay.AddRange(nonSelectable);
		_cardsToDisplay.Sort(new DuelScene_CDC_Comparer(selectable, _cardTitleProvider));
		SetOpenedBrowser(_browserManager.OpenBrowser(this));
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax(_request.Min, _request.Max);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCards);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_request.Prompt);
		_headerTextProvider.ClearParams();
	}

	protected override Dictionary<string, ButtonStateData> GenerateDefaultButtonStates(int currentSelectionCount, int minSelections, int maxSelections, AllowCancel cancelType)
	{
		Dictionary<string, ButtonStateData> dictionary = base.GenerateDefaultButtonStates(currentSelectionCount, minSelections, maxSelections, cancelType);
		ButtonStateData value = null;
		if (dictionary.TryGetValue("DoneButton", out value))
		{
			int num = currentSelectionCount;
			int min = _request.Min;
			uint max = _request.Max;
			bool num2 = num == 0 && min == 0;
			bool flag = num < min && min != 0;
			bool flag2 = num >= min && num < max;
			bool flag3 = num == max;
			if (num2)
			{
				value.StyleType = ButtonStyle.StyleType.Secondary;
				value.LocalizedString = "DuelScene/ClientPrompt/Decline_Action";
			}
			else if (flag)
			{
				value.StyleType = ButtonStyle.StyleType.Tepid;
				value.LocalizedString = new MTGALocalizedString
				{
					Key = "DuelScene/ClientPrompt/Submit_N",
					Parameters = new Dictionary<string, string> { 
					{
						"submitCount",
						num.ToString()
					} }
				};
			}
			else if (flag2)
			{
				value.StyleType = ButtonStyle.StyleType.Secondary;
				value.LocalizedString = new MTGALocalizedString
				{
					Key = "DuelScene/ClientPrompt/Submit_N",
					Parameters = new Dictionary<string, string> { 
					{
						"submitCount",
						num.ToString()
					} }
				};
			}
			else if (flag3)
			{
				value.StyleType = ButtonStyle.StyleType.Main;
				value.LocalizedString = new MTGALocalizedString
				{
					Key = "DuelScene/ClientPrompt/Submit_N",
					Parameters = new Dictionary<string, string> { 
					{
						"submitCount",
						num.ToString()
					} }
				};
			}
			bool enabled = num >= min && num <= max;
			value.Enabled = enabled;
		}
		return dictionary;
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (CanClick(cardView, SimpleInteractionType.Primary))
		{
			OnClick(cardView, SimpleInteractionType.Primary);
		}
		else
		{
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_invalid.EventName, cardView.gameObject);
		}
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton")
		{
			SubmitResponse();
		}
		else if (buttonKey == "CancelButton")
		{
			CancelRequest();
		}
	}

	private void SubmitResponse()
	{
		_request.SubmitSelection(_selected);
	}

	private void CancelRequest()
	{
		if (_request.CanCancel)
		{
			_request.Cancel();
		}
	}

	private bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		DuelScene_CDC duelScene_CDC = entity as DuelScene_CDC;
		if (duelScene_CDC == null)
		{
			return false;
		}
		uint instanceId = duelScene_CDC.InstanceId;
		if (!_request.Options.Contains(instanceId))
		{
			return false;
		}
		if (!_selected.Contains(instanceId))
		{
			return _selected.Count < _request.Max;
		}
		return true;
	}

	private void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		uint instanceId = entity.InstanceId;
		if (_selected.Contains(instanceId))
		{
			_selected.Remove(instanceId);
		}
		else
		{
			_selected.Add(instanceId);
		}
		int count = _selected.Count;
		int min = _request.Min;
		int max = (int)_request.Max;
		uint sourceId = _request.OriginalMessage.SearchReq.SourceId;
		if (CurrentSelection(sourceId, (uint)min, (uint)max, (uint)count, _gameStateProvider.LatestGameState).CanAutoSubmit() && _gameplaySettings.FullControlDisabled)
		{
			SubmitResponse();
			return;
		}
		_buttonStateData = GenerateDefaultButtonStates(_selected.Count, min, max, _request.CancellationType);
		_browserManager.CurrentBrowser.UpdateButtons();
		currentSelections.Clear();
		selectable.Clear();
		foreach (uint item in _selected)
		{
			if (_cardViewProvider.TryGetCardView(item, out var cardView) && !currentSelections.Contains(cardView))
			{
				selectable.Add(cardView);
				currentSelections.Add(cardView);
			}
		}
		if (count >= max)
		{
			return;
		}
		foreach (uint option in _request.Options)
		{
			if (_cardViewProvider.TryGetCardView(option, out var cardView2) && !selectable.Contains(cardView2))
			{
				selectable.Add(cardView2);
			}
		}
	}

	public override Dictionary<DuelScene_CDC, HighlightType> GetBrowserHighlights()
	{
		browserHighlights.Clear();
		foreach (DuelScene_CDC item in selectable)
		{
			browserHighlights[item] = (HasMaxCount() ? HighlightType.Hot : HighlightType.None);
		}
		foreach (DuelScene_CDC item2 in nonSelectable)
		{
			browserHighlights[item2] = HighlightType.None;
		}
		foreach (DuelScene_CDC currentSelection in currentSelections)
		{
			browserHighlights[currentSelection] = HighlightType.Selected;
		}
		return browserHighlights;
	}

	private bool HasMaxCount()
	{
		SearchRequest request = _request;
		if (request == null)
		{
			return false;
		}
		return request.Max != 0;
	}
}
