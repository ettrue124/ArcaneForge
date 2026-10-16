using System;
using System.Collections.Generic;
using AssetLookupTree;
using GreClient.CardData;
using GreClient.Rules;
using MovementSystem;
using Pooling;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Hangers;
using Wotc.Mtgo.Gre.External.Messaging;

namespace Wotc.Mtga.DuelScene.Interactions.SelectN;

public class SelectNWorkflowTranslation : IWorkflowTranslation<SelectNRequest>
{
	private const string CARD_ID_STRING = "CardId";

	private const uint BABA_LYSAGA_ABILITY_ID = 151474u;

	private const int DISCARD_BROWSER_CARD_REQUIREMENT = 15;

	private static readonly HashSet<ZoneType> _browserZoneTypes = new HashSet<ZoneType>
	{
		ZoneType.Sideboard,
		ZoneType.Graveyard,
		ZoneType.Exile,
		ZoneType.Library
	};

	private readonly GameManager _gameManager;

	private readonly IContext _context;

	private readonly IWorkflowTranslation<SelectNRequest> _choiceSelectionTranslation;

	private readonly IWorkflowTranslation<SelectNRequest> _choiceWithContextTranslation;

	private readonly IWorkflowTranslation<SelectNRequest> _zoneSelectionTranslation;

	private readonly IWorkflowTranslation<SelectNRequest> _defaultTranslation;

	private readonly IObjectPool _objectPool;

	private readonly IUnityObjectPool _unityObjectPool;

	private readonly ICardDatabaseAdapter _cardDatabase;

	private readonly IGameStateProvider _gameStateProvider;

	private readonly IResolutionEffectProvider _resolutionEffectProvider;

	private readonly IGameplaySettingsProvider _gameplaySettingsProvider;

	private readonly IEntityViewManager _entityViewManager;

	private readonly ICardHolderProvider _cardHolderProvider;

	private readonly IFakeCardViewController _fakeCardViewController;

	private readonly IBrowserManager _browserManager;

	private readonly IPromptTextProvider _promptTextProvider;

	private readonly IBrowserHeaderTextProvider _headerTextProvider;

	private readonly ISplineMovementSystem _splineMovementSystem;

	private readonly IPlayWarningManager _playWarningManager;

	private readonly AssetLookupSystem _assetLookupSystem;

	private readonly IEqualityComparer<ICardDataAdapter> _comparer;

