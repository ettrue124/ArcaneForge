using System;
using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class ResolutionCastWorkflow : SelectCardsWorkflow<ActionsAvailableRequest>
{
	private class CardViewSorter : IComparer<DuelScene_CDC>
	{
		public IReadOnlyList<Wotc.Mtgo.Gre.External.Messaging.Action> ActiveActions { private get; set; }

		public int Compare(DuelScene_CDC a, DuelScene_CDC b)
		{
			if (ActiveActions != null)
			{
				bool value = ActiveActions.Find(a.InstanceId, instanceIdComparer) != null;
				int num = (ActiveActions.Find(b.InstanceId, instanceIdComparer) != null).CompareTo(value);
				if (num != 0)
				{
					return num;
				}
				return b.InstanceId.CompareTo(a.InstanceId);
			}
			return 0;
			static bool instanceIdComparer(Wotc.Mtgo.Gre.External.Messaging.Action action, uint instanceId)
			{
				return action.InstanceId == instanceId;
			}
		}
	}

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IActionSubmission _actionSubmission;

	private readonly IActionProcessor _actionProcessor;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly Dictionary<uint, List<GreInteraction>> _actionsByInstanceId = new Dictionary<uint, List<GreInteraction>>(25);

	private readonly List<uint> _zones = new List<uint>();

	private readonly Stack<BrowserBase> _stackedBrowsers = new Stack<BrowserBase>();

	private Wotc.Mtgo.Gre.External.Messaging.Action _passAction;

	private uint _currentZoneId = uint.MaxValue;

	private static CardViewSorter cardViewSorter = new CardViewSorter();

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCardsMultiZone;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "MultiZone";
	}

	public ResolutionCastWorkflow(ActionsAvailableRequest request, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IBrowserManager browserManager, IActionSubmission actionSubmission, IActionProcessor actionProcessor, IBrowserHeaderTextProvider headerTextProvider, AssetLookupSystem assetLookupSystem)
		: base(request)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_browserManager = browserManager;
		_actionSubmission = actionSubmission ?? NullActionSubmission.Default;
		_actionProcessor = actionProcessor ?? NullActionProcessor.Default;
		_headerTextProvider = headerTextProvider;
		_assetLookupSystem = assetLookupSystem;
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState gameState = _gameStateProvider.LatestGameState;
		SetUpZoneInfo(gameState, _request.Actions, _request.InactiveActions);
		SetupActionMaps(gameState, _request.Actions, _request.InactiveActions);
		UpdateSelectableCards(_request.Actions);
		UpdateNonSelectableCards(_request.InactiveActions);
		_cardsToDisplay = ToDisplay(_currentZoneId, gameState, _request.Actions, _request.InactiveActions);
		SetupHeaders();
		UpdateButtonStateData(_currentZoneId, _zones);
		IBrowser openedBrowser = _browserManager.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
		_browserManager.BrowserOpened += OnBrowserOpened;
		_browserManager.BrowserClosed += OnBrowserClosed;
	}

	private void SetUpZoneInfo(MtgGameState gameState, IEnumerable<Wotc.Mtgo.Gre.External.Messaging.Action> actions, IEnumerable<Wotc.Mtgo.Gre.External.Messaging.Action> inactiveActions)
	{
		_zones.Clear();
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action action in actions)
		{
			if (gameState.TryGetCard(action.InstanceId, out var card) && card.Zone != null && !_zones.Contains(card.Zone.Id))
			{
				_zones.Add(card.Zone.Id);
			}
		}
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action inactiveAction in inactiveActions)
		{
			if (gameState.TryGetCard(inactiveAction.InstanceId, out var card2) && card2.Zone != null && !_zones.Contains(card2.Zone.Id))
			{
				_zones.Add(card2.Zone.Id);
			}
		}
		if (_currentZoneId == uint.MaxValue && _zones.Count > 0)
		{
			_currentZoneId = _zones[0];
		}
	}

	private void SetupActionMaps(MtgGameState gameState, IEnumerable<Wotc.Mtgo.Gre.External.Messaging.Action> actions, IEnumerable<Wotc.Mtgo.Gre.External.Messaging.Action> inactiveActions)
	{
		_actionsByInstanceId.Clear();
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action action in actions)
		{
			if (_passAction == null && action.IsActionType(ActionType.Pass))
			{
				_passAction = action;
				continue;
			}
			uint instanceId = action.InstanceId;
			if (instanceId != 0 && action.SelectionType == 0 && action.Selection == 0 && action.GrpId != 2)
			{
				GreInteraction item = new GreInteraction(action);
				if (_actionsByInstanceId.TryGetValue(instanceId, out var value))
				{
					value.Add(item);
					continue;
				}
				_actionsByInstanceId[instanceId] = new List<GreInteraction> { item };
			}
		}
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action inactiveAction in inactiveActions)
		{
			uint instanceId2 = inactiveAction.InstanceId;
			if (instanceId2 != 0 && inactiveAction.SelectionType == 0 && inactiveAction.Selection == 0 && gameState.GetCardById(instanceId2) != null)
			{
				GreInteraction item2 = new GreInteraction(inactiveAction, isActive: false);
				if (_actionsByInstanceId.TryGetValue(instanceId2, out var value2))
				{
					value2.Add(item2);
					continue;
				}
				_actionsByInstanceId[instanceId2] = new List<GreInteraction> { item2 };
			}
		}
	}

	private void UpdateSelectableCards(IEnumerable<Wotc.Mtgo.Gre.External.Messaging.Action> actions)
	{
		selectable.Clear();
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action action in actions)
		{
			uint instanceId = action.InstanceId;
			if (!action.IsActionType(ActionType.Pass) && instanceId != 0 && _cardViewProvider.TryGetCardView(instanceId, out var cardView) && !selectable.Contains(cardView))
			{
				selectable.Add(cardView);
			}
		}
	}

	private void UpdateNonSelectableCards(IEnumerable<Wotc.Mtgo.Gre.External.Messaging.Action> inactiveActions)
	{
		nonSelectable.Clear();
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action inactiveAction in inactiveActions)
		{
			uint instanceId = inactiveAction.InstanceId;
			if (instanceId != 0 && _cardViewProvider.TryGetCardView(instanceId, out var cardView) && !selectable.Contains(cardView) && !nonSelectable.Contains(cardView))
			{
				nonSelectable.Add(cardView);
			}
		}
	}

	private List<DuelScene_CDC> ToDisplay(uint currentZoneId, MtgGameState gameState, IReadOnlyList<Wotc.Mtgo.Gre.External.Messaging.Action> actions, IReadOnlyList<Wotc.Mtgo.Gre.External.Messaging.Action> inactiveActions)
	{
		List<DuelScene_CDC> list = new List<DuelScene_CDC>();
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action action in actions)
		{
			if (!gameState.TryGetCard(action.InstanceId, out var card) || card.Zone == null || card.Zone.Id != currentZoneId || !_cardViewProvider.TryGetCardView(action.InstanceId, out var cardView) || list.Contains(cardView))
			{
				continue;
			}
			if (action.AbilityGrpId == 278)
			{
				CardPrintingData cardPrintingById = _cardDatabase.CardDataProvider.GetCardPrintingById(action.GrpId);
				MtgCardInstance mtgCardInstance = cardPrintingById.CreateInstance();
				mtgCardInstance.InstanceId = action.InstanceId;
				mtgCardInstance.SkinCode = card.SkinCode;
				mtgCardInstance.BaseSkinCode = card.BaseSkinCode;
				mtgCardInstance.Controller = card.Controller;
				mtgCardInstance.Owner = card.Owner;
				mtgCardInstance.Zone = card.Zone;
				CardData cardData = new CardData(mtgCardInstance, cardPrintingById);
				if (cardView.Model.HasPerpetualChanges())
				{
					PerpetualChangeUtilities.CopyPerpetualEffects(cardView.Model, cardData, _cardDatabase.AbilityDataProvider);
				}
				cardView.SetModel(cardData);
			}
			list.Add(cardView);
		}
		foreach (Wotc.Mtgo.Gre.External.Messaging.Action inactiveAction in inactiveActions)
		{
			if (gameState.TryGetCard(inactiveAction.InstanceId, out var card2) && card2.Zone != null && card2.Zone.Id == currentZoneId && _cardViewProvider.TryGetCardView(inactiveAction.InstanceId, out var cardView2) && !list.Contains(cardView2))
			{
				list.Add(cardView2);
			}
		}
		cardViewSorter.ActiveActions = actions;
		list.Sort(cardViewSorter);
		return list;
	}

	private void SetupHeaders()
	{
		if (_request.Actions.Exists((Wotc.Mtgo.Gre.External.Messaging.Action x) => x.IsCastAction()))
		{
			_headerTextProvider.ClearParams();
			_headerTextProvider.SetMinMax(0, 1u);
			_headerTextProvider.SetWorkflow(this);
			_headerTextProvider.SetRequest(_request);
			_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCardsMultiZone);
			_header = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Option_ToCast");
			_subHeader = _headerTextProvider.GetSubHeaderText(_request.Prompt);
			_headerTextProvider.ClearParams();
		}
		else
		{
			string key = ((nonSelectable.Count == 1) ? "DuelScene/Browsers/NoActionsBrowser_Header_Single" : "DuelScene/Browsers/NoActionsBrowser_Header_Plural");
			_header = _cardDatabase.ClientLocProvider.GetLocalizedText(key);
			_subHeader = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/NoActionsBrowser_SubHeader");
		}
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

	public bool CanClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (entity is DuelScene_CDC)
		{
			return _actionsByInstanceId.ContainsKey(entity.InstanceId);
		}
		return false;
	}

	public void OnClick(IEntityView entity, SimpleInteractionType clickType)
	{
		if (_actionsByInstanceId.TryGetValue(entity.InstanceId, out var value))
		{
			_actionProcessor.HandleActions(entity, value);
		}
	}

	private void UpdateButtonStateData(uint currentZoneId, List<uint> zones)
	{
		if (zones.Count > 1)
		{
			_buttonStateData = GenerateMultiZoneButtonStates(0, 1, _request.CancellationType, zones, null, currentZoneId, _gameStateProvider.LatestGameState, _cardDatabase.ClientLocProvider);
		}
		else
		{
			_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, 0, 1, _request.CancellationType);
		}
		if (!_buttonStateData.TryGetValue("DoneButton", out var value))
		{
			return;
		}
		string text = "DuelScene/ClientPrompt/Decline_Action";
		ButtonStyle.StyleType styleType = ButtonStyle.StyleType.Secondary;
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SecondaryButtonTextPayload> loadedTree))
		{
			SecondaryButtonTextPayload payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
			if (payload != null)
			{
				text = payload.LocKey.Key;
			}
		}
		if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SecondaryButtonStylePayload> loadedTree2))
		{
			SecondaryButtonStylePayload payload2 = loadedTree2.GetPayload(_assetLookupSystem.Blackboard);
			if (payload2 != null)
			{
				styleType = payload2.Style;
			}
		}
		value.LocalizedString = text;
		value.StyleType = styleType;
	}

	private void OnBrowserOpened(BrowserBase browser)
	{
		if (_openedBrowser != browser && !(browser is ViewDismissBrowser) && !_stackedBrowsers.Contains(browser))
		{
			_stackedBrowsers.Push(browser);
		}
	}

	private void OnBrowserClosed(BrowserBase browser)
	{
		if (_openedBrowser != browser && !(browser is ViewDismissBrowser) && _stackedBrowsers.Contains(browser) && _stackedBrowsers.Peek() == browser)
		{
			_stackedBrowsers.Pop();
			if (_stackedBrowsers.Count == 0)
			{
				IBrowser openedBrowser = _browserManager.OpenBrowser(this);
				SetOpenedBrowser(openedBrowser);
			}
		}
	}

	public override void CleanUp()
	{
		if (_browserManager != null)
		{
			_browserManager.BrowserOpened -= OnBrowserOpened;
			_browserManager.BrowserClosed -= OnBrowserClosed;
		}
		base.CleanUp();
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton" && _passAction != null)
		{
			_actionSubmission.SubmitAction(_passAction);
		}
		else if (buttonKey.StartsWith("ZoneButton"))
		{
			uint currentZoneId = (_currentZoneId = (uint)Convert.ToInt32(buttonKey.Replace("ZoneButton", string.Empty)));
			_cardsToDisplay = ToDisplay(currentZoneId, _gameStateProvider.LatestGameState, _request.Actions, _request.InactiveActions);
			UpdateButtonStateData(currentZoneId, _zones);
			(_openedBrowser as SelectCardsBrowser_MultiZone).OnZoneUpdated();
		}
	}
}
