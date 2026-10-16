using System;
using System.Collections.Generic;
using System.Linq;
using Core.Shared.Code.ClientModels;
using Core.Shared.Code.DebugTools;
using GreClient.History;
using GreClient.Network;
using GreClient.Rules;
using Newtonsoft.Json.Linq;
using SharedClientCore.SharedClientCore.Code.PVPChallenge.Models;
using UnityEngine;
using Wizards.Arena.Client.Logging;
using Wizards.Arena.TcpConnection;
using Wizards.MDN;
using Wizards.Mtga.Diagnostics;
using Wizards.Mtga.FrontDoorModels;
using Wizards.Mtga.Inventory;
using Wotc.Mtga;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.DuelScene;
using Wotc.Mtga.Extensions;
using Wotc.Mtgo.Gre.External.Messaging;

public class MatchManager : IPlayerInfoProvider, IDisposable
{
	public class PlayerInfo
	{
		private string _screenName = string.Empty;

		public uint SeatId;

		public uint TeamId;

		public string WizardsAccountIdForPrivateGaming;

		public bool IsWotc;

		public RankingClassType RankingClass;

		public int RankingTier;

		public float MythicPercentile;

		public int MythicPlacement;

		public string AvatarSelection;

		public string SleeveSelection;

		public string TitleSelection;

		public ClientPetSelection PetSelection;

		public static string PrivateMatchOpponentNotJoinedYet = "UNJOINED";

		private readonly List<uint> _deckCards = new List<uint>();

		private readonly List<uint> _sideboardCards = new List<uint>();

		private readonly List<CardSkinTuple> _cardStyles = new List<CardSkinTuple>();

		public string ScreenName
		{
			get
			{
				return _screenName;
			}
			set
			{
				_screenName = SharedUtilities.StripRichText(value.SanitizedDisplayName());
			}
		}

		public IReadOnlyList<ClientCommanderInfo> Commanders { get; set; }

		public List<string> EmoteSelection { get; set; }

		public IReadOnlyList<uint> DeckCards => _deckCards;

		public IEnumerable<uint> SideboardCards => _sideboardCards;

		public IEnumerable<CardSkinTuple> CardStyles => _cardStyles;

		public PlayerInfo(string name)
		{
			if (!string.IsNullOrEmpty(name) && name.Contains("#"))
			{
				WizardsAccountIdForPrivateGaming = name;
			}
			ScreenName = name;
		}

		public void SetDeckInfo(IEnumerable<uint> deckCards, IEnumerable<uint> sideboardCards, IEnumerable<CardSkinTuple> cardStyles)
		{
			_deckCards.Clear();
			if (deckCards != null)
			{
				_deckCards.AddRange(deckCards);
			}
			_sideboardCards.Clear();
			if (sideboardCards != null)
			{
				_sideboardCards.AddRange(sideboardCards);
			}
			_cardStyles.Clear();
			if (cardStyles != null)
			{
				_cardStyles.AddRange(cardStyles);
			}
			BacktraceIntegration.AddBreadcrumb("Deck set for " + _screenName, LogType.Log, new Dictionary<string, string>
			{
				{ "player name", _screenName },
				{
					"player deck",
					string.Join(",", _deckCards)
				},
				{
					"player sideboard",
					string.Join(",", _sideboardCards)
				},
				{
					"player commander(s)",
					string.Join(",", Commanders.Select((ClientCommanderInfo c) => c.GrpId))
				},
				{
					"player card styles",
					string.Join(",", _cardStyles)
				}
			});
		}
	}

	public struct GameResult
	{
		public ResultType Result;

		public GREPlayerNum Winner;

		public ResultReason Reason;

		public MatchScope Scope;

		public uint WinnerId;
	}

	private readonly Wizards.Arena.Client.Logging.ILogger _logger;

	private Func<bool> _hasDebugRole;

	private ICardDatabaseAdapter _cardDataProvider;

	private GreInterface _greInterface;

	public List<PlayerInfo> Players = new List<PlayerInfo>();

	public bool HasReconnected;

	private bool disposed;

	private bool HasDebugRole
	{
		get
		{
			if (_hasDebugRole != null)
			{
				return _hasDebugRole();
			}
			return false;
		}
	}

	public GreInterface GreInterface
	{
		get
		{
			return _greInterface;
		}
		private set
		{
			if (_greInterface != null)
			{
				_greInterface.Dispose();
			}
			_greInterface = value;
		}
	}

	public MessageHistory MessageHistory { get; private set; }

	public IGREConnection GreConnection { get; private set; }

	public GreClient.Network.ConnectionState ConnectionState => Translate.ToConnectionState(GreConnection);

	public ConnectionConfig ConnectionConfig { get; private set; }

