using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssetLookupTree;
using Assets.Core.Shared.Code;
using Core.Code.AssetLookupTree.AssetLookup;
using Wizards.Arena.Enums.Event;
using Wizards.Arena.Promises;
using Wizards.MDN;
using Wizards.Mtga.Decks;
using Wizards.Mtga.FrontDoorModels;
using Wizards.Mtga.Inventory;
using Wizards.Mtga.PlayBlade.Extensions;
using Wizards.Unification.Models.Events;
using Wizards.Unification.Models.PlayBlade;
using Wotc.Mtga.Events;
using Wotc.Mtga.Network.ServiceWrappers;

namespace Wizards.Mtga.PlayBlade;

public class BladeData : IBladeModel
{
	private readonly DeckViewBuilder _deckViewBuilder;

	private readonly RecentlyPlayedDataProvider _recentlyPlayedDataProvider;

	private readonly ViewedEventsDataProvider _viewedEventsDataProvider;

	private bool _unviewedEventsExist;

	public IReadOnlyList<BladeEventInfo> Events { get; private set; }

	public IReadOnlyList<BladeEventFilter> EventFilters { get; private set; }

	public IReadOnlyDictionary<PlayBladeQueueType, List<BladeQueueInfo>> Queues { get; private set; }

	public IReadOnlyList<DeckViewInfo> Decks { get; private set; }

	public IReadOnlyList<RecentlyPlayedInfo> RecentlyPlayed { get; private set; }

	public static Task<BladeData> GetBladeData()
	{
		return GetBladeData(Pantry.Get<EventManager>(), Pantry.Get<AssetLookupManager>().AssetLookupSystem, Pantry.Get<DeckDataProvider>(), Pantry.Get<IPlayerRankServiceWrapper>()?.CombinedRank, WrapperController.Instance.SparkyTourState, Pantry.Get<RecentlyPlayedDataProvider>(), Pantry.Get<InventoryManager>().Inventory, Pantry.Get<PlayBladeConfigDataProvider>().GetPlayBladeConfig(), Pantry.Get<ViewedEventsDataProvider>());
	}

	public static async Task<BladeData> GetBladeData(EventManager eventManager, AssetLookupSystem assetLookupSystem, DeckDataProvider deckDataProvider, CombinedRankInfo combinedRankInfo, SparkyTourState sparkyTourState, RecentlyPlayedDataProvider recentlyPlayedDataProvider, ClientPlayerInventory playerInventory, List<PlayBladeQueueEntry> playBladeConfig, ViewedEventsDataProvider viewedEventsDataProvider)
	{
		List<EventContext> list = FindActiveAndEntryAvailable(eventManager?.EventContexts ?? new List<EventContext>(), playerInventory);
		BladeData bladeData = new BladeData(list, recentlyPlayedDataProvider, viewedEventsDataProvider);
		List<Client_Deck> cachedDecks = deckDataProvider.GetCachedDecks();
		List<BladeEventInfo> list2 = list.FindAll((EventContext x) =>
		{
			EventBladeBehavior eventBladeBehavior = x.PlayerEvent.EventUXInfo.EventBladeBehavior;
			return eventBladeBehavior == EventBladeBehavior.Queue || eventBladeBehavior == EventBladeBehavior.Hidden;
		}).ConvertAll((EventContext x) => x.ConvertToBladeEventInfo(assetLookupSystem, combinedRankInfo));
		List<BladeEventInfo> eventBladeInfos = GetSortedEventContexts(list).ConvertAll((EventContext x) => x.ConvertToBladeEventInfo(assetLookupSystem, combinedRankInfo));
		Dictionary<PlayBladeQueueType, List<BladeQueueInfo>> queueBladeInfos = HydrateMockWithRealEvents(list2, playBladeConfig);
		await bladeData.AsyncInit(recentlyPlayedDataProvider, list2, cachedDecks, sparkyTourState, eventManager, eventBladeInfos, queueBladeInfos);
		return bladeData;
	}