	public SelectNWorkflowTranslation(IWorkflowTranslation<SelectNRequest> defaultTranslation, IContext context, AssetLookupSystem assetLookupSystem, GameManager gameManager, IEqualityComparer<ICardDataAdapter> comparer)
	{
		_gameManager = gameManager;
		_context = context ?? (context = NullContext.Default);
		_assetLookupSystem = assetLookupSystem;
		_comparer = comparer;
		ICardsInDeckProvider cardsInDeckProvider = new CardsInDeckProvider(context);
		IChoiceOptionsTranslation choiceOptionsTranslation = new ChoiceOptionsTranslation(new StaticListTranslation(new Dictionary<StaticList, IChoiceOptionsTranslation>
		{
			[StaticList.SubTypes] = new StaticSubtypeTranslation(context.Get<IGreLocProvider>(), new UniqueSubtypeHintProvider(context.Get<IObjectPool>(), new StaticSubtypeHintProvider(context.Get<ICardDataProvider>(), context.Get<IGameStateProvider>(), cardsInDeckProvider, assetLookupSystem))),
			[StaticList.CardNames] = new StaticCardNameTranslation(context, cardsInDeckProvider, assetLookupSystem),
			[StaticList.SuperTypes] = new StaticSuperTypeTranslation(context),
			[StaticList.CounterTypes] = new StaticCounterTypeTranslation(context),
			[StaticList.Keywords] = new StaticKeywordTranslation(context.Get<IAbilityTextProvider>()),
			[StaticList.CardTypes] = new StaticCardTypeTranslation(context, cardsInDeckProvider)
		}), new DynamicListTranslation(context, assetLookupSystem));
		_choiceWithContextTranslation = new ChoiceWithContextTranslation(context, choiceOptionsTranslation);
		_choiceSelectionTranslation = new ChoiceSelectionTranslation(context, choiceOptionsTranslation, assetLookupSystem);
		_zoneSelectionTranslation = new SelectZonesTranslation(context, assetLookupSystem);
		_defaultTranslation = defaultTranslation ?? new SelectionWorkflowTranslation(context, assetLookupSystem);
		_objectPool = context.Get<IObjectPool>() ?? NullObjectPool.Default;
		_cardDatabase = context.Get<ICardDatabaseAdapter>() ?? NullCardDatabaseAdapter.Default;
		_unityObjectPool = context.Get<IUnityObjectPool>() ?? NullUnityObjectPool.Default;
		_gameStateProvider = context.Get<IGameStateProvider>() ?? NullGameStateProvider.Default;
		_resolutionEffectProvider = context.Get<IResolutionEffectProvider>() ?? NullResolutionEffectProvider.Default;
		_gameplaySettingsProvider = context.Get<IGameplaySettingsProvider>() ?? NullGameplaySettingsProvider.Default;
		_entityViewManager = context.Get<IEntityViewManager>() ?? NullEntityViewManager.Default;
		_cardHolderProvider = context.Get<ICardHolderProvider>() ?? NullCardHolderProvider.Default;
		_fakeCardViewController = context.Get<IFakeCardViewController>() ?? NullFakeCardViewController.Default;
		_browserManager = context.Get<IBrowserManager>() ?? NullBrowserManager.Default;
		_promptTextProvider = context.Get<IPromptTextProvider>() ?? NullPromptTextProvider.Default;
		_headerTextProvider = context.Get<IBrowserHeaderTextProvider>() ?? NullBrowserHeaderTextProvider.Default;
		_splineMovementSystem = context.Get<ISplineMovementSystem>();
		_playWarningManager = context.Get<IPlayWarningManager>() ?? NullPlayWarningManager.Default;
	}