	public string MatchID { get; private set; }

	public string FabricUri { get; private set; }

	public uint LocalPlayerSeatId { get; private set; }

	public PlayerInfo LocalPlayerInfo { get; private set; } = new PlayerInfo("Local Player");

	public string LocalPlayerName
	{
		get
		{
			if (LocalPlayerInfo == null)
			{
				return string.Empty;
			}
			return LocalPlayerInfo.ScreenName;
		}
	}

	public PlayerInfo OpponentInfo { get; private set; } = new PlayerInfo("Opponent");

	public string OpponentName
	{
		get
		{
			if (OpponentInfo == null)
			{
				return string.Empty;
			}
			return OpponentInfo.ScreenName;
		}
	}

	public MatchWinCondition WinCondition { get; private set; }

	public List<GameResult> GameResults { get; private set; } = new List<GameResult>();

	public uint CurrentGameNumber { get; private set; }

	public MatchState MatchState { get; private set; }

	public SuperFormat Format { get; private set; }

	public GameVariant Variant { get; private set; }

	public HashSet<HeadlessClient> Familiars { get; private set; } = new HashSet<HeadlessClient>();

	public GreClient.Network.MatchConfig CreateMatchConfig { get; private set; }

	public string BattlefieldId { get; set; }

	public bool IsPracticeGame { get; set; }

	public bool IsPrivateGame { get; set; }

	public bool PrivateGameWaitingForMatchMade { get; set; }

	public PVPChallengeData ChallengeData { get; private set; }

	public EventContext Event { get; private set; }

	public Client_Survey PostMatchSurveyConfig { get; private set; } = new Client_Survey();

	public event System.Action OnConnectedToService;

	public event Action<string, string> OnRoomCreated;

	public event Action<GREToClientMessage> ConnectRespReceived;

	public event System.Action MatchCompleted;

	public event Action<MatchState> MatchStateChanged;

	public event System.Action SideboardSubmitted;

	public event Action<(string reason, string details)> MatchFailed;

	public event System.Action OnLostConnection;

	public event System.Action ConnectionFailed;

	public event Action<string, string, string> ServerExceptionReceived;

	public event Action<GREToClientMessage> MessageReceived;

	public event Action<ClientToGREMessage> MessageSent;

	public PlayerInfo PlayerInfoForNum(GREPlayerNum num)
	{
		if (num != GREPlayerNum.LocalPlayer)
		{
			return OpponentInfo;
		}
		return LocalPlayerInfo;
	}

	public MatchManager(Wizards.Arena.Client.Logging.ILogger logger)
	{
		_logger = logger ?? new ConsoleLogger();
	}

	public void SetDebugFunc(Func<bool> hasDebugRole)
	{
		_hasDebugRole = hasDebugRole;
	}

	public void Initialize(IGREConnection connection, ICardDatabaseAdapter cardDataProvider)
	{
		Reset();
		GreConnection = new RateLimitedConnection(connection, 1000u, 15u);
		_cardDataProvider = cardDataProvider;
		MessageHistory = CreateHistory();
		GreInterface = GreInterfaceFactory.Create(cardDataProvider, MessageHistory, _logger);
		connection.MatchConnectionStateChanged += OnCurrentRoomStateChanged;
		connection.MessageReceived += OnMessageReceived;
		connection.ConnectionFailed += OnConnectionFailed;
		connection.ConnectionLost += OnMatchConnectionLost;
		connection.MatchCompleted += OnMatchCompleted;
		connection.MatchFailed += OnMatchFailed;
		connection.ServerExceptionReceived += OnServerExceptionReceived;
	}

	public void Reset()
	{
		GreInterface = null;
		MessageHistory = null;
		CloseConnection("MatchManager.Reset");
		LocalPlayerInfo = new PlayerInfo("Local Player");
		OpponentInfo = new PlayerInfo("Opponent");
		Players.Clear();
		CurrentGameNumber = 0u;
		WinCondition = MatchWinCondition.None;
		MatchState = MatchState.None;
		Format = SuperFormat.None;
		Variant = GameVariant.None;
		GameResults = new List<GameResult>();
		CreateMatchConfig = default;
		HasReconnected = false;
		IsPrivateGame = false;
		IsPracticeGame = false;
		PrivateGameWaitingForMatchMade = false;
		ChallengeData = null;
		Event = null;
		ConnectionConfig = null;
		FabricUri = null;
		OnConnectedToService = null;
		OnRoomCreated = null;
		MatchStateChanged = null;
		MatchCompleted = null;
		SideboardSubmitted = null;
		MatchFailed = null;
		foreach (HeadlessClient familiar in Familiars)
		{
			familiar.Dispose();
		}
		Familiars.Clear();
	}

