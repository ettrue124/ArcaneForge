using System;
using System.Collections.Generic;
using System.Linq;
using Core.Shared.Code.Network.Utils;
using EventPage.Components.NetworkModels;
using Newtonsoft.Json;
using Wizards.Arena.Enums.Event;
using Wizards.Arena.Enums.Store;
using Wizards.Arena.Models.Network;
using Wizards.Arena.Promises;
using Wizards.MDN;
using Wizards.Models;
using Wizards.Mtga.Decks;
using Wizards.Mtga.FrontDoorModels;
using Wizards.Unification.Models.Deck;
using Wizards.Unification.Models.Draft;
using Wizards.Unification.Models.Event;
using Wizards.Unification.Models.Events;
using Wizards.Unification.Models.FrontDoor;
using Wizards.Unification.Models.Player;
using Wotc.Mtga.Cards.Database;

namespace Wotc.Mtga.Network.ServiceWrappers;

public class AwsEventServiceWrapper : IEventsServiceWrapper
{
	public class AwsCourseInfo : ICourseInfoWrapper
	{
		private readonly ClientPlayerCourseV3 _clientPlayerCourse;

		public Guid Id => _clientPlayerCourse.CourseId;

		public PlayerEventState CurrentEventState
		{
			get
			{
				PlayerEventState result = PlayerEventState.PreMatch;
				if (_clientPlayerCourse.CurrentModule == EModule.CreateMatch)
				{
					result = PlayerEventState.ReadyToMatch;
				}
				else if (_clientPlayerCourse.CurrentModule == EModule.ClaimPrize)
				{
					result = PlayerEventState.DoneWithMatches;
				}
				else if (_clientPlayerCourse.CurrentModule == EModule.Complete)
				{
					result = PlayerEventState.Completed;
				}
				return result;
			}
		}

		public PlayerEventModule CurrentEventModule => _clientPlayerCourse.CurrentModule switch
		{
			EModule.Join => PlayerEventModule.Join, 
			EModule.Sealed => PlayerEventModule.None, 
			EModule.GrantCardPool => PlayerEventModule.DeckSelect, 
			EModule.DeckSelect => PlayerEventModule.DeckSelect, 
			EModule.CreateMatch => PlayerEventModule.TransitionToMatches, 
			EModule.MatchResults => PlayerEventModule.TransitionToMatches, 
			EModule.RankUpdate => PlayerEventModule.None, 
			EModule.Complete => PlayerEventModule.Complete, 
			EModule.ClaimPrize => PlayerEventModule.ClaimPrize, 
			EModule.Momir => PlayerEventModule.None, 
			EModule.Choice => PlayerEventModule.Choice, 
			EModule.EntryReward => PlayerEventModule.None, 
			EModule.WinLossGate => PlayerEventModule.WinLossGate, 
			EModule.WinNoGate => PlayerEventModule.WinNoGate, 
			EModule.BotDraft => PlayerEventModule.Draft, 
			EModule.JumpStart => PlayerEventModule.Jumpstart, 
			EModule.PlayerDraft => PlayerEventModule.HumanDraft, 
			_ => PlayerEventModule.None, 
		};

		public List<uint> CardPool => _clientPlayerCourse.CardPool ?? new List<uint>();

		public List<CollationCardPool> CardPoolByCollation => _clientPlayerCourse.CardPoolByCollation ?? new List<CollationCardPool>();

		public string MadeChoice => _clientPlayerCourse.MadeChoice;

		public List<string> CardStyles => _clientPlayerCourse.CardStyles ?? new List<string>();

		public Client_Deck CourseDeck
		{
			get
			{
				Client_Deck client_Deck = new Client_Deck(DeckServiceWrapperHelpers.ToClientModel(_clientPlayerCourse.CourseDeckSummary));
				if (_clientPlayerCourse.CourseDeck != null)
				{
					client_Deck.UpdateWith(DeckServiceWrapperHelpers.ToClientModel(_clientPlayerCourse.CourseDeck));
				}
				return client_Deck;
			}
		}

		public string DraftId => "";

		public bool HasUnclaimedRewards => CurrentEventModule == PlayerEventModule.ClaimPrize;

		public int GamesPlayed => CurrentWins + CurrentLosses;

		public int CurrentWins => _clientPlayerCourse.CurrentWins;

		public int CurrentLosses => _clientPlayerCourse.CurrentLosses;

		public List<DTO_JumpStartSelection> CurrentChoices => _clientPlayerCourse.JumpStart?.CurrentChoices;

