using System.Collections.Generic;
using System.Linq;
using AssetLookupTree;
using AssetLookupTree.Payloads.Prefab;
using GreClient.Rules;
using TMPro;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.Mulligan;

public class MulliganWorkflow : BrowserWorkflowBase<MulliganRequest>, IUpdateWorkflow
{
	private readonly DuelSceneLogger _logger;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IBrowserController _browserController;

	private readonly ICanvasRootProvider _canvasRootProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly CardPreviewAnimation _cardPreviewAnimation;

	private bool _decisionMade;

	private bool _keepClicked;

	private MulliganRulesView _mulliganRulesView;

	private MulliganBrowser _mulliganBrowser => (MulliganBrowser)_openedBrowser;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.Mulligan;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Mulligan";
	}

	public MulliganWorkflow(MulliganRequest request, ICardViewManager cardViewManager, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IBrowserController browserController, ICanvasRootProvider canvasRootProvider, AssetLookupSystem assetLookupSystem, CardPreviewAnimation cardPreviewAnimation, GameManager gameManager)
		: base(request)
	{
		_gameStateProvider = gameStateProvider;
		_cardViewProvider = cardViewManager;
		_cardDatabase = cardDatabase;
		_browserController = browserController;
		_assetLookupSystem = assetLookupSystem;
		_canvasRootProvider = canvasRootProvider;
		_cardPreviewAnimation = cardPreviewAnimation;
		_logger = gameManager.Logger;
		AudioManager.PlayAudio(WwiseEvents.sfx_basicloc_draw_card_multi, AudioManager.Default);
	}

	public override void CleanUp()
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		if (_decisionMade && !_keepClicked && mtgGameState.LocalHand.CardIds.Count > 1)
		{
			_closeBrowserOnCleanup = false;
		}
		if (_mulliganBrowser != null)
		{
			_mulliganBrowser.KeepPressed -= OnKeepPressed;
			_mulliganBrowser.MulliganPressed -= OnMulliganPressed;
			_mulliganBrowser.RulesPressed -= OnRulesPressed;
		}
		OnRulesClosePressed(_mulliganRulesView);
		_cardPreviewAnimation.CleanUp();
		base.CleanUp();
	}

	protected override void OnBrowserOpened()
	{
		MtgGameState mtgGameState = _gameStateProvider.CurrentGameState;
		GREPlayerNum clientPlayerEnum = mtgGameState.ActivePlayer.ClientPlayerEnum;
		_mulliganBrowser.HeaderText = ((clientPlayerEnum == GREPlayerNum.LocalPlayer) ? Languages.ActiveLocProvider.GetLocalizedText("DuelScene/StartingPlayer/Player_First_No_Arrow") : Languages.ActiveLocProvider.GetLocalizedText("DuelScene/StartingPlayer/Opponent_First_No_Arrow"));
		bool flag = mtgGameState.Opponent.PendingMessageType != ClientMessageType.MulliganResp;
		uint count = (uint)mtgGameState.OpponentHand.CardIds.Count;
		if (flag)
		{
			_mulliganBrowser.SubheaderText = Languages.ActiveLocProvider.GetLocalizedText((count == 1) ? "DuelScene/StartingPlayer/OpponentKeepSingular" : "DuelScene/StartingPlayer/OpponentKeepPlural", ("count", count.ToString()));
		}
		else
		{
			uint mulliganCount = mtgGameState.Opponent.MulliganCount;
			uint freeMulliganCount = _request.FreeMulliganCount;
			if (mulliganCount == 0)
			{
				_mulliganBrowser.SubheaderText = Languages.ActiveLocProvider.GetLocalizedText("DuelScene/StartingPlayer/OpponentWaiting");
			}
			else if (count - ComputeNonfreeMulligansTaken(mulliganCount, freeMulliganCount) == 0)
			{
				_mulliganBrowser.SubheaderText = Languages.ActiveLocProvider.GetLocalizedText((mulliganCount == 1) ? "DuelScene/StartingPlayer/OpponentMulliganAllSingular" : "DuelScene/StartingPlayer/OpponentMulliganAllPlural", ("count", mulliganCount.ToString()));
			}
			else
			{
				_mulliganBrowser.SubheaderText = Languages.ActiveLocProvider.GetLocalizedText((mulliganCount == 1) ? "DuelScene/StartingPlayer/OpponentMulliganSingular" : "DuelScene/StartingPlayer/OpponentMulliganPlural", ("count", mulliganCount.ToString()));
			}
		}
		uint num = ((!_request.FreeMulligan) ? (ComputeNonfreeMulligansTaken(_request.MulliganCount, _request.FreeMulliganCount) + 1) : 0u);
		if (num == 0)
		{
			_mulliganBrowser.MulliganConsequenceText = string.Empty;
		}
		else
		{
			_mulliganBrowser.MulliganConsequenceText = Languages.ActiveLocProvider.GetLocalizedText((num == 1) ? "DuelScene/StartingPlayer/LondonMulliganConsequenceSingular" : "DuelScene/StartingPlayer/LondonMulliganConsequencePlural", ("count", num.ToString()));
		}
		_mulliganBrowser.KeepPressed += OnKeepPressed;
		_mulliganBrowser.MulliganPressed += OnMulliganPressed;
		_mulliganBrowser.RulesPressed += OnRulesPressed;
		_cardPreviewAnimation.Play(_cardsToDisplay);
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState currentState = _gameStateProvider.CurrentGameState;
		_buttonStateData = new Dictionary<string, ButtonStateData>();
		ButtonStateData buttonStateData = new ButtonStateData();
		buttonStateData.Enabled = true;
		buttonStateData.BrowserElementKey = "KeepButton";
		uint num = getCardKeepCount();
		if (num == 1)
		{
			buttonStateData.LocalizedString = "DuelScene/StartingPlayer/KeepSingular";
		}
		else
		{
			buttonStateData.LocalizedString = "DuelScene/StartingPlayer/KeepPlural";
		}
		buttonStateData.LocalizedString.Parameters = new Dictionary<string, string> { 
		{
			"count",
			num.ToString()
		} };
		buttonStateData.StyleType = ButtonStyle.StyleType.Main;
		_buttonStateData.Add("KeepButton", buttonStateData);
		ButtonStateData buttonStateData2 = new ButtonStateData();
		buttonStateData2.Enabled = true;
		buttonStateData2.BrowserElementKey = "MulliganButton";
		buttonStateData2.LocalizedString = getMulliganButtonLoc();
		buttonStateData2.StyleType = ButtonStyle.StyleType.Secondary;
		_buttonStateData.Add("MulliganButton", buttonStateData2);
		_cardsToDisplay = _cardViewProvider.GetCardViews(currentState.LocalHand.CardIds);
		bool flag = false;
		foreach (DuelScene_CDC item in _cardsToDisplay)
		{
			if (item.Model.IsDisplayedFaceDown)
			{
				Debug.LogError("Error! Facedown card in opening hand: ");
				Utilities.FieldLogger.LogFields(item.Model, "ICardDataAdapter");
				flag = true;
			}
		}
		if (flag)
		{
			Utilities.FieldLogger.LogFields(_gameStateProvider.LatestGameState, "GameState");
		}
		SortCards(_cardsToDisplay, _cardDatabase.CardTitleProvider);
		SetOpenedBrowser(_browserController.OpenBrowser(this));
		uint getCardKeepCount()
		{
			uint count = (uint)currentState.LocalHand.CardIds.Count;
			if (_request.FreeMulligan)
			{
				return count;
			}
			if (_request.MulliganType == MulliganType.London)
			{
				return count - ComputeNonfreeMulligansTaken(_request.MulliganCount, _request.FreeMulliganCount);
			}
			return count;
		}
		string getMulliganButtonLoc()
		{
			if (_request.FreeMulligan)
			{
				return "DuelScene/StartingPlayer/FreeMulligan_ButtonText";
			}
			return "DuelScene/StartingPlayer/Mulligan";
		}
	}

	public static void SortCards(List<DuelScene_CDC> cardList, ICardTitleProvider cardTitleProvider)
	{
		cardList.Sort((DuelScene_CDC x, DuelScene_CDC y) =>
		{
			bool flag = x.Model.CardTypes.Contains(CardType.Land);
			bool flag2 = y.Model.CardTypes.Contains(CardType.Land);
			if (flag && !flag2)
			{
				return -1;
			}
			if (flag2 && !flag)
			{
				return 1;
			}
			IReadOnlyList<CardColor> getFrameColors = x.Model.GetFrameColors;
			IReadOnlyList<CardColor> getFrameColors2 = y.Model.GetFrameColors;
			if (getFrameColors.Count > 0 && getFrameColors2.Count > 0 && getFrameColors[0] != getFrameColors2[0])
			{
				return getFrameColors[0].CompareTo(getFrameColors2[0]);
			}
			return (x.Model.ConvertedManaCost != y.Model.ConvertedManaCost) ? x.Model.ConvertedManaCost.CompareTo(y.Model.ConvertedManaCost) : cardTitleProvider.GetCardTitle(x.Model).CompareTo(cardTitleProvider.GetCardTitle(y.Model));
		});
	}

	private static uint ComputeNonfreeMulligansTaken(uint takenMulliganCount, uint freeMulliganCount)
	{
		uint result = 0u;
		if (takenMulliganCount > freeMulliganCount)
		{
			result = takenMulliganCount - freeMulliganCount;
		}
		return result;
	}

	private void OnKeepPressed()
	{
		_decisionMade = true;
		_keepClicked = true;
		_logger.UpdateStartingHand(_gameStateProvider.CurrentGameState.Value.LocalHand.VisibleCards.Select((MtgCardInstance card) => card.BaseGrpId).ToList());
		_request.KeepHand();
		OnSubmit();
	}

	private void OnMulliganPressed()
	{
		_decisionMade = true;
		_keepClicked = false;
		_logger?.OnMulligan();
		_request.MulliganHand();
		OnSubmit();
	}

	private void OnRulesPressed()
	{
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.CardBrowserElementID = "MulliganRules";
		BrowserElementPrefab payload = _assetLookupSystem.TreeLoader.LoadTree<BrowserElementPrefab>(returnNewTree: false).GetPayload(_assetLookupSystem.Blackboard);
		if (payload == null || payload.PrefabPath == null)
		{
			return;
		}
		GameObject gameObject = AssetLoader.Instantiate(payload.PrefabPath, _canvasRootProvider.GetCanvasRoot(CanvasLayer.Overlay));
		if ((object)gameObject != null)
		{
			MulliganRulesView component = gameObject.GetComponent<MulliganRulesView>();
			if ((object)component != null)
			{
				_mulliganRulesView = component;
				_mulliganRulesView.ClosePressed += OnRulesClosePressed;
				string localizedText = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/StartingPlayer/LondonRules", ("quantity", _request.StartingHandSize.ToString()));
				gameObject.GetComponentInChildren<TextMeshProUGUI>().text = localizedText;
			}
		}
	}

	private static void OnRulesClosePressed(MulliganRulesView rulesView)
	{
		if ((bool)rulesView)
		{
			rulesView.ClosePressed -= OnRulesClosePressed;
			if ((bool)rulesView.gameObject)
			{
				Object.Destroy(rulesView.gameObject);
			}
		}
	}

	private void OnSubmit()
	{
		_cardPreviewAnimation.CleanUp();
	}

	public void Update()
	{
		_cardPreviewAnimation.Update(Time.deltaTime);
	}
}
