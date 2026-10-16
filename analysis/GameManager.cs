using System;
using System.Collections.Generic;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.Prefab;
using Core.Code.Familiar;
using GreClient.CardData;
using GreClient.Network;
using GreClient.Rules;
using InteractionSystem;
using MTGA.KeyboardManager;
using MovementSystem;
using Pooling;
using ReferenceMap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Wizards.Arena.Client.Logging;
using Wizards.Mtga;
using Wizards.Mtga.Assets;
using Wizards.Mtga.Logging;
using Wizards.Mtga.Platforms;
using Wizards.Mtga.UI;
using Wotc;
using Wotc.Mtga;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.DuelScene.Browsers;
using Wotc.Mtga.DuelScene.CardView;
using Wotc.Mtga.DuelScene.Companions;
using Wotc.Mtga.DuelScene.Emotes;
using Wotc.Mtga.DuelScene.Input;
using Wotc.Mtga.DuelScene.Interactions;
using Wotc.Mtga.DuelScene.Interactions.ActionsAvailable;
using Wotc.Mtga.DuelScene.Interactions.AssignDamage;
using Wotc.Mtga.DuelScene.Interactions.SelectN;
using Wotc.Mtga.DuelScene.Interactions.SelectTargets;
using Wotc.Mtga.DuelScene.Loop;
using Wotc.Mtga.DuelScene.PlayerNameViews;
using Wotc.Mtga.DuelScene.UXEvents;
using Wotc.Mtga.DuelScene.VFX;
using Wotc.Mtga.DuelScene.ZoneCounts;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Hangers;
using Wotc.Mtga.Loc;
using Wotc.Mtga.Providers;
using Wotc.Mtgo.Gre.External.Messaging;

public class GameManager : MonoBehaviour, IKeyUpSubscriber, IKeySubscriber, IKeyDownSubscriber, IKeyHeldSubscriber
{
	private IContext _matchContext = NullContext.Default;

	public IContext Context = NullContext.Default;

	private readonly IHighlightManager _highlightManager = new HighlightManager();

	private IDimmingController _dimmingController = NullDimmingController.Default;

	private IntentionLineManager _intentionLineManager;

	private IVisualStateCardProvider _visualStateCardProvider = NullVisualCardProvider.Default;

	private ICombatIconUpdater _combatIconUpdater = NullCombatIconUpdater.Default;

	private bool _sideboardSubmitted;

	private NPEState _npeState;

	private GreInterface _gre;

	private NPEDirector _npeDirector;

	private NPEController _npeController;

	private IEventTranslation _uxEventTranslator;

	private IPromptTextManager _promptTextManager = NullPromptTextManager.Default;

	private AltArtMatchSettings _altArtMatchSettings;

	private SettingsMenuHost _settingsMenuHost;

	private readonly IGameStateManager _gameStateManager = new GameStateManager();

	public readonly MapAggregate ReferenceMapAggregate = new MapAggregate();

	private IEmoteDataProvider _emoteDataProvider;

	private readonly HashSet<FamiliarRequestHandler> _familiarRequestHandlers = new HashSet<FamiliarRequestHandler>();

	private readonly HashSet<IDisposable> _disposables = new HashSet<IDisposable>();

	private readonly HashSet<IUpdate> _updateObservers = new HashSet<IUpdate>();

	private readonly SettingsMessageController _settingsMessageController = new SettingsMessageController();

	private IDuelSceneStateManager _stateManager = NullDuelSceneStateManager.Default;

	private IClientUpdateHandler<ClientUpdateBase> _clientUpdateHandler = NullClientUpdateHandler<ClientUpdateBase>.Default;

	public Camera MainCamera { get; private set; }

	private StaticZoomController _staticHoverCardHolder { get; set; }

	private MatchSceneManager MatchSceneManager { get; set; }

	public DuelSceneLogger Logger { get; private set; }

	public CardHolderManager CardHolderManager { get; private set; }

	public EntityViewManager ViewManager { get; private set; }

	public UIManager UIManager { get; private set; }

	public SpinnerController SpinnerController { get; private set; }

	public UIMessageHandler UIMessageHandler { get; private set; } = new UIMessageHandler();

	public CombatAnimationPlayer CombatAnimationPlayer { get; private set; }

	public GameInteractionSystem InteractionSystem { get; private set; }

	public BrowserManager BrowserManager { get; private set; }

	public MatchManager MatchManager { get; private set; }

	public AssetCache<SplineMovementData> SplineCache { get; private set; } = new AssetCache<SplineMovementData>();

	public NPEDirector NpeDirector => _npeDirector;

	public IGREConnection GreConnection => MatchManager?.GreConnection;

	public ResolutionEffectModel ActiveResolutionEffect => Context.Get<IResolutionEffectProvider>()?.ResolutionEffect;

	public MtgGameState CurrentGameState => _gameStateManager.CurrentGameState;

	public MtgGameState LatestGameState => _gameStateManager.LatestGameState;

	public IObjectPool GenericPool { get; private set; }

	public IUnityObjectPool UnityPool { get; private set; }

	public ICardDatabaseAdapter CardDatabase { get; private set; }

	public ISplineMovementSystem SplineMovementSystem { get; private set; } = NullSplineMovementSystem.Default;

	public UXEventQueue UXEventQueue { get; private set; } = new UXEventQueue();

	public WorkflowController WorkflowController { get; private set; }

	public IVfxProvider VfxProvider { get; private set; } = NullVfxProvider.Default;

	public WorkflowBase CurrentInteraction => WorkflowController?.CurrentWorkflow;

	private WorkflowBase PendingInteraction => WorkflowController?.PendingWorkflow;

	public AutoResponseManager AutoRespManager { get; private set; }

	public SettingsMessage CurrentSettings => AutoRespManager.CurrentSettings;

	private bool AllowInput => _stateManager.AllowInput;

	public PriorityLevelEnum Priority => PriorityLevelEnum.DuelScene;

	public AssetLookupSystem AssetLookupSystem { get; private set; }

	public MtgGameState GetCurrentGameState()
	{
		return CurrentGameState;
	}

	public WorkflowBase GetCurrentInteraction()
	{
		return CurrentInteraction;
	}

	public void Init(MatchSceneManager matchSceneManager, AssetLookupSystem assetLookupSystem, IContext matchContext)
	{
		MatchSceneManager = matchSceneManager;
		AssetLookupSystem = assetLookupSystem;
		_matchContext = matchContext;
		GenericPool = matchContext.Get<IObjectPool>() ?? NullObjectPool.Default;
		SplineMovementSystem = new SplineMovementSystem(GenericPool);
		UnityPool = new DuelSceneUnityObjectPool(matchContext.Get<IUnityObjectPool>(), SplineMovementSystem);
		CardDatabase = matchContext.Get<ICardDatabaseAdapter>();
		_emoteDataProvider = matchContext.Get<IEmoteDataProvider>();
		MatchManager = matchContext.Get<MatchManager>();
		_gre = MatchManager.GreInterface;
		Logger = matchContext.Get<DuelSceneLogger>();
		_npeState = matchContext.Get<NPEState>();
		_settingsMenuHost = matchContext.Get<SettingsMenuHost>();
		_stateManager = new DuelSceneStateManager(matchContext.Get<IMatchSceneStateManager>());
		_altArtMatchSettings = new AltArtMatchSettings(CardDatabase, AssetLookupSystem);
		AssetLookupSystem.Blackboard.AddFillerDelegate(FillAltBlackboard);
	}