		public List<DTO_JumpStartSelection> PacketsChosen => _clientPlayerCourse.JumpStart?.PacketsChosen;

		public List<DTO_JumpStartSelection> HistoricalChoices => new List<DTO_JumpStartSelection>();

		public string InternalEventName => _clientPlayerCourse.InternalEventName;

		public string HumanDraftId => _clientPlayerCourse.DraftId;

		public int NPENextGameNumber => 0;

		public AwsCourseInfo(ClientPlayerCourseV3 clientPlayerCourse)
		{
			_clientPlayerCourse = clientPlayerCourse;
			if (_clientPlayerCourse.CourseDeckSummary == null)
			{
				_clientPlayerCourse.CourseDeckSummary = new DTO_DeckSummaryV3
				{
					DeckId = Guid.NewGuid()
				};
			}
		}

		public AwsCourseInfo(string internalEventName)
			: this(new ClientPlayerCourseV3
			{
				InternalEventName = internalEventName,
				CurrentModule = EModule.Join
			})
		{
		}
	}

	private FrontDoorConnectionAWS _fd;

	private IInventoryServiceWrapper _inventoryServiceWrapper;

	private int _cacheVersion;

	private List<EventPage.Components.NetworkModels.EventInfoV3> _cachedEvents = new List<EventPage.Components.NetworkModels.EventInfoV3>();

	private List<ClientDynamicFilterTag> _dynamicFilterTags = new List<ClientDynamicFilterTag>();

	public List<ClientDynamicFilterTag> DynamicFilterTags => _dynamicFilterTags;

	public AwsEventServiceWrapper(FrontDoorConnectionAWS fd, IInventoryServiceWrapper inventoryServiceWrapper)
	{
		_fd = fd;
		_inventoryServiceWrapper = inventoryServiceWrapper;
	}

	public Promise<ICourseInfoWrapper> ClaimNoGatePrize(string internalEventName)
	{
		return ClaimPrizePromise(internalEventName);
	}

	public Promise<ICourseInfoWrapper> ClaimPrize(string internalEventName)
	{
		return ClaimPrizePromise(internalEventName);
	}

	private Promise<ICourseInfoWrapper> ClaimPrizePromise(string internalEventName)
	{
		return _fd.ClaimPrize(internalEventName).IfSuccess((Promise<CourseResponseV2> promise) =>
		{
			if (promise.Result.InventoryInfo != null)
			{
				_inventoryServiceWrapper.OnInventoryInfoUpdated_AWS(AWSInventoryConversions.ConvertInventoryInfo(promise.Result.InventoryInfo));
			}
		}).Then((Promise<CourseResponseV2> p) =>
		{
			if (!p.Successful || p.Result.Course.CurrentModule != EModule.Complete)
			{
				SimplePromise<ICourseInfoWrapper> simplePromise = new SimplePromise<ICourseInfoWrapper>();
				simplePromise.SetResult(new AwsCourseInfo(p.Result.Course));
				return simplePromise;
			}
			return DropFromEvent(internalEventName);
		});
	}

	public Promise<ICourseInfoWrapper> ResignFromEvent(string internalEventName)
	{
		return _fd.ResignFromEvent(internalEventName).Then((Promise<ClientPlayerCourseV3> p) =>
		{
			if (!p.Successful || p.Result.CurrentModule != EModule.Complete)
			{
				SimplePromise<ICourseInfoWrapper> simplePromise = new SimplePromise<ICourseInfoWrapper>();
				simplePromise.SetResult(new AwsCourseInfo(p.Result));
				return simplePromise;
			}
			return DropFromEvent(internalEventName);
		});
	}

	public Promise<ICourseInfoWrapper> DropFromEvent(string internalEventName)
	{
		return ((Promise<CourseResponseV2>)_fd.DropFromEvent(internalEventName)).Convert((Func<CourseResponseV2, ICourseInfoWrapper>)((CourseResponseV2 _) => new AwsCourseInfo(internalEventName)));
	}

	public Promise<ActiveEventsResponseV2> GetEvents()
	{
		return RequestEvents(0, showAllEvents: true);
	}

	public Promise<List<EventPage.Components.NetworkModels.EventInfoV3>> GetAllEvents(ICardDataProvider cardDatabase, bool showAllEvents)
	{
		return RequestEvents(_cacheVersion, showAllEvents).Convert((ActiveEventsResponseV2 response) =>
		{
			if (response.CacheVersion != _cacheVersion)
			{
				_cachedEvents = AwsEventHelpers.ConvertToEventsList(cardDatabase, response);
				_dynamicFilterTags = response.DynamicFilterTags;
			}
			_cacheVersion = response.CacheVersion;
			return _cachedEvents;
		});
	}

