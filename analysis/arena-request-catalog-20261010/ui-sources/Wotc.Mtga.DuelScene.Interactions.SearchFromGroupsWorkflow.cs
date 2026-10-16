using System;
using System.Collections.Generic;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class SearchFromGroupsWorkflow : SelectCardsWorkflow<SearchFromGroupsRequest>, IRoundTripWorkflow, IUpdateWorkflow
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IEntityViewProvider _entityViewProvider;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly IBrowserController _browserController;

	private readonly UXEventQueue _eventQueue;

	private GroupNode _rootNode;

	private Dictionary<uint, List<DuelScene_CDC>> cardViewsByZoneId;

	private uint _currentZoneId;

	private bool _additionalZoneSubmitted;

	private bool _waitForUXUpdates;

	private List<uint> selectedIds;

	public override string GetCardHolderLayoutKey()
	{
		return "MultiZone";
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCardsMultiZone;
	}

	public SearchFromGroupsWorkflow(SearchFromGroupsRequest request, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IEntityViewProvider entityViewProvider, IPromptTextProvider promptTextProvider, IBrowserController browserController, UXEventQueue eventQueue)
		: base(request)
	{
		_currentZoneId = uint.MaxValue;
		_request = request;
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_entityViewProvider = entityViewProvider;
		_promptTextProvider = promptTextProvider;
		_browserController = browserController;
		_eventQueue = eventQueue;
	}

	protected override void ApplyInteractionInternal()
	{
		cardViewsByZoneId = new Dictionary<uint, List<DuelScene_CDC>>();
		_cardsToDisplay = new List<DuelScene_CDC>();
		selectedIds = new List<uint>();
		selectable.Clear();
		UpdateBrowserContentsBasedOnRequest();
		_header = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Select_Title");
		_subHeader = _promptTextProvider.GetPromptText(_request.Prompt);
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private void UpdateBrowserContentsBasedOnRequest()
	{
		_rootNode = new GroupNode(_request);
		cardViewsByZoneId.Clear();
		foreach (uint selectedId in selectedIds)
		{
			_rootNode.Branch(selectedId);
		}
		List<uint> selectableIds = _rootNode.SelectableIds;
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		for (int i = 0; i < _request.ZonesToSearch.Count; i++)
		{
			MtgZone zoneById = mtgGameState.GetZoneById(_request.ZonesToSearch[i]);
			List<DuelScene_CDC> list = new List<DuelScene_CDC>();
			foreach (uint cardId in zoneById.CardIds)
			{
				if (_entityViewProvider.TryGetCardView(cardId, out var cardView))
				{
					list.Add(cardView);
					if (selectableIds.Contains(cardId))
					{
						selectable.Add(cardView);
					}
					else
					{
						nonSelectable.Add(cardView);
					}
				}
			}
			list.Sort(new DuelScene_CDC_Comparer(selectable, _cardDatabase.CardTitleProvider));
			cardViewsByZoneId.Add(zoneById.Id, list);
		}
		if (_request.ZonesToSearch.Count > 0)
		{
			_cardsToDisplay = GetCurrentZoneCardViews(_request.ZonesToSearch[0]);
		}
		UpdateButtonStateData();
		if (_openedBrowser != null)
		{
			(_openedBrowser as SelectCardsBrowser_MultiZone).OnZoneUpdated();
		}
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (_additionalZoneSubmitted)
		{
			return;
		}
		if (buttonKey.StartsWith("ZoneButton"))
		{
			uint currentZoneId = (uint)Convert.ToInt32(buttonKey.Replace("ZoneButton", string.Empty));
			Debug.Log("Pressed Zone Button with Id " + currentZoneId);
			_currentZoneId = currentZoneId;
			if (_request.AdditionalZones.Contains(_currentZoneId))
			{
				SubmitZoneResponse(_currentZoneId);
				_openedBrowser.UpdateButtons();
			}
			else
			{
				_cardsToDisplay = GetCurrentZoneCardViews(_currentZoneId);
				UpdateSelectable();
				UpdateButtonStateData();
				(_openedBrowser as SelectCardsBrowser_MultiZone).OnZoneUpdated();
			}
		}
		if (buttonKey == "DoneButton")
		{
			OnSubmitPressed();
		}
		else if (buttonKey == "CancelButton")
		{
			OnCancelPressed();
		}
	}

	private void SubmitZoneResponse(uint zoneId)
	{
		_additionalZoneSubmitted = true;
		_waitForUXUpdates = true;
		selectedIds.Clear();
		selectedIds.AddRange(_rootNode.SelectedIds);
		_request.SubmitZone(zoneId);
	}

	private void OnSubmitPressed()
	{
		if (_rootNode.CanSubmit)
		{
			_request.SubmitSelection(_rootNode.Submit());
		}
	}

	private void OnCancelPressed()
	{
		_request.Cancel();
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (!nonSelectable.Contains(cardView))
		{
			uint instanceId = cardView.Model.InstanceId;
			if (_rootNode.SelectedIds.Contains(instanceId))
			{
				_rootNode.Prune(instanceId);
			}
			else if (_rootNode.SelectableIds.Contains(instanceId))
			{
				_rootNode.Branch(instanceId);
			}
			UpdateSelectable();
			UpdateButtonStateData();
			_openedBrowser.UpdateButtons();
		}
	}

	private void UpdateSelectable()
	{
		selectable.Clear();
		nonSelectable.Clear();
		currentSelections.Clear();
		bool flag = _request.MaxSelections == uint.MaxValue || _rootNode.SelectedIds.Count < _request.MaxSelections;
		foreach (DuelScene_CDC item in _cardsToDisplay)
		{
			uint instanceId = item.InstanceId;
			if (_rootNode.SelectedIds.Contains(instanceId))
			{
				currentSelections.Add(item);
				selectable.Add(item);
			}
			else if (flag && _rootNode.SelectableIds.Contains(instanceId))
			{
				selectable.Add(item);
			}
			else
			{
				nonSelectable.Add(item);
			}
		}
	}

	public List<DuelScene_CDC> GetCurrentZoneCardViews(uint currentZoneId)
	{
		return cardViewsByZoneId[currentZoneId];
	}

	private void UpdateButtonStateData()
	{
		if (_currentZoneId == uint.MaxValue)
		{
			_currentZoneId = ((_request.ZonesToSearch.Count > 0) ? _request.ZonesToSearch[0] : uint.MaxValue);
		}
		_buttonStateData = GenerateMultiZoneButtonStates((int)_request.MinSelections, (int)_request.MaxSelections, _request.CancellationType, _request.ZonesToSearch, _request.AdditionalZones, _currentZoneId, _gameStateProvider.LatestGameState, _cardDatabase.ClientLocProvider);
		if (_buttonStateData.TryGetValue("DoneButton", out var value))
		{
			if (_rootNode.SelectedIds.Count == 0 && _request.MaxSelections != 0)
			{
				value.LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Decline";
			}
			else
			{
				value.LocalizedString = "DuelScene/ClientPrompt/Submit_N";
				value.LocalizedString.Parameters = new Dictionary<string, string> { 
				{
					"submitCount",
					_rootNode.SelectedIds.Count.ToString()
				} };
			}
			bool flag = value.Enabled;
			switch (_request.AllowFailToFind)
			{
			case AllowFailToFind.None:
			case AllowFailToFind.Any:
				flag = _rootNode.SelectedIds.Count >= _request.MinSelections && _rootNode.SelectedIds.Count <= _request.MaxSelections;
				break;
			case AllowFailToFind.Zero:
				flag = _rootNode.SelectedIds.Count == 0 || (_rootNode.SelectedIds.Count >= _request.MinSelections && _rootNode.SelectedIds.Count <= _request.MaxSelections);
				break;
			}
			value.StyleType = ((flag && _rootNode.SelectedIds.Count == _request.MinSelections) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
			value.Enabled = flag;
		}
	}

	public bool CanHandleRequest(BaseUserRequest req)
	{
		return req is SearchFromGroupsRequest;
	}

	public void OnRoundTrip(BaseUserRequest req)
	{
		_request = req as SearchFromGroupsRequest;
		_additionalZoneSubmitted = false;
	}

	public bool IsWaitingForRoundTrip()
	{
		return _additionalZoneSubmitted;
	}

	public bool CanCleanupAfterOutboundMessage(ClientToGREMessage message)
	{
		if (message.Type == ClientMessageType.SearchFromGroupsResp)
		{
			return message.SearchFromGroupsResp.AddZoneToSearchScope == 0;
		}
		return false;
	}

	public void Update()
	{
		if (_waitForUXUpdates && !_additionalZoneSubmitted && _eventQueue.PendingEvents.Count <= 0 && _eventQueue.RunningEvents.Count <= 0)
		{
			UpdateBrowserContentsBasedOnRequest();
			_waitForUXUpdates = false;
		}
	}
}