	private void Start()
	{
		if (MatchManager == null || MatchSceneManager == null)
		{
			Debug.Log("GameManager default load");
			Scenes.LoadScene("DuelSceneDebugLauncher");
			return;
		}
		bool handheld = PlatformUtils.IsHandheld();
		bool flag = MatchManager.Players.Count > 2;
		SceneManager.SetActiveScene(SceneManager.GetSceneByName(MatchSceneManager.BattlefieldSceneName));
		bool fog = RenderSettings.fog;
		float fogDensity = RenderSettings.fogDensity;
		float fogEndDistance = RenderSettings.fogEndDistance;
		float fogStartDistance = RenderSettings.fogStartDistance;
		UnityEngine.Color fogColor = RenderSettings.fogColor;
		FogMode fogMode = RenderSettings.fogMode;
		SceneManager.SetActiveScene(base.gameObject.scene);
		RenderSettings.fog = fog;
		RenderSettings.fogDensity = fogDensity;
		RenderSettings.fogEndDistance = fogEndDistance;
		RenderSettings.fogStartDistance = fogStartDistance;
		RenderSettings.fogColor = fogColor;
		RenderSettings.fogMode = fogMode;
		CameraAdapter cameraAdapter = null;
		GameObject[] rootGameObjects = base.gameObject.scene.GetRootGameObjects();
		foreach (GameObject gameObject in rootGameObjects)
		{
			if (gameObject != null && gameObject.TryGetComponent<CameraAdapter>(out var component))
			{
				cameraAdapter = component;
				MainCamera = component.CameraReference;
				break;
			}
		}
		IClientLocProvider clientLocProvider = CardDatabase.ClientLocProvider;
		BattlefieldManager battlefieldManager = UnityEngine.Object.FindFirstObjectByType<BattlefieldManager>();
		battlefieldManager.InitBattlefieldReactions(UXEventQueue);
		CosmeticsProvider cosmeticsProvider = _matchContext.Get<CosmeticsProvider>();
		CardViewBuilder cardViewBuilder = _matchContext.Get<CardViewBuilder>();
		SignalBase<GameStatePlaybackCompleteSignalArgs> signalBase = new GameStatePlaybackCompleteSignal();
		SignalBase<GameStatePlaybackStartedSignalArgs> playbackStarted = new GameStatePlaybackStartedSignal();
		SignalBase<PlayerCreatedSignalArgs> playerCreatedEvent = new PlayerCreatedSignal();
		SignalBase<PlayerDeletedSignalArgs> playerDeletedEvent = new PlayerDeletedSignal();
		SignalBase<CompanionCreatedSignalArgs> signalBase2 = new CompanionCreatedSignal();
		SignalBase<CardHolderCreatedSignalArgs> signalBase3 = new CardHolderCreatedSignal();
		SignalBase<ZoneCardHolderCreatedSignalArgs> signalBase4 = new ZoneCardHolderCreatedSignal();
		SignalBase<SubZoneCardHolderCreatedSignalArgs> signalBase5 = new SubZoneCardHolderCreatedSignal();
		SignalBase<CardHolderDeletedSignalArgs> signalBase6 = new CardHolderDeletedSignal();
		SignalBase<PromptTextUpdatedSignalArgs> signalBase7 = new PromptTextUpdatedSignal();
		SignalBase<ZoneCountCreatedSignalArgs> signalBase8 = new ZoneCountCreatedSignal();
		SignalBase<ZoneCountDeletedSignalArgs> signalBase9 = new ZoneCountDeletedSignal();
		SignalBase<CameraViewportChangedSignalArgs> signalBase10 = new CameraViewportChangedSignal();
		SignalBase<IncrementPlayerLifeSignalArgs> signalBase11 = new IncrementPlayerLifeSignal();
		IAssetCacheProvider<SplineMovementData> splineCacheProvider = new SplineCacheProvider(SplineCache);
		TurnController turnController = new TurnController();
		MutableBrowserProvider mutableBrowserProvider = new MutableBrowserProvider();
		MutableWorkflowProvider mutableWorkflowProvider = new MutableWorkflowProvider();
		MutableCardViewProvider mutableCardViewProvider = new MutableCardViewProvider();
		MutableFakeCardViewProvider mutableFakeCardViewProvider = new MutableFakeCardViewProvider();
		MutableAvatarViewProvider mutableAvatarViewProvider = new MutableAvatarViewProvider();
		MutableEntityViewProvider mutableEntityViewProvider = new MutableEntityViewProvider(mutableCardViewProvider, mutableFakeCardViewProvider, mutableAvatarViewProvider);
		MutableCardHolderProvider mutableCardHolderProvider = new MutableCardHolderProvider();
		MutableZoneCountProvider provider = new MutableZoneCountProvider();
		MutablePlayerTimerProvider mutablePlayerTimerProvider = new MutablePlayerTimerProvider();
		ICardMovementController cardMovementController = new CardMovementController(mutableCardHolderProvider, new AltCardHolderCalculator(GenericPool, _gameStateManager, mutableCardHolderProvider));
		ICompanionDataProvider dataProvider = new CompanionDataProvider(_gameStateManager, MatchManager);
		ICompanionViewManager companionViewManager = new CompanionViewManager(new MutableCompanionViewProvider(), new CompanionViewController(new CompanionBuilder(AssetLookupSystem, cosmeticsProvider, this), dataProvider, signalBase2));
		IGameStatePlaybackController gameStatePlaybackController = new GameStatePlaybackController(_gameStateManager, _stateManager, playbackStarted, signalBase);
		ILifeTotalController lifeTotalController = new LifeTotalController(mutableAvatarViewProvider, signalBase, signalBase11);
		IPromptTextProvider promptTextProvider = new PromptTextProvider(CardDatabase, CardDatabase.PromptEngine, _gameStateManager);
		_promptTextManager = new PromptTextManager(promptTextProvider, new PromptTextController(CardDatabase.ClientLocProvider, promptTextProvider, signalBase7));
		CanvasManager canvasManager = new CanvasManager(MainCamera);
		ICanvasRootProvider canvasRootProvider = new CanvasRootProvider(canvasManager);
		IZoneCountManager zoneCountManager = new ZoneCountManager(provider, new ZoneCountViewBuilder(_gameStateManager, signalBase8, signalBase9, canvasRootProvider, AssetLookupSystem));
		IPlayerNameViewManager playerNameViewManager = new PlayerNameViewManager(cosmeticsProvider, AssetLookupSystem, clientLocProvider, MatchManager, MatchManager, new PlayerNameViewBuilder(AssetLookupSystem));
		IRelativePlayerIdCalculator relativePlayerIdCalculator;
		if (!flag)
		{
			relativePlayerIdCalculator = NullRelativePlayerIdCalculator.Default;
		}
		else
		{
			IRelativePlayerIdCalculator relativePlayerIdCalculator2 = new RelativePlayerIdCalculator(MatchManager.LocalPlayerSeatId, (uint)MatchManager.Players.Count);
			relativePlayerIdCalculator = relativePlayerIdCalculator2;
		}
		IRelativePlayerIdCalculator relativePlayerIdCalculator3 = relativePlayerIdCalculator;
		IPlayerOffsetCalculator playerOffsetCalculator;
		if (!flag)
		{
			playerOffsetCalculator = NullPlayerOffsetCalculator.Default;
		}
		else
		{
			IPlayerOffsetCalculator playerOffsetCalculator2 = new QuadrantOffsetCalculator(relativePlayerIdCalculator3);
			playerOffsetCalculator = playerOffsetCalculator2;
		}
		IPlayerOffsetCalculator playerOffsetCalculator3 = playerOffsetCalculator;
		IPlayerFocusController playerFocusController;
		if (!flag)
		{
			playerFocusController = NullPlayerFocusController.Default;
		}
		else
		{
			IPlayerFocusController playerFocusController2 = new PlayerFocusController_Quadrant(cameraAdapter.CameraRoot, SplineMovementSystem, relativePlayerIdCalculator3, playerOffsetCalculator3, _gameStateManager, mutablePlayerTimerProvider, playerNameViewManager, playerDeletedEvent);
			playerFocusController = playerFocusController2;
		}
		IPlayerFocusController playerFocusController3 = playerFocusController;
		bool flag2 = _npeState.ActiveNPEGame != null;
		IPlayerTimerManager playerTimerManager = PlayerTimerManagerFactory.Create(flag2, handheld, AssetLookupSystem, mutablePlayerTimerProvider, _gameStateManager, turnController, canvasRootProvider);
		_dimmingController = new DimmingController(GenericPool, mutableCardViewProvider);
		VfxProvider = new VfxProvider(GenericPool, UnityPool, cardViewBuilder.CardMaterialBuilder, AssetLookupSystem, SplineMovementSystem, _gameStateManager, mutableEntityViewProvider, SpaceConverterFactory.Create(_gameStateManager, mutableCardHolderProvider, mutableEntityViewProvider, battlefieldManager.transform));
		IResourceAnimationPlayer resourceAnimationPlayer = new ResourceAnimationPlayer(GenericPool, mutableEntityViewProvider, VfxProvider, SplineMovementSystem, splineCacheProvider, AssetLookupSystem);
		CombatAnimationPlayer = new CombatAnimationPlayer(UnityPool, VfxProvider, SplineMovementSystem, splineCacheProvider, mutableCardHolderProvider, AssetLookupSystem);
		ICardDissolveController cardDissolveController = new CardDissolveController(AssetLookupSystem, VfxProvider, cardViewBuilder, SplineMovementSystem);
		IEntityNameProvider<MtgCardInstance> cardNameProvider = new CardInstanceNameProvider(new AbilityInstanceNameProvider(CardDatabase.AbilityTextProvider), new CardNameProvider(CardDatabase.CardTitleProvider));
		IEntityNameProvider<MtgPlayer> playerNameProvider = new PlayerNameProvider(MatchManager);
		IEntityNameProvider<MtgEntity> entityNameProvider = new EntityNameProvider(cardNameProvider, playerNameProvider);
		IEntityNameProvider<uint> entityNameProvider2 = new IdNameProvider(entityNameProvider, _gameStateManager);
		ICardBuilder<DuelScene_CDC> cardBuilder = new DuelSceneCardBuilder(cardViewBuilder, _gameStateManager, mutableWorkflowProvider, VfxProvider, entityNameProvider2);
		IEqualityComparer<ICardDataAdapter> comparer = new IsSameCardDataComparer(GenericPool, _gameStateManager, ReferenceMapAggregate);
		Transform transform = new GameObject("Players").transform;
		transform.SetSiblingIndex(3);
		CardViewManager cardViewManager = new CardViewManager(mutableCardViewProvider, cardBuilder);
		FakeCardViewManager fakeCardViewManager = new FakeCardViewManager(mutableFakeCardViewProvider, cardBuilder);
		Wotc.Mtga.DuelScene.AvatarBuilder avatarBuilder = new Wotc.Mtga.DuelScene.AvatarBuilder(playerCreatedEvent, playerDeletedEvent, AssetLookupSystem, transform);
		IAvatarLayout avatarLayout;
		if (!flag)
		{
			avatarLayout = NullAvatarLayout.Default;
		}
		else
		{
			IAvatarLayout avatarLayout2 = new AvatarLayout_Quadrant(playerOffsetCalculator3);
			avatarLayout = avatarLayout2;
		}
		ViewManager = new EntityViewManager(mutableEntityViewProvider, cardViewManager, fakeCardViewManager, new AvatarViewManager(mutableAvatarViewProvider, avatarBuilder, avatarLayout));
		CardHolderManager = new CardHolderManager(mutableCardHolderProvider, new CardHolderBuilder(AssetLookupSystem, signalBase3, signalBase6), signalBase4, signalBase5);
		MatchManager.SideboardSubmitted += OnSideboardSubmitted;
		if (flag2)
		{
			NPE_Game activeNPEGame = _npeState.ActiveNPEGame;
			_npeState.BI_NPEProgressUpdate(new NPEState.NPEProgressContext(NPEState.NPEProgressMarker.Started_Game, _npeState.ActiveNPEGameNumber));
			if (AssetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<NPEControllerPrefab> loadedTree))
			{
				NPEControllerPrefab payload = loadedTree.GetPayload(AssetLookupSystem.Blackboard);
				if (payload != null)
				{
					_npeController = AssetLoader.Instantiate<NPEController>(payload.PrefabPath);
					_npeController.Init(activeNPEGame, this, _matchContext.Get<IMatchSceneStateProvider>(), AssetLookupSystem, MatchManager);
					_npeDirector = new NPEDirector(_npeController, _npeState, UXEventQueue, activeNPEGame);
					_npeController.SetDirector(_npeDirector);
				}
			}
			SceneManager.MoveGameObjectToScene(_npeController.gameObject, SceneManager.GetSceneByName("DuelScene"));
		}
		else
		{
			_npeDirector = null;
		}
		GreInterface gre = _gre;
		IGameStateManager gameStateManager = _gameStateManager;
		NPEDirector npeDirector = _npeDirector;
		ISettingsMessageGenerator settingsMessageGenerator2;
		if (npeDirector == null || npeDirector.AllowsEndTurnButton)
		{
			ISettingsMessageGenerator settingsMessageGenerator = new SettingsMessageGenerator();
			settingsMessageGenerator2 = settingsMessageGenerator;
		}
		else
		{
			settingsMessageGenerator2 = NullSettingsMessageGenerator.Default;
		}
		AutoRespManager = new AutoResponseManager(this, gre, gameStateManager, mutableWorkflowProvider, settingsMessageGenerator2, _settingsMenuHost);
		UIMessageHandler.UpdateSendUIMessageCallback(_gre.SubmitUIMessage);
		HangerController hangerController = new HangerController();
		hangerController.Init(base.transform, MainCamera, SplineMovementSystem, FaceHanger.Create(AssetLookupSystem, base.transform, LayerMask.NameToLayer("Hand")), AbilityHanger.Create(AssetLookupSystem, base.transform, LayerMask.NameToLayer("Hand")));
		CardDragController cardDragController = new CardDragController(GenericPool, AssetLookupSystem, cardViewBuilder, MainCamera, canvasManager, mutableCardHolderProvider, mutableBrowserProvider, hangerController, SplineMovementSystem);
		TooltipSystem tooltipSystem = _matchContext.Get<TooltipSystem>();
		UIManager = new UIManager(canvasManager, this, AssetLookupSystem, UnityPool, GenericPool, tooltipSystem, MatchManager, _settingsMenuHost, _npeState, playerNameViewManager);
		UIManager.PhaseLadder.Init(this, _gameStateManager, turnController, _settingsMessageController, _gre, tooltipSystem);
		UIManager.TurnChanged.Init(AssetLookupSystem);
		MutableEmoteControllerProvider mutableEmoteControllerProvider = new MutableEmoteControllerProvider();
		IEmoteManager emoteManager2;
		if (_npeDirector == null)
		{
			IEmoteManager emoteManager = new EmoteManager(mutableEmoteControllerProvider, AssetLookupSystem, Logger, _emoteDataProvider, EmoteManager.GetEquippedEmotes(_emoteDataProvider, MatchManager, cosmeticsProvider), IEmoteSelectionGroupBuilder.Create(mutableAvatarViewProvider, mutableEmoteControllerProvider, mutablePlayerTimerProvider, UIManager), UXEventQueue, UIMessageHandler, canvasManager.GetCanvasRoot(CanvasLayer.ScreenSpace_Default, "Emotes"));
			emoteManager2 = emoteManager;
		}
		else
		{
			emoteManager2 = NullEmoteManager.Default;
		}
		IEmoteManager emoteManager3 = emoteManager2;
		IPlayerNumericAidController playerNumericAidController = new PlayerNumericAidController(GenericPool, emoteManager3);
		UIManager.PlayerNames.SetHighlightController(_highlightManager);
		BrowserManager = new BrowserManager(mutableBrowserProvider, MatchManager, _stateManager, this, canvasManager, cardDragController, _settingsMenuHost);
		CardHolderManager.BuildNonZoneCardHolders();
		IPlayWarningManager playWarningManager = new PlayWarningManager(CardDatabase, mutableCardHolderProvider, cardMovementController, BrowserManager, turnController, AssetLookupSystem);
		CardHoverController cardHoverController = new CardHoverController(UnityPool, SplineMovementSystem, MainCamera, cardViewBuilder, cardBuilder, mutableCardViewProvider, _highlightManager, CardHolderManager, BrowserManager, hangerController, new RelatedCardIdProvider(GenericPool, _gameStateManager, mutableEntityViewProvider, ReferenceMapAggregate, mutableWorkflowProvider, ViewManager), _npeDirector);
		SpinnerController = new SpinnerController(GenericPool, UnityPool, canvasManager.GetCanvasRoot(CanvasLayer.ScreenSpace_Default), MainCamera, _gameStateManager, ViewManager, mutableCardHolderProvider, AssetLookupSystem);
		Context = new Context(GenerateGameScopedComponents(CardDatabase, SplineMovementSystem, splineCacheProvider, _gameStateManager, mutableWorkflowProvider, mutableWorkflowProvider, turnController, VfxProvider, cardNameProvider, playerNameProvider, entityNameProvider, entityNameProvider2, cardBuilder, ViewManager, emoteManager3, companionViewManager, CardHolderManager, cardMovementController, _stateManager, gameStatePlaybackController, lifeTotalController, BrowserManager, new GameStartController(_gameStateManager, BrowserManager), _promptTextManager, zoneCountManager, playerFocusController3, cardDissolveController, cardHoverController, cardHoverController, _highlightManager, _dimmingController, canvasRootProvider, playerNumericAidController, resourceAnimationPlayer, playerTimerManager, playerOffsetCalculator3, playWarningManager), _matchContext);
		DeckFormat deckFormat = MatchManager.Event?.PlayerEvent?.Format;
		Transform hangerParent = base.transform;
		if (PlatformUtils.IsHandheld())
		{
			string prefabPath = AssetLookupSystem.GetPrefabPath<StaticZoomControllerPrefab, StaticZoomController>();
			_staticHoverCardHolder = AssetLoader.Instantiate<StaticZoomController>(prefabPath, canvasManager.GetCanvasRoot(CanvasLayer.ScreenSpace_UnderStack_ScaledCorrectly));
			_staticHoverCardHolder.Init(this, ViewManager, SplineMovementSystem, cardViewBuilder, clientLocProvider, MatchManager, FaceInfoGeneratorFactory.DuelScene.Handheld.HoverGenerator(CardDatabase, _highlightManager, AssetLookupSystem, deckFormat, GenericPool));
		}
		IFaceInfoGenerator faceInfoGenerator = FaceInfoGeneratorFactory.DuelScene.HoverGenerator(CardDatabase, _highlightManager, deckFormat, GenericPool);
		IFaceInfoGenerator faceInfoGenerator2 = FaceInfoGeneratorFactory.DuelScene.ExamineGenerator(CardDatabase, AssetLookupSystem, deckFormat, GenericPool);
		hangerController.InitFaceHanger(faceInfoGenerator, cardViewBuilder);
		hangerController.InitAbilityHanger(hangerParent, Context, AssetLookupSystem, faceInfoGenerator, deckFormat, NpeDirector);
		CardHolderManager.Examine.InitHangers(this, SplineMovementSystem, AssetLookupSystem, CardDatabase, cardViewBuilder, comparer);
		CardHolderManager.Examine.HangerController.InitFaceHanger(faceInfoGenerator2, cardViewBuilder);
		CardHolderManager.Examine.HangerController.InitAbilityHanger(hangerParent, Context, AssetLookupSystem, faceInfoGenerator2, deckFormat, NpeDirector);
		InteractionSystem = new GameInteractionSystem(CardDatabase, _highlightManager, _gameStateManager, mutableWorkflowProvider, CardHolderManager, _staticHoverCardHolder, ViewManager, CombatAnimationPlayer, CardHolderManager.Examine, SplineMovementSystem, BrowserManager, cardHoverController, cardDragController, hangerController);
		_combatIconUpdater = new CombatIconUpdater(_gameStateManager, mutableWorkflowProvider);
		_visualStateCardProvider = new NoDuplicatesDecorator(GenericPool, new AggregateVisualCardProvider(new ViewManagerCardProvider(ViewManager), new BrowserCardProvider(CardHolderManager, BrowserManager)));
		_intentionLineManager = new IntentionLineManager(GenericPool, UnityPool, ReferenceMapAggregate, UXEventQueue, _gameStateManager, mutableWorkflowProvider, mutableEntityViewProvider, signalBase3, CombatAnimationPlayer, BrowserManager, AssetLookupSystem, base.transform);
		if (_npeDirector != null)
		{
			UIManager.ShowEndTurnButton = _npeDirector.AllowsEndTurnButton;
			UIManager.ShowPhaseLadder = _npeDirector.ShowPhaseLadder;
			UIManager.PhaseLadder.InTutorial = true;
		}
		_uxEventTranslator = new DefaultEventTranslation(this, AssetLookupSystem, Context, UXEventPostProcessing.Generate(Context, AssetLookupSystem, this));
		WorkflowController = new WorkflowController(WorkflowTranslator(comparer), mutableWorkflowProvider, OverridesConfiguration.Local.GetFeatureToggleValue("Loop") ? new LoopModel(_gameStateManager, UIMessageHandler, new UnityLogger("loop", LoggerLevel.Debug), UXEventQueue, GenericPool) : null);
		foreach (HeadlessClient familiar in MatchManager.Familiars)
		{
			_familiarRequestHandlers.Add(new FamiliarRequestHandler(familiar, Context));
		}
		ICheckpointController checkpointController2;
		if (!MatchManager.CreateMatchConfig.Equals(default(GreClient.Network.MatchConfig)))
		{
			ICheckpointController checkpointController = new CheckpointController(_gre, MatchManager.CreateMatchConfig);
			checkpointController2 = checkpointController;
		}
		else
		{
			checkpointController2 = NullCheckpointController.Default;
		}
		ICheckpointController checkpointController3 = checkpointController2;
		_clientUpdateHandler = new ClientUpdateHandler(GameStateUpdateHandlerFactory.Create(_npeDirector, ViewManager, ReferenceMapAggregate, _gameStateManager, playerTimerManager, UXEventQueue, _uxEventTranslator, _promptTextManager, WorkflowController), new SettingsNotificationHandler(_settingsMessageController, AutoRespManager), new UIMessageNotificationHandler(UIMessageHandler), new PromptNotificationHandler(Context, UXEventQueue, AssetLookupSystem), new TimeoutNotificationHandler(AssetLookupSystem, canvasRootProvider, mutablePlayerTimerProvider), new TimerUpdateHandler(playerTimerManager), new EdictalUpdateHandler(WorkflowController, UIManager.ManaColorSelector, BrowserManager), new CheckpointResponseHandler(checkpointController3));
		_disposables.Add(new PlayerCreatedMediator(UIManager, Context, AssetLookupSystem, playerCreatedEvent));
		_disposables.Add(new CurrentGameStateUpdatedMediator(Context));
		_disposables.Add(new CardHolderRelativeToCameraMediator(GenericPool, cameraAdapter, mutableCardHolderProvider, signalBase10, signalBase3, signalBase6));
		_disposables.Add(new CardDragStartedMediator(cardHoverController, cardDragController));
		_disposables.Add(new PlayerHandShakeMediator(GenericPool, CombatAnimationPlayer, signalBase4));
		_disposables.Add(new ZoneLayoutMediator(GenericPool, playerOffsetCalculator3, signalBase4, signalBase5, signalBase2));
		_disposables.Add(new BattlefieldCombatDamageMediator(battlefieldManager, CombatAnimationPlayer));
		_disposables.Add(new PlayerNameIntroVFXMediator(Context.Get<IDuelSceneStateProvider>(), UIManager));
		_disposables.Add(new CompanionEmoteMediator(GenericPool, Context.Get<IEntityDialogControllerProvider>(), playerCreatedEvent, signalBase2));
		_disposables.Add(new StaticElementLayoutMediator(signalBase3, signalBase6, UIManager.BattleFieldStaticElementsLayout));
		_disposables.Add(new StackModifiedCardholderMediator(signalBase3, signalBase6, GenericPool));
		_disposables.Add(new PromptTextDisplayMediator(UIManager.UserPrompt, BrowserManager, signalBase7));
		_disposables.Add(new LanguageChangedMediator(_promptTextManager, ViewManager, mutableCardHolderProvider, Languages.LanguageChangedSignal));
		_disposables.Add(new PreCardDestroyedMediator(cardViewBuilder, SplineMovementSystem));
		_disposables.Add(new WorkflowVisualsMediator(WorkflowController, Context.Get<IHighlightController>(), _dimmingController, _intentionLineManager, _promptTextManager, UIManager));
		_disposables.Add(new InteractionAppliedMediator(WorkflowController, Context.Get<IGameStartController>(), Logger));
		_disposables.Add(new InteractionClearedMediator(WorkflowController, BrowserManager));
		_disposables.Add(new ZoneCardHolderMediator(signalBase3, signalBase6, WorkflowController, BrowserManager, Context.Get<IHighlightProvider>()));
		_disposables.Add(new FullControlMediator(UIManager.FullControl, AutoRespManager));
		_disposables.Add(new ZoneCountViewMediator(_gameStateManager, signalBase8, signalBase9, signalBase3, signalBase6));
		_disposables.Add(new EndTurnButtonMediator(UIManager.EndTurnButton, cardDragController, AutoRespManager));
		_disposables.Add(new ClearDynamicAbilitiesMediator(CardDatabase.DynamicAbilityDataProvider));
		_disposables.Add(new PlayerPresenceMediator(UIMessageHandler, Context.Get<IPlayerPresenceController>()));
		_disposables.Add(new BattlefieldInputMediator(GenericPool, UnityPool, InteractionSystem));
		_disposables.Add(new BattlefieldEventMediator(GenericPool, _gameStateManager, signalBase, signalBase11));
		_disposables.Add(new KeyboardManagerSubscriptionMediator(Context.Get<KeyboardManager>(), this, BrowserManager, _staticHoverCardHolder));
		_disposables.Add(new BrowserVisibilityLowTimeMediator(BrowserManager, mutablePlayerTimerProvider));
		_disposables.Add(new PhaseChangeMediator(turnController, mutableCardViewProvider, mutableCardHolderProvider, AutoRespManager));
		_disposables.Add(new LoopMediator(WorkflowController.LoopModel, playerNameViewManager));
		_disposables.Add(new CardHolderCreationMediator(signalBase3, this, ViewManager, SplineMovementSystem, cardViewBuilder, CardDatabase.ClientLocProvider, MatchManager, mutableCardHolderProvider, comparer));
		_disposables.AddIfNotNull(SplineMovementSystem as IDisposable);
		_disposables.AddIfNotNull(_uxEventTranslator as IDisposable);
		_disposables.AddIfNotNull(UIManager);
		_disposables.AddIfNotNull(hangerController);
		_disposables.AddIfNotNull(cardHoverController);
		_disposables.AddIfNotNull(InteractionSystem);
		_disposables.AddIfNotNull(Context.Get<IActionEffectController>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<ISuspendLikeController>() as IDisposable);
		_disposables.AddIfNotNull(UIMessageHandler);
		_disposables.AddIfNotNull(WorkflowController);
		_disposables.AddIfNotNull(ViewManager);
		_disposables.AddIfNotNull(Context.Get<IPendingEffectController>());
		_disposables.AddIfNotNull(Context.Get<IQualificationController>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IGamewideCountController>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IReplacementEffectController>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IPlayerAbilityController>() as IDisposable);
		_disposables.AddIfNotNull(_npeDirector);
		_disposables.AddIfNotNull(_intentionLineManager);
		_disposables.AddIfNotNull(Context.Get<ICardDissolveController>() as IDisposable);
		_disposables.AddIfNotNull(cardDragController);
		_disposables.AddIfNotNull(UXEventQueue);
		_disposables.AddIfNotNull(Context.Get<IHighlightManager>() as IDisposable);
		_disposables.AddIfNotNull(_altArtMatchSettings);
		_disposables.AddIfNotNull(Context.Get<IAssetCacheProvider<SplineMovementData>>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IPlayerPresenceController>() as IDisposable);
		_disposables.AddIfNotNull(SpinnerController);
		_disposables.AddIfNotNull(Context.Get<IResolutionEffectController>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IGameStateManager>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<ITurnController>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IPlayerSpriteProvider>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IWorkflowProvider>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IEmoteManager>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<ICompanionViewManager>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IFaceDownIdProvider>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IDesignationController>() as IDisposable);
		_disposables.AddIfNotNull(CombatAnimationPlayer);
		_disposables.AddIfNotNull(Context.Get<ICardHoverController>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IGameStartController>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<ICardHolderManager>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IPromptTextManager>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IPlayerFocusController>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IRevealedCardsManager>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IZoneCountManager>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IDuelSceneStateManager>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IDimmingController>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<ILifeTotalController>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IPlayerNumericAidController>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IManaProducedAnimationPlayer>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<ICounterProducedAnimationPlayer>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IPlayerTimerManager>() as IDisposable);
		_disposables.AddIfNotNull(Context.Get<IPlayWarningManager>() as IDisposable);
		_updateObservers.Add(new CameraViewportChangedDispatcher(cameraAdapter, signalBase10));
		_updateObservers.AddIfNotNull(Context.Get<IPlayerFocusController>() as IUpdate);
		_updateObservers.Add(BrowserManager);
		_updateObservers.AddIfNotNull(Logger);
		_updateObservers.AddIfNotNull(SpinnerController);
		_updateObservers.AddIfNotNull(Context.Get<ICardDissolveController>() as IUpdate);
		_updateObservers.Add(hangerController);
		_updateObservers.Add(new HoverEndHandler(mutableWorkflowProvider, UIManager.ManaColorSelector, CombatAnimationPlayer, CardHolderManager.Examine, mutableBrowserProvider, cardHoverController));
		_updateObservers.Add(new DragEndHandler(cardDragController, UIManager.ManaColorSelector, CombatAnimationPlayer, mutableBrowserProvider));
		_updateObservers.Add(new ButtonDisplayHandler(UIManager, mutableBrowserProvider, UIManager.ManaColorSelector));
		_updateObservers.Add(cardHoverController);
		_updateObservers.Add(cardDragController);
		foreach (FamiliarRequestHandler familiarRequestHandler in _familiarRequestHandlers)
		{
			_updateObservers.Add(familiarRequestHandler);
		}
		if (CanDisplayDebugUI())
		{
			new GameObject("DebugUI").AddComponent<DebugUI>().AddDebugModules(DebugModule.BasicModules(this, checkpointController3, _gameStateManager, MatchManager));
		}
		if (MatchManager.IsPracticeGame)
		{
			BotControlConfigurationSO botControlConfigurationSO = BotControlManager.FetchBotConfig(AssetLookupSystem);
			SparkyChatter item = new SparkyChatter(new ChatterConfig(botControlConfigurationSO.sparkyIdleTimer, botControlConfigurationSO.sparkyIdleChatterOptions, botControlConfigurationSO.sparkyThinkingTimer, botControlConfigurationSO.sparkyThinkingChatterOptions, botControlConfigurationSO.sparkyCreatureDeathChatterOptions, botControlConfigurationSO.gameStartChatterOptions, botControlConfigurationSO.emoteReplyChatterOptions), CardDatabase.ClientLocProvider, _gameStateManager, emoteManager3, UXEventQueue, UIMessageHandler);
			_disposables.Add(item);
			_updateObservers.Add(item);
		}
		ScreenKeepAlive.KeepScreenAwake();
	}

	private IReadOnlyDictionary<Type, object> GenerateGameScopedComponents(ICardDatabaseAdapter cardDatabase, ISplineMovementSystem splineMovementSystem, IAssetCacheProvider<SplineMovementData> splineCacheProvider, IGameStateManager gameStateManager, IWorkflowProvider workflowProvider, IClickableWorkflowProvider clickableWorkflowProvider, TurnController turnController, IVfxProvider vfxProvider, IEntityNameProvider<MtgCardInstance> cardNameProvider, IEntityNameProvider<MtgPlayer> playerNameProvider, IEntityNameProvider<MtgEntity> entityNameProvider, IEntityNameProvider<uint> idNameProvider, ICardBuilder<DuelScene_CDC> dsCardBuilder, IEntityViewManager viewManager, IEmoteManager emoteManager, ICompanionViewManager companionViewManager, ICardHolderManager cardHolderManager, ICardMovementController cardMovementController, IDuelSceneStateManager duelSceneStateManager, IGameStatePlaybackController gameStatePlaybackController, ILifeTotalController lifeTotalController, IBrowserManager browserManager, IGameStartController gameStartController, IPromptTextManager promptTextManager, IZoneCountManager zoneCountManager, IPlayerFocusController playerFocusController, ICardDissolveController cardDissolveController, IAvatarHoverController avatarHoverController, ICardHoverController cardHoverController, IHighlightManager highlightManager, IDimmingController dimmingController, ICanvasRootProvider canvasRootProvider, IPlayerNumericAidController playerNumericAidController, IResourceAnimationPlayer resourceAnimationPlayer, IPlayerTimerManager playerTimerManager, IPlayerOffsetCalculator playerOffsetCalculator, IPlayWarningManager playWarningManager)
	{
		IStaticListLocProvider staticListLocProvider = new StaticListLocProvider(cardDatabase.GreLocProvider);
		ResolutionEffectController value = new ResolutionEffectController();
		IGameEffectBuilder gameEffectBuilder = new GameEffectBuilder(viewManager, cardHolderManager);
		IRevealedCardsManager value2 = new RevealedCardsManager(new MutableRevealedCardsProvider(), cardDatabase, gameStateManager, cardHolderManager);
		Dictionary<Type, object> dictionary = new Dictionary<Type, object>();
		Type typeFromHandle = typeof(ISplineMovementSystem);
		dictionary[typeFromHandle] = splineMovementSystem;
		Type typeFromHandle2 = typeof(IAssetCacheProvider<SplineMovementData>);
		dictionary[typeFromHandle2] = splineCacheProvider;
		Type typeFromHandle3 = typeof(IGameStateManager);
		dictionary[typeFromHandle3] = gameStateManager;
		Type typeFromHandle4 = typeof(IGameStateProvider);
		dictionary[typeFromHandle4] = gameStateManager;
		Type typeFromHandle5 = typeof(IGameStateController);
		dictionary[typeFromHandle5] = gameStateManager;
		Type typeFromHandle6 = typeof(IWorkflowProvider);
		dictionary[typeFromHandle6] = workflowProvider;
		Type typeFromHandle7 = typeof(IClickableWorkflowProvider);
		dictionary[typeFromHandle7] = clickableWorkflowProvider;
		Type typeFromHandle8 = typeof(IGameplaySettingsProvider);
		dictionary[typeFromHandle8] = AutoRespManager;
		Type typeFromHandle9 = typeof(IReferenceMap);
		dictionary[typeFromHandle9] = ReferenceMapAggregate;
		Type typeFromHandle10 = typeof(UXEventQueue);
		dictionary[typeFromHandle10] = UXEventQueue;
		Type typeFromHandle11 = typeof(ICardBuilder<DuelScene_CDC>);
		dictionary[typeFromHandle11] = dsCardBuilder;
		Type typeFromHandle12 = typeof(IEntityViewManager);
		dictionary[typeFromHandle12] = viewManager;
		Type typeFromHandle13 = typeof(IEntityViewProvider);
		dictionary[typeFromHandle13] = viewManager;
		Type typeFromHandle14 = typeof(ICardViewProvider);
		dictionary[typeFromHandle14] = viewManager;
		Type typeFromHandle15 = typeof(ICardViewController);
		dictionary[typeFromHandle15] = viewManager;
		Type typeFromHandle16 = typeof(ICardViewManager);
		dictionary[typeFromHandle16] = viewManager;
		Type typeFromHandle17 = typeof(IFakeCardViewProvider);
		dictionary[typeFromHandle17] = viewManager;
		Type typeFromHandle18 = typeof(IFakeCardViewController);
		dictionary[typeFromHandle18] = viewManager;
		Type typeFromHandle19 = typeof(IFakeCardViewManager);
		dictionary[typeFromHandle19] = viewManager;
		Type typeFromHandle20 = typeof(IAvatarViewProvider);
		dictionary[typeFromHandle20] = viewManager;
		Type typeFromHandle21 = typeof(IAvatarViewController);
		dictionary[typeFromHandle21] = viewManager;
		Type typeFromHandle22 = typeof(IAvatarViewManager);
		dictionary[typeFromHandle22] = viewManager;
		Type typeFromHandle23 = typeof(IPlayerTimerProvider);
		dictionary[typeFromHandle23] = playerTimerManager;
		Type typeFromHandle24 = typeof(IPlayerTimerController);
		dictionary[typeFromHandle24] = playerTimerManager;
		Type typeFromHandle25 = typeof(IPlayerTimerManager);
		dictionary[typeFromHandle25] = playerTimerManager;
		Type typeFromHandle26 = typeof(IRevealedCardsProvider);
		dictionary[typeFromHandle26] = value2;
		Type typeFromHandle27 = typeof(IRevealedCardsController);
		dictionary[typeFromHandle27] = value2;
		Type typeFromHandle28 = typeof(IRevealedCardsManager);
		dictionary[typeFromHandle28] = value2;
		Type typeFromHandle29 = typeof(IEmoteManager);
		dictionary[typeFromHandle29] = emoteManager;
		Type typeFromHandle30 = typeof(IEmoteControllerProvider);
		dictionary[typeFromHandle30] = emoteManager;
		Type typeFromHandle31 = typeof(IEntityDialogControllerProvider);
		dictionary[typeFromHandle31] = emoteManager;
		Type typeFromHandle32 = typeof(ICompanionViewManager);
		dictionary[typeFromHandle32] = companionViewManager;
		Type typeFromHandle33 = typeof(ICompanionViewProvider);
		dictionary[typeFromHandle33] = companionViewManager;
		Type typeFromHandle34 = typeof(ICompanionViewController);
		dictionary[typeFromHandle34] = companionViewManager;
		Type typeFromHandle35 = typeof(IEntityNameProvider<uint>);
		dictionary[typeFromHandle35] = idNameProvider;
		Type typeFromHandle36 = typeof(IEntityNameProvider<MtgEntity>);
		dictionary[typeFromHandle36] = entityNameProvider;
		Type typeFromHandle37 = typeof(IEntityNameProvider<MtgPlayer>);
		dictionary[typeFromHandle37] = playerNameProvider;
		Type typeFromHandle38 = typeof(IEntityNameProvider<MtgCardInstance>);
		dictionary[typeFromHandle38] = cardNameProvider;
		Type typeFromHandle39 = typeof(ICardHolderProvider);
		dictionary[typeFromHandle39] = cardHolderManager;
		Type typeFromHandle40 = typeof(ICardHolderController);
		dictionary[typeFromHandle40] = cardHolderManager;
		Type typeFromHandle41 = typeof(ICardHolderManager);
		dictionary[typeFromHandle41] = cardHolderManager;
		Type typeFromHandle42 = typeof(IGameEffectController);
		dictionary[typeFromHandle42] = cardHolderManager;
		Type typeFromHandle43 = typeof(ICardMovementController);
		dictionary[typeFromHandle43] = cardMovementController;
		Type typeFromHandle44 = typeof(IStaticListLocProvider);
		dictionary[typeFromHandle44] = staticListLocProvider;
		Type typeFromHandle45 = typeof(IBrowserProvider);
		dictionary[typeFromHandle45] = browserManager;
		Type typeFromHandle46 = typeof(IBrowserController);
		dictionary[typeFromHandle46] = browserManager;
		Type typeFromHandle47 = typeof(IBrowserManager);
		dictionary[typeFromHandle47] = browserManager;
		Type typeFromHandle48 = typeof(IZoneCountManager);
		dictionary[typeFromHandle48] = zoneCountManager;
		Type typeFromHandle49 = typeof(IZoneCountController);
		dictionary[typeFromHandle49] = zoneCountManager;
		Type typeFromHandle50 = typeof(IZoneCountProvider);
		dictionary[typeFromHandle50] = zoneCountManager;
		Type typeFromHandle51 = typeof(IHighlightProvider);
		dictionary[typeFromHandle51] = highlightManager;
		Type typeFromHandle52 = typeof(IHighlightController);
		dictionary[typeFromHandle52] = highlightManager;
		Type typeFromHandle53 = typeof(IHighlightManager);
		dictionary[typeFromHandle53] = highlightManager;
		Type typeFromHandle54 = typeof(IDimmingController);
		dictionary[typeFromHandle54] = dimmingController;
		Type typeFromHandle55 = typeof(IGameStartController);
		dictionary[typeFromHandle55] = gameStartController;
		Type typeFromHandle56 = typeof(IAvatarHoverController);
		dictionary[typeFromHandle56] = avatarHoverController;
		Type typeFromHandle57 = typeof(ICardHoverController);
		dictionary[typeFromHandle57] = cardHoverController;
		Type typeFromHandle58 = typeof(ICanvasRootProvider);
		dictionary[typeFromHandle58] = canvasRootProvider;
		Type typeFromHandle59 = typeof(IManaProducedAnimationPlayer);
		dictionary[typeFromHandle59] = new ManaProducedAnimationPlayer(gameStateManager, viewManager, cardHolderManager, resourceAnimationPlayer);
		Type typeFromHandle60 = typeof(ICounterProducedAnimationPlayer);
		dictionary[typeFromHandle60] = new CounterProducedProducedAnimationPlayer(gameStateManager, viewManager, cardHolderManager, resourceAnimationPlayer);
		Type typeFromHandle61 = typeof(IActionEffectController);
		dictionary[typeFromHandle61] = new ActionEffectController(new LimboActionEffectController(cardDatabase, gameStateManager, gameEffectBuilder), new SideboardActionEffectController(cardDatabase.CardDataProvider, gameStateManager, cardHolderManager, dsCardBuilder, cardDatabase.ClientLocProvider, AssetLookupSystem));
		Type typeFromHandle62 = typeof(IPendingEffectController);
		dictionary[typeFromHandle62] = new PendingEffectController(cardDatabase, cardDatabase.ClientLocProvider, dsCardBuilder, cardHolderManager, AssetLookupSystem);
		Type typeFromHandle63 = typeof(IDelayedTriggerController);
		dictionary[typeFromHandle63] = new DelayedTriggerController(cardDatabase, viewManager, cardHolderManager, AssetLookupSystem);
		Type typeFromHandle64 = typeof(IDesignationController);
		dictionary[typeFromHandle64] = new DesignationController(cardDatabase.CardDataProvider, gameStateManager, gameEffectBuilder, viewManager, cardDatabase.PromptEngine, UIManager.PlayerNames, AssetLookupSystem, VfxProvider);
		Type typeFromHandle65 = typeof(IQualificationController);
		dictionary[typeFromHandle65] = new QualificationController(cardDatabase, cardDatabase.ClientLocProvider, viewManager, gameEffectBuilder, gameStateManager, AssetLookupSystem);
		Type typeFromHandle66 = typeof(IGamewideCountController);
		dictionary[typeFromHandle66] = new GamewideCountController(cardDatabase, gameStateManager, gameEffectBuilder);
		Type typeFromHandle67 = typeof(ISuspendLikeController);
		dictionary[typeFromHandle67] = new SuspendLikeController(cardDatabase, gameStateManager, gameEffectBuilder);
		Type typeFromHandle68 = typeof(IReplacementEffectController);
		dictionary[typeFromHandle68] = new ReplacementEffectController(cardDatabase, gameStateManager, gameEffectBuilder, entityNameProvider);
		Type typeFromHandle69 = typeof(ITurnController);
		dictionary[typeFromHandle69] = turnController;
		Type typeFromHandle70 = typeof(ITurnInfoProvider);
		dictionary[typeFromHandle70] = turnController;
		Type typeFromHandle71 = typeof(IExtraTurnRenderer);
		dictionary[typeFromHandle71] = new ExtraTurnRenderer(viewManager, cardHolderManager, cardDatabase.ClientLocProvider);
		Type typeFromHandle72 = typeof(IPlayerAbilityController);
		dictionary[typeFromHandle72] = new PlayerAbilityController(cardDatabase, gameStateManager, gameEffectBuilder, AssetLookupSystem);
		Type typeFromHandle73 = typeof(IPlayerPresenceController);
		dictionary[typeFromHandle73] = new PlayerPresenceController(viewManager, cardHolderManager);
		Type typeFromHandle74 = typeof(IFaceDownIdProvider);
		dictionary[typeFromHandle74] = new FaceDownIdProvider(gameStateManager);
		Type typeFromHandle75 = typeof(IVfxProvider);
		dictionary[typeFromHandle75] = vfxProvider;
		Type typeFromHandle76 = typeof(IResolutionEffectController);
		dictionary[typeFromHandle76] = value;
		Type typeFromHandle77 = typeof(IResolutionEffectProvider);
		dictionary[typeFromHandle77] = value;
		Type typeFromHandle78 = typeof(IActionProvider);
		dictionary[typeFromHandle78] = new ActionProvider(gameStateManager, workflowProvider);
		Type typeFromHandle79 = typeof(IPlayerSpriteProvider);
		dictionary[typeFromHandle79] = new PlayerSpriteProvider(AssetLookupSystem, gameStateManager, MatchManager);
		Type typeFromHandle80 = typeof(IAvatarInputController);
		dictionary[typeFromHandle80] = IAvatarInputController.Create(emoteManager, playerTimerManager, cardHolderManager, avatarHoverController, clickableWorkflowProvider, UIManager.FullControl);
		Type typeFromHandle81 = typeof(IDuelSceneStateManager);
		dictionary[typeFromHandle81] = duelSceneStateManager;
		Type typeFromHandle82 = typeof(IDuelSceneStateProvider);
		dictionary[typeFromHandle82] = duelSceneStateManager;
		Type typeFromHandle83 = typeof(IDuelSceneStateController);
		dictionary[typeFromHandle83] = duelSceneStateManager;
		Type typeFromHandle84 = typeof(IGameStatePlaybackController);
		dictionary[typeFromHandle84] = gameStatePlaybackController;
		Type typeFromHandle85 = typeof(ILifeTotalController);
		dictionary[typeFromHandle85] = lifeTotalController;
		Type typeFromHandle86 = typeof(IPromptTextProvider);
		dictionary[typeFromHandle86] = promptTextManager;
		Type typeFromHandle87 = typeof(IPromptTextController);
		dictionary[typeFromHandle87] = promptTextManager;
		Type typeFromHandle88 = typeof(IPromptTextManager);
		dictionary[typeFromHandle88] = promptTextManager;
		Type typeFromHandle89 = typeof(IModalBrowserCardHeaderProvider);
		dictionary[typeFromHandle89] = new ModalBrowserCardHeaderProvider(cardDatabase, staticListLocProvider, ModalBrowserCardHeaderProvider.DefaultSubProviders(cardDatabase, gameStateManager, entityNameProvider, AssetLookupSystem));
		Type typeFromHandle90 = typeof(IPlayerFocusController);
		dictionary[typeFromHandle90] = playerFocusController;
		Type typeFromHandle91 = typeof(IBrowserHeaderTextProvider);
		dictionary[typeFromHandle91] = new BrowserHeaderTextProvider(AssetLookupSystem, cardDatabase.ClientLocProvider, promptTextManager);
		Type typeFromHandle92 = typeof(ICardDissolveController);
		dictionary[typeFromHandle92] = cardDissolveController;
		Type typeFromHandle93 = typeof(IPlayerNumericAidController);
		dictionary[typeFromHandle93] = playerNumericAidController;
		Type typeFromHandle94 = typeof(IPlayerOffsetCalculator);
		dictionary[typeFromHandle94] = playerOffsetCalculator;
		Type typeFromHandle95 = typeof(IPlayWarningProvider);
		dictionary[typeFromHandle95] = playWarningManager;
		Type typeFromHandle96 = typeof(IPlayWarningController);
		dictionary[typeFromHandle96] = playWarningManager;
		Type typeFromHandle97 = typeof(IPlayWarningManager);
		dictionary[typeFromHandle97] = playWarningManager;
		return dictionary;
	}

	private IWorkflowTranslator WorkflowTranslator(IEqualityComparer<ICardDataAdapter> comparer)
	{
		IWorkflowTranslator nestedTranslation;
		if (_npeDirector != null)
		{
			ClientSideInteraction clientSideInteraction = new ClientSideInteraction(Logger, UIManager, Context, AssetLookupSystem);
			IWorkflowTranslation<SelectNRequest> selectNTranslation = new SelectNWorkflowTranslation(new NPESelectionWorkflowTranslation(Context, AssetLookupSystem, _npeDirector), Context, AssetLookupSystem, this, comparer);
			IWorkflowTranslation<ActionsAvailableRequest> actionsAvailableTranslation = new ActionsAvailableTranslation(Context, AssetLookupSystem, clientSideInteraction, Logger, new NPEActionsAvailableWorkflowTranslation(this, clientSideInteraction, Context, _npeDirector));
			IWorkflowTranslation<AssignDamageRequest> assignDamageTranslation = new AutoAssignDamageTranslation();
			IWorkflowTranslation<DeclareBlockersRequest> declareBlockersTranslation = new DeclareBlockersTranslation_NPE(_npeDirector, Context, UIManager);
			IWorkflowTranslation<DeclareAttackerRequest> declareAttackersTranslation = new DeclareAttackersTranslation_NPE(_npeDirector, Context, UIManager, this);
			IWorkflowTranslation<SelectTargetsRequest> selectTargetsTranslation = new SelectTargetsTranslation_NPE(Context, AssetLookupSystem, _npeDirector);
			nestedTranslation = new WorkflowTranslator(this, Context, AssetLookupSystem, selectNTranslation, actionsAvailableTranslation, assignDamageTranslation, declareBlockersTranslation, declareAttackersTranslation, selectTargetsTranslation, Logger);
		}
		else
		{
			ClientSideInteraction clientSideInteraction2 = new ClientSideInteraction(Logger, UIManager, Context, AssetLookupSystem);
			IWorkflowTranslation<SelectNRequest> selectNTranslation2 = new SelectNWorkflowTranslation(new SelectionWorkflowTranslation(Context, AssetLookupSystem), Context, AssetLookupSystem, this, comparer);
			IWorkflowTranslation<ActionsAvailableRequest> actionsAvailableTranslation2 = new ActionsAvailableTranslation(Context, AssetLookupSystem, clientSideInteraction2, Logger, new DefaultTranslation(this, clientSideInteraction2, Context));
			IWorkflowTranslation<AssignDamageRequest> assignDamageTranslation2 = new AssignDamageTranslation(Context);
			IWorkflowTranslation<DeclareBlockersRequest> declareBlockersTranslation2 = new DeclareBlockersTranslation(Context, UIManager);
			IWorkflowTranslation<DeclareAttackerRequest> declareAttackersTranslation2 = new DeclareAttackersTranslation(Context, UIManager, this);
			IWorkflowTranslation<SelectTargetsRequest> selectTargetsTranslation2 = new SelectTargetsTranslation(Context, new AutoTargetingSolution(Context), AssetLookupSystem);
			nestedTranslation = new WorkflowTranslator(this, Context, AssetLookupSystem, selectNTranslation2, actionsAvailableTranslation2, assignDamageTranslation2, declareBlockersTranslation2, declareAttackersTranslation2, selectTargetsTranslation2, Logger);
		}
		return new InformationalWorkflowDecorator(NullWorkflowTranslation<BaseUserRequest>.Default, nestedTranslation);
	}

	private void OnDestroy()
	{
		foreach (IDisposable disposable in _disposables)
		{
			disposable.Dispose();
		}
		_disposables.Clear();
		_updateObservers.Clear();
		BrowserManager?.Clear();
		if ((bool)_npeController)
		{
			AudioManager.ExecuteActionOnEvent(WwiseEvents.sfx_npe_intro_blackspace.EventName, AkActionOnEventType.AkActionOnEventType_Stop, _npeController.gameObject);
			UnityEngine.Object.Destroy(_npeController);
		}
		if ((bool)MainCamera)
		{
			MainCamera.enabled = false;
		}
		OpeningHandActionWorkflow.ClearPendingActions();
		RigidbodyWind.ToggleAll(enabled: false);
		if (MatchManager != null)
		{
			MatchManager.SideboardSubmitted -= OnSideboardSubmitted;
		}
		if (AudioManager.Instance != null)
		{
			AudioManager.PostEvent("StopAllDSSounds", AudioManager.Default);
		}
		if (AssetLookupSystem.Blackboard != null)
		{
			AssetLookupSystem.Blackboard.RemoveFillerDelegate(FillAltBlackboard);
			AssetLookupSystem = default;
		}
		ScreenKeepAlive.AllowScreenTimeout();
		BrowserManager = null;
		_npeController = null;
		_npeDirector = null;
		ViewManager = null;
		_uxEventTranslator = null;
		WorkflowController = null;
		_intentionLineManager = null;
		InteractionSystem = null;
		UXEventQueue = null;
		_altArtMatchSettings = null;
		AutoRespManager = null;
	}

	private void Undo()
	{
		if (!_settingsMenuHost.IsOpen() && !SocialUI.IsSendFriendInviteShowing() && !BrowserManager.IsAnyBrowserOpen)
		{
			WorkflowController.CurrentWorkflow?.TryUndo();
		}
	}

	public void TogglePhaseLadder()
	{
		if (!SocialUI.IsSendFriendInviteShowing())
		{
			MDNPlayerPrefs.ShowPhaseLadder = !MDNPlayerPrefs.ShowPhaseLadder;
			MDNPlayerPrefs.SeenPhaseLadderHint = true;
			UIManager.PhaseLadder.OnEnable();
		}
	}

	private void OnEscapePressed()
	{
		bool flag = WorkflowController.PendingWorkflow is IntermissionWorkflow || WorkflowController.CurrentWorkflow is IntermissionWorkflow;
		if (_settingsMenuHost.IsOpen())
		{
			_settingsMenuHost.Close();
		}
		else if (UIManager.ManaColorSelector.IsOpen)
		{
			UIManager.ManaColorSelector.TryCloseSelector();
		}
		else if (UIManager.ConfirmWidget.IsOpen)
		{
			UIManager.ConfirmWidget.Cancel();
		}
		else if (!flag && !_sideboardSubmitted)
		{
			InteractionSystem.CancelAnyDrag();
			_settingsMenuHost.Open();
		}
	}

	private void Update()
	{
		if (MatchManager == null || _gre == null)
		{
			return;
		}
		foreach (ClientUpdateBase item in _gre.RetrieveClientUpdates())
		{
			Update_LoggerOnClientUpdate(item);
			_clientUpdateHandler.Handle(item);
		}
		Update_UpdateObservers(Time.deltaTime);
		bool wasRunning = Update_UXEventQueue();
		Update_WorkflowController();
		Update_HighlightsAndDimming(wasRunning);
		Update_InteractionSystemVisualState();
		Update_SplineMovement();
		Update_IntentionLineManager();
		Update_ProcessInteraction();
	}

	private void Update_LoggerOnClientUpdate(ClientUpdateBase clientUpdate)
	{
		Logger?.OnClientUpdate(clientUpdate);
	}

	private void Update_UpdateObservers(float timeStep)
	{
		foreach (IUpdate updateObserver in _updateObservers)
		{
			updateObserver.OnUpdate(timeStep);
		}
	}

	private bool Update_UXEventQueue()
	{
		bool isRunning = UXEventQueue.IsRunning;
		if (_npeDirector == null || !_npeDirector.ShouldBePaused)
		{
			UXEventQueue.Update(Time.deltaTime);
		}
		return isRunning;
	}

	private void Update_WorkflowController()
	{
		WorkflowController?.Update(UXEventQueue.Events);
	}

	private void Update_HighlightsAndDimming(bool wasRunning)
	{
		if (wasRunning && !UXEventQueue.IsRunning)
		{
			_highlightManager.SetDirty();
			_dimmingController.SetDirty();
		}
	}

	private void Update_InteractionSystemVisualState()
	{
		IEnumerable<DuelScene_CDC> cardViews = _visualStateCardProvider.GetCardViews();
		_combatIconUpdater.UpdateCombatIcons(cardViews);
		_highlightManager.UpdateHighlights(cardViews, ViewManager.GetAllAvatars());
		_dimmingController.UpdateDimming(cardViews);
		Context.Get<IPlayerPresenceController>()?.Update(cardViews);
	}

	private void Update_SplineMovement()
	{
		SplineMovementSystem.UpdateMovement();
	}

	private void Update_IntentionLineManager()
	{
		_intentionLineManager?.Update();
	}

	private void Update_ProcessInteraction()
	{
		InteractionSystem?.ProcessInteraction();
	}

	public MatchManager.PlayerInfo GetPlayerInfoForNum(GREPlayerNum playerNum)
	{
		return MatchManager.PlayerInfoForNum(playerNum);
	}

	private void OnSideboardSubmitted()
	{
		_sideboardSubmitted = true;
	}

	public bool TryGetMainButton(out StyledButton button)
	{
		button = UIManager.GetButtonWithStyle(ButtonStyle.StyleType.Main);
		if (button == null)
		{
			button = UIManager.GetButtonWithTag(ButtonTag.Primary);
		}
		return button != null;
	}

	public bool HandleKeyUp(Key curr, Modifiers mods)
	{
		if (!AllowInput)
		{
			return true;
		}
		if (!_settingsMenuHost.IsOpen() && !SocialUI.IsSendFriendInviteShowing())
		{
			if (CurrentInteraction.CanKeyUp(curr))
			{
				if (CurrentInteraction.AppliedState == InteractionAppliedState.Applied)
				{
					CurrentInteraction.OnKeyUp(curr);
				}
			}
			else if (curr == Key.Space && _npeDirector == null && !BrowserManager.IsAnyBrowserOpen)
			{
				if (TryGetMainButton(out var button))
				{
					button.SimulateClickRelease();
				}
			}
			else if (curr == Key.Enter)
			{
				AutoRespManager.ToggleAutoPass();
			}
			else if (curr == Key.L && _npeDirector == null)
			{
				TogglePhaseLadder();
			}
			else if (curr == Key.Z && _npeDirector == null)
			{
				Undo();
			}
			else if (curr == Key.Tab && HasDebugPermissions())
			{
				CurrentInteraction?.BaseRequest?.AutoRespond();
			}
		}
		return true;
	}

	public bool HandleKeyDown(Key curr, Modifiers mods)
	{
		if (curr == Key.Escape)
		{
			OnEscapePressed();
		}
		else if (AllowInput && !_settingsMenuHost.IsOpen() && !SocialUI.IsSendFriendInviteShowing())
		{
			if (CurrentInteraction.CanKeyDown(curr))
			{
				CurrentInteraction.OnKeyDown(curr);
			}
			else if (curr == Key.Space && _npeDirector == null && !BrowserManager.IsAnyBrowserOpen)
			{
				StyledButton styledButton = UIManager.GetButtonWithStyle(ButtonStyle.StyleType.Main);
				if (styledButton == null)
				{
					styledButton = UIManager.GetButtonWithTag(ButtonTag.Primary);
				}
				if (styledButton != null && curr == Key.Space)
				{
					styledButton.SimulateClickDown();
				}
			}
			else
			{
				switch (curr)
				{
				case Key.LeftCtrl:
				case Key.RightCtrl:
					if (mods.Shift)
					{
						AutoRespManager.ToggleLockedFullControl();
					}
					else
					{
						AutoRespManager.ToggleFullControl();
					}
					break;
				case Key.LeftShift:
				case Key.RightShift:
					if (mods.Ctrl)
					{
						AutoRespManager.ToggleLockedFullControl();
					}
					break;
				}
			}
		}
		return true;
	}

	public bool HandleKeyHeld(Key key, float totalMS)
	{
		if (AllowInput && !_settingsMenuHost.IsOpen() && !SocialUI.IsSendFriendInviteShowing() && CurrentInteraction.CanKeyHeld(key, totalMS))
		{
			CurrentInteraction.OnKeyHeld(key, totalMS);
		}
		return true;
	}

	private void FillAltBlackboard(IBlackboard bb)
	{
		bb.GameState = CurrentGameState;
		bb.ActiveResolution = ActiveResolutionEffect;
		bb.BattlefieldId = BattlefieldUtil.BattlefieldId;
		bb.Language = Languages.CurrentLanguage;
		bb.InWrapper = false;
		bb.InDuelScene = true;
		WorkflowBase workflowBase = CurrentInteraction ?? PendingInteraction;
		if (workflowBase != null)
		{
			bb.Interaction = workflowBase;
			bb.Request = workflowBase.BaseRequest;
		}
		if (BrowserManager?.CurrentBrowser is ICardBrowser cardBrowser)
		{
			bb.CardBrowserType = cardBrowser.BrowserType;
			bb.CardBrowserLayoutID = cardBrowser.CardHolderLayoutKey;
		}
		bb.Prompt = bb.Interaction?.Prompt;
		bb.IdNameProvider = Context.Get<IEntityNameProvider<uint>>();
	}

	private bool HasDebugPermissions()
	{
		bool flag = Context.Get<IAccountClient>().AccountInformation?.HasRole_Debugging() ?? false;
		return Debug.isDebugBuild | flag;
	}

	private bool CanDisplayDebugUI()
	{
		if (!HasDebugPermissions())
		{
			return OverridesConfiguration.Local.GetFeatureToggleValue("debug.showSparkyMenu");
		}
		return true;
	}
}
