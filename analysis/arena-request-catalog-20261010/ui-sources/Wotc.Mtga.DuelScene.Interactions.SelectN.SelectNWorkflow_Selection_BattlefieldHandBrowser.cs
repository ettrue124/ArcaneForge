using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflow_Selection_BattlefieldHandBrowser : SelectCardsWorkflow<SelectNRequest>
{
	private readonly ICardViewProvider _cardViewProvider;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private List<DuelScene_CDC> _handList = new List<DuelScene_CDC>();

	private List<DuelScene_CDC> _battlefieldList = new List<DuelScene_CDC>();

	public List<DuelScene_CDC> HandCardList => _handList;

	public List<DuelScene_CDC> BattlefieldCardList => _battlefieldList;

	private bool CanSubmit
	{
		get
		{
			if (currentSelections.Count >= _request.MinSel)
			{
				return currentSelections.Count <= _request.MaxSel;
			}
			return false;
		}
	}

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCardsBattlefieldHand;
	}

	public SelectNWorkflow_Selection_BattlefieldHandBrowser(SelectNRequest request, ICardViewProvider cardViewProvider, IClientLocProvider clientLocProvider, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider, IGameStateProvider gameStateProvider, ICardDatabaseAdapter cardDatabase)
		: base(request)
	{
		_cardViewProvider = cardViewProvider;
		_clientLocProvider = clientLocProvider;
		_browserController = browserController;
		_headerTextProvider = headerTextProvider;
		_gameStateProvider = gameStateProvider;
		_cardDatabase = cardDatabase;
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
		_subHeader = _headerTextProvider.GetSubHeaderText(_request.Prompt);
		_headerTextProvider.ClearParams();
	}

	public string GetHandHeader()
	{
		return _clientLocProvider.GetLocalizedText("Enum/ZoneType/ZoneType_Hand");
	}

	public string GetBattlefieldHeader()
	{
		return _clientLocProvider.GetLocalizedText("Enum/ZoneType/ZoneType_Battlefield");
	}

	protected override void ApplyInteractionInternal()
	{
		_cardsToDisplay = new List<DuelScene_CDC>();
		foreach (uint id in _request.Ids)
		{
			if (_cardViewProvider.TryGetCardView(id, out var cardView))
			{
				selectable.Add(cardView);
				if (cardView.Model.ZoneType == ZoneType.Hand)
				{
					_handList.Add(cardView);
				}
				else if (cardView.Model.ZoneType == ZoneType.Battlefield)
				{
					_battlefieldList.Add(cardView);
				}
			}
		}
		_cardsToDisplay.AddRange(_battlefieldList);
		_cardsToDisplay.AddRange(_handList);
		ICardDataAdapter headerAndSubheader = _gameStateProvider.LatestGameState.Value.GetCardById(_request.SourceId).ToCardData(_cardDatabase);
		SetHeaderAndSubheader(headerAndSubheader);
		UpdateButtonData();
		IBrowser openedBrowser = _browserController.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}

	protected override Dictionary<string, ButtonStateData> GenerateDefaultButtonStates(int currentSelectionCount, int minSelections, int maxSelections, AllowCancel cancelType)
	{
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.LocalizedString = new MTGALocalizedString
		{
			Key = "DuelScene/ClientPrompt/Submit_N",
			Parameters = new Dictionary<string, string> { 
			{
				"submitCount",
				currentSelectionCount.ToString()
			} }
		};
		buttonStateData.Enabled = CanSubmit;
		buttonStateData.BrowserElementKey = "2Button_Left";
		buttonStateData.StyleType = ButtonStyle.StyleType.Secondary;
		_buttonStateData.Add("SubmitButton", buttonStateData);
		if (cancelType != AllowCancel.No && cancelType != AllowCancel.None)
		{
			ButtonStateData buttonStateData2 = new ButtonStateData();
			buttonStateData2.LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_Decline";
			buttonStateData2.BrowserElementKey = "2Button_Right";
			buttonStateData2.Enabled = true;
			buttonStateData2.StyleType = ButtonStyle.StyleType.Secondary;
			_buttonStateData.Add("CancelButton", buttonStateData2);
		}
		return _buttonStateData;
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "SubmitButton")
		{
			_request.SubmitSelection(currentSelections.Select((DuelScene_CDC x) => x.Model.InstanceId));
		}
		else if (buttonKey == "CancelButton")
		{
			_request.Cancel();
		}
	}

	private void UpdateButtonData()
	{
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, _request.MinSel, (int)_request.MaxSel, _request.CancellationType);
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		base.CardBrowser_OnCardViewSelected(cardView);
		UpdateButtonData();
		_openedBrowser.UpdateButtons();
	}
}