	public void SetEvent(EventContext evt)
	{
		Event = evt;
	}

	public void StartChallenge(PVPChallengeData data)
	{
		ChallengeData = data;
		PrivateGameWaitingForMatchMade = true;
		OpponentInfo = new PlayerInfo(data.OpponentFullName);
		LocalPlayerInfo = new PlayerInfo(data.LocalPlayerDisplayName);
		LocalPlayerInfo.AvatarSelection = data.LocalPlayer?.Cosmetics?.avatarSelection;
	}

	public void StartAIBotMatch(string displayName)
	{
		LocalPlayerInfo = new PlayerInfo(displayName);
	}

	public void RegisterFamiliarClient(HeadlessClient familiar)
	{
		Familiars.Add(familiar);
	}

	public void CancelPrivateGaming()
	{
		PrivateGameWaitingForMatchMade = false;
		OpponentInfo = new PlayerInfo("Opponent");
	}

	private MessageHistory CreateHistory()
	{
		if (PerformanceCSVLogger.IsReporting)
		{
			return new NullHistory();
		}
		if (HasDebugRole)
		{
			return new DebugHistory();
		}
		return new NullHistory();
	}

	private void OnConnectionFailed(string reason)
	{
		ConnectionFailed?.Invoke();
	}

	private void OnMatchConnectionLost(string reason)
	{
		OnLostConnection?.Invoke();
	}

	private void OnMatchCompleted()
	{
		MatchCompleted?.Invoke();
	}

	private void OnMatchFailed((string reason, string details) failureParams)
	{
		MatchFailed?.Invoke(failureParams);
	}

	private void OnServerExceptionReceived(string context, string errorCode, string message)
	{
		ServerExceptionReceived?.Invoke(context, errorCode, message);
	}

	private void CloseConnection(string reason)
	{
		if (GreConnection != null)
		{
			GreConnection.MatchConnectionStateChanged -= OnCurrentRoomStateChanged;
			GreConnection.MessageReceived -= OnMessageReceived;
			GreConnection.ConnectionFailed -= OnConnectionFailed;
			GreConnection.ConnectionLost -= OnMatchConnectionLost;
			GreConnection.MatchCompleted -= OnMatchCompleted;
			GreConnection.ServerExceptionReceived -= OnServerExceptionReceived;
			GreConnection.Close(TcpConnectionCloseType.NormalClosure, reason);
			if (GreConnection is IDisposable disposable)
			{
				disposable.Dispose();
			}
			GreConnection = null;
		}
	}

	public void ConnectToMatchService(ConnectionConfig config)
	{
		ConnectionConfig = config;
		GreConnection?.Connect(config);
	}

	public void CreateMatch(GreClient.Network.MatchConfig matchConfig)
	{
		CreateMatchConfig = matchConfig;
		GreConnection.CreateMatch(matchConfig.ToCreateMatchMessage(Guid.NewGuid()));
	}

	public void StartCheckpoint(MatchCheckpoint checkpoint)
	{
		CreateMatchConfig = checkpoint.MatchConfig;
		GreConnection.CreateMatch(checkpoint.ToCreateMatchMessage(Guid.NewGuid()));
	}

	public void JoinMatch(string fabricUri, string matchId)
	{
		FabricUri = fabricUri;
		MatchID = matchId;
		GreConnection.ConnectToGRE(fabricUri, matchId);
	}

	public void ConnectAndJoinMatch(ConnectionConfig conConfig, string controllerFabricUri, string matchId)
	{
		FabricUri = controllerFabricUri;
		MatchID = matchId;
		ConnectToMatchService(conConfig);
	}

	private void OnCurrentRoomStateChanged(MatchDoorConnectionState oldState, MatchDoorConnectionState newState)
	{
		_logger.Info("STATE CHANGED", new JObject(new JProperty("old", oldState.ToString()), new JProperty("new", newState.ToString())));
		switch (newState)
		{
		case MatchDoorConnectionState.ConnectedToMatchDoor:
			OnConnectedToService?.Invoke();
			if (FabricUri != null && MatchID != null)
			{
				JoinMatch(FabricUri, MatchID);
			}
			break;
		case MatchDoorConnectionState.ConnectedToMatchDoor_MatchCreated:
			FabricUri = GreConnection.McFabricUri;
			MatchID = GreConnection.MatchId;
			OnRoomCreated?.Invoke(FabricUri, MatchID);
			JoinMatch(FabricUri, MatchID);
			break;
		case MatchDoorConnectionState.None:
			if (oldState == MatchDoorConnectionState.Disconnected)
			{
				OnConnectionFailed(string.Empty);
			}
			break;
		case MatchDoorConnectionState.Disconnected:
			if (oldState == MatchDoorConnectionState.Playing)
			{
				OnMatchConnectionLost("Disconnected");
			}
			break;
		case MatchDoorConnectionState.ConnectedToMatchDoor_CreatingMatch:
			break;
		}
	}

