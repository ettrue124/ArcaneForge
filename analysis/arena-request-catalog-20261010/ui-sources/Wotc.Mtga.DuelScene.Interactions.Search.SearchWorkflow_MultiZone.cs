using System;
using System.Collections.Generic;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.Search;

public class SearchWorkflow_MultiZone : SelectCardsWorkflow<SearchRequest>, IRoundTripWorkflow
{
	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IGameplaySettingsProvider _gameplaySettings;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserController _browserController;

	private uint _currentZoneId = uint.MaxValue;

	private bool _additionalZoneSubmitted;

	private Dictionary<uint, List<DuelScene_CDC>> _cardViewsByZoneId;

	public override string GetCardHolderLayoutKey()
	{
		return "MultiZone";
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCardsMultiZone;
	}

	public SearchWorkflow_MultiZone(SearchRequest request, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IGameplaySettingsProvider gameplaySettings, ICardViewProvider cardViewProvider, IBrowserController browserController)
		: base(request)
	{
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_gameplaySettings = gameplaySettings;
		_cardViewProvider = cardViewProvider;
		_browserController = browserController;
	}

	protected override void ApplyInteractionInternal()
	{
		_header = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Select_Cards_Title");
		_subHeader = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Select_From_Available_Cards");
		_cardsToDisplay = new List<DuelScene_CDC>();
		currentSelections.Clear();
		selectable.Clear();
		_cardViewsByZoneId = new Dictionary<uint, List<DuelScene_CDC>>();
		UpdateBrowserContentsBasedOnRequest();
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private void UpdateBrowserContentsBasedOnRequest()
	{
		_cardViewsByZoneId.Clear();
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		for (int i = 0; i < _request.ZonesToSearch.Count; i++)
		{
			MtgZone zoneById = mtgGameState.GetZoneById(_request.ZonesToSearch[i]);
			List<DuelScene_CDC> list = new List<DuelScene_CDC>();
			foreach (uint cardId in zoneById.CardIds)
			{
				if (_cardViewProvider.TryGetCardView(cardId, out var cardView))
				{
					list.Add(cardView);
					if (_request.Options.Contains(cardId))
					{
						selectable.Add(cardView);
					}
				}
			}
			list.Sort(new DuelScene_CDC_Comparer(selectable, _cardDatabase.CardTitleProvider));
			_cardViewsByZoneId.Add(zoneById.Id, list);
		}
		_cardsToDisplay = GetCurrentZoneCardViews((_currentZoneId == uint.MaxValue) ? _request.ZonesToSearch[0] : _currentZoneId);
		UpdateButtonStateData();
		if (_openedBrowser != null)
		{
			(_openedBrowser as SelectCardsBrowser_MultiZone).OnZoneUpdated();
		}
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		base.CardBrowser_OnCardViewSelected(cardView);
		if (_gameplaySettings.FullControlDisabled && currentSelections.Count == _request.Max)
		{
			SubmitResponse();
			_openedBrowser.Close();
		}
		UpdateButtonStateData();
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
				UpdateButtonStateData();
				(_openedBrowser as SelectCardsBrowser_MultiZone).OnZoneUpdated();
			}
		}
		else if (buttonKey == "DoneButton")
		{
			SubmitResponse();
		}
		else if (buttonKey == "CancelButton")
		{
			CancelRequest();
		}
	}

	private List<DuelScene_CDC> GetCurrentZoneCardViews(uint currentZoneId)
	{
		return _cardViewsByZoneId[currentZoneId];
	}

	private void SubmitResponse()
	{
		List<uint> list = new List<uint>();
		foreach (DuelScene_CDC currentSelection in currentSelections)
		{
			list.Add(currentSelection.InstanceId);
		}
		_request.SubmitSelection(list);
	}

	private void SubmitZoneResponse(uint zoneId)
	{
		_additionalZoneSubmitted = true;
		_request.SubmitZone(zoneId);
	}

	private void CancelRequest()
	{
		if (_request.CanCancel)
		{
			_request.Cancel();
		}
	}

	private void UpdateButtonStateData()
	{
		if (_currentZoneId == uint.MaxValue)
		{
			_currentZoneId = _request.ZonesToSearch[0];
		}
		_buttonStateData = GenerateMultiZoneButtonStates(_request.Min, (int)_request.Max, _request.CancellationType, _request.ZonesToSearch, _request.AdditionalZones, _currentZoneId, _gameStateProvider.LatestGameState, _cardDatabase.ClientLocProvider);
	}

	public bool CanHandleRequest(BaseUserRequest req)
	{
		return req is SearchRequest;
	}

	public void OnRoundTrip(BaseUserRequest req)
	{
		_request = req as SearchRequest;
		_additionalZoneSubmitted = false;
		UpdateBrowserContentsBasedOnRequest();
	}

	public bool IsWaitingForRoundTrip()
	{
		return _additionalZoneSubmitted;
	}

	public bool CanCleanupAfterOutboundMessage(ClientToGREMessage message)
	{
		return message.Type == ClientMessageType.SearchResp;
	}
}
