using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using AssetLookupTree;
using AssetLookupTree.Blackboard;
using AssetLookupTree.Payloads.Battlefield;
using AssetLookupTree.Payloads.Prefab;
using Core.Code.Input;
using Core.MatchScene;
using Core.Meta.MainNavigation;
using Core.Shared.Code.ClientModels;
using Core.Shared.Code.Connection;
using Cysharp.Threading.Tasks;
using MTGA.KeyboardManager;
using UnityEngine;
using UnityEngine.CrashReportHandler;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Wizards.Arena.Promises;
using Wizards.MDN;
using Wizards.Models.ClientBusinessEvents;
using Wizards.Mtga;
using Wizards.Mtga.Assets;
using Wizards.Mtga.BI;
using Wizards.Mtga.Deeplink;
using Wizards.Mtga.FrontDoorModels;
using Wizards.Mtga.Platforms;
using Wizards.Mtga.PlayBlade;
using Wotc.Mtga;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.Events;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtga.Network.ServiceWrappers;
using Wotc.Mtga.Profiling;
using Wotc.Mtga.Providers;
using Wotc.Mtgo.Gre.External.Messaging;

public class MatchSceneManager : MonoBehaviour
{
	public enum SubScene
	{
		None,
		WaitingForMatch,
		MatchReady,
		DuelScene,
		MatchEnd,
		WaitingForGame,
		GameReady,
		ExitMatchScene,
		Disconnected
	}

	public enum SceneLoadState
	{
		None,
		Loaded,
		Unloaded,
		Irrelevant
	}

	private struct SceneStateData
	{
		public SceneLoadState DuelScene;

		public SceneLoadState Battlefield;

		public SceneLoadState MatchEnd;

		public SceneLoadState WaitingForGame;
	}

	private class StateTransitionData
	{
		public SubScene Old;

		public SubScene New;

		public bool WaitOnSceneLoad;

		public Func<IEnumerator> Execute;
	}

	public static MatchSceneManager Instance;

	[SerializeField]
	private MatchSceneCurtainOverlay _curtainOverlay;

	[SerializeField]
	private Transform _loadingIndicatorParent;

	[SerializeField]
	private FrameTimingRecorder _frameTimingRecorder;

	private const string DUELSCENE_SCENE_NAME = "DuelScene";

	private const string WAITING_FOR_GAME_SCENE_NAME = "PreGameScene";

	private const string MATCH_END_SCENE_NAME = "MatchEndScene";

	private string _battlefield_sceneName;

	private readonly Queue<SubScene> _subSceneQueue = new Queue<SubScene>();

	private Coroutine _changeSubSceneCoroutine;

	private Dictionary<SubScene, SceneStateData> _subSceneToStateData;

	private Dictionary<string, AsyncOperation> _asyncSceneLoadsByScene;

	private Dictionary<string, AsyncOperation> _asyncSceneUnLoadsByScene;

	private List<StateTransitionData> _stateTransitions;

	private PreGameScene _preGameScene;

	private MatchEndScene _matchEndScene;

	private GameManager _gameManager;

	private IContext _sessionContext = NullContext.Default;

	private ICardDatabaseAdapter _cardDatabase;

	private CardMaterialBuilder _cardMaterialBuilder;

	private IBILogger _biLogger;

	private KeyboardManager _keyboardManager;

	private IActionSystem _actionSystem;

	private FrontDoorConnectionManager _frontDoorConnectionManager;

	private NPEState _npeState;

	private Matchmaking _matchmaking;

	private IMatchdoorServiceWrapper _matchdoorServiceWrapper;

	private IPlayBladeSelectionProvider _playBladeSelectionProvider;

	private CosmeticsProvider _cosmetics;

	private IPlayerRankManager _playerRankManager = NullPlayerRankManager.Default;

	private MatchManager _matchManager;

	private AssetLookupSystem _assetLookupSystem;

	private GameObject _loadingIndicator;

	private PostMatchClientUpdate _postMatchClientUpdate;

	private string _postMatchSceneToLoad = string.Empty;

	private readonly IMatchSceneStateManager _matchSceneStateManager = new MatchSceneStateManager();

	private readonly HashSet<IDisposable> _disposables = new HashSet<IDisposable>();

	private DuelSceneLogger _logger;

	private MatchState _matchState;

	public bool AutoConnectToMatchServer = true;

	private bool _stateTransitionInProgress;

	public string BattlefieldSceneName => _battlefield_sceneName;

	public SubScene Current => _matchSceneStateManager.SubScene;

	public event Action<MatchEndScene> MatchEndSceneCreated;

	private void FillAltBlackboard(IBlackboard bb)
	{
		bb.Event = _matchManager?.Event;
		bb.BattlefieldId = BattlefieldUtil.BattlefieldId;
	}