	private void OnMessageReceived(GREToClientMessage msg)
	{
		if (msg.SystemSeatIds.Count == 1)
		{
			LocalPlayerSeatId = msg.SystemSeatIds[0];
		}
		switch (msg.Type)
		{
		case GREMessageType.ConnectResp:
		{
			ConnectResp connectResp = msg.ConnectResp;
			if (connectResp.Status == ConnectionStatus.Success)
			{
				DeckMessage deckMessage = connectResp.DeckMessage;
				LocalPlayerInfo.SetDeckInfo(deckMessage.DeckCards, deckMessage.SideboardCards, connectResp.Skins);
			}
			ConnectRespReceived?.Invoke(msg);
			break;
		}
		case GREMessageType.GameStateMessage:
		{
			GameInfo gameInfo = msg.GameStateMessage.GameInfo;
			if (gameInfo == null)
			{
				break;
			}
			if (gameInfo.MatchWinCondition != MatchWinCondition.None)
			{
				WinCondition = gameInfo.MatchWinCondition;
			}
			IEnumerable<ResultSpec> results = gameInfo.Results;
			if (results != null)
			{
				GameResults.Clear();
				foreach (ResultSpec item in results)
				{
					GameResults.Add(new GameResult
					{
						Result = item.Result,
						Winner = calcWinner(item.WinningTeamId, item.Result),
						Reason = item.Reason,
						Scope = item.Scope,
						WinnerId = item.WinningTeamId
					});
				}
			}
			if (gameInfo.SuperFormat != Format)
			{
				Format = gameInfo.SuperFormat;
			}
			if (gameInfo.Variant != Variant)
			{
				Variant = gameInfo.Variant;
			}
			uint gameNumber = gameInfo.GameNumber;
			if (gameNumber != CurrentGameNumber)
			{
				CurrentGameNumber = gameNumber;
			}
			MatchState matchState = gameInfo.MatchState;
			if (matchState != MatchState)
			{
				MatchState = matchState;
				if (MatchState == MatchState.GameInProgress)
				{
					GreInterface = GreInterfaceFactory.Create(_cardDataProvider, MessageHistory, _logger);
				}
				MatchStateChanged?.Invoke(MatchState);
			}
			break;
		}
		}
		MessageReceived.SafeInvoke(msg);
		MessageHistory.TrackIncoming(msg);
		GreInterface.OnMessageReceived(msg);
		if (msg.Type == GREMessageType.SubmitDeckConfirmation)
		{
			SideboardSubmitted?.Invoke();
		}
		GREPlayerNum calcWinner(uint winningTeamId, ResultType resultType)
		{
			if (resultType == ResultType.WinLoss)
			{
				if (winningTeamId != LocalPlayerSeatId)
				{
					return GREPlayerNum.Opponent;
				}
				return GREPlayerNum.LocalPlayer;
			}
			return GREPlayerNum.Invalid;
		}
	}

	public void Update()
	{
		if (GreConnection != null)
		{
			GreConnection.ProcessMessages();
		}
		if (GreInterface != null)
		{
			GreInterface.ProcessQueues();
			ClientToGREMessage clientToGREMessage = GreInterface.DequeueOutgoingMessage();
			if (clientToGREMessage != null)
			{
				MessageSent.SafeInvoke(clientToGREMessage);
				MessageHistory.TrackOutgoing(clientToGREMessage);
				GreConnection.SendMessage(clientToGREMessage);
			}
		}
		foreach (HeadlessClient familiar in Familiars)
		{
			familiar.Update();
		}
	}

	public void Dispose()
	{
		_hasDebugRole = null;
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposed)
		{
			if (disposing)
			{
				GreInterface = null;
				CloseConnection("MatchManager.Dispose");
			}
			disposed = true;
		}
	}

	public void SetPostMatchSurveyConfig(Client_Survey config)
	{
		PostMatchSurveyConfig = config;
	}

	public PlayerInfo GetPlayerInfo(uint seatId)
	{
		return Players.Find(seatId, (PlayerInfo x, uint id) => x.SeatId == id);
	}

	public IEnumerable<PlayerInfo> GetAllPlayerInfo()
	{
		return Players;
	}

	public IEnumerable<PlayerInfo> GetReportablePlayerInfos()
	{
		return from pi in GetAllPlayerInfo()
			where pi.SeatId != LocalPlayerSeatId && pi.WizardsAccountIdForPrivateGaming != null
			select pi;
	}
}