	private BladeData(List<EventContext> availableEvents, RecentlyPlayedDataProvider recentlyPlayedDataProvider, ViewedEventsDataProvider viewedEventsDataProvider)
	{
		_recentlyPlayedDataProvider = recentlyPlayedDataProvider;
		_viewedEventsDataProvider = viewedEventsDataProvider;
		_viewedEventsDataProvider.TrimViewedEvents(availableEvents.Select((EventContext evt) => evt.PlayerEvent.EventInfo.EventId).ToList());
		_deckViewBuilder = Pantry.Get<DeckViewBuilder>();
	}

	private async Task AsyncInit(RecentlyPlayedDataProvider recentlyPlayedDataProvider, List<BladeEventInfo> convertedQueueData, List<Client_Deck> cachedDecks, SparkyTourState sparkyTourState, EventManager evtManager, List<BladeEventInfo> eventBladeInfos, Dictionary<PlayBladeQueueType, List<BladeQueueInfo>> queueBladeInfos)
	{
		RecentlyPlayed = CreateRecentlyPlayedInfos(deckInfos: (List<DeckViewInfo>)(Decks = _deckViewBuilder.CreateDeckViewInfos(cachedDecks)), recentGamesData: DeDupeRecentGamesData((await recentlyPlayedDataProvider.GetData().AsTask).Result), queuesInfos: convertedQueueData, eventInfos: eventBladeInfos);
		bool eventsUnlocked = await sparkyTourState.EventsUnlocked();
		Events = eventBladeInfos;
		Queues = queueBladeInfos;
		EventFilters = CreateEventFilters(Events, eventsUnlocked, evtManager?.DynamicFilterTags ?? new List<ClientDynamicFilterTag>());
		foreach (BladeEventInfo @event in Events)
		{
			Promise<bool> promise = await _viewedEventsDataProvider.HasBeenViewed(@event.EventName).AsTask;
			if (promise.Successful && !promise.Result)
			{
				_unviewedEventsExist = true;
				break;
			}
		}
	}

	private static List<EventContext> FindActiveAndEntryAvailable(List<EventContext> events, ClientPlayerInventory inventory)
	{
		return events.FindAll((EventContext x) => x.PlayerEvent.ShowInPlayblade(inventory));
	}

	private static List<BladeEventFilter> CreateEventFilters(IReadOnlyList<BladeEventInfo> events, bool eventsUnlocked, List<ClientDynamicFilterTag> dynamicFilterTags)
	{
		List<BladeEventFilter> list = new List<BladeEventFilter>
		{
			new BladeEventFilter
			{
				LocTitle = "PlayBlade/Filters/Default/All",
				FilterCriteria = "",
				FilterType = EventFilterType.All
			}
		};
		if (eventsUnlocked)
		{
			list.Add(new BladeEventFilter
			{
				LocTitle = "PlayBlade/Filters/Default/InProgress",
				FilterCriteria = "",
				FilterType = EventFilterType.InProgress
			});
			list.Add(new BladeEventFilter
			{
				LocTitle = "PlayBlade/Filters/Default/New",
				FilterCriteria = "New",
				FilterType = EventFilterType.New
			});
			list.Add(new BladeEventFilter
			{
				LocTitle = "PlayBlade/Filters/Default/Limited",
				LocSubtitle = "PlayBlade/Filters/Default/Limited_Desc",
				FilterCriteria = "",
				FilterType = EventFilterType.Limited
			});
			list.Add(new BladeEventFilter
			{
				LocTitle = "PlayBlade/Filters/Default/Constructed",
				LocSubtitle = "PlayBlade/Filters/Default/Constructed_Desc",
				FilterCriteria = "",
				FilterType = EventFilterType.Constructed
			});
			HashSet<string> tagNames = BladeFilterUtils.RemoveSingleUseBladeFilters(events);
			foreach (ClientDynamicFilterTag item2 in (from v in dynamicFilterTags
				where v != null && tagNames.Contains(v.TagId)
				orderby v.BillboardPriority descending
				select v).ToList())
			{
				BladeEventFilter item = new BladeEventFilter
				{
					LocTitle = item2.LocTitle,
					LocSubtitle = item2.LocSubtitle,
					FilterCriteria = item2.TagId,
					FilterType = EventFilterType.Tag
				};
				list.Add(item);
			}
		}
		return list;
	}