	private void Awake()
	{
		if (Instance == null)
		{
			Instance = this;
			_matchSceneStateManager.SetState(new MatchSceneState(Current, subSceneTransitioning: false, lockSceneTransitions: false));
			_disposables.AddIfNotNull(_matchSceneStateManager as IDisposable);
			_asyncSceneLoadsByScene = new Dictionary<string, AsyncOperation>
			{
				{ "DuelScene", null },
				{ "MatchEndScene", null },
				{ "PreGameScene", null }
			};
			_asyncSceneUnLoadsByScene = new Dictionary<string, AsyncOperation>
			{
				{ "DuelScene", null },
				{ "MatchEndScene", null },
				{ "PreGameScene", null }
			};
			_subSceneToStateData = new Dictionary<SubScene, SceneStateData>
			{
				{
					SubScene.WaitingForMatch,
					new SceneStateData
					{
						DuelScene = SceneLoadState.Unloaded,
						Battlefield = SceneLoadState.Unloaded,
						MatchEnd = SceneLoadState.Unloaded,
						WaitingForGame = SceneLoadState.Loaded
					}
				},
				{
					SubScene.MatchReady,
					new SceneStateData
					{
						DuelScene = SceneLoadState.Loaded,
						Battlefield = SceneLoadState.Loaded,
						MatchEnd = SceneLoadState.Unloaded,
						WaitingForGame = SceneLoadState.Loaded
					}
				},
				{
					SubScene.DuelScene,
					new SceneStateData
					{
						DuelScene = SceneLoadState.Loaded,
						Battlefield = SceneLoadState.Loaded,
						MatchEnd = SceneLoadState.Unloaded,
						WaitingForGame = SceneLoadState.Unloaded
					}
				},
				{
					SubScene.MatchEnd,
					new SceneStateData
					{
						DuelScene = SceneLoadState.Irrelevant,
						Battlefield = SceneLoadState.Irrelevant,
						MatchEnd = SceneLoadState.Loaded,
						WaitingForGame = SceneLoadState.Irrelevant
					}
				},
				{
					SubScene.WaitingForGame,
					new SceneStateData
					{
						DuelScene = SceneLoadState.Unloaded,
						Battlefield = SceneLoadState.Loaded,
						MatchEnd = SceneLoadState.Unloaded,
						WaitingForGame = SceneLoadState.Loaded
					}
				},
				{
					SubScene.GameReady,
					new SceneStateData
					{
						DuelScene = SceneLoadState.Loaded,
						Battlefield = SceneLoadState.Loaded,
						MatchEnd = SceneLoadState.Unloaded,
						WaitingForGame = SceneLoadState.Loaded
					}
				},
				{
					SubScene.ExitMatchScene,
					new SceneStateData
					{
						DuelScene = SceneLoadState.Unloaded,
						Battlefield = SceneLoadState.Unloaded,
						MatchEnd = SceneLoadState.Unloaded,
						WaitingForGame = SceneLoadState.Unloaded
					}
				}
			};
			_stateTransitions = new List<StateTransitionData>
			{
				new StateTransitionData
				{
					Old = SubScene.None,
					New = SubScene.WaitingForMatch,
					WaitOnSceneLoad = true,
					Execute = AnimateWaitingForMatch
				},
				new StateTransitionData
				{
					Old = SubScene.WaitingForMatch,
					New = SubScene.MatchReady,
					WaitOnSceneLoad = false,
					Execute = AnimateGameFound
				},
				new StateTransitionData
				{
					Old = SubScene.None,
					New = SubScene.MatchEnd,
					WaitOnSceneLoad = true,
					Execute = AnimateMatchEnd
				},
				new StateTransitionData
				{
					Old = SubScene.WaitingForGame,
					New = SubScene.GameReady,
					WaitOnSceneLoad = false,
					Execute = AnimateGameFound
				},
				new StateTransitionData
				{
					Old = SubScene.None,
					New = SubScene.ExitMatchScene,
					WaitOnSceneLoad = true,
					Execute = OnExitMatchScene
				}
			};
			SceneManager.sceneLoaded += OnSceneLoaded;
			SceneManager.sceneUnloaded += OnSceneUnloaded;
		}
		else
		{
			UnityEngine.Debug.LogError("DUPLICATE MATCH MANAGER DETECTED");
		}
	}

	private void SetupLoadingIndicator()
	{
		_loadingIndicator = AssetLoader.Instantiate(_assetLookupSystem.GetPrefabPath<LoadingPanelPrefab, GameObject>(), _loadingIndicatorParent);
		EnableLoadingIndicator(PlatformUtils.IsHandheld());
	}

	private void EnableLoadingIndicator(bool enable)
	{
		if (!(_loadingIndicator == null))
		{
			_loadingIndicator.UpdateActive(enable);
		}
	}

