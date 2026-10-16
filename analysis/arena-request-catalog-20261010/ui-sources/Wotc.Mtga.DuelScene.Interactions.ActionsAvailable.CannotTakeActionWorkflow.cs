using System.Collections.Generic;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class CannotTakeActionWorkflow : SelectCardsWorkflow<ActionsAvailableRequest>
{
	private const string INACTIVE_ACTION_FORMAT = "INACTIVE ACTION FAKE CARD {0}";

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IFakeCardViewController _fakeCardController;

	private readonly IBrowserController _browserController;

	private readonly DuelSceneLogger _logger;

	private readonly HashSet<uint> _failedToCastFakeCards = new HashSet<uint>();

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public CannotTakeActionWorkflow(ActionsAvailableRequest request, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IFakeCardViewController fakeCardController, IBrowserController browserController, DuelSceneLogger logger)
		: base(request)
	{
		_cardDatabase = cardDatabase;
		_gameStateProvider = gameStateProvider;
		_cardViewProvider = cardViewProvider;
		_fakeCardController = fakeCardController;
		_browserController = browserController;
		_logger = logger;
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_logger.UpdateActionsPerPhaseStep(mtgGameState.CurrentPhase, mtgGameState.CurrentStep);
		nonSelectable.Clear();
		foreach (Action inactiveAction in _request.InactiveActions)
		{
			uint instanceId = inactiveAction.InstanceId;
			MtgCardInstance value;
			if (_cardViewProvider.TryGetCardView(instanceId, out var cardView))
			{
				nonSelectable.Add(cardView);
			}
			else if (!_failedToCastFakeCards.Contains(instanceId) && !inactiveAction.IsCastSplitCardAction() && !inactiveAction.IsCastAdventureAction() && !inactiveAction.IsCastRoomAction() && mtgGameState.AllCards.TryGetValue(instanceId, out value))
			{
				_failedToCastFakeCards.Add(instanceId);
				ICardDataAdapter cardDataAdapter = new CardData(value, _cardDatabase.CardDataProvider.GetCardPrintingById(inactiveAction.GrpId));
				if (_cardViewProvider.TryGetCardView(value.ParentId, out var cardView2) && cardView2.Model.HasPerpetualChanges())
				{
					PerpetualChangeUtilities.CopyPerpetualEffects(cardView2.Model, cardDataAdapter, _cardDatabase.AbilityDataProvider);
				}
				nonSelectable.Add(_fakeCardController.CreateFakeCard($"INACTIVE ACTION FAKE CARD {instanceId.ToString()}", cardDataAdapter));
			}
		}
		_cardsToDisplay = new List<DuelScene_CDC>();
		_cardsToDisplay.AddRange(nonSelectable);
		_buttonStateData = GenerateDefaultButtonStates(currentSelections.Count, 0, 1, _request.CancellationType);
		MtgCardInstance topCardOnStack = mtgGameState.GetTopCardOnStack();
		if (topCardOnStack != null && topCardOnStack.GrpId == 103346 && _buttonStateData.ContainsKey("DoneButton"))
		{
			_buttonStateData["DoneButton"].LocalizedString = "DuelScene/ClientPrompt/ClientPrompt_Button_DealDamage";
		}
		_header = ((nonSelectable.Count == 1) ? Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/NoActionsBrowser_Header_Single") : Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/NoActionsBrowser_Header_Plural"));
		_subHeader = Languages.ActiveLocProvider.GetLocalizedText("DuelScene/Browsers/NoActionsBrowser_SubHeader");
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	public override void CleanUp()
	{
		foreach (uint failedToCastFakeCard in _failedToCastFakeCards)
		{
			_fakeCardController.DeleteFakeCard($"INACTIVE ACTION FAKE CARD {failedToCastFakeCard.ToString()}");
		}
		_failedToCastFakeCards.Clear();
		base.CleanUp();
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
		_logger?.PriorityPassed();
		_request.SubmitPass();
	}
}