	public Promise<ICourseInfoWrapper> GetEventCourse(string internalEventName)
	{
		return GetEventCourses().Convert((List<ICourseInfoWrapper> res) => res?.FirstOrDefault((ICourseInfoWrapper info) => info.InternalEventName == internalEventName) ?? new AwsCourseInfo(internalEventName));
	}

	public Promise<List<ICourseInfoWrapper>> GetEventCourses()
	{
		return GetCourses().Convert((PlayerCoursesResponseV3 resp) => ((IEnumerable<ClientPlayerCourseV3>)resp.Courses).Select((Func<ClientPlayerCourseV3, ICourseInfoWrapper>)((ClientPlayerCourseV3 playerCourse) => new AwsCourseInfo(playerCourse))).ToList());
	}

	public Promise<ICourseInfoWrapper> JoinAndPay(string internalEventName, EventEntryCurrencyType currency, int currencyAmount, string tokenId, string eventChoice, bool debugIgnoreEntryLimit)
	{
		return JoinEvent(internalEventName, currency.ToPurchaseCurrency(), currencyAmount, tokenId, eventChoice, debugIgnoreEntryLimit).IfSuccess((Promise<CourseResponseV2> promise) =>
		{
			if (promise.Result.InventoryInfo != null)
			{
				_inventoryServiceWrapper.OnInventoryInfoUpdated_AWS(AWSInventoryConversions.ConvertInventoryInfo(promise.Result.InventoryInfo));
			}
		}).Convert((Func<CourseResponseV2, ICourseInfoWrapper>)((CourseResponseV2 res) => new AwsCourseInfo(res.Course)));
	}

	private static bool TryDeserializeInventoryInfoFromModulePayload(ModuleResponse moduleResponse, out Wizards.Unification.Models.Player.InventoryInfo invInfo)
	{
		if (string.IsNullOrEmpty(moduleResponse?.Payload))
		{
			invInfo = null;
			return false;
		}
		try
		{
			invInfo = JsonConvert.DeserializeObject<Wizards.Unification.Models.Player.InventoryInfo>(moduleResponse.Payload);
			return true;
		}
		catch
		{
			invInfo = null;
			return false;
		}
	}

	public Promise<Unit> JoinNewMatchQueue(string internalEventName)
	{
		return _fd.JoinNewMatchQueue(internalEventName).Convert((string _) => Unit.Value);
	}

	public Promise<ICourseInfoWrapper> SubmitEventDeck(string internalEventName, Client_Deck deck)
	{
		return _fd.SubmitEventDeck(internalEventName, deck).Convert((Func<ClientPlayerCourseV3, ICourseInfoWrapper>)((ClientPlayerCourseV3 course) => new AwsCourseInfo(course)));
	}

	public List<ICourseInfoWrapper> CreateCoursesForJoinState(List<ICourseInfoWrapper> courseInfoList, List<EventPage.Components.NetworkModels.EventInfoV3> events)
	{
		foreach (EventPage.Components.NetworkModels.EventInfoV3 eventV3 in events)
		{
			if (!courseInfoList.Exists((ICourseInfoWrapper c) => c.InternalEventName == eventV3.InternalEventName))
			{
				ICourseInfoWrapper item = new AwsCourseInfo(new ClientPlayerCourseV3
				{
					InternalEventName = eventV3.InternalEventName,
					CurrentModule = EModule.Join,
					ModulePayload = "",
					CourseDeckSummary = new DTO_DeckSummaryV3(),
					CourseDeck = new Wizards.Unification.Models.Deck.Deck(),
					CurrentWins = 0,
					CurrentLosses = 0
				});
				courseInfoList.Add(item);
			}
		}
		return courseInfoList;
	}

	public Promise<bool> GetJoinEventQueueStatus()
	{
		SimplePromise<bool> simplePromise = new SimplePromise<bool>();
		simplePromise.SetError(new Error(0, "Not Implemented"));
		return simplePromise;
	}

	public Promise<ICourseInfoWrapper> TryRejoinHumanDraft(string internalEventName)
	{
		return ReJoinPlayerDraft(internalEventName).Convert((Func<ClientPlayerCourseV3, ICourseInfoWrapper>)((ClientPlayerCourseV3 res) => new AwsCourseInfo(res)));
	}