	public WorkflowBase Translate(SelectNRequest selectNRequest)
	{
		MtgGameState mtgGameState = _gameStateProvider.LatestGameState;
		if (selectNRequest.IsTriggeredAbilitySelection)
		{
			return new TriggeredAbilityWorkflow(selectNRequest, _entityViewManager, _cardDatabase.ClientLocProvider, _browserManager, _unityObjectPool, FaceInfoGeneratorFactory.DuelScene.LeftBattlefieldGenerator(_cardDatabase, GetLatestGameState, _comparer), _assetLookupSystem, _context.Get<CardViewBuilder>(), _gameManager.transform);
		}
		if (selectNRequest.IsStackingDecision)
		{
			return new SelectNWorkflow_Stacking(selectNRequest);
		}
		if (selectNRequest.IsWishSelection)
		{
			return new SelectNWorkflow_Wish_DEBUG(selectNRequest, _cardDatabase);
		}
		if (selectNRequest.IsManaPoolSelection)
		{
			return new SelectNWorkflow_ManaPool(selectNRequest, _entityViewManager, _gameStateProvider);
		}
		if (selectNRequest.IsCardColorSelection || selectNRequest.IsManaColorSelection)
		{
			return new SelectColorWorkflow(selectNRequest, _cardDatabase, _gameStateProvider, _gameplaySettingsProvider, _promptTextProvider, _entityViewManager, _cardHolderProvider, _browserManager, _headerTextProvider, _playWarningManager, _gameManager.UIManager.ManaColorSelector, _assetLookupSystem);
		}
		if (selectNRequest.IsBasicLandSelection)
		{
			return new SelectBasicLandWorkflow(selectNRequest, _gameManager.UIManager.ManaColorSelector, _entityViewManager, _cardHolderProvider, _cardDatabase.ClientLocProvider, _browserManager, _gameplaySettingsProvider, _cardDatabase.PromptEngine, _cardDatabase, _gameStateProvider, _headerTextProvider, _assetLookupSystem);
		}
		if (selectNRequest.IsBasicLandSubtypeSelection)
		{
			return new SelectBasicLandSubtypeWorkflow(selectNRequest, _gameStateProvider, _cardHolderProvider, _entityViewManager, _cardDatabase.ClientLocProvider, _browserManager, _gameManager.UIManager.ManaColorSelector);
		}
		if (selectNRequest.IsCounterSelection)
		{
			return new SelectNWorkflow_Counters(selectNRequest, _objectPool, _unityObjectPool, _cardDatabase, _gameStateProvider, _gameplaySettingsProvider, _promptTextProvider, _browserManager, _headerTextProvider, _fakeCardViewController, _assetLookupSystem, _gameManager.SpinnerController);
		}
		if (selectNRequest.IsZoneSelection)
		{
			return _zoneSelectionTranslation.Translate(selectNRequest);
		}
		if (selectNRequest.IsTypeKindSelection)
		{
			return new TypeKindsWorkflow(selectNRequest);
		}
		if (selectNRequest.IsAnchorWordSelection(_gameStateProvider.LatestGameState))
		{
			return new SelectNWorkflow_AnchorWord(selectNRequest, _context);
		}
		if (selectNRequest.IsChoiceSelectionWithContext)
		{
			return _choiceWithContextTranslation.Translate(selectNRequest);
		}
		if (selectNRequest.IsChoiceSelection)
		{
			return _choiceSelectionTranslation.Translate(selectNRequest);
		}
		if (selectNRequest.IsLookSelection)
		{
			return new SelectNWorkflow_Look(selectNRequest, _objectPool, _cardDatabase, _entityViewManager, _browserManager, _fakeCardViewController);
		}
		if (selectNRequest.IsParitySelection)
		{
			return new ParityWorkflow(selectNRequest);
		}
		if (selectNRequest.IsDungeonSelection(_cardDatabase.CardDataProvider))
		{
			return new DungeonSelectWorkflow(selectNRequest, _entityViewManager, _cardDatabase.CardDataProvider, _cardHolderProvider, _browserManager, _splineMovementSystem);
		}
		if (selectNRequest.IsDungeonRoomSelection())
		{
			return new DungeonRoomSelectWorkflow(selectNRequest, DungeonRoomSelectWorkflow.DecidingPlayerDungeon(mtgGameState), _cardDatabase.ClientLocProvider, _browserManager);
		}
		if (selectNRequest.IsAbilitySelection)
		{
			return new SelectNWorkflow_Ability(selectNRequest, _cardDatabase, _cardDatabase.AbilityDataProvider, _fakeCardViewController, _gameStateProvider, _browserManager, _headerTextProvider);
		}
		if (selectNRequest.IsPrintingSelection)
		{
			return new SelectNWorkflow_Printing(selectNRequest, _cardDatabase, _gameStateProvider, _gameplaySettingsProvider, _fakeCardViewController, _browserManager, _headerTextProvider);
		}
		if (selectNRequest.IsRevealedCardSelection(mtgGameState))
		{
			return new RevealedCardSelection(selectNRequest, _cardDatabase, _gameStateProvider, _gameplaySettingsProvider, _fakeCardViewController, _browserManager, _headerTextProvider);
		}
		if (selectNRequest.IsPrintingIndexSelection)
		{
			return new SelectPrintingIndexWorkflow(selectNRequest, _cardDatabase, _gameStateProvider, _fakeCardViewController, _browserManager, _headerTextProvider);
		}
		if (SurveilStyleBrowserWorkflow<SelectNRequest>.UseSurveilStyleBrowser(selectNRequest, _assetLookupSystem, mtgGameState))
		{
			return new SelectNWorkflow_Selection_SurveilStyleBrowser(selectNRequest, _browserManager, _gameStateProvider, _cardDatabase, _cardDatabase.ClientLocProvider, _entityViewManager);
		}
		if (selectNRequest.IsRoomUnlockSubCardSelection(mtgGameState))
		{
			return new SelectNWorkflow_Selection_SubRooms(selectNRequest, _gameStateProvider, _cardDatabase, _fakeCardViewController, _cardHolderProvider, _browserManager, _headerTextProvider, _cardDatabase.ClientLocProvider);
		}
		if (selectNRequest.IsLimboSelection(mtgGameState, _cardDatabase))
		{
			return new LimboSelectionWorkflow(selectNRequest, _cardDatabase, _gameStateProvider, _gameplaySettingsProvider, _fakeCardViewController, _browserManager, _headerTextProvider, _entityViewManager);
		}
		List<uint> list = new List<uint>(selectNRequest.Ids);
		foreach (uint unfilteredId in selectNRequest.UnfilteredIds)
		{
			if (!list.Contains(unfilteredId))
			{
				list.Add(unfilteredId);
			}
		}
		HashSet<MtgZone> zonesFromSelectN = GetZonesFromSelectN(mtgGameState, selectNRequest, _entityViewManager);
		bool flag = OpenInSingleZoneBrowser(zonesFromSelectN) || OpenSelectNInMultiZoneBrowser(zonesFromSelectN);
		if (selectNRequest.IsWeightedSelection)
		{
			if (flag && !SelectNWorkflow_Selection_Weighted.IsForage(selectNRequest.Prompt))
			{
				return new SelectNWorkflow_Selection_Weighted_Browser(selectNRequest, _gameplaySettingsProvider, _entityViewManager, _browserManager, _headerTextProvider, _cardDatabase.AbilityDataProvider, _gameStateProvider, _assetLookupSystem);
			}
			if (useCardTypeSelection(mtgGameState, selectNRequest.Prompt?.Parameters))
			{
				return new SelectNWorkflow_Selection_DifferentCardTypes(selectNRequest, _gameStateProvider);
			}
			return new SelectNWorkflow_Selection_Weighted(selectNRequest, _cardDatabase.ClientLocProvider, _cardDatabase.AbilityDataProvider, _gameStateProvider, _gameplaySettingsProvider, _browserManager, _entityViewManager, _cardHolderProvider, _assetLookupSystem);
		}
		if (flag)
		{
			return new SelectNWorkflow_Selection_Browser(selectNRequest, _objectPool, _cardDatabase, _gameStateProvider, _resolutionEffectProvider, _gameplaySettingsProvider, _entityViewManager, _browserManager, _headerTextProvider, _playWarningManager);
		}
		if (OpenInBattlefieldHandBrowser(mtgGameState, selectNRequest, _entityViewManager))
		{
			return new SelectNWorkflow_Selection_BattlefieldHandBrowser(selectNRequest, _entityViewManager, _cardDatabase.ClientLocProvider, _browserManager, _headerTextProvider, _gameStateProvider, _cardDatabase);
		}
		if (IsDiscardHandActionWithBrowser(zonesFromSelectN, selectNRequest))
		{
			return new SelectNWorkflow_Selection_DiscardHandBrowser(selectNRequest, _entityViewManager, _cardDatabase.ClientLocProvider, _browserManager);
		}
		return _defaultTranslation.Translate(selectNRequest);
		static bool useCardTypeSelection(MtgGameState gameState, IEnumerable<PromptParameter> promptParameters)
		{
			foreach (PromptParameter item in promptParameters ?? Array.Empty<PromptParameter>())
			{
				if (item.Type == ParameterType.Number && item.ParameterName == "CardId" && gameState.TryGetCard((uint)item.NumberValue, out var card) && card.GrpId == 151474)
				{
					return true;
				}
			}
			return false;
		}
	}

