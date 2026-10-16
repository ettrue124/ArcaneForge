using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.CardData;
using GreClient.Rules;
using UnityEngine.InputSystem;
using WorkflowVisuals;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Selection_Weighted_Browser : SelectCardsWorkflow<SelectNRequest>, IAutoRespondWorkflow, IKeybindingWorkflow
{
	private class SelectNWeightedHighlightsGenerator : IHighlightsGenerator
	{
		private readonly IReadOnlyCollection<DuelScene_CDC> _nonSelectableCards;

		private readonly IReadOnlyCollection<DuelScene_CDC> _selectableCards;

		private readonly IReadOnlyCollection<DuelScene_CDC> _selectedCards;

		private readonly IReadOnlyCollection<uint> _hotIds;

		public SelectNWeightedHighlightsGenerator(IReadOnlyCollection<DuelScene_CDC> nonSelectableCards, IReadOnlyCollection<DuelScene_CDC> selectableCards, IReadOnlyCollection<DuelScene_CDC> selectedCards, IReadOnlyCollection<uint> hotIds)
		{
			_nonSelectableCards = nonSelectableCards;
			_selectableCards = selectableCards;
			_selectedCards = selectedCards;
			_hotIds = hotIds;
		}

		public Highlights GetHighlights()
		{
			Highlights highlights = new Highlights();
			if (_hotIds.Count == 0)
			{
				foreach (DuelScene_CDC selectableCard in _selectableCards)
				{
					highlights.IdToHighlightType_Workflow[selectableCard.InstanceId] = HighlightType.Hot;
				}
			}
			else
			{
				foreach (DuelScene_CDC selectableCard2 in _selectableCards)
				{
					highlights.IdToHighlightType_Workflow[selectableCard2.InstanceId] = HighlightType.Cold;
				}
				foreach (uint hotId in _hotIds)
				{
					highlights.IdToHighlightType_Workflow[hotId] = HighlightType.Hot;
				}
			}
			foreach (DuelScene_CDC nonSelectableCard in _nonSelectableCards)
			{
				highlights.IdToHighlightType_Workflow[nonSelectableCard.InstanceId] = HighlightType.None;
			}
			foreach (DuelScene_CDC selectedCard in _selectedCards)
			{
				highlights.IdToHighlightType_Workflow[selectedCard.InstanceId] = HighlightType.Selected;
			}
			return highlights;
		}
	}

	private readonly IGameplaySettingsProvider _autoRespManager;

	private readonly Dictionary<uint, int> _idsToWeights = new Dictionary<uint, int>();

	private readonly ICardViewProvider _cardViewProvider;

	private readonly WeightedSelectionState _selectionState;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly IAbilityDataProvider _abilityDataProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private int _currentSelectionWeight;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public SelectNWorkflow_Selection_Weighted_Browser(SelectNRequest request, IGameplaySettingsProvider autoResponseManager, ICardViewProvider cardViewProvider, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider, IAbilityDataProvider abilityDataProvider, IGameStateProvider gameStateProvider, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_autoRespManager = autoResponseManager;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_headerTextProvider = headerTextProvider ?? NullBrowserHeaderTextProvider.Default;
		_abilityDataProvider = abilityDataProvider ?? NullAbilityDataProvider.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_assetLookupSystem = assetLookupSystem;
		_highlightsGenerator = new SelectNWeightedHighlightsGenerator(nonSelectable, selectable, currentSelections, _request.HotIds);
		_selectionState = new WeightedSelectionState(request);
	}

	protected override void ApplyInteractionInternal()
	{
		for (int i = 0; i < _request.Weights.Count; i++)
		{
			_idsToWeights[_request.Ids[i]] = _request.Weights[i];
		}
		_cardsToDisplay = _cardViewProvider.GetCardViews(_request.UnfilteredIds);
		UpdateSelectable();
		UpdateButtonData();
		int cardId = 0;
		foreach (PromptParameter parameter in _request.Prompt.Parameters)
		{
			if (parameter.ParameterName.Equals("CardId") && parameter.Type == ParameterType.Number)
			{
				cardId = parameter.NumberValue;
			}
		}
		ICardDataAdapter headerAndSubheader = null;
		if (_cardViewProvider.TryGetCardView((uint)cardId, out var cardView))
		{
			headerAndSubheader = cardView.Model;
		}
		SetHeaderAndSubheader(headerAndSubheader);
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
		_subHeader = _headerTextProvider.GetSubHeaderText(_prompt);
		_headerTextProvider.ClearParams();
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton")
		{
			SubmitSelections();
		}
		else if (buttonKey == "CancelButton")
		{
			_request.Cancel();
		}
	}

	private void SubmitSelections()
	{
		List<uint> list = new List<uint>();
		foreach (DuelScene_CDC currentSelection in currentSelections)
		{
			list.Add(currentSelection.InstanceId);
		}
		_request.SubmitSelection(list);
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		base.CardBrowser_OnCardViewSelected(cardView);
		if (_autoRespManager.FullControlDisabled && _request.Weights.Count <= 0 && currentSelections.Count == _request.MaxSel)
		{
			SubmitSelections();
			return;
		}
		_currentSelectionWeight = 0;
		foreach (DuelScene_CDC currentSelection in currentSelections)
		{
			_currentSelectionWeight += _idsToWeights[currentSelection.InstanceId];
		}
		UpdateSelectable();
		UpdateButtonData();
		_openedBrowser.UpdateButtons();
	}

	private void UpdateSelectable()
	{
		nonSelectable.Clear();
		selectable.Clear();
		selectable.AddRange(currentSelections);
		foreach (uint unfilteredId in _request.UnfilteredIds)
		{
			if (!_request.Ids.Contains(unfilteredId) && _cardViewProvider.TryGetCardView(unfilteredId, out var cardView))
			{
				nonSelectable.Add(cardView);
			}
		}
		foreach (DuelScene_CDC item in _cardsToDisplay)
		{
			if (!selectable.Contains(item))
			{
				if (CanSelectCard(item))
				{
					selectable.Add(item);
				}
				else
				{
					nonSelectable.Add(item);
				}
			}
		}
		_cardsToDisplay.Sort(SortCardsToDisplay);
	}

	private bool CanSelectCard(DuelScene_CDC cardView)
	{
		if (_idsToWeights.TryGetValue(cardView.InstanceId, out var value))
		{
			return _selectionState.CanSelect(currentSelections.Count + 1, _currentSelectionWeight + value);
		}
		return _selectionState.CanSelect(currentSelections.Count + 1, 0);
	}

	private int SortCardsToDisplay(DuelScene_CDC x, DuelScene_CDC y)
	{
		bool value = selectable.Contains(x);
		int num = selectable.Contains(y).CompareTo(value);
		if (num != 0)
		{
			return num;
		}
		int value2 = _request.UnfilteredIds.IndexOf(x.InstanceId);
		return _request.UnfilteredIds.IndexOf(y.InstanceId).CompareTo(value2);
	}

	private void UpdateButtonData()
	{
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, _request.MinSel, (int)_request.MaxSel, _request.CancellationType);
	}

	protected override Dictionary<string, ButtonStateData> GenerateDefaultButtonStates(int currentSelectionCount, int minSelections, int maxSelections, AllowCancel cancelType)
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.LocalizedString = new MTGALocalizedString
		{
			Key = "DuelScene/ClientPrompt/Submit_N",
			Parameters = new Dictionary<string, string> { 
			{
				"submitCount",
				_currentSelectionWeight.ToString()
			} }
		};
		buttonStateData.Enabled = _selectionState.CanSubmit(currentSelections.Count, _currentSelectionWeight);
		buttonStateData.BrowserElementKey = ((cancelType == AllowCancel.No) ? "SingleButton" : "2Button_Left");
		buttonStateData.StyleType = ButtonStyle.StyleType.Main;
		_buttonStateData.Add("DoneButton", buttonStateData);
		if (cancelType != AllowCancel.No && cancelType != AllowCancel.None)
		{
			ButtonStateData buttonStateData2 = new ButtonStateData();
			buttonStateData2.LocalizedString = ((cancelType == AllowCancel.Abort) ? "DuelScene/ClientPrompt/ClientPrompt_Button_Cancel" : "DuelScene/ClientPrompt/ClientPrompt_Button_FailToFind");
			buttonStateData2.BrowserElementKey = "2Button_Right";
			buttonStateData2.Enabled = true;
			buttonStateData2.StyleType = ButtonStyle.StyleType.Secondary;
			_buttonStateData.Add("CancelButton", buttonStateData2);
			MtgCardInstance resolvingCardInstance = mtgGameState.ResolvingCardInstance;
			if (resolvingCardInstance != null && resolvingCardInstance.ObjectType == GameObjectType.Ability)
			{
				_assetLookupSystem.Blackboard.Clear();
				_assetLookupSystem.Blackboard.Request = _request;
				_assetLookupSystem.Blackboard.Ability = _abilityDataProvider.GetAbilityPrintingById(resolvingCardInstance.GrpId);
				if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SecondaryButtonTextPayload> loadedTree))
				{
					SecondaryButtonTextPayload payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
					if (payload != null)
					{
						buttonStateData2.LocalizedString = payload.LocKey.Key;
					}
				}
			}
		}
		return _buttonStateData;
	}

	public bool TryAutoRespond()
	{
		if (_autoRespManager.FullControlEnabled)
		{
			return false;
		}
		if (_request.Ids.Count == _request.MinSel && _request.Ids.Count == _request.MaxSel)
		{
			_request.SubmitSelection(_request.Ids);
			return true;
		}
		return false;
	}

	public override void TryUndo()
	{
		if (_request.AllowUndo)
		{
			_request.Undo();
		}
	}

	public bool CanKeyUp(Key key)
	{
		return key == Key.Z;
	}

	public void OnKeyUp(Key key)
	{
		if (key == Key.Z)
		{
			TryUndo();
		}
	}

	public bool CanKeyDown(Key key)
	{
		return false;
	}

	public void OnKeyDown(Key key)
	{
	}

	public bool CanKeyHeld(Key key, float holdDuration)
	{
		return false;
	}

	public void OnKeyHeld(Key key, float holdDuration)
	{
	}
}
