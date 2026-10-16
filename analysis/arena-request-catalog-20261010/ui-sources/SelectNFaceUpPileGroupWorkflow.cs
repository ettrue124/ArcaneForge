using System.Collections.Generic;
using GreClient.Rules;
using MovementSystem;
using UnityEngine;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;

internal class SelectNFaceUpPileGroupWorkflow : GroupedBrowserWorkflow<SelectNGroupRequest>
{
	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserController _browserController;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IBrowserHeaderTextProvider _browserHeaderTextProvider;

	private string _pileSelectedButtonKey;

	public SelectNFaceUpPileGroupWorkflow(SelectNGroupRequest request, ICardViewProvider cardViewProvider, ISplineMovementSystem splineMovementSystem, IBrowserController browserController, ICardHolderProvider cardHolderProvider, IBrowserHeaderTextProvider browserHeaderTextProvider)
		: base(request)
	{
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_splineMovementSystem = splineMovementSystem ?? NullSplineMovementSystem.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_cardHolderProvider = cardHolderProvider ?? NullCardHolderProvider.Default;
		_browserHeaderTextProvider = browserHeaderTextProvider ?? NullBrowserHeaderTextProvider.Default;
	}

	protected override void ApplyInteractionInternal()
	{
		_cardsToDisplay = new List<List<DuelScene_CDC>>();
		InitializeButtonStateData();
		for (int i = 0; i < _request.Groups.Count; i++)
		{
			_cardsToDisplay.Add(_cardViewProvider.GetCardViews(_request.Groups[i].Ids));
		}
		SetHeaderAndSubheader();
		SetOpenedBrowser(OpenBrowser());
	}

	private IBrowser OpenBrowser()
	{
		IBrowser browser = _browserController.OpenBrowser(this);
		if (browser is SelectGroupBrowser selectGroupBrowser)
		{
			selectGroupBrowser.SetHeader(_header);
			selectGroupBrowser.SetSubheader(_subHeader);
		}
		return browser;
	}

	private void SetHeaderAndSubheader()
	{
		_browserHeaderTextProvider.ClearParams();
		_header = _browserHeaderTextProvider.GetHeaderText("DuelScene/ClientPrompt/Select_Pile_Title");
		_subHeader = _browserHeaderTextProvider.GetSubHeaderText(_request.Prompt);
	}

	private void InitializeButtonStateData()
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.Enabled = true;
		buttonStateData.LocalizedString = "DuelScene/Browsers/Piles_TurnFaceUp";
		buttonStateData.BrowserElementKey = "GroupAButton";
		buttonStateData.StyleType = ButtonStyle.StyleType.Secondary;
		_buttonStateData.Add("GroupAButton", buttonStateData);
		ButtonStateData buttonStateData2 = new ButtonStateData();
		buttonStateData2.Enabled = true;
		buttonStateData2.LocalizedString = "DuelScene/Browsers/Piles_TurnFaceUp";
		buttonStateData2.BrowserElementKey = "GroupBButton";
		buttonStateData2.StyleType = ButtonStyle.StyleType.Secondary;
		_buttonStateData.Add("GroupBButton", buttonStateData2);
	}

	private void ChangeButtonStateData(string buttonKey)
	{
		foreach (ButtonStateData value in _buttonStateData.Values)
		{
			if (value.BrowserElementKey == buttonKey)
			{
				value.StyleType = ButtonStyle.StyleType.Main;
				value.LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Confirm";
			}
			else
			{
				value.StyleType = ButtonStyle.StyleType.Secondary;
				value.LocalizedString = "DuelScene/Browsers/Piles_TurnFaceUp";
			}
		}
		_openedBrowser.UpdateButtons();
	}

	private void FlipCardPiles(int pileSelected)
	{
		foreach (List<DuelScene_CDC> item in _cardsToDisplay)
		{
			bool flag = pileSelected == _cardsToDisplay.IndexOf(item);
			foreach (DuelScene_CDC item2 in item)
			{
				IdealPoint flipPoint;
				if (flag)
				{
					_splineMovementSystem.RemoveTemporaryGoal(item2.Root);
				}
				else if (TryGetFlipPoint(item2, out flipPoint))
				{
					_splineMovementSystem.AddTemporaryGoal(item2.Root, flipPoint, AllowInteractionType.Never);
				}
			}
		}
	}

	private bool TryGetFlipPoint(DuelScene_CDC cardView, out IdealPoint flipPoint)
	{
		flipPoint = default;
		if (cardView == null || cardView.Root == null)
		{
			return false;
		}
		if (!_cardHolderProvider.TryGetCardHolder(GREPlayerNum.Invalid, CardHolderType.CardBrowserDefault, out CardHolderBase result))
		{
			return false;
		}
		IdealPoint layoutEndpoint = result.GetLayoutEndpoint(cardView);
		flipPoint = new IdealPoint(layoutEndpoint.Position, layoutEndpoint.Rotation * Quaternion.Euler(0f, 180f, 0f), layoutEndpoint.Scale);
		return true;
	}

	public override void Browser_OnButtonPressed(string buttonKey)
	{
		int num = ((!(buttonKey == "GroupAButton")) ? 1 : 0);
		if (_pileSelectedButtonKey == buttonKey)
		{
			OnGroupSelected(num);
			return;
		}
		_pileSelectedButtonKey = buttonKey;
		FlipCardPiles(num);
		ChangeButtonStateData(buttonKey);
	}

	public void OnGroupSelected(int groupIndex)
	{
		_request.SubmitGroupSelection((uint)_request.Groups[groupIndex].GroupId);
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectGroup;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "SelectGroup";
	}
}