	private static IEnumerable<MtgZone> GetZonesFromZoneIds(MtgGameState currentState, List<uint> zoneIds)
	{
		foreach (uint zoneId in zoneIds)
		{
			yield return currentState.GetZoneById(zoneId);
		}
	}

	private static IEnumerable<MtgZone> GetZonesFromCardIds(MtgGameState currentState, IEnumerable<uint> cardIds, ICardViewProvider cardViewProvider)
	{
		foreach (uint cardId in cardIds)
		{
			DuelScene_CDC cardView;
			if (currentState.TryGetCard(cardId, out var card))
			{
				ZoneType type = card.Zone.Type;
				MtgZone zone = card.Zone;
				if (type != ZoneType.Limbo)
				{
					yield return zone;
				}
			}
			else if (cardViewProvider.TryGetCardView(cardId, out cardView))
			{
				ZoneType type2 = cardView.Model.Zone.Type;
				MtgZone zone2 = cardView.Model.Zone;
				if (type2 != ZoneType.Limbo)
				{
					yield return zone2;
				}
			}
		}
	}

	private static HashSet<MtgZone> GetZonesFromSelectN(MtgGameState currentState, SelectNRequest request, ICardViewProvider cardViewProvider)
	{
		HashSet<MtgZone> hashSet = new HashSet<MtgZone>();
		foreach (MtgZone zonesFromZoneId in GetZonesFromZoneIds(currentState, request.ZoneIds))
		{
			hashSet.Add(zonesFromZoneId);
		}
		foreach (MtgZone zonesFromCardId in GetZonesFromCardIds(currentState, request.Ids, cardViewProvider))
		{
			hashSet.Add(zonesFromCardId);
		}
		foreach (MtgZone zonesFromCardId2 in GetZonesFromCardIds(currentState, request.UnfilteredIds, cardViewProvider))
		{
			hashSet.Add(zonesFromCardId2);
		}
		return hashSet;
	}