	private static string GetColorChallengeEventLock(string deckDescription, IColorChallengeStrategy colorChallengeStrategy)
	{
		string result = null;
		if (!string.IsNullOrWhiteSpace(deckDescription))
		{
			IColorChallengeTrack colorChallengeTrack = colorChallengeStrategy.Tracks.Values.FirstOrDefault((IColorChallengeTrack e) => e.DeckSummary?.Description == deckDescription);
			if (colorChallengeTrack != null && !colorChallengeTrack.Completed)
			{
				result = colorChallengeTrack.Name;
			}
		}
		return result;
	}

	private static Dictionary<PlayBladeQueueType, List<BladeQueueInfo>> HydrateMockWithRealEvents(List<BladeEventInfo> Events, List<PlayBladeQueueEntry> playBladeConfig)
	{
		Dictionary<PlayBladeQueueType, List<BladeQueueInfo>> dictionary = new Dictionary<PlayBladeQueueType, List<BladeQueueInfo>>();
		foreach (PlayBladeQueueEntry info in playBladeConfig)
		{
			BladeEventInfo bladeEventInfo = ((info.EventNameBO1 == null) ? null : Events.FirstOrDefault((BladeEventInfo x) => x.EventName == info.EventNameBO1));
			BladeEventInfo bladeEventInfo2 = ((info.EventNameBO3 == null) ? null : Events.FirstOrDefault((BladeEventInfo x) => x.EventName == info.EventNameBO3));
			if (bladeEventInfo == null && bladeEventInfo2 == null)
			{
				continue;
			}
			BladeQueueInfo bladeQueueInfo = new BladeQueueInfo
			{
				QueueId = info.Id,
				EventInfo_BO1 = bladeEventInfo,
				EventInfo_BO3 = bladeEventInfo2,
				LocTitle = info.LocTitle,
				DeckConstraintInfo_B01 = new DeckConstraintInfo
				{
					MainDeckKey = info.DeckSizeBO1,
					SideBoardKey = info.SideBoardBO1
				},
				DeckConstraintInfo_B03 = new DeckConstraintInfo
				{
					MainDeckKey = info.DeckSizeBO3,
					SideBoardKey = info.SideBoardBO3
				},
				Tags = (info.Tags ?? new List<string>()),
				PlayBladeQueueType = info.QueueType
			};
			if (bladeQueueInfo.EventInfo_BO1 != null || bladeQueueInfo.EventInfo_BO3 != null)
			{
				if (!dictionary.ContainsKey(info.QueueType))
				{
					dictionary.Add(info.QueueType, new List<BladeQueueInfo>());
				}
				dictionary[info.QueueType].Add(bladeQueueInfo);
			}
		}
		return dictionary;
	}

	private static List<RecentGamesData> DeDupeRecentGamesData(List<RecentGamesData> recentGames)
	{
		List<RecentGamesData> list = new List<RecentGamesData>();
		for (int num = recentGames.Count - 1; num >= 0; num--)
		{
			RecentGamesData recentGame = recentGames[num];
			if (recentGame.EventName == "AIBotMatch_Rebalanced")
			{
				recentGame = new RecentGamesData("AIBotMatch", recentGame.DeckId);
			}
			if (!list.Exists((RecentGamesData game) => game.EventName == recentGame.EventName))
			{
				list.Insert(0, recentGame);
			}
		}
		return list;
	}

