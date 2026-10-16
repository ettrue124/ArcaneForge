using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventPage.Components.NetworkModels;
using Wizards.Arena.Client.Logging;
using Wizards.Arena.Promises;
using Wizards.Mtga;
using Wizards.Mtga.Format;
using Wizards.Unification.Models.Events;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Events;
using Wotc.Mtga.Network.ServiceWrappers;

namespace Wizards.MDN;

public class EventManager : IEventManager
{
	private readonly IEventsServiceWrapper _eventsServiceWrapper;

	private readonly IAccountClient _accountClient;

	private IColorChallengeStrategy _colorChallengeStrategy;

	private CardDatabase _cardDatabase;

	private DateTime _lastEventContextRefresh;

	private ILogger _logger = new UnityCrossThreadLogger("EventManager");

	public List<EventContext> EventContexts { get; } = new List<EventContext>();

	public List<ClientDynamicFilterTag> DynamicFilterTags { get; private set; } = new List<ClientDynamicFilterTag>();

	public Dictionary<string, EventContext> EventsByInternalName { get; } = new Dictionary<string, EventContext>();

	public EventContext PrivateGameEventContext => EventContexts.FirstOrDefault((EventContext e) => e.PlayerEvent is PrivateGamePlayerEvent);

	public bool RefreshingEventContexts { get; private set; }

	public EventContext ColorMasteryEventContext => EventContexts.FirstOrDefault((EventContext _) => _.PlayerEvent is ColorChallengePlayerEvent);

	public ColorChallengePlayerEvent ColorMasteryEvent => ColorMasteryEventContext?.PlayerEvent as ColorChallengePlayerEvent;

	public EventManager(IEventsServiceWrapper serviceWrapper, IAccountClient accountClient)
	{
		_eventsServiceWrapper = serviceWrapper;
		_accountClient = accountClient;
	}

	public void Inject(CardDatabase cardDatabase)
	{
		_cardDatabase = cardDatabase;
	}

	private static IPlayerEvent GetPlayerEvent(EventPage.Components.NetworkModels.EventInfoV3 eventInfo, ICourseInfoWrapper courseInfo)
	{
		IGenericFactory<EventPage.Components.NetworkModels.EventInfoV3, ICourseInfoWrapper, IPlayerEvent> genericFactory;
		if (eventInfo.InternalEventName.Contains("DirectGame"))
		{
			genericFactory = PrivateGamePlayerEvent.CurrentFactory;
		}
		else if (eventInfo.InternalEventName.Contains("NPE"))
		{
			genericFactory = NPEPlayerEvent.CurrentFactory;
		}
		else if (eventInfo.InternalEventName == "ColorChallenge")
		{
			genericFactory = ColorChallengePlayerEvent.CurrentFactory;
		}
		else
		{
			genericFactory = ((!FormatUtilities.IsLimited(eventInfo.FormatType)) ? BasicPlayerEvent.CurrentFactory : LimitedPlayerEvent.CurrentFactory);
		}
		return genericFactory.Create(eventInfo, courseInfo);
	}

	public EventContext GetEventContext(string internalEventName)
	{
		EventContext value = null;
		if (internalEventName == "ColorChallenge")
		{
			value = ColorMasteryEventContext;
		}
		else
		{
			EventsByInternalName.TryGetValue(internalEventName, out value);
		}
		return value;
	}

	public IEnumerator Coroutine_GetEventsAndCourses()
	{
		return GetEventsAndCourses().AsCoroutine();
	}

	public async Task GetEventsAndCourses()
	{
		if (RefreshingEventContexts || (EventContexts.Count != 0 && _lastEventContextRefresh > DateTime.Now - TimeSpan.FromSeconds(10.0)))
		{
			return;
		}
		RefreshingEventContexts = true;
		Dictionary<string, EventContext> previousEventContexts = EventsByInternalName.ToDictionary((KeyValuePair<string, EventContext> kvp) => kvp.Key, (KeyValuePair<string, EventContext> kvp) => kvp.Value);
		EventContexts.Clear();
		EventsByInternalName.Clear();
		bool toggleValueById = Pantry.Get<IClientFeatureToggleDataProvider>().GetToggleValueById("ShowAllActiveEvents");
		if (_eventsServiceWrapper != null)
		{
			Promise<List<EventPage.Components.NetworkModels.EventInfoV3>> eventsPromise = _eventsServiceWrapper.GetAllEvents(_cardDatabase.CardDataProvider, toggleValueById);
			Promise<List<ICourseInfoWrapper>> coursesPromise = _eventsServiceWrapper.GetEventCourses();
			await Task.WhenAll(eventsPromise.AsTask, coursesPromise.AsTask);
			if (eventsPromise.Successful)
			{
				List<EventPage.Components.NetworkModels.EventInfoV3> result = eventsPromise.Result;
				DynamicFilterTags = _eventsServiceWrapper.DynamicFilterTags;
				List<EventPage.Components.NetworkModels.EventInfoV3> list = new List<EventPage.Components.NetworkModels.EventInfoV3>();
				foreach (EventPage.Components.NetworkModels.EventInfoV3 item in result)
				{
					bool flag = item.AllowedCountryCodes != null && item.AllowedCountryCodes.Any();
					bool flag2 = item.ExcludedCountryCodes != null && item.ExcludedCountryCodes.Any();
					if (!flag && !flag2)
					{
						list.Add(item);
					}
					else if (flag && item.AllowedCountryCodes.Contains(_accountClient.AccountInformation.CountryCode))
					{
						list.Add(item);
					}
					else if ((!flag & flag2) && !item.ExcludedCountryCodes.Contains(_accountClient.AccountInformation.CountryCode))
					{
						list.Add(item);
					}
				}
				result = list;
				if (coursesPromise.Successful)
				{
					List<ICourseInfoWrapper> result2 = coursesPromise.Result;
					result2 = _eventsServiceWrapper.CreateCoursesForJoinState(result2, result);
					foreach (EventPage.Components.NetworkModels.EventInfoV3 evt in result)
					{
						ICourseInfoWrapper courseInfoWrapper = result2.FirstOrDefault((ICourseInfoWrapper c) => c.InternalEventName == evt.InternalEventName);
						if (courseInfoWrapper != null || !(evt.InternalEventName != "DirectGame"))
						{
							PostMatchContext postMatchContext = (previousEventContexts.TryGetValue(evt.InternalEventName, out var value) ? value.PostMatchContext : null);
							EventContext eventContext = new EventContext
							{
								PlayerEvent = GetPlayerEvent(evt, courseInfoWrapper),
								PostMatchContext = postMatchContext
							};
							EventContexts.Add(eventContext);
							EventsByInternalName.Add(evt.InternalEventName, eventContext);
						}
					}
					_lastEventContextRefresh = DateTime.Now;
				}
			}
			else
			{
				_logger.Error("Client.EventManager.Coroutine_GetEventsAndCourses.PromiseUnsuccessful");
			}
		}
		RefreshingEventContexts = false;
	}
}
