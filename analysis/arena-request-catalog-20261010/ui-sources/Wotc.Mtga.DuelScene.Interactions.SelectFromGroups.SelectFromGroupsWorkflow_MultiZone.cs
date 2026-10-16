using System;
using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;

namespace Wotc.Mtga.DuelScene.Interactions.SelectFromGroups;

public class SelectFromGroupsWorkflow_MultiZone : SelectCardsWorkflow<SelectFromGroupsRequest>
{
	private readonly IClientLocProvider _locProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private uint _currentZoneId = uint.MaxValue;

	private readonly Dictionary<uint, List<DuelScene_CDC>> _cardViewsByZoneId = new Dictionary<uint, List<DuelScene_CDC>>();

	private List<uint> _lockedIds = new List<uint>();

	private GroupNode _rootNode;

	public override string GetCardHolderLayoutKey()
	{
		return "MultiZone";
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCardsMultiZone;
	}

	public SelectFromGroupsWorkflow_MultiZone(SelectFromGroupsRequest request, IClientLocProvider locProvider, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider)
		: base(request)
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		_locProvider = locProvider;
		_gameStateProvider = gameStateProvider;
		_cardViewProvider = cardViewProvider;
		_browserController = browserController;
		_headerTextProvider = headerTextProvider;
	}

	protected override void ApplyInteractionInternal()
	{
		_lockedIds = SelectFromGroupsRequest.GetUniqueGroupIds(_request.Groups);
		_rootNode = new GroupNode(_request);
		foreach (uint lockedId in _lockedIds)
		{
			_rootNode.Branch(lockedId);
		}
		UpdateBrowserContentsBasedOnRequest(_gameStateProvider.LatestGameState);
		SetHeaderAndSubheader();
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax((int)_request.MinSelections, _request.MaxSelections);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCardsMultiZone);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_prompt);
		_headerTextProvider.ClearParams();
	}

	private void UpdateBrowserContentsBasedOnRequest(MtgGameState gameState)
	{
		_cardViewsByZoneId.Clear();
		uint? num = null;
		foreach (uint zoneId in _request.ZoneIds)
		{
			if (!num.HasValue)
			{
				num = zoneId;
			}
			_cardViewsByZoneId.Add(zoneId, new List<DuelScene_CDC>());
		}
		foreach (uint id2 in _rootNode.Ids)
		{
			uint id = gameState.GetCardById(id2).Zone.Id;
			if (!num.HasValue)
			{
				num = id;
			}
			DuelScene_CDC cardView = _cardViewProvider.GetCardView(id2);
			if (!_cardViewsByZoneId.ContainsKey(id))
			{
				_cardViewsByZoneId.Add(id, new List<DuelScene_CDC>());
			}
			_cardViewsByZoneId[id].Add(cardView);
		}
		foreach (uint key in _cardViewsByZoneId.Keys)
		{
			MtgZone zone = gameState.GetZoneById(key);
			_cardViewsByZoneId[key].Sort((DuelScene_CDC lhs, DuelScene_CDC rhs) =>
			{
				int num2 = zone.CardIds.IndexOf(lhs.InstanceId);
				int value = zone.CardIds.IndexOf(rhs.InstanceId);
				return num2.CompareTo(value);
			});
		}
		_currentZoneId = num.Value;
		_cardsToDisplay = GetCurrentZoneCardViews(_currentZoneId);
		UpdateButtonStateData();
		UpdateSelectable();
		if (_openedBrowser is SelectCardsBrowser_MultiZone selectCardsBrowser_MultiZone)
		{
			selectCardsBrowser_MultiZone.OnZoneUpdated();
		}
	}

	private List<DuelScene_CDC> GetCurrentZoneCardViews(uint currentZoneId)
	{
		return _cardViewsByZoneId[currentZoneId];
	}

	private void UpdateButtonStateData()
	{
		_buttonStateData.Clear();
		SelectCardsWorkflow<SelectFromGroupsRequest>.AddZoneButtonsToDictionary(_buttonStateData, _request.ZoneIds, new List<uint>(), _currentZoneId, _gameStateProvider.LatestGameState, _locProvider);
		bool canSubmit = _rootNode.CanSubmit;
		ButtonStateData value = new ButtonStateData
		{
			BrowserElementKey = "DoneButton",
			Enabled = canSubmit,
			LocalizedString = "DuelScene/KeywordSelection/KeywordSelection_Done",
			StyleType = ButtonStyle.StyleType.Main
		};
		_buttonStateData.Add("DoneButton", value);
	}

	private void UpdateSelectable()
	{
		selectable.Clear();
		nonSelectable.Clear();
		currentSelections.Clear();
		foreach (DuelScene_CDC item in _cardsToDisplay)
		{
			if (_cardViewsByZoneId[_currentZoneId].Contains(item))
			{
				uint instanceId = item.InstanceId;
				if (_rootNode.SelectedIds.Contains(instanceId))
				{
					currentSelections.Add(item);
					selectable.Add(item);
				}
				else if (_rootNode.SelectableIds.Contains(instanceId))
				{
					selectable.Add(item);
				}
				else
				{
					nonSelectable.Add(item);
				}
			}
		}
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		uint instanceId = cardView.InstanceId;
		if (!_lockedIds.Contains(instanceId))
		{
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

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey.StartsWith("ZoneButton"))
		{
			uint currentZoneId = (uint)Convert.ToInt32(buttonKey.Replace("ZoneButton", string.Empty));
			_currentZoneId = currentZoneId;
			_cardsToDisplay = GetCurrentZoneCardViews(_currentZoneId);
			UpdateSelectable();
			UpdateButtonStateData();
			(_openedBrowser as SelectCardsBrowser_MultiZone).OnZoneUpdated();
		}
		else if (buttonKey == "DoneButton")
		{
			_request.Submit(_rootNode.Submit());
		}
		else if (buttonKey == "CancelButton")
		{
			_request.Cancel();
		}
	}
}