	public void Initialize(IContext context, AssetLookupSystem assetLookupSystem, string postMatchSceneToLoad)
	{
		_sessionContext = context;
		_postMatchSceneToLoad = postMatchSceneToLoad;
		_assetLookupSystem = assetLookupSystem;
		_cardDatabase = _sessionContext.Get<ICardDatabaseAdapter>();
		_cardMaterialBuilder = _sessionContext.Get<CardMaterialBuilder>();
		_biLogger = _sessionContext.Get<IBILogger>();
		_sessionContext.Get<SettingsMenuHost>();
		_keyboardManager = _sessionContext.Get<KeyboardManager>();
		_actionSystem = _sessionContext.Get<IActionSystem>();
		_frontDoorConnectionManager = _sessionContext.Get<FrontDoorConnectionManager>();
		_npeState = _sessionContext.Get<NPEState>();
		_matchmaking = _sessionContext.Get<Matchmaking>();
		_matchdoorServiceWrapper = context.Get<IMatchdoorServiceWrapper>();
		_playBladeSelectionProvider = context.Get<IPlayBladeSelectionProvider>();
		_cosmetics = context.Get<CosmeticsProvider>();
		_playerRankManager = context.Get<IPlayerRankManager>();
		_matchManager = _sessionContext.Get<MatchManager>();
		_matchManager.ConnectRespReceived += OnConnectRespReceived_UnloadPostMatchScene;
		_matchManager.ConnectRespReceived += OnConnectRespReceived_HandleErrors;
		_matchManager.MatchStateChanged += MatchStateChanged;
		_matchManager.SideboardSubmitted += OnSideboardSubmitted;
		_matchManager.MatchFailed += OnMatchFailed;
		Application.deepLinkActivated += OnDeepLink;
		_matchmaking.RemovedFromMatchmaking += OnRemovedFromMatchmaking;
		_logger = new DuelSceneLogger(_matchManager, _biLogger, _npeState, _frameTimingRecorder);
		_playBladeSelectionProvider.SetSelectedTab(BladeType.LastPlayed);
		EnqueueSubSceneChange(SubScene.WaitingForMatch);
		if (_matchmaking.IsMatchReady())
		{
			_matchmaking.JoinPendingMatch();
		}
		else
		{
			_matchmaking.MatchReady += OnMatchReady;
		}
		_frontDoorConnectionManager.SetIdleTimerActive(active: false);
		_assetLookupSystem.Blackboard.AddFillerDelegate(FillAltBlackboard);
		if (context.Get<IClientFeatureToggleDataProvider>().GetToggleValueById("SocialBladeV2Status"))
		{
			context.Get<WrapperSceneManagement>().LoadScene(new WrapperSceneInformation("Social", WrapperSceneLifeCycle.LoggedIn));
		}
		SetupLoadingIndicator();
		_disposables.Add(new SetGREDebugInfoMediator(_matchManager, _sessionContext.Get<IDebugInfoController>()));
	}

	public void OnDeepLink(string url)
	{
		DeepLinking.LogDeepLinkNotUsed(url, "In Duelscene Match, DeepLink ignored", _biLogger);
	}

