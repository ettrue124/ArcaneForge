using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Payloads.DuelScene.Interactions;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class ActionsAvailableWorkflow_Browser : SelectCardsWorkflow<ActionsAvailableRequest>, IYieldWorkflow
{
	private readonly IActionSubmission _actionSubmission;

	private readonly IActionProcessor _actionProcessor;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly DuelSceneLogger _logger;

	private Action _passAction;

	private readonly Dictionary<uint, List<GreInteraction>> _actionsByInstanceId = new Dictionary<uint, List<GreInteraction>>(25);

	private readonly Stack<BrowserBase> _stackedBrowsers = new Stack<BrowserBase>();

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public ActionsAvailableWorkflow_Browser(ActionsAvailableRequest request, IActionSubmission actionSubmission, IActionProcessor actionProcessor, IClientLocProvider clientLocProvider, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IBrowserManager browserManager, IBrowserHeaderTextProvider headerTextProvider, AssetLookupSystem assetLookupSystem, DuelSceneLogger dsLogger)
		: base(request)
	{
		_actionSubmission = actionSubmission;
		_actionProcessor = actionProcessor;
		_clientLocProvider = clientLocProvider;
		_gameStateProvider = gameStateProvider;
		_cardViewProvider = cardViewProvider;
		_browserManager = browserManager;
		_headerTextProvider = headerTextProvider;
		_assetLookupSystem = assetLookupSystem;
		_logger = dsLogger;
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_logger.UpdateActionsPerPhaseStep(mtgGameState.CurrentPhase, mtgGameState.CurrentStep);
		selectable.Clear();
		nonSelectable.Clear();
		_actionsByInstanceId.Clear();
		foreach (Action action in _request.Actions)
		{
			if (_passAction == null && action.IsActionType(ActionType.Pass))
			{
				_passAction = action;
				continue;
			}
			uint instanceId = action.InstanceId;
			if (instanceId == 0)
			{
				continue;
			}
			if (_cardViewProvider.TryGetCardView(instanceId, out var cardView) && !selectable.Contains(cardView))
			{
				selectable.Add(cardView);
			}
			GreInteraction item = new GreInteraction(action);
			if (action.GrpId != 2 && action.SelectionType == 0 && action.Selection == 0)
			{
				List<GreInteraction> value = null;
				if (_actionsByInstanceId.TryGetValue(instanceId, out value))
				{
					value.Add(item);
					continue;
				}
				_actionsByInstanceId[instanceId] = new List<GreInteraction> { item };
			}
		}
		foreach (Action inactiveAction in _request.InactiveActions)
		{
			uint instanceId2 = inactiveAction.InstanceId;
			if (instanceId2 == 0 || mtgGameState.GetCardById(instanceId2) == null)
			{
				continue;
			}
			if (_cardViewProvider.TryGetCardView(instanceId2, out var cardView2) && !selectable.Contains(cardView2) && !nonSelectable.Contains(cardView2))
			{
				nonSelectable.Add(cardView2);
			}
			GreInteraction item2 = new GreInteraction(inactiveAction, isActive: false);
			if (inactiveAction.SelectionType == 0 && inactiveAction.Selection == 0)
			{
				List<GreInteraction> value2 = null;
				if (_actionsByInstanceId.TryGetValue(instanceId2, out value2))
				{
					value2.Add(item2);
					continue;
				}
				_actionsByInstanceId[instanceId2] = new List<GreInteraction> { item2 };
			}
		}
		_cardsToDisplay = new List<DuelScene_CDC>();
		_cardsToDisplay.AddRange(selectable);
		_cardsToDisplay.AddRange(nonSelectable);
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, 0, 1, _request.CancellationType);
		if (_buttonStateData.TryGetValue("DoneButton", out var value3))
		{
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
			value3.LocalizedString = text;
			value3.StyleType = styleType;
		}
		SetHeaderText();
		SetOpenedBrowser(_browserManager.OpenBrowser(this));
		_browserManager.BrowserOpened += OnBrowserOpened;
		_browserManager.BrowserClosed += OnBrowserClosed;
	}

	private void SetHeaderText()
	{
		_headerTextProvider.SetMinMax(0, 1u);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCards);
		bool flag = _request.Actions.Exists((Action x) => x.ActionType == ActionType.Cast);
		bool flag2 = _request.Actions.Exists((Action x) => x.ActionType == ActionType.Play);
		if (flag & flag2)
		{
			_header = _clientLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Option_ToPlayOrCast");
		}
		else if (flag)
		{
			_header = _clientLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Option_ToCast");
		}
		else if (flag2)
		{
			_header = _clientLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_Option_ToPlay");
		}
		else
		{
			_header = _headerTextProvider.GetHeaderText();
		}
		_subHeader = _headerTextProvider.GetSubHeaderText(_prompt);
		_headerTextProvider.ClearParams();
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton" && _passAction != null)
		{
			_actionSubmission.SubmitAction(_passAction);
		}
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

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (_actionsByInstanceId.TryGetValue(cardView.InstanceId, out var value))
		{
			_actionProcessor.HandleActions(cardView, value);
		}
		else
		{
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_invalid.EventName, cardView.gameObject);
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

	public void OnAutoYieldEnabled()
	{
		if (AppliedState == InteractionAppliedState.Applied && _passAction != null)
		{
			_actionSubmission.SubmitAction(_passAction);
		}
	}
}