	public static bool IsDiscardHandActionWithBrowser(IReadOnlyCollection<MtgZone> involvedZones, SelectNRequest request)
	{
		if (!involvedZones.Exists((MtgZone x) => x.Type == ZoneType.Hand))
		{
			return false;
		}
		if (request.Context != SelectionContext.Discard)
		{
			return false;
		}
		if (request.Ids.Count < 15)
		{
			return false;
		}
		if (request.MinSel <= 7)
		{
			return false;
		}
		return true;
	}

	private static bool OpenSelectNInMultiZoneBrowser(IReadOnlyCollection<MtgZone> involvedZones)
	{
		if (involvedZones.Count <= 1)
		{
			return false;
		}
		foreach (MtgZone involvedZone in involvedZones)
		{
			ZoneType type = involvedZone.Type;
			if ((uint)(type - 1) > 1u && (uint)(type - 5) > 1u && type != ZoneType.Sideboard)
			{
				return false;
			}
		}
		return true;
	}

	public static bool OpenInSingleZoneBrowser(IReadOnlyCollection<MtgZone> involvedZones)
	{
		if (involvedZones.Count != 1)
		{
			return false;
		}
		if (involvedZones.Exists((MtgZone x) => x.Type == ZoneType.Hand && x.OwnerNum == GREPlayerNum.Opponent))
		{
			return true;
		}
		return involvedZones.Exists((MtgZone x) => _browserZoneTypes.Contains(x.Type));
	}

	public static bool OpenInBattlefieldHandBrowser(MtgGameState currentState, SelectNRequest request, ICardViewProvider cardViewProvider)
	{
		uint num = 0u;
		uint num2 = 0u;
		foreach (MtgZone zonesFromCardId in GetZonesFromCardIds(currentState, request.Ids, cardViewProvider))
		{
			switch (zonesFromCardId.Type)
			{
			case ZoneType.Hand:
				num++;
				break;
			case ZoneType.Battlefield:
				num2++;
				break;
			default:
				return false;
			}
		}
		if (request.Prompt.PromptId == 16461 && num != 0)
		{
			return num2 == 1;
		}
		return false;
	}

	private MtgGameState GetLatestGameState()
	{
		return _gameStateProvider.LatestGameState;
	}
}
