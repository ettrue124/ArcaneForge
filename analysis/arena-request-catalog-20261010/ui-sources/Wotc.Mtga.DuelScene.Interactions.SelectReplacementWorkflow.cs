using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using GreClient.CardData.RulesTextOverrider;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions;

public class SelectReplacementWorkflow : SelectCardsWorkflow<SelectReplacementRequest>
{
	private const string FAKE_CARD_KEY_FORMAT = "Select Replacement Effect #{0}";

	private readonly Dictionary<DuelScene_CDC, ReplacementEffect> _cdcToReplacementEffects = new Dictionary<DuelScene_CDC, ReplacementEffect>();

	private readonly Dictionary<string, ButtonStateData> _buttonMap = new Dictionary<string, ButtonStateData>();

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IFakeCardViewController _fakeCardViewController;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IBrowserController _browserController;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	public override DuelSceneBrowserType GetBrowserType()
	{
		return DuelSceneBrowserType.SelectCards;
	}

	public override string GetCardHolderLayoutKey()
	{
		return "Modal";
	}

	public SelectReplacementWorkflow(SelectReplacementRequest request, ICardDatabaseAdapter cardDatabase, IGameStateProvider gameStateProvider, IFakeCardViewController fakeCardViewController, IBrowserController browserController, IBrowserHeaderTextProvider headerTextProvider)
		: base(request)
	{
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_fakeCardViewController = fakeCardViewController ?? NullFakeCardViewController.Default;
		_browserController = browserController ?? NullBrowserController.Default;
		_headerTextProvider = headerTextProvider ?? NullBrowserHeaderTextProvider.Default;
	}

	protected override void ApplyInteractionInternal()
	{
		MtgGameState gameState = _gameStateProvider.LatestGameState;
		foreach (ReplacementEffect replacement in _request.Replacements)
		{
			DuelScene_CDC duelScene_CDC = _fakeCardViewController.CreateFakeCard($"Select Replacement Effect #{replacement.ReplacementEffectId}", ConvertToCardData(replacement, gameState), isVisible: true);
			_cdcToReplacementEffects.Add(duelScene_CDC, replacement);
			_cardsToDisplay.Add(duelScene_CDC);
			selectable.Add(duelScene_CDC);
		}
		SetHeaderAndSubheader();
		_buttonStateData = GenerateButtonMap();
		SetOpenedBrowser(_browserController.OpenBrowser(this));
	}

	private void SetHeaderAndSubheader()
	{
		_headerTextProvider.ClearParams();
		_headerTextProvider.SetMinMax(1, 1u);
		_headerTextProvider.SetWorkflow(this);
		_headerTextProvider.SetRequest(_request);
		_headerTextProvider.SetBrowserType(DuelSceneBrowserType.SelectCards);
		_header = _headerTextProvider.GetHeaderText();
		_subHeader = _headerTextProvider.GetSubHeaderText(_prompt);
		_headerTextProvider.ClearParams();
	}

	public override void CleanUp()
	{
		foreach (ReplacementEffect replacement in _request.Replacements)
		{
			_fakeCardViewController.DeleteFakeCard($"Select Replacement Effect #{replacement.ReplacementEffectId}");
		}
		_cdcToReplacementEffects.Clear();
		_cardsToDisplay.Clear();
		selectable.Clear();
		base.CleanUp();
	}

	private Dictionary<string, ButtonStateData> GenerateButtonMap()
	{
		_buttonMap.Clear();
		if (_request.IsOptional)
		{
			_buttonMap["DeclineButton"] = new ButtonStateData
			{
				BrowserElementKey = "DeclineButton",
				LocalizedString = LocKeyForSelectReplacementType(_request.ReplacementsType),
				StyleType = ButtonStyleForSelectReplacementType(_request.ReplacementsType)
			};
		}
		return _buttonMap;
	}

	private static string LocKeyForSelectReplacementType(SelectReplacementsType selectReplacementsType)
	{
		if (selectReplacementsType == SelectReplacementsType.AllDredge)
		{
			return "DuelScene/Interaction/SelectReplacement/Draw";
		}
		return "DuelScene/ClientPrompt/Decline_Action";
	}