	public void AddDraftNotificationCallback(Action<DraftNotification> callback)
	{
		_fd.OnMsg_DraftNotification += callback;
	}

	public void AddPodQueueNotificationCallback(Action<PodQueueNotification> callback)
	{
		_fd.OnMsg_PodQueueNotification += callback;
	}

	public Promise<ICourseInfoWrapper> CompleteDraft(string internalEventName, bool botDraft)
	{
		if (botDraft)
		{
			return GetEventCourse(internalEventName);
		}
		return _fd.CompleteDraft(internalEventName, isBotDraft: false).IfSuccess((Promise<ClientPlayerCourseV3> p) => _fd.ConfirmDraftCardPoolGrant(internalEventName)).Convert((Func<ClientPlayerCourseV3, ICourseInfoWrapper>)((ClientPlayerCourseV3 course) => new AwsCourseInfo(course)));
	}

	public Promise<ICourseInfoWrapper> CreateBotDraft(string internalEventName)
	{
		return _fd.CreateBotDraft(internalEventName).Convert((ModuleResponse _) => (ICourseInfoWrapper)null);
	}

	public Promise<ICourseInfoWrapper> SubmitEventChoice(string internalEventName, string choice)
	{
		return _fd.SetJumpStartPacket(internalEventName, choice).Then((Promise<CourseResponseV2> p) =>
		{
			if (p.Successful)
			{
				if (p.Result.InventoryInfo != null)
				{
					_inventoryServiceWrapper.OnInventoryInfoUpdated_AWS(AWSInventoryConversions.ConvertInventoryInfo(p.Result.InventoryInfo));
				}
				return new SimplePromise<ICourseInfoWrapper>(new AwsCourseInfo(p.Result.Course));
			}
			return new SimplePromise<ICourseInfoWrapper>(p.Error);
		});
	}

	public Promise<ModuleResponse> DropFromAllPodQueues()
	{
		return LeavePlayerDraftQueues();
	}

	public Promise<ClientBotDraftResponse> GetBotDraftStatus(string internalEventName)
	{
		return _fd.GetBotDraftStatus(internalEventName).Convert((ModuleResponse resp) => CreateBotDraftResponse(JsonConvert.DeserializeObject<BotDraftResponse>(resp.Payload), internalEventName));
	}

	private static ClientBotDraftResponse CreateBotDraftResponse(BotDraftResponse pickResponse, string eventName)
	{
		return new ClientBotDraftResponse
		{
			draftPack = pickResponse.DraftPack,
			packStyles = pickResponse.PackStyles,
			draftStatus = (Wizards.Mtga.FrontDoorModels.DraftStatus)pickResponse.DraftStatus,
			internalEventName = eventName,
			packNumber = pickResponse.PackNumber,
			pickedCards = (pickResponse.PickedCards ?? new List<string>()),
			pickNumber = pickResponse.PickNumber,
			numCardsToPick = Math.Max(1, pickResponse.NumCardsToPick),
			draftId = null,
			result = (Wizards.Mtga.FrontDoorModels.BotDraftCmdResult)pickResponse.Result,
			pickedStyles = pickResponse.PickedStyles
		};
	}

	public Promise<Unit> JoinNewPodQueue(string internalEventName)
	{
		return JoinPlayerDraftQueue(internalEventName).Convert((Func<ModuleResponse, Unit>)((ModuleResponse _) => default));
	}

	public Promise<ClientBotDraftResponse> MakeBotDraftPick(string internalEventName, List<int> cardIds, int packNumber, int pickNumber)
	{
		return _fd.MakeBotDraftPick(internalEventName, cardIds, packNumber, pickNumber).IfSuccess((Promise<ModuleResponse> promise) =>
		{
			Wizards.Unification.Models.Player.InventoryInfo dTO_InventoryInfo = promise.Result.DTO_InventoryInfo;
			if (dTO_InventoryInfo != null && dTO_InventoryInfo.Changes?.Count > 0)
			{
				_inventoryServiceWrapper.OnInventoryInfoUpdated_AWS(AWSInventoryConversions.ConvertInventoryInfo(promise.Result.DTO_InventoryInfo));
			}
		}).Convert((ModuleResponse resp) => CreateBotDraftResponse(JsonConvert.DeserializeObject<BotDraftResponse>(resp.Payload), internalEventName));
	}

