using System;
using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.Browser.OptionalAction;
using GreClient.CardData;
using GreClient.Rules;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.OptionalAction;

public class OptionalActionTranslation : IWorkflowTranslation<OptionalActionMessageRequest>
{
	private readonly IContext _context;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly ICardBuilder<DuelScene_CDC> _cardBuilder;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly ICardViewProvider _cardViewProvider;

	private readonly IBrowserManager _browserManager;

	private readonly IResolutionEffectProvider _resolutionEffectProvider;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IWorkflowTranslation<OptionalActionMessageRequest> _defaultTranslation;

	public OptionalActionTranslation(IContext context, AssetLookupSystem assetLookupSystem)
		: this(context, context.Get<ICardDatabaseAdapter>(), context.Get<ICardBuilder<DuelScene_CDC>>(), context.Get<IGameStateProvider>(), context.Get<ICardViewProvider>(), context.Get<IBrowserManager>(), context.Get<IResolutionEffectProvider>(), assetLookupSystem)
	{
	}

	private OptionalActionTranslation(IContext context, ICardDatabaseAdapter cardDatabase, ICardBuilder<DuelScene_CDC> cardBuilder, IGameStateProvider gameStateProvider, ICardViewProvider cardViewProvider, IBrowserManager browserManager, IResolutionEffectProvider resolutionEffectProvider, AssetLookupSystem assetLookupSystem)
	{
		_context = context ?? NullContext.Default;
		_cardDatabase = cardDatabase ?? NullCardDatabaseAdapter.Default;
		_cardBuilder = cardBuilder ?? NullCardBuilder<DuelScene_CDC>.Default;
		_gameStateProvider = gameStateProvider ?? NullGameStateProvider.Default;
		_cardViewProvider = cardViewProvider ?? NullCardViewProvider.Default;
		_browserManager = browserManager ?? NullBrowserManager.Default;
		_resolutionEffectProvider = resolutionEffectProvider ?? NullResolutionEffectProvider.Default;
		_defaultTranslation = new DefaultTranslation(_context, assetLookupSystem);
		_assetLookupSystem = assetLookupSystem;
	}