	private void SetupBattlefield()
	{
		AssetLookupTree<BattlefieldScenePayload> assetLookupTree = _assetLookupSystem.TreeLoader.LoadTree<BattlefieldScenePayload>();
		_assetLookupSystem.Blackboard.Clear();
		_assetLookupSystem.Blackboard.PlayerInfoMatch = _matchManager.PlayerInfoForNum(GREPlayerNum.LocalPlayer);
		_assetLookupSystem.Blackboard.Event = _matchManager.Event;
		if (IsNPEMatch())
		{
			_assetLookupSystem.Blackboard.BattlefieldId = _npeState.ActiveNPEGame.Battlefield;
		}
		else if (IsDebugMatch())
		{
			_assetLookupSystem.Blackboard.BattlefieldId = BattlefieldUtil.BattlefieldId;
		}
		else
		{
			if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<IdOverridePayload> loadedTree))
			{
				IdOverridePayload payload = loadedTree.GetPayload(_assetLookupSystem.Blackboard);
				if (payload != null)
				{
					_assetLookupSystem.Blackboard.BattlefieldId = payload.Text;
					goto IL_0101;
				}
			}
			_assetLookupSystem.Blackboard.BattlefieldId = _matchManager.BattlefieldId;
		}
		goto IL_0101;
		IL_0101:
		BattlefieldScenePayload payload2 = assetLookupTree.GetPayload(_assetLookupSystem.Blackboard);
		if (payload2 != null)
		{
			BattlefieldUtil.SetBattlefield(payload2);
		}
		else
		{
			_assetLookupSystem.Blackboard.BattlefieldId = BattlefieldUtil.GetRandomBattlefieldId(_assetLookupSystem);
			payload2 = assetLookupTree.GetPayload(_assetLookupSystem.Blackboard);
			BattlefieldUtil.SetBattlefield(payload2);
		}
		SetupBattlefieldSceneData(BattlefieldUtil.BattlefieldSceneName);
		bool flag = false;
		try
		{
			flag = AssetLoader.AddReferenceCount(BattlefieldUtil.BattlefieldPath);
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
		if (!flag)
		{
			if (PlatformUtils.IsHandheld())
			{
				SetupBattlefieldSceneData(Path.GetFileNameWithoutExtension(BattlefieldUtil.MobileFallbackBattlefieldPath));
			}
			else
			{
				SetupBattlefieldSceneData(Path.GetFileNameWithoutExtension(BattlefieldUtil.DesktopFallbackBattlefieldPath));
			}
			ResourceErrorLogger.LogAssetBundleError(_biLogger, "Failed to load bundle for battlefield", new Dictionary<string, string> { 
			{
				"BattlefieldID",
				BattlefieldUtil.BattlefieldId
			} });
		}
		if (!string.IsNullOrEmpty(BattlefieldUtil.BattlefieldAudioBankName))
		{
			AudioManager.Instance.LoadBattleField(BattlefieldUtil.BattlefieldAudioBankName);
		}
	}

	private void SetupBattlefieldSceneData(string sceneName)
	{
		_battlefield_sceneName = sceneName;
		_asyncSceneLoadsByScene[sceneName] = null;
		_asyncSceneUnLoadsByScene[sceneName] = null;
	}

	private void OnConnectRespReceived_UnloadPostMatchScene(GREToClientMessage connectMessage)
	{
		if (connectMessage.ConnectResp.Status != ConnectionStatus.Success)
		{
			return;
		}
		_matchManager.ConnectRespReceived -= OnConnectRespReceived_UnloadPostMatchScene;
		if (string.IsNullOrEmpty(_postMatchSceneToLoad))
		{
			return;
		}
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (sceneAt.name == _postMatchSceneToLoad)
			{
				SceneManager.UnloadSceneAsync(sceneAt);
			}
		}
	}

	private void OnConnectRespReceived_HandleErrors(GREToClientMessage connectMessage)
	{
		ConnectionStatus connectStatus = connectMessage.ConnectResp.Status;
		if (connectStatus != ConnectionStatus.Success)
		{
			SystemMessageManager.Instance.ShowOk(titleText(), errorText());
			ExitMatchScene();
		}
		string errorText()
		{
			if (connectStatus == ConnectionStatus.ProtoVersionIncompat)
			{
				return "Proto Incompatability";
			}
			if (connectStatus == ConnectionStatus.GrpversionIncompat)
			{
				return "Version Incompatability";
			}
			return "Invalid State";
		}
		static string titleText()
		{
			return Languages.ActiveLocProvider.GetLocalizedText("DuelScene/ClientPrompt/GreConnectionError");
		}
	}

	private void MatchStateChanged(MatchState matchState)
	{
		if (_matchState == matchState)
		{
			return;
		}
		if (string.IsNullOrEmpty(_battlefield_sceneName))
		{
			SetupBattlefield();
		}
		switch (matchState)
		{
		case MatchState.GameInProgress:
			if (_matchState == MatchState.None)
			{
				EnqueueSubSceneChange(SubScene.MatchReady);
				EnqueueSubSceneChange(SubScene.DuelScene);
			}
			else
			{
				EnqueueSubSceneChange(SubScene.WaitingForGame);
				EnqueueSubSceneChange(SubScene.GameReady);
				EnqueueSubSceneChange(SubScene.DuelScene);
			}
			break;
		case MatchState.MatchComplete:
			UpdateNPEProgress();
			if (Current == SubScene.DuelScene)
			{
				IMatchSceneStateManager matchSceneStateManager = _matchSceneStateManager;
				bool? lockSceneTransitions = true;
				matchSceneStateManager.SetState(null, null, lockSceneTransitions);
			}
			EnqueueSubSceneChange(SubScene.MatchEnd);
			break;
		}
		_matchState = matchState;
	}

	private void OnSideboardSubmitted()
	{
		EnqueueSubSceneChange(SubScene.WaitingForGame);
	}

	private void OnMatchFailed((string reason, string details) errorParams)
	{
		UnityEngine.Debug.LogException(new Exception(errorParams.details));
		SystemMessageManager.ShowSystemMessage(Languages.ActiveLocProvider.GetLocalizedText("DuelScene/GamplayError/Unexpected_Match_Complete_Title"), Languages.ActiveLocProvider.GetLocalizedText("DuelScene/GamplayError/Unexpected_Match_Complete_Message", ("Reason", errorParams.reason), ("Player", Languages.ActiveLocProvider.GetLocalizedText("DuelScene/EndMatch/Draw"))));
		EnqueueSubSceneChange(SubScene.MatchEnd);
	}

	private void UpdateNPEProgress()
	{
		if (_npeState.ActiveNPEGame != null)
		{
			bool flag = _matchManager.GameResults.Exists((MatchManager.GameResult x) => x.Scope == MatchScope.Match && x.Winner == GREPlayerNum.LocalPlayer);
			_npeState.BI_NPEProgressUpdate(new NPEState.NPEProgressContext(NPEState.NPEProgressMarker.Completed_Game, _npeState.ActiveNPEGameNumber, 0u, Phase.None, Step.None, flag));
			if ((_npeState.ActiveNPEGameNumber >= 4) & flag)
			{
				BIEventTracker.TrackEvent(EBiEvent.CompletedNpe);
			}
		}
	}

	private void Update()
	{
		if (Current != SubScene.ExitMatchScene && !_matchSceneStateManager.LockSceneTransitions && _changeSubSceneCoroutine == null && _subSceneQueue.Count > 0)
		{
			SubScene subScene = _subSceneQueue.Dequeue();
			if (Current != subScene)
			{
				_changeSubSceneCoroutine = StartCoroutine(ChangeSubSceneCoroutine(subScene));
			}
		}
	}

	private void EnqueueSubSceneChange(SubScene @new)
	{
		if (Current == SubScene.ExitMatchScene)
		{
			UnityEngine.Debug.LogErrorFormat("TRYING TO ENQUEUE SUBSCENE CHANGE AFTER EXITING | {0}", @new);
		}
		else
		{
			_subSceneQueue.Enqueue(@new);
		}
	}

	private IEnumerator ChangeSubSceneCoroutine(SubScene subScene)
	{
		StateTransitionData transition = _stateTransitions.Find((StateTransitionData x) => (x.Old == SubScene.None || x.Old == Current) && x.New == subScene);
		if (Current != SubScene.None && subScene != SubScene.MatchEnd && (transition == null || transition.Old == SubScene.None))
		{
			_curtainOverlay.SetEnabled(enabled: true);
			while (_curtainOverlay.CurtainAlpha < 1f)
			{
				yield return null;
			}
			if (subScene == SubScene.ExitMatchScene)
			{
				EnableLoadingIndicator(enable: true);
			}
		}
		_matchSceneStateManager.SetState(subScene, true);
		SceneStateData stateData = _subSceneToStateData[Current];
		_stateTransitionInProgress = false;
		if (transition != null && !transition.WaitOnSceneLoad)
		{
			StartCoroutine(ExecuteStateTransition(transition));
			yield return null;
		}
		bool isReady = false;
		while (!isReady)
		{
			bool flag = CheckSceneLoadState(stateData.Battlefield, _battlefield_sceneName);
			if (!flag)
			{
				SetSceneState(stateData.Battlefield, _battlefield_sceneName);
			}
			bool flag2 = CheckSceneLoadState(stateData.DuelScene, "DuelScene");
			if (!flag2)
			{
				SetSceneState(stateData.DuelScene, "DuelScene");
			}
			bool flag3 = CheckSceneLoadState(stateData.MatchEnd, "MatchEndScene");
			if (!flag3)
			{
				SetSceneState(stateData.MatchEnd, "MatchEndScene");
			}
			bool flag4 = CheckSceneLoadState(stateData.WaitingForGame, "PreGameScene");
			if (!flag4)
			{
				SetSceneState(stateData.WaitingForGame, "PreGameScene");
			}
			isReady = flag2 & flag & flag4 & flag3;
			yield return null;
		}
		if (Current != SubScene.ExitMatchScene)
		{
			_curtainOverlay.SetEnabled(enabled: false);
			while (_curtainOverlay.CurtainAlpha > 0f)
			{
				yield return null;
			}
		}
		if (transition?.WaitOnSceneLoad ?? false)
		{
			StartCoroutine(ExecuteStateTransition(transition));
		}
		while (_stateTransitionInProgress)
		{
			yield return null;
		}
		if (Current == SubScene.DuelScene)
		{
			AudioManager.PlayMusic(Current.ToString());
			AudioManager.PlayAmbiance(Current.ToString());
			CrashReportHandler.SetUserMetadata("matchId", _matchManager.MatchID);
		}
		IMatchSceneStateManager matchSceneStateManager = _matchSceneStateManager;
		bool? subSceneTransitioning = false;
		matchSceneStateManager.SetState(null, subSceneTransitioning);
		_changeSubSceneCoroutine = null;
	}

	private IEnumerator ExecuteStateTransition(StateTransitionData transition)
	{
		if (transition != null)
		{
			_stateTransitionInProgress = true;
			yield return transition.Execute();
			_stateTransitionInProgress = false;
		}
	}

	private bool CheckSceneLoadState(SceneLoadState state, string sceneName)
	{
		Scene sceneByName = SceneManager.GetSceneByName(sceneName);
		switch (state)
		{
		case SceneLoadState.Loaded:
			if (_asyncSceneLoadsByScene[sceneName] != null)
			{
				return false;
			}
			if (_asyncSceneUnLoadsByScene[sceneName] != null)
			{
				return false;
			}
			return SceneIsLoaded(sceneName);
		case SceneLoadState.Unloaded:
			if (string.IsNullOrEmpty(sceneName))
			{
				return true;
			}
			if (_asyncSceneLoadsByScene[sceneName] != null)
			{
				return false;
			}
			if (_asyncSceneUnLoadsByScene[sceneName] != null)
			{
				return false;
			}
			if (!sceneByName.IsValid())
			{
				return !sceneByName.isLoaded;
			}
			return false;
		case SceneLoadState.Irrelevant:
			return true;
		default:
			return false;
		}
	}

	private void SetSceneState(SceneLoadState state, string sceneName)
	{
		if (string.IsNullOrEmpty(sceneName))
		{
			return;
		}
		switch (state)
		{
		case SceneLoadState.Loaded:
			if (_asyncSceneUnLoadsByScene[sceneName] == null)
			{
				AsyncOperation asyncOperation = _asyncSceneLoadsByScene[sceneName];
				if (asyncOperation == null)
				{
					asyncOperation = Scenes.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
					_asyncSceneLoadsByScene[sceneName] = asyncOperation;
				}
			}
			break;
		case SceneLoadState.Unloaded:
			if (_asyncSceneLoadsByScene[sceneName] != null)
			{
				if (!_asyncSceneLoadsByScene[sceneName].allowSceneActivation)
				{
					_asyncSceneLoadsByScene[sceneName].allowSceneActivation = true;
				}
			}
			else if (SceneIsLoaded(sceneName) && _asyncSceneUnLoadsByScene[sceneName] == null)
			{
				_asyncSceneUnLoadsByScene[sceneName] = SceneManager.UnloadSceneAsync(sceneName);
			}
			break;
		}
	}

	private bool SceneIsLoaded(string sceneName)
	{
		int sceneCount = SceneManager.sceneCount;
		for (int i = 0; i < sceneCount; i++)
		{
			Scene sceneAt = SceneManager.GetSceneAt(i);
			if (sceneAt.name == sceneName)
			{
				if (sceneAt.IsValid())
				{
					return sceneAt.isLoaded;
				}
				return false;
			}
		}
		return false;
	}

	private void OnDestroy()
	{
		_sessionContext = NullContext.Default;
		foreach (IDisposable disposable in _disposables)
		{
			disposable.Dispose();
		}
		_disposables.Clear();
		if (_logger != null)
		{
			_logger.Dispose();
			_logger = null;
		}
		_assetLookupSystem.Blackboard.RemoveFillerDelegate(FillAltBlackboard);
		_matchmaking.MatchReady -= OnMatchReady;
		_matchmaking.RemovedFromMatchmaking -= OnRemovedFromMatchmaking;
		Application.deepLinkActivated -= OnDeepLink;
		if (_matchManager != null)
		{
			_matchManager.ConnectRespReceived -= OnConnectRespReceived_UnloadPostMatchScene;
			_matchManager.ConnectRespReceived -= OnConnectRespReceived_HandleErrors;
			_matchManager.MatchStateChanged -= MatchStateChanged;
			_matchManager.SideboardSubmitted -= OnSideboardSubmitted;
			_matchManager.MatchFailed -= OnMatchFailed;
		}
		if (Instance == this)
		{
			Instance = null;
		}
		SceneManager.sceneLoaded -= OnSceneLoaded;
		SceneManager.sceneUnloaded -= OnSceneUnloaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		string text = scene.name;
		UnityEngine.Debug.LogFormat("OnSceneLoaded for {0}", text);
		switch (text)
		{
		case "PreGameScene":
			EnableLoadingIndicator(enable: false);
			_preGameScene = scene.GetSceneComponent<PreGameScene>();
			if (_preGameScene != null)
			{
				_matchManager.OnConnectedToService += _preGameScene.ConnectedMatchService;
				_preGameScene.Init(_assetLookupSystem, _keyboardManager, _actionSystem, _cardDatabase, _cardMaterialBuilder, _matchManager, _npeState, _cosmetics);
			}
			else
			{
				UnityEngine.Debug.LogWarning("PreGameScene not found");
			}
			break;
		case "MatchEndScene":
			EnableLoadingIndicator(enable: false);
			_matchEndScene = scene.GetSceneComponent<MatchEndScene>();
			MatchEndSceneCreated?.Invoke(_matchEndScene);
			break;
		case "DuelScene":
			EnableLoadingIndicator(enable: false);
			_gameManager = scene.GetSceneComponent<GameManager>();
			if (_gameManager != null)
			{
				Dictionary<Type, object> referenceMap = new Dictionary<Type, object>
				{
					[typeof(DuelSceneLogger)] = _logger,
					[typeof(IMatchSceneStateProvider)] = _matchSceneStateManager,
					[typeof(IMatchSceneStateController)] = _matchSceneStateManager,
					[typeof(IMatchSceneStateManager)] = _matchSceneStateManager,
					[typeof(IPlayerInfoProvider)] = _matchManager
				};
				_gameManager.Init(this, _assetLookupSystem, new Context(referenceMap, _sessionContext));
				_logger.GameManager = _gameManager;
			}
			else
			{
				UnityEngine.Debug.LogWarning("GameManager not found");
			}
			break;
		}
		if (_asyncSceneLoadsByScene.ContainsKey(text))
		{
			_asyncSceneLoadsByScene[text] = null;
		}
		else if (scene.name != gameObject.scene.name)
		{
			UnityEngine.Debug.LogFormat("cannot find async scene load for {0}", text);
		}
	}

	private IEnumerator UnloadUnusedAssetsYield()
	{
		yield return Resources.UnloadUnusedAssets();
		GC.Collect();
	}

	private void OnSceneUnloaded(Scene scene)
	{
		string text = scene.name;
		UnityEngine.Debug.LogFormat("OnSceneUnloaded for {0}", text);
		switch (text)
		{
		case "PreGameScene":
			_preGameScene = null;
			if (_matchManager.CurrentGameNumber > 1)
			{
				StartCoroutine(UnloadUnusedAssetsYield());
			}
			break;
		case "MatchEndScene":
			_matchEndScene = null;
			break;
		case "DuelScene":
			_logger.GameManager = null;
			_gameManager = null;
			break;
		}
		if (_asyncSceneUnLoadsByScene.ContainsKey(text))
		{
			_asyncSceneUnLoadsByScene[text] = null;
			return;
		}
		UnityEngine.Debug.LogFormat("cannot find async scene unload for {0}", text);
	}

	private IEnumerator OnExitMatchScene()
	{
		UnityEngine.Debug.Log($"{DateTime.Now} OnExitMatchScene");
		CrashReportHandler.SetUserMetadata("matchId", null);
		EnableLoadingIndicator(enable: true);
		_frontDoorConnectionManager.SetIdleTimerActive(active: true);
		AudioManager.SetRTPCValue("examine_card", 0f);
		if (!string.IsNullOrEmpty(_postMatchSceneToLoad))
		{
			_matchManager.Reset();
			Scenes.LoadSceneAsync(_postMatchSceneToLoad);
			yield break;
		}
		bool flag = _matchManager.GameResults.Count > 0;
		EventContext eventContext = _matchManager.Event;
		bool wonGame = _matchManager.GameResults.Exists((MatchManager.GameResult x) => x.Scope == MatchScope.Match && x.Result == ResultType.WinLoss && x.Winner == GREPlayerNum.LocalPlayer);
		int num = 0;
		foreach (MatchManager.GameResult gameResult in _matchManager.GameResults)
		{
			if (gameResult.Scope == MatchScope.Game && gameResult.Result == ResultType.WinLoss && gameResult.Winner == GREPlayerNum.LocalPlayer)
			{
				num++;
			}
		}
		bool flag2 = _npeState.ActiveNPEGame != null;
		bool valueOrDefault = eventContext?.PlayerEvent?.EventUXInfo?.HasEventPage == true;
		bool flag3 = ((eventContext?.PlayerEvent is ColorChallengePlayerEvent { EventInfo: not null } colorChallengePlayerEvent) ? colorChallengePlayerEvent.EventInfo.IsPreconEvent : valueOrDefault);
		bool valueOrDefault2 = eventContext?.PlayerEvent?.EventInfo?.UpdateDailyWeeklyRewards == true;
		_matchManager.Reset();
		PostMatchContext postMatchContext = null;
		if (flag)
		{
			PostMatchContext postMatchContext2 = new PostMatchContext
			{
				WonGame = wonGame,
				GamesWon = num
			};
			PostMatchClientUpdate postMatchClientUpdate = _postMatchClientUpdate;
			postMatchContext2.GameEndAffectsQuest = postMatchClientUpdate != null && postMatchClientUpdate.questUpdate?.Count > 0;
			postMatchContext2.MatchesOfThisEventTypeCanAffectDailyWeeklyWins = valueOrDefault2;
			postMatchContext2.PostMatchClientUpdate = _postMatchClientUpdate;
			postMatchContext = postMatchContext2;
			if (eventContext != null)
			{
				eventContext.PostMatchContext = postMatchContext;
			}
		}
		if (flag2)
		{
			PAPA.SceneLoading.LoadNPEScene();
			yield break;
		}
		if (flag3)
		{
			PAPA.SceneLoading.LoadWrapperScene(eventContext);
			yield break;
		}
		PAPA.SceneLoading.LoadWrapperScene(new HomePageContext
		{
			PostMatchContext = postMatchContext
		});
	}

	private IEnumerator AnimateWaitingForMatch()
	{
		_preGameScene.GameCancelled += () =>
		{
			_matchmaking.TryCancel();
		};
		yield return null;
	}

	private IEnumerator AnimateGameFound()
	{
		EventSystem sceneComponent = _preGameScene.gameObject.scene.GetSceneComponent<EventSystem>();
		if ((bool)sceneComponent)
		{
			sceneComponent.enabled = false;
		}
		_preGameScene.AnimateGameFound();
		float waitingTimer = 0f;
		while (!_preGameScene.IsComplete)
		{
			waitingTimer += Time.deltaTime;
			if (waitingTimer >= 10f)
			{
				UnityEngine.Debug.LogError("VS OUTRO TIMEOUT");
				break;
			}
			yield return null;
		}
	}

	private IEnumerator AnimateMatchEnd()
	{
		bool animationComplete = false;
		MatchManager.GameResult result = _matchManager.GameResults.Find((MatchManager.GameResult x) => x.Scope == MatchScope.Match);
		string matchId = _matchManager.MatchID;
		string internalEventName = _matchManager?.Event?.PlayerEvent?.EventInfo?.InternalEventName ?? "Unknown Event";
		_matchEndScene.Init(result, _matchManager.LocalPlayerInfo.AvatarSelection, _assetLookupSystem, _npeState, _matchManager);
		bool matchDataFound = false;
		_postMatchClientUpdate = null;
		if (WaitForMatchResults())
		{
			IPlayerEvent playerEvent = _matchManager.Event?.PlayerEvent;
			string eventName = playerEvent?.EventInfo?.InternalEventName;
			if (playerEvent is IColorChallengePlayerEvent colorChallengePlayerEvent)
			{
				eventName = colorChallengePlayerEvent.MatchMakingName;
			}
			Promise<PostMatchClientUpdate> promise = RetryPromise<PostMatchClientUpdate>.Create(() => _matchdoorServiceWrapper.GetMatchResults(eventName, _matchManager.MatchID).AsPromise(), (Promise<PostMatchClientUpdate> p) => !p.Successful || !p.Result.FoundMatch, (int tries) => RetryPromiseHelpers.IncrementalBackoff(TimeSpan.FromSeconds(0.20000000298023224), TimeSpan.FromSeconds(0.4000000059604645), tries), new RetryTermination.MaxTimeout(TimeSpan.FromSeconds(8.0))).IfSuccess((Promise<PostMatchClientUpdate> p) =>
			{
				matchDataFound = true;
				_postMatchClientUpdate = p.Result;
			});
			yield return promise.AsCoroutine();
		}
		bool valueOrDefault = _matchManager.Event?.PlayerEvent?.EventInfo?.IsRanked == true;
		_matchEndScene.EndOfMatchAnimationsCompleted += onEndOfMatchProgressComplete;
		RankProgress rankProgress = _playerRankManager.GetRankProgress();
		MythicRatingUpdated mythicRating = _playerRankManager.GetMythicRating();
		_matchEndScene.ShowEndOfMatchProgress((valueOrDefault & matchDataFound) ? rankProgress : null, mythicRating, (_matchManager.Event?.PlayerEvent?.EventInfo?.FormatType).GetValueOrDefault());
		yield return StartCoroutine(WaitFor(() => animationComplete, 10f, "MatchSceneManager WaitFor timed out! (Ranked Animation)"));
		_matchEndScene.EndOfMatchAnimationsCompleted -= onEndOfMatchProgressComplete;
		Stopwatch surveyElapsedWatch;
		GameEndSurvey survey;
		bool showingSurvey;
		if (shouldShowSurvey())
		{
			surveyElapsedWatch = new Stopwatch();
			survey = _matchEndScene.SurveyUI;
			showingSurvey = true;
			GameEndSurvey gameEndSurvey = survey;
			gameEndSurvey.FeedbackSubmitted = (Action<string>)Delegate.Combine(gameEndSurvey.FeedbackSubmitted, new Action<string>(onSurveyComplete));
			survey.gameObject.UpdateActive(active: true);
			surveyElapsedWatch.Start();
			yield return StartCoroutine(WaitFor(() => !showingSurvey, 60f, "MatchSceneManager WaitFor timed out! (Survey)"));
			if (showingSurvey)
			{
				survey.ForceClose();
				survey.gameObject.UpdateActive(active: false);
			}
			GameEndSurvey gameEndSurvey2 = survey;
			gameEndSurvey2.FeedbackSubmitted = (Action<string>)Delegate.Remove(gameEndSurvey2.FeedbackSubmitted, new Action<string>(onSurveyComplete));
		}
		bool exitMatch = false;
		_matchEndScene.ExitMatchCompleted += onExitMatch;
		bool enableViewBattlefieldButton = !UnityEngine.Object.FindFirstObjectByType<SideboardInterface>() && !_preGameScene;
		_matchEndScene.EnableEndOfMatchControls(enableViewBattlefieldButton);
		while (!exitMatch)
		{
			yield return null;
		}
		_matchEndScene.ExitMatchCompleted -= onExitMatch;
		if (string.IsNullOrEmpty(_postMatchSceneToLoad))
		{
			_playerRankManager.SetCombinedRank();
		}
		ExitMatchScene();
		void onEndOfMatchProgressComplete()
		{
			animationComplete = true;
		}
		void onExitMatch()
		{
			exitMatch = true;
		}
		void onSurveyComplete(string feedback)
		{
			_biLogger.Send(ClientBusinessEventType.MatchEndSurvey, new MatchEndSurvey
			{
				MatchFeedback = feedback,
				MatchId = _matchManager.MatchID,
				EventId = internalEventName,
				Elapsed = Math.Floor(surveyElapsedWatch.Elapsed.TotalMilliseconds)
			});
			showingSurvey = false;
			surveyElapsedWatch.Stop();
			GameEndSurvey gameEndSurvey3 = survey;
			gameEndSurvey3.FeedbackSubmitted = (Action<string>)Delegate.Remove(gameEndSurvey3.FeedbackSubmitted, new Action<string>(onSurveyComplete));
		}
		bool shouldShowSurvey()
		{
			if (MDNPlayerPrefs.DEBUG_AlwaysSurvey)
			{
				return true;
			}
			if (_matchManager == null)
			{
				return false;
			}
			if (string.IsNullOrEmpty(matchId))
			{
				return false;
			}
			Client_Survey postMatchSurveyConfig = _matchManager.PostMatchSurveyConfig;
			if (postMatchSurveyConfig == null)
			{
				return false;
			}
			if (postMatchSurveyConfig.Chance <= 0m)
			{
				return false;
			}
			EventContext eventContext = _matchManager.Event;
			if (eventContext == null)
			{
				return false;
			}
			if (eventContext.PlayerEvent == null)
			{
				return false;
			}
			if (string.IsNullOrEmpty(eventContext.PlayerEvent.MatchMakingName))
			{
				return false;
			}
			List<string> excludeEvents = postMatchSurveyConfig.ExcludeEvents;
			if (excludeEvents == null)
			{
				return false;
			}
			if (excludeEvents.Contains(internalEventName))
			{
				return false;
			}
			return (decimal)BitConverter.ToUInt32(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(_matchManager.MatchID)), 0) / 4294967295m * 100m < _matchManager.PostMatchSurveyConfig.Chance;
		}
	}

	private static IEnumerator WaitFor(Func<bool> isReady, float timeoutTime = 5f, string timeoutMessage = null)
	{
		if (timeoutTime <= 0f)
		{
			timeoutTime = 5f;
		}
		WaitForSeconds waitTime = new WaitForSeconds(0.1f);
		while (!isReady())
		{
			yield return waitTime;
			timeoutTime -= 0.1f;
			if (timeoutTime <= 0f)
			{
				UnityEngine.Debug.LogError(string.IsNullOrEmpty(timeoutMessage) ? "MatchSceneManager WaitFor timed out!" : timeoutMessage);
				break;
			}
		}
	}

	public void ExitMatchScene()
	{
		AudioManager.Instance.UnLoadBattleField();
		EnqueueSubSceneChange(SubScene.ExitMatchScene);
	}

	private void OnRemovedFromMatchmaking()
	{
		ExitMatchScene();
	}

	private void OnMatchReady()
	{
		if (AutoConnectToMatchServer)
		{
			if (_preGameScene != null)
			{
				_preGameScene.ReceivingMatchConfig();
			}
			else if (!IsNPEMatch() && !IsDebugMatch())
			{
				UnityEngine.Debug.LogError("_preGameScene was null!");
			}
			_matchmaking.JoinPendingMatch();
			_matchmaking.MatchReady -= OnMatchReady;
		}
	}

	private bool IsDebugMatch()
	{
		return _matchManager.Event == null;
	}

	private bool WaitForMatchResults()
	{
		return !IsDebugMatch();
	}

	private bool IsNPEMatch()
	{
		if (_npeState.ActiveNPEGame != null)
		{
			return !string.IsNullOrEmpty(_npeState.ActiveNPEGame.Battlefield);
		}
		return false;
	}

	public void JoinMatchNow()
	{
		_matchmaking.JoinPendingMatch();
	}

	public static void Load(PAPA papa, string postMatchSceneToLoad, System.Action onLoaded)
	{
		LoadAsync(papa.ToMatchContext(), papa.AssetLookupSystem, postMatchSceneToLoad, onLoaded, papa.destroyCancellationToken).Forget();
	}

	private static async UniTaskVoid LoadAsync(IContext context, AssetLookupSystem assetLookupSystem, string postMatchSceneToLoad, System.Action onLoaded, CancellationToken token)
	{
		await new LoadMatchSceneUniTask(LoadSceneMode.Single, context, assetLookupSystem, postMatchSceneToLoad).Load(token);
		onLoaded?.Invoke();
	}
}