	public Promise<PickResponse> MakeHumanDraftPick(string draftId, List<int> cardIds, int packNumber, int pickNumber)
	{
		return _fd.MakeHumanDraftPick(draftId, cardIds, packNumber, pickNumber);
	}

	public Promise<PickResponse> PlayerDraftReserveCard(string draftId, List<int> cardIds, int packNumber, int pickNumber)
	{
		return _fd.PlayerDraftReserveCard(draftId, cardIds, packNumber, pickNumber);
	}

	public Promise<PickResponse> PlayerDraftClearReservedCards(string draftId)
	{
		return _fd.PlayerDraftClearReservedCard(draftId);
	}

	public Promise<Unit> MarkHumanDraftDisconnected(string draftId)
	{
		return null;
	}

	public Promise<ICourseInfoWrapper> ReadyDraft(string internalEventName, string draftId)
	{
		return ((Promise<ClientPlayerCourseV3>)_fd.ReadyDraft(internalEventName, draftId)).Convert((Func<ClientPlayerCourseV3, ICourseInfoWrapper>)((ClientPlayerCourseV3 course) => new AwsCourseInfo(course)));
	}

	public void RemoveDraftNotificationCallback(Action<DraftNotification> callback)
	{
		_fd.OnMsg_DraftNotification -= callback;
	}

	public void RemovePodQueueNotificationCallback(Action<PodQueueNotification> callback)
	{
		_fd.OnMsg_PodQueueNotification -= callback;
	}

	public Promise<TablePacksResponse> GetHumanDraftTablePacks(string draftId)
	{
		return _fd.GetTablePacks(draftId);
	}

	public Promise<ICourseInfoWrapper> SubmitEventDeckFromChoices(string internalEventName, string deckId)
	{
		return _fd.SetChoice(internalEventName, deckId).Convert((Func<ClientPlayerCourseV3, ICourseInfoWrapper>)((ClientPlayerCourseV3 course) => new AwsCourseInfo(course)));
	}

	public Promise<ICourseInfoWrapper> SetChoice(string internalEventName, string choice)
	{
		return _fd.SetChoice(internalEventName, choice).Convert((Func<ClientPlayerCourseV3, ICourseInfoWrapper>)((ClientPlayerCourseV3 course) => new AwsCourseInfo(course)));
	}

	private Promise<ActiveEventsResponseV2> RequestEvents(int cacheOverride, bool showAllEvents)
	{
		GetActiveEventsReqV2 request = new GetActiveEventsReqV2
		{
			CacheVersion = cacheOverride,
			ShowAllEvents = showAllEvents
		};
		return _fd.SendMessage<ActiveEventsResponseV2>(CmdType.EventGetActiveEventsV2, request);
	}

	private Promise<PlayerCoursesResponseV3> GetCourses()
	{
		GetCoursesReq request = new GetCoursesReq();
		return _fd.SendMessage<PlayerCoursesResponseV3>(CmdType.EventGetCoursesV2, request);
	}

	private Promise<CourseResponseV2> JoinEvent(string internalEventName, Wizards.Arena.Enums.Store.PurchaseCurrency currency = Wizards.Arena.Enums.Store.PurchaseCurrency.None, int amount = 0, string customToken = null, string eventChoice = null, bool debugIgnoreEntryLimits = false)
	{
		JoinEventReq request = new JoinEventReq
		{
			EventName = internalEventName,
			EntryCurrencyType = currency,
			EntryCurrencyPaid = amount,
			CustomTokenId = customToken,
			EventChoice = eventChoice,
			DebugIgnoreEntryLimits = debugIgnoreEntryLimits
		};
		return _fd.SendMessage<CourseResponseV2>(CmdType.EventJoin, request);
	}

	private Promise<ModuleResponse> JoinPlayerDraftQueue(string internalEventName)
	{
		JoinDraftQueueReq request = new JoinDraftQueueReq
		{
			EventName = internalEventName
		};
		return _fd.SendMessage<ModuleResponse>(CmdType.EventJoinDraftQueue, request);
	}

	private Promise<ClientPlayerCourseV3> ReJoinPlayerDraft(string internalEventName)
	{
		ReJoinDraftReq request = new ReJoinDraftReq
		{
			EventName = internalEventName
		};
		return _fd.SendMessage<ClientPlayerCourseV3>(CmdType.EventJoinDraft, request);
	}

	private Promise<ModuleResponse> LeavePlayerDraftQueues()
	{
		return _fd.SendMessage<ModuleResponse>(CmdType.EventLeaveDraftQueue, "");
	}
}
