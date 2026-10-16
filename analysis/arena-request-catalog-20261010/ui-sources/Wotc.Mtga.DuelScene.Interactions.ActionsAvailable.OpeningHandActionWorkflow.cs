using System.Collections.Generic;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class OpeningHandActionWorkflow : SelectCardsWorkflow<ActionsAvailableRequest>, IAutoRespondWorkflow
{
	private static readonly Stack<Action> _pendingActions = new Stack<Action>();

	private readonly IClientLocProvider _clientLocProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserManager _browserManager;

	private readonly Dictionary<DuelScene_CDC, List<Action>> _actionsByCdc = new Dictionary<DuelScene_CDC, List<Action>>();

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public OpeningHandActionWorkflow(ActionsAvailableRequest availableActionsDecision, IClientLocProvider clientLocProvider, ICardViewProvider cardViewProvider, IBrowserManager browserManager)
		: base(availableActionsDecision)
	{
		_request = availableActionsDecision;
		_clientLocProvider = clientLocProvider;
		_cardViewProvider = cardViewProvider;
		_browserManager = browserManager;
	}

	public bool TryAutoRespond()
	{
		if (_pendingActions.Count > 0)
		{
			_request.SubmitAction(_pendingActions.Pop());
			return true;
		}
		return false;
	}

	protected override void ApplyInteractionInternal()
	{
		if (_request.CanPass)
		{
			_pendingActions.Push(_request.Actions.Find((Action x) => x.ActionType == ActionType.Pass));
		}
		foreach (Action action in _request.Actions)
		{
			if (action.ActionType == ActionType.OpeningHandAction && _cardViewProvider.TryGetCardView(action.InstanceId, out var cardView))
			{
				if (!_cardsToDisplay.Contains(cardView))
				{
					_cardsToDisplay.Add(cardView);
				}
				if (!selectable.Contains(cardView))
				{
					selectable.Add(cardView);
				}
				if (!currentSelections.Contains(cardView))
				{
					currentSelections.Add(cardView);
				}
				if (_actionsByCdc.TryGetValue(cardView, out var value))
				{
					value.Add(action);
					continue;
				}
				_actionsByCdc[cardView] = new List<Action> { action };
			}
		}
		_header = _clientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/ClientPrompt_OpeningHandAction_Header");
		_subHeader = _clientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/ClientPrompt_OpeningHandAction_SubHeader");
		_buttonStateData = new Dictionary<string, ButtonStateData> { ["DoneButton"] = new ButtonStateData
		{
			LocalizedString = new MTGALocalizedString
			{
				Key = "DuelScene/ClientPrompt/ClientPrompt_Button_Submit"
			},
			BrowserElementKey = "SingleButton",
			StyleType = ButtonStyle.StyleType.Main
		} };
		SetOpenedBrowser(_browserManager.OpenBrowser(this));
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		base.CardBrowser_OnCardViewSelected(cardView);
		ReCalculateButtonStates();
		_browserManager.CurrentBrowser.UpdateButtons();
	}

	private void ReCalculateButtonStates()
	{
		if (currentSelections.Count == selectable.Count)
		{
			_buttonStateData["DoneButton"].LocalizedString.Key = "DuelScene/ClientPrompt/ClientPrompt_Button_Submit";
			_buttonStateData["DoneButton"].StyleType = ButtonStyle.StyleType.Main;
		}
		else if (currentSelections.Count > 1)
		{
			_buttonStateData["DoneButton"].LocalizedString.Key = "DuelScene/ClientPrompt/ClientPrompt_Button_Submit_XActions";
			_buttonStateData["DoneButton"].LocalizedString.Parameters = new Dictionary<string, string> { 
			{
				"number",
				currentSelections.Count.ToString()
			} };
			_buttonStateData["DoneButton"].StyleType = ButtonStyle.StyleType.Main;
		}
		else if (currentSelections.Count == 1)
		{
			_buttonStateData["DoneButton"].LocalizedString.Key = "DuelScene/ClientPrompt/ClientPrompt_Button_Submit_Action";
			_buttonStateData["DoneButton"].StyleType = ButtonStyle.StyleType.Main;
		}
		else if (_request.CanPass)
		{
			_buttonStateData["DoneButton"].LocalizedString.Key = "DuelScene/ClientPrompt/Decline_Action";
			_buttonStateData["DoneButton"].StyleType = ButtonStyle.StyleType.Secondary;
		}
		_browserManager.CurrentBrowser.UpdateButtons();
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey != "DoneButton")
		{
			return;
		}
		if (currentSelections.Count == 0)
		{
			_request.SubmitPass();
			return;
		}
		foreach (DuelScene_CDC currentSelection in currentSelections)
		{
			List<Action> value = null;
			if (!_actionsByCdc.TryGetValue(currentSelection, out value))
			{
				continue;
			}
			foreach (Action item in value)
			{
				_pendingActions.Push(item);
			}
		}
		_request.SubmitAction(_pendingActions.Pop());
	}

	public static void ClearPendingActions()
	{
		_pendingActions.Clear();
	}
}
