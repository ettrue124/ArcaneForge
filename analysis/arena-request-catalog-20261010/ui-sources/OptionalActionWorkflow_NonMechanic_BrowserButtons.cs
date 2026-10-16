using AssetLookupTree.Payloads.Browser.Metadata;
using AssetLookupTree.Payloads.Browser.OptionalAction;
using GreClient.CardData;
using GreClient.Rules;
using UnityEngine;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

public class OptionalActionWorkflow_NonMechanic_BrowserButtons : OptionalActionBrowserWorkflow
{
	private readonly SpecialBrowserLayout _layoutInfo;

	private DuelScene_CDC _createdCdc;

	private readonly ICardDataProvider _cardDataProvider;

	private readonly ICardTitleProvider _cardTitleProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IFakeCardViewController _fakeCardViewController;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly IGreLocProvider _greLocProvider;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IBrowserManager _browserManager;

	public OptionalActionWorkflow_NonMechanic_BrowserButtons(OptionalActionMessageRequest request, SpecialBrowserLayout layoutInfo, ICardDataProvider cardDataProvider, ICardTitleProvider cardTitleProvider, ICardViewProvider cardViewProvider, IGameStateProvider gameStateProvider, IFakeCardViewController fakeCardViewController, IPromptTextProvider promptTextProvider, IGreLocProvider greLocProvider, IClientLocProvider clientLocProvider, IBrowserManager browserManager)
		: base(request)
	{
		_layoutInfo = layoutInfo;
		_cardDataProvider = cardDataProvider;
		_cardTitleProvider = cardTitleProvider;
		_cardViewProvider = cardViewProvider;
		_gameStateProvider = gameStateProvider;
		_fakeCardViewController = fakeCardViewController;
		_promptTextProvider = promptTextProvider;
		_greLocProvider = greLocProvider;
		_clientLocProvider = clientLocProvider;
		_browserManager = browserManager;
	}

	public override void CleanUp()
	{
		if ((bool)_createdCdc)
		{
			_fakeCardViewController.DeleteFakeCard(_createdCdc.InstanceId.ToString());
			_createdCdc = null;
		}
		base.CleanUp();
	}

	protected override void ApplyInteractionInternal()
	{
		uint sourceId = GetSourceId(_gameStateProvider.LatestGameState);
		if (_cardViewProvider.TryGetCardView(sourceId, out var cardView))
		{
			_cardsToDisplay.Add(cardView);
		}
		_header = GetHeaderLoc();
		_subHeader = ((_layoutInfo.Data.SubheaderLocKey != null) ? _clientLocProvider.GetLocalizedText(_layoutInfo.Data.SubheaderLocKey) : (_subHeader = _promptTextProvider.GetPromptText(_request.Prompt)));
		SetupButtons(_layoutInfo.Data.YesText, _layoutInfo.Data.NoText, !_layoutInfo.Data.YesOnLeft, (_request.OriginalMessage.OptionalActionMessage.Highlight == Wotc.Mtgo.Gre.External.Messaging.HighlightType.Cold) ? ButtonStyle.StyleType.Tepid : ButtonStyle.StyleType.Secondary, ButtonStyle.StyleType.Secondary);
		IBrowser openedBrowser = _browserManager.OpenBrowser(this);
		SetOpenedBrowser(openedBrowser);
	}

	private uint GetSourceId(MtgGameState gameState)
	{
		switch (_layoutInfo.Data.SourceCardType)
		{
		case SourceCardType.TopOfLibrary_Local:
		case SourceCardType.TopOfLibrary_Opponent:
		{
			MtgPlayer owner = ((_layoutInfo.Data.SourceCardType == SourceCardType.TopOfLibrary_Local) ? gameState.LocalPlayer : gameState.Opponent);
			MtgZone zone = gameState.GetZone(ZoneType.Library, owner);
			if (zone.CardIds.Count <= 0)
			{
				return 0u;
			}
			return zone.CardIds[0];
		}
		case SourceCardType.RecipientId:
			if (_request.RecipientIds.Count <= 0)
			{
				return 0u;
			}
			return _request.RecipientIds[0];
		case SourceCardType.PromptCardNumber:
			if (_request.Prompt != null)
			{
				foreach (PromptParameter parameter in _request.Prompt.Parameters)
				{
					if (parameter.ParameterName == "CardId" && parameter.Type == ParameterType.Number && parameter.NumberValue != _request.SourceId && gameState.TryGetCard((uint)parameter.NumberValue, out var card3))
					{
						return card3.InstanceId;
					}
				}
			}
			return 0u;
		case SourceCardType.ReplacementEffectSource:
		{
			if (gameState.TryGetCard(_request.SourceId, out var card))
			{
				foreach (ReplacementEffectData replacementEffect in card.ReplacementEffects)
				{
					foreach (uint srcId in replacementEffect.SourceIds)
					{
						if (gameState.GetCardById(srcId) != null)
						{
							if (gameState.TryGetCard(replacementEffect.AffectedId, out var card2) && card2.LinkedFaceInstances.Count > 0)
							{
								CardPrintingData cardPrintingById = _cardDataProvider.GetCardPrintingById(card2.GrpId, card2.SkinCode);
								CardData cardData = new CardData(card2, cardPrintingById);
								_createdCdc = _fakeCardViewController.CreateFakeCard(cardData.InstanceId.ToString(), cardData);
								_cardsToDisplay.Add(_createdCdc);
								return 0u;
							}
							if (card2 != null && card2.LinkedInfoText.Count != card.LinkedInfoText.Count)
							{
								MtgCardInstance copy = card.GetCopy();
								copy.LinkedInfoText.AddRange(card2.LinkedInfoText);
								CardPrintingData cardPrintingById2 = _cardDataProvider.GetCardPrintingById(card2.GrpId, card2.SkinCode);
								CardData cardData2 = new CardData(copy, cardPrintingById2);
								_createdCdc = _fakeCardViewController.CreateFakeCard(cardData2.InstanceId.ToString(), cardData2);
								_cardsToDisplay.Add(_createdCdc);
								return 0u;
							}
							return srcId;
						}
						MtgCardInstance mtgCardInstance = gameState.Limbo.VisibleCards.Find((MtgCardInstance x) => x.InstanceId == srcId);
						if (mtgCardInstance != null)
						{
							CardData cardData3 = new CardData(mtgCardInstance, _cardDataProvider.GetCardPrintingById(mtgCardInstance.GrpId));
							_createdCdc = _fakeCardViewController.CreateFakeCard(cardData3.InstanceId.ToString(), cardData3);
							_createdCdc.transform.position = Vector3.zero;
							_cardsToDisplay.Add(_createdCdc);
							return 0u;
						}
					}
				}
			}
			return 0u;
		}
		default:
			return 0u;
		}
	}

	private string GetHeaderLoc()
	{
		if (_layoutInfo.Data.GrpId != 0)
		{
			return _cardTitleProvider.GetCardTitle(_layoutInfo.Data.GrpId, null);
		}
		if (_layoutInfo.Data.LocId > 0)
		{
			return _greLocProvider.GetLocalizedText((uint)_layoutInfo.Data.LocId);
		}
		return string.Empty;
	}
}
