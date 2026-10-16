using AssetLookupTree;
using AssetLookupTree.Payloads.UI.DuelScene;
using GreClient.CardData;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;

public class ConfirmWidgetTranslation : SingleActionTranslation
{
	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IAbilityDataProvider _abilityDataProvider;

	private readonly IClientLocProvider _clientLocProvider;

	private readonly IGreLocProvider _greLocProvider;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly ConfirmWidget _widgetUI;

	public ConfirmWidgetTranslation(AssetLookupSystem assetLookupSystem, IAbilityDataProvider abilityProvider, IClientLocProvider locProvider, IGreLocProvider greLocProvider, ICardHolderProvider cardHolderProvider, ConfirmWidget widgetUI)
	{
		_assetLookupSystem = assetLookupSystem;
		_abilityDataProvider = abilityProvider;
		_clientLocProvider = locProvider;
		_greLocProvider = greLocProvider;
		_cardHolderProvider = cardHolderProvider;
		_widgetUI = widgetUI;
	}

	protected override WorkflowVariant TranslateAction(DuelScene_CDC cardView, Action action)
	{
		ICardHolder currentCardHolder = cardView.CurrentCardHolder;
		if (currentCardHolder == null)
		{
			return null;
		}
		ConfirmWidget.Option? widgetData = GetWidgetData(currentCardHolder.CardHolderType, cardView.Model, action);
		if (widgetData.HasValue && MDNPlayerPrefs.GameplayWarningsEnabled)
		{
			return new ConfirmChoice_Widget(_cardHolderProvider, _widgetUI, cardView, (action, widgetData.Value));
		}
		return null;
	}

	public ConfirmWidget.Option? GetWidgetData(CardHolderType cardHolder, ICardDataAdapter card, Action action)
	{
		if (cardHolder != CardHolderType.Battlefield)
		{
			return null;
		}
		if (action.ActionType == ActionType.MakePayment)
		{
			return null;
		}
		if (action.ActionType == ActionType.SpecialTurnFaceUp)
		{
			return new ConfirmWidget.Option
			{
				Text = _clientLocProvider.GetLocalizedText("DuelScene/Browsers/Piles_TurnFaceUp"),
				IconPath = null
			};
		}
		uint abilityGrpId = action.AbilityGrpId;
		if (abilityGrpId == 0)
		{
			return null;
		}
		if (action.ActionType == ActionType.Special)
		{
			uint abilityGrpId2 = action.AbilityGrpId;
			if (abilityGrpId2 == 349 || abilityGrpId2 == 350)
			{
				return new ConfirmWidget.Option
				{
					Text = _clientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/UnlockRoom"),
					IconPath = GetSpritePath(card)
				};
			}
		}
		AbilityPrintingData abilityPrintingById = _abilityDataProvider.GetAbilityPrintingById(abilityGrpId);
		if (abilityPrintingById == null)
		{
			return null;
		}
		if (abilityPrintingById.RequiresConfirmation != RequiresConfirmation.Misclick)
		{
			return null;
		}
		if (abilityPrintingById.Tags.Contains(MetaDataTag.SacrificeFoodAbility))
		{
			return new ConfirmWidget.Option
			{
				Text = _clientLocProvider.GetLocalizedText("DuelScene/Prompt/SacrificeConfirm", ("cardTitle", _greLocProvider.GetLocalizedText(card.TitleId))),
				IconPath = GetSpritePath(card)
			};
		}
		return new ConfirmWidget.Option
		{
			Text = _clientLocProvider.GetLocalizedText("DuelScene/ClientPrompt/ActivateAbility"),
			IconPath = null
		};
	}

	private string GetSpritePath(ICardDataAdapter cardModel)
	{
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.SetCardDataExtensive(cardModel);
		_assetLookupSystem.Blackboard.CardHolderType = CardHolderType.CardBrowserViewDismiss;
		WidgetButtonSpritePayload payload = _assetLookupSystem.TreeLoader.LoadTree<WidgetButtonSpritePayload>().GetPayload(_assetLookupSystem.Blackboard);
		_assetLookupSystem.Blackboard.Clear();
		return payload?.Reference.RelativePath;
	}
}