	private static ButtonStyle.StyleType ButtonStyleForSelectReplacementType(SelectReplacementsType selectReplacementsType)
	{
		if (selectReplacementsType == SelectReplacementsType.AllDredge)
		{
			return ButtonStyle.StyleType.Secondary;
		}
		return ButtonStyle.StyleType.Main;
	}

	protected override void Browser_OnBrowserButtonPressed(string buttonKey)
	{
		if (buttonKey == "DeclineButton")
		{
			_request.Decline();
		}
	}

	private ICardDataAdapter ConvertToCardData(ReplacementEffect replacementEffect, MtgGameState gameState)
	{
		MtgEntity entity = GetEntity(gameState, replacementEffect.ObjectInstance);
		CardData cardData = null;
		MtgCardInstance card;
		if (entity is MtgCardInstance mtgCardInstance)
		{
			AbilityPrintingData abilityPrintingById = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(replacementEffect.AbilityGrpId);
			if (mtgCardInstance.ObjectType == GameObjectType.Ability)
			{
				cardData = CardDataExtensions.CreateAbility(mtgCardInstance, _cardDatabase);
			}
			else
			{
				CardData cardData2 = CardDataExtensions.CreateWithDatabase(mtgCardInstance, _cardDatabase);
				if (abilityPrintingById != null)
				{
					cardData = CardDataExtensions.CreateAbilityCard(abilityPrintingById, cardData2, _cardDatabase);
				}
				else
				{
					cardData = CardDataExtensions.CreateAbilityCard(AbilityPrintingData.InvalidAbility(replacementEffect.AbilityGrpId), cardData2, _cardDatabase);
					if (cardData2.LinkedFaceType.IsAnyAdventureFace())
					{
						cardData.RulesTextOverride = new ClientLocTextOverride(_cardDatabase.ClientLocProvider, "AbilityHanger/SpecialHangers/ReplacementEffect/Adventure");
					}
					else if (cardData2.LinkedFaceType.IsAnyOmenFace())
					{
						cardData.RulesTextOverride = new ClientLocTextOverride(_cardDatabase.ClientLocProvider, "AbilityHanger/SpecialHangers/ReplacementEffect/Omen");
					}
				}
			}
		}
		else if (entity is MtgPlayer && gameState.TryGetCard(replacementEffect.ConferringObjectZcid, out card))
		{
			CardData cardData3 = CardDataExtensions.CreateWithDatabase(card, _cardDatabase);
			cardData = CardDataExtensions.CreateAbilityCard(_cardDatabase.AbilityDataProvider.GetAbilityPrintingById(replacementEffect.AbilityGrpId), cardData3, _cardDatabase);
			cardData.Instance.LinkedInfoTitleLocIds = cardData3.LinkedInfoTitleLocIds.ToHashSet();
		}
		if (entity == null)
		{
			entity = GetEntity(gameState, replacementEffect.AffectedObject);
			if (entity is MtgCardInstance mtgCardInstance2 && mtgCardInstance2.CounterDatas.Count > 0)
			{
				CardData parent = CardDataExtensions.CreateWithDatabase(mtgCardInstance2, _cardDatabase);
				AbilityPrintingData abilityPrintingById2 = _cardDatabase.AbilityDataProvider.GetAbilityPrintingById(replacementEffect.AbilityGrpId);
				if (abilityPrintingById2 != null)
				{
					cardData = CardDataExtensions.CreateAbilityCard(abilityPrintingById2, parent, _cardDatabase);
				}
			}
		}
		if (cardData == null)
		{
			cardData = CardDataExtensions.CreateBlank();
		}
		cardData.Instance.InstanceId = 0u;
		return cardData;
	}

	private static MtgEntity GetEntity(MtgGameState state, uint id)
	{
		if (state.TryGetEntity(id, out var mtgEntity))
		{
			return mtgEntity;
		}
		if (state.TrackedHistoricCards.TryGetValue(id, out var value))
		{
			return value;
		}
		return null;
	}

	protected override void CardBrowser_OnCardViewSelected(DuelScene_CDC cardView)
	{
		if (_cdcToReplacementEffects.TryGetValue(cardView, out var value))
		{
			_request.SubmitReplacement(value);
		}
		else
		{
			AudioManager.PlayAudio(WwiseEvents.sfx_ui_invalid.EventName, cardView.gameObject);
		}
	}
}
