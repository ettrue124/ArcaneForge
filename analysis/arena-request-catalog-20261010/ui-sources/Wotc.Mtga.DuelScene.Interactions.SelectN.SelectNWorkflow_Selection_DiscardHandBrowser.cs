using System.Collections.Generic;
using System.Linq;
using GreClient.Rules;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Selection_DiscardHandBrowser : SelectCardsWorkflow<SelectNRequest>
{
	private readonly ICardViewProvider _cardViewProvider;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IBrowserController _browserController;

	private const string CARD_QUANTITY_PARAM_STRING = "cardQuantity";

	private const string SUBMIT_COUNT_PARAM_STRING = "submitCount";

	private readonly int _handSize;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public SelectNWorkflow_Selection_DiscardHandBrowser(SelectNRequest request, ICardViewProvider cardViewProvider, IClientLocProvider clientLocProvider, IBrowserController browserController)
		: base(request)
	{
		_cardViewProvider = cardViewProvider;
		_clientLocProvider = clientLocProvider;
		_browserController = browserController;
		_handSize = _request.Ids.Count - _request.MinSel;
	}

	protected override void ApplyInteractionInternal()
	{
		_header = _clientLocProvider.GetLocalizedText("DuelScene/Browsers/Choose_CardsToKeep");
		_subHeader = _clientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/Choose_KeepX_DiscardRest", ("cardQuantity", _handSize.ToString()));
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, _request.MinSel, (int)_request.MaxSel, _request.CancellationType);
		foreach (uint id in _request.Ids)
		{
			if (_cardViewProvider.TryGetCardView(id, out var cardView))
			{
				selectable.Add(cardView);
			}
		}
		_cardsToDisplay.AddRange(selectable);
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}

	protected override Dictionary<string, ButtonStateData> GenerateDefaultButtonStates(int currentSelectionCount, int minSelections, int maxSelections, AllowCancel cancelType)
	{
		_buttonStateData = base.GenerateDefaultButtonStates(currentSelections.Count, _request.MinSel, (int)_request.MaxSel, _request.CancellationType);
		ButtonStateData value = null;
		if (_buttonStateData.TryGetValue("DoneButton", out value))
		{
			value.StyleType = ButtonStyle.StyleType.Secondary;
			value.Enabled = false;
			value.LocalizedString = new MTGALocalizedString
			{
				Key = "DuelScene/ClientPrompt/Submit_N",
				Parameters = new Dictionary<string, string> { 
				{
					"submitCount",
					currentSelections.Count.ToString()
				} }
			};
		}
		return _buttonStateData;
	}

	private void UpdateButtonState()
	{
		ButtonStateData value = null;
		if (_buttonStateData.TryGetValue("DoneButton", out value))
		{
			value.StyleType = ((currentSelections.Count == _handSize) ? ButtonStyle.StyleType.Main : ButtonStyle.StyleType.Secondary);
			value.Enabled = currentSelections.Count == _handSize;
			value.LocalizedString = new MTGALocalizedString
			{
				Key = "DuelScene/ClientPrompt/Submit_N",
				Parameters = new Dictionary<string, string> { 
				{
					"submitCount",
					currentSelections.Count.ToString()
				} }
			};
		}
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		base.CardBrowser_OnCardViewSelected(cardView);
		UpdateButtonState();
		_openedBrowser.UpdateButtons();
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "DoneButton")
		{
			SubmitResponse();
		}
	}

	private void SubmitResponse()
	{
		IEnumerable<uint> second = currentSelections.Select((DuelScene_CDC cardView) => cardView.InstanceId);
		IEnumerable<uint> selections = _request.Ids.Except(second);
		_request.SubmitSelection(selections);
	}
}