	public WorkflowBase Translate(OptionalActionMessageRequest req)
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		MtgCardInstance cardById = mtgGameState.GetCardById(req.SourceId);
		if (req.Mechanics.Contains(CardMechanicType.Mutate))
		{
			return new OptionalActionWorkflow_Mutate(req, _gameStateProvider, _cardDatabase, _browserManager);
		}
		if (req.Mechanics.Contains(CardMechanicType.Riot))
		{
			return new OptionalActionWorkflow_Riot(req, _cardDatabase, _cardBuilder, _gameStateProvider, _cardViewProvider, _browserManager);
		}
		if (req.Mechanics.Contains(CardMechanicType.Explore))
		{
			return new ExploreWorkflow(req, _cardDatabase.ClientLocProvider, _gameStateProvider, _cardDatabase, _browserManager, _cardViewProvider);
		}
		if (req.Mechanics.Contains(CardMechanicType.Transform))
		{
			return _defaultTranslation.Translate(req);
		}
		if (OptionalActionMessage_ScryishWorkflow.UseScryStyleBrowser(req, _assetLookupSystem, mtgGameState))
		{
			return new OptionalActionMessage_ScryishWorkflow(req, _cardDatabase, _gameStateProvider, _resolutionEffectProvider, _cardViewProvider, _browserManager, _assetLookupSystem);
		}
		if (SurveilStyleBrowserWorkflow<OptionalActionMessageRequest>.UseSurveilStyleBrowser(req, _assetLookupSystem, mtgGameState))
		{
			return new OptionalActionMessageWorkflow_SurveilStyleBrowser(req, _cardDatabase.ClientLocProvider, _browserManager, _gameStateProvider, _cardDatabase, _cardViewProvider);
		}
		if (TryGetSpecialBrowserLayout(req, mtgGameState, _cardDatabase, _assetLookupSystem, out var layoutInfo))
		{
			return new OptionalActionWorkflow_NonMechanic_BrowserButtons(req, layoutInfo, _context.Get<ICardDataProvider>(), _context.Get<ICardTitleProvider>(), _context.Get<ICardViewProvider>(), _context.Get<IGameStateProvider>(), _context.Get<IFakeCardViewController>(), _context.Get<IPromptTextProvider>(), _context.Get<IGreLocProvider>(), _context.Get<IClientLocProvider>(), _context.Get<IBrowserManager>());
		}
		if (TryGetNonMechanicCardButtonBrowserText(cardById, req, _cardDatabase, _assetLookupSystem, mtgGameState, out var browserText))
		{
			return new NonMechanicCardButtonWorkflow(req, cardById, browserText, _cardBuilder, _cardDatabase.ClientLocProvider, _cardDatabase, _browserManager, _context.Get<IPromptTextProvider>());
		}
		if (UseCommanderWorkflow(cardById, req.SourceId, mtgGameState, req))
		{
			return new OptionalActionMessageWorkflow_Commander(req);
		}
		return _defaultTranslation.Translate(req);
	}

	private static bool UseCommanderWorkflow(MtgCardInstance sourceCard, uint sourceId, MtgGameState gameState, OptionalActionMessageRequest req)
	{
		if (sourceCard == null)
		{
			return false;
		}
		if (!req.Mechanics.Exists((CardMechanicType x) => x == CardMechanicType.ZoneTransfer))
		{
			return false;
		}
		MtgPlayer localPlayer = gameState.LocalPlayer;
		if (localPlayer == null)
		{
			return false;
		}
		if (!localPlayer.CommanderIds.Contains(sourceId))
		{
			return false;
		}
		Prompt prompt = req.Prompt;
		if (prompt == null)
		{
			return false;
		}
		IList<PromptParameter> parameters = prompt.Parameters;
		if (parameters == null)
		{
			return false;
		}
		if (parameters.Count < 2)
		{
			return false;
		}
		PromptParameter promptParameter = parameters[0];
		if (promptParameter.ParameterName != "CardId")
		{
			return false;
		}
		if (promptParameter.Type != ParameterType.Number)
		{
			return false;
		}
		if (promptParameter.NumberValue != sourceCard.InstanceId)
		{
			return false;
		}
		PromptParameter promptParameter2 = parameters[1];
		if (promptParameter2.ParameterName != "CardId")
		{
			return false;
		}
		if (promptParameter2.Type != ParameterType.Number)
		{
			return false;
		}
		if (Enum.IsDefined(typeof(ZoneType), promptParameter2.NumberValue))
		{
			return promptParameter2.NumberValue != 0;
		}
		return false;
	}

	private static bool TryGetSpecialBrowserLayout(OptionalActionMessageRequest req, MtgGameState gameState, ICardDatabaseAdapter cardDatabase, AssetLookupSystem assetLookupSystem, out SpecialBrowserLayout layoutInfo)
	{
		uint sourceId = req.SourceId;
		if (gameState.ReplacementEffects.TryGetValue(sourceId, out var value) && TryGetSpecialBrowserLayout(value, cardDatabase.AbilityDataProvider, assetLookupSystem, out layoutInfo))
		{
			return true;
		}
		if (gameState.TryGetCard(sourceId, out var card) && TryGetSpecialBrowserLayout(CardDataExtensions.CreateWithDatabase(card, cardDatabase), assetLookupSystem, out layoutInfo))
		{
			return true;
		}
		layoutInfo = null;
		return false;
	}

	private static bool TryGetSpecialBrowserLayout(IEnumerable<ReplacementEffectData> replacementEffects, IAbilityDataProvider abilityDataProvider, AssetLookupSystem assetLookupSystem, out SpecialBrowserLayout layoutInfo)
	{
		foreach (ReplacementEffectData replacementEffect in replacementEffects)
		{
			if (TryGetSpecialBrowserLayout(abilityDataProvider.GetAbilityPrintingById(replacementEffect.AbilityId), assetLookupSystem, out layoutInfo))
			{
				return true;
			}
		}
		layoutInfo = null;
		return false;
	}

	private static bool TryGetSpecialBrowserLayout(AbilityPrintingData ability, AssetLookupSystem assetLookupSystem, out SpecialBrowserLayout layoutInfo)
	{
		layoutInfo = null;
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.Ability = ability;
		if (assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SpecialBrowserLayout> loadedTree))
		{
			layoutInfo = loadedTree?.GetPayload(assetLookupSystem.Blackboard);
		}
		return layoutInfo != null;
	}

	private static bool TryGetSpecialBrowserLayout(ICardDataAdapter card, AssetLookupSystem assetLookupSystem, out SpecialBrowserLayout specialBrowserLayout)
	{
		specialBrowserLayout = null;
		assetLookupSystem.Blackboard.Clear();
		assetLookupSystem.Blackboard.SetCardDataExtensive(card);
		if (assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SpecialBrowserLayout> loadedTree))
		{
			specialBrowserLayout = loadedTree?.GetPayload(assetLookupSystem.Blackboard);
		}
		return specialBrowserLayout != null;
	}

	private static bool TryGetNonMechanicCardButtonBrowserText(MtgCardInstance cardInstance, OptionalActionMessageRequest req, ICardDatabaseAdapter cdb, AssetLookupSystem als, MtgGameState gameState, out BrowserText browserText)
	{
		NonMechanic_CardButtons nonMechanicCardButtonPayload = GetNonMechanicCardButtonPayload(cardInstance, req, cdb, als, gameState);
		if (nonMechanicCardButtonPayload != null)
		{
			browserText = new BrowserText(nonMechanicCardButtonPayload.Header, nonMechanicCardButtonPayload.SubheaderOverride, nonMechanicCardButtonPayload.YesCardRulesText, nonMechanicCardButtonPayload.NoCardRulesText, nonMechanicCardButtonPayload.BuildParameters(als.Blackboard));
			return true;
		}
		browserText = default;
		return false;
	}

	private static NonMechanic_CardButtons GetNonMechanicCardButtonPayload(MtgCardInstance cardInstance, OptionalActionMessageRequest req, ICardDatabaseAdapter cdb, AssetLookupSystem als, MtgGameState gameState)
	{
		if (cardInstance == null)
		{
			return null;
		}
		if (als.TreeLoader.TryLoadTree(out AssetLookupTree<NonMechanic_CardButtons> loadedTree))
		{
			CardData cardData = CardDataExtensions.CreateWithDatabase(cardInstance, cdb);
			IBlackboard blackboard = als.Blackboard;
			blackboard.Clear();
			blackboard.SetCardDataExtensive(cardData);
			blackboard.CardHolderType = cardData.ZoneType.ToCardHolderType();
			blackboard.Request = req;
			blackboard.GameState = gameState;
			return loadedTree.GetPayload(blackboard);
		}
		return null;
	}
}
