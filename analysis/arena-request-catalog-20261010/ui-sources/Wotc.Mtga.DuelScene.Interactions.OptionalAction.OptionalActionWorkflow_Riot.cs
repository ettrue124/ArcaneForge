using System.Collections.Generic;
using System.Linq;
using AssetLookupTree.Blackboard;
using GreClient.CardData;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;

namespace Wotc.Mtga.DuelScene.Interactions.OptionalAction;

public class OptionalActionWorkflow_Riot : OptionalActionBrowserWorkflow
{
	private const int RIOT_ABILITY_ID = 175;

	private bool cardHasHaste;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserController _browserController;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.Riot;
	}

	public OptionalActionWorkflow_Riot(OptionalActionMessageRequest request, ICardDatabaseAdapter cardDatabase, ICardBuilder<DuelScene_CDC> cardBuilder, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IBrowserController browserController)
		: base(request)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_cardBuilder = cardBuilder ?? NullCardBuilder<DuelScene_CDC>.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_browserController = browserController ?? NullBrowserController.Default;
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		_cardsToDisplay = new List<DuelScene_CDC>();
		if (!_cardViewProvider.TryGetCardView(_request.SourceId, out var cardView))
		{
			if (!mtgGameState.VisibleCards.TryGetValue(_request.SourceId, out var value))
			{
				value = MtgCardInstance.UnknownCardData(_request.SourceId, mtgGameState.Battlefield);
				Debug.LogErrorFormat("No source card with id {0} found in latest GameState for Riot browser.", _request.SourceId);
			}
			cardView = _cardBuilder.CreateCDC(value.ToCardData(_cardDatabase));
		}
		_cardsToDisplay.Add(cardView);
		AbilityPrintingData abilityPrintingData = _cardsToDisplay[0].Model.Abilities.FirstOrDefault((AbilityPrintingData x) => x.Id == 9);
		cardHasHaste = abilityPrintingData != null;
		_header = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Riot_BrowserHeader");
		_subHeader = _cardDatabase.ClientLocProvider.GetLocalizedText("DuelScene/Browsers/Riot_BrowserSubHeader");
		SetupButtons("DuelScene/Browsers/Riot_BrowserButton_Counter", "DuelScene/Browsers/Riot_BrowserButton_Haste", yesOnRight: true, ButtonStyle.StyleType.Secondary, cardHasHaste ? ButtonStyle.StyleType.Tepid : ButtonStyle.StyleType.Secondary);
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	public override void SetFxBlackboardData(IBlackboard bb)
	{
		base.SetFxBlackboardData(bb);
		if (_cardsToDisplay.Count > 0)
		{
			bb.SetCardDataExtensive(_cardsToDisplay[0].Model);
		}
		else
		{
			bb.SetCardDataRaw(null);
		}
		bb.Ability = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(175u);
	}

	public override void SetFxBlackboardDataForCard(DuelScene_CDC cardView, IBlackboard bb)
	{
		base.SetFxBlackboardDataForCard(cardView, bb);
		bb.Ability = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(175u);
	}
}