	private List<RecentlyPlayedInfo> CreateRecentlyPlayedInfos(List<RecentGamesData> recentGamesData, List<BladeEventInfo> queuesInfos, List<BladeEventInfo> eventInfos, List<DeckViewInfo> deckInfos)
	{
		List<RecentlyPlayedInfo> list = new List<RecentlyPlayedInfo>();
		foreach (RecentGamesData rpData in recentGamesData.GetRange(0, recentGamesData.Count))
		{
			BladeEventInfo bladeEventInfo = queuesInfos.FirstOrDefault((BladeEventInfo ev) => ev.EventName == rpData.EventName);
			bool flag = bladeEventInfo != null;
			if (bladeEventInfo == null)
			{
				bladeEventInfo = eventInfos.FirstOrDefault((BladeEventInfo ev) => ev.EventName == rpData.EventName);
			}
			if (bladeEventInfo == null || (bladeEventInfo.CloseTime < ServerGameTime.GameTime && bladeEventInfo.EventName != "ColorChallenge"))
			{
				_recentlyPlayedDataProvider.RemoveRecentPlayedGame(rpData);
				continue;
			}
			DeckViewInfo deckViewInfo = GetDeckViewInfo(deckInfos, rpData, bladeEventInfo, flag);
			if ((deckViewInfo == null) & flag)
			{
				_recentlyPlayedDataProvider.RemoveRecentPlayedGame(rpData);
				continue;
			}
			RecentlyPlayedInfo item = new RecentlyPlayedInfo
			{
				EventInfo = bladeEventInfo,
				SelectedDeckInfo = deckViewInfo,
				IsQueueEvent = flag
			};
			list.Add(item);
		}
		return list;
	}

	private DeckViewInfo GetDeckViewInfo(List<DeckViewInfo> deckInfos, RecentGamesData rpData, BladeEventInfo eventInfo, bool isQueue)
	{
		DeckViewInfo deckViewInfo = deckInfos.FirstOrDefault((DeckViewInfo dk) => dk.deckId == rpData.DeckId);
		if (deckViewInfo != null)
		{
			return deckViewInfo;
		}
		if (!isQueue)
		{
			EventContext eventContext = WrapperController.Instance.EventManager.EventContexts.FirstOrDefault((EventContext evt) => eventInfo.EventName == evt.PlayerEvent.EventInfo.InternalEventName);
			if (eventContext?.PlayerEvent.CourseData.CourseDeck != null && eventContext.PlayerEvent.CourseData.CourseDeck.Id != Guid.Empty)
			{
				return _deckViewBuilder.CreateDeckViewInfoFromEvent(eventContext.PlayerEvent);
			}
		}
		return null;
	}

	private static List<EventContext> GetSortedEventContexts(List<EventContext> startingList)
	{
		List<EventContext> list = startingList.FindAll((EventContext x) => x.PlayerEvent.EventUXInfo.EventBladeBehavior == EventBladeBehavior.EventPage);
		List<EventContext> list2 = (from e in list
			where e.PlayerEvent.GetTimerState() == EventTimerState.Preview
			orderby e.PlayerEvent.EventUXInfo.DisplayPriority
			select e).ToList();
		List<EventContext> second = (from e in list
			where e.PlayerEvent.GetTimerState() == EventTimerState.Unjoined_Locked
			orderby e.PlayerEvent.EventUXInfo.DisplayPriority
			select e).ToList();
		List<EventContext> second2 = (from e in list.Where((EventContext e) =>
			{
				Guid? guid = e.PlayerEvent.CourseData?.Id;
				Guid empty = Guid.Empty;
				if (!guid.HasValue)
				{
					return true;
				}
				return guid.HasValue && guid.GetValueOrDefault() != empty;
			}).Except(list2)
			orderby e.PlayerEvent.EventUXInfo.DisplayPriority
			select e).ToList();
		List<EventContext> second3 = (from e in list.Except(list2).Except(second2).Except(second)
			orderby e.PlayerEvent.EventUXInfo.DisplayPriority
			select e).ToList();
		return list2.Concat(second2).Concat(second3).ToList();
	}

	public bool UnviewedEventsExist()
	{
		return _unviewedEventsExist;
	}
}
