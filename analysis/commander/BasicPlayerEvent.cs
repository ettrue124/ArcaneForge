using System;
using System.Collections.Generic;
using System.Linq;
using EventPage.Components.NetworkModels;
using Wizards.Arena.Enums.UILayout;
using Wizards.Arena.Promises;
using Wizards.MDN;
using Wizards.MDN.Objectives;
using Wizards.MDN.Services.Models.Event;
using Wizards.Models;
using Wizards.Mtga;
using Wizards.Mtga.Decks;
using Wizards.Mtga.FrontDoorModels;
using Wizards.Mtga.Inventory;
using Wotc.Mtga.Network.ServiceWrappers;

namespace Wotc.Mtga.Events;

public class BasicPlayerEvent : IPlayerEvent
{
	private class DefaultFactory : IGenericFactory<EventInfoV3, ICourseInfoWrapper, BasicPlayerEvent>
	{
		public BasicPlayerEvent Create(EventInfoV3 eventInfo, ICourseInfoWrapper course)
		{
			return new BasicPlayerEvent(eventInfo, course, Pantry.Get<IEventsServiceWrapper>(), Pantry.Get<DeckDataProvider>(), Pantry.Get<FormatManager>());
		}
	}

	public static readonly IGenericFactory<EventInfoV3, ICourseInfoWrapper, BasicPlayerEvent> CurrentFactory = new DefaultFactory();

	protected EventInfoV3 _eventInfo;

	protected ICourseInfoWrapper _courseInfo;

	protected IEventsServiceWrapper _eventsServiceWrapper;

	protected FormatManager _formatManager;

	protected DeckDataProvider _deckDataProvider;

	public IEventInfo EventInfo { get; private set; }

	public IEventUXInfo EventUXInfo { get; private set; }

	public DeckFormat Format { get; private set; }

	public CourseData CourseData { get; private set; }

	public RankingClassType MinimumRank => (RankingClassType)_eventInfo.MinimumRank;

	public bool HasUnclaimedRewards
	{
		get
		{
			if (_courseInfo.HasUnclaimedRewards)
			{
				return _eventInfo.EventUXInfo.Prizes.Count <= 0;
			}
			return false;
		}
	}

	public virtual string DefaultTemplateName => "ConstructedEventTemplate";

	public int GamesPlayed => _courseInfo.GamesPlayed;

	public int CurrentWins => _courseInfo.CurrentWins;

	public int CurrentLosses => _courseInfo.CurrentLosses;

	public bool InPlayingMatchesModule
	{
		get
		{
			if (CourseData.CurrentModule != PlayerEventModule.TransitionToMatches && CourseData.CurrentModule != PlayerEventModule.WinLossGate)
			{
				return CourseData.CurrentModule == PlayerEventModule.WinNoGate;
			}
			return true;
		}
	}

	public List<DTO_JumpStartSelection> CurrentChoices => _courseInfo.CurrentChoices;

	public List<DTO_JumpStartSelection> PacketsChosen => _courseInfo.PacketsChosen;

	public List<DTO_JumpStartSelection> HistoricalChoices => _courseInfo.HistoricalChoices;

	public string MatchMakingName => EventInfo.InternalEventName;

	public MatchWinCondition WinCondition => _eventInfo.WinCondition;

	public List<uint> CollationIds => null;

	public CardGrantTime? CardGrantTime => null;

	public UILayoutInfo UILayoutOptions => null;

	public int AvgPodmakingSec => 0;

	public List<uint> Emblems => null;

	public int MaxLosses => 0;

	public LayoutDeckButtonBehavior DeckButtonBehavior => LayoutDeckButtonBehavior.Undefined;

	public bool ShowCopyDecksButton => false;

	public int MaxWins => 0;

	public List<uint> CardPool => null;

	public BasicPlayerEvent(EventInfoV3 eventInfo, ICourseInfoWrapper course, IEventsServiceWrapper eventsServiceWrapper, DeckDataProvider deckDataProvider, FormatManager formatManager = null)
	{
		_eventInfo = eventInfo;
		_eventsServiceWrapper = eventsServiceWrapper;
		_formatManager = formatManager;
		_deckDataProvider = deckDataProvider;
		EventInfo = new BasicEventInfo(eventInfo);
		EventUXInfo = new BasicEventUXInfo(eventInfo.EventUXInfo);
		if (_formatManager != null)
		{
			Format = _formatManager.GetAllFormats().FirstOrDefault((DeckFormat f) => f.FormatName == EventUXInfo.DeckSelectFormat);
		}
		if (course != null)
		{
			_courseInfo = course;
			CourseData = new CourseData(course);
		}
	}

	public bool HasPrize(int? wins)
	{
		if (wins.HasValue)
		{
			return _eventInfo.EventUXInfo.Prizes.ContainsKey(wins.Value);
		}
		Dictionary<int, Guid> prizes = _eventInfo.EventUXInfo.Prizes;
		if (prizes == null)
		{
			return false;
		}
		return prizes.Count > 0;
	}

	public bool ShowInPlayblade(ClientPlayerInventory inventory)
	{
		if (EventInfo.EventState == MDNEventState.NotActive)
		{
			return false;
		}
		if (EventInfo.EntryFees == null)
		{
			return true;
		}
		if (CourseData != null && CourseData.CurrentModule != PlayerEventModule.Join && CourseData.CurrentModule != PlayerEventModule.Pay && CourseData.CurrentModule != PlayerEventModule.PayEntry)
		{
			return true;
		}
		foreach (EventEntryFeeInfo entryFee in EventInfo.EntryFees)
		{
			if (!entryFee.UsesRemaining)
			{
				continue;
			}
			switch (entryFee.CurrencyType)
			{
			case EventEntryCurrencyType.SealedToken:
				if (inventory.sealedTokens >= entryFee.Quantity)
				{
					return true;
				}
				break;
			case EventEntryCurrencyType.DraftToken:
				if (inventory.draftTokens >= entryFee.Quantity)
				{
					return true;
				}
				break;
			case EventEntryCurrencyType.EventToken:
			{
				if (inventory.CustomTokens != null && !string.IsNullOrWhiteSpace(entryFee.ReferenceId) && inventory.CustomTokens.TryGetValue(entryFee.ReferenceId, out var value) && value >= entryFee.Quantity)
				{
					return true;
				}
				break;
			}
			default:
				return true;
			}
		}
		return false;
	}

	protected void _updatePlayerCourse(ICourseInfoWrapper course)
	{
		_courseInfo = course;
		if (CourseData == null)
		{
			CourseData = new CourseData(_courseInfo);
		}
		else
		{
			CourseData.Update(_courseInfo);
		}
	}

	public Promise<ICourseInfoWrapper> SetChoice(string choice)
	{
		return _eventsServiceWrapper.SetChoice(EventInfo.InternalEventName, choice).Then((Promise<ICourseInfoWrapper> p) =>
		{
			ICourseInfoWrapper result = p.Result;
			if (result != null)
			{
				_updatePlayerCourse(result);
			}
		});
	}

	public Promise<ICourseInfoWrapper> SubmitEventChoice(string choice)
	{
		return _eventsServiceWrapper.SubmitEventChoice(EventInfo.InternalEventName, choice).Then((Promise<ICourseInfoWrapper> p) =>
		{
			ICourseInfoWrapper result = p.Result;
			if (result != null)
			{
				_updatePlayerCourse(result);
			}
		});
	}

	public Promise<ICourseInfoWrapper> JoinAndPay(EventEntryCurrencyType currency, string eventChoice = null, bool debugIgnoreEntryLimits = false)
	{
		EventEntryFeeInfo eventEntryFeeInfo = EventInfo.EntryFees.FirstOrDefault((EventEntryFeeInfo x) => x.CurrencyType == currency);
		int currencyAmount = eventEntryFeeInfo?.Quantity ?? 0;
		string tokenId = eventEntryFeeInfo?.ReferenceId;
		return _eventsServiceWrapper.JoinAndPay(EventInfo.InternalEventName, currency, currencyAmount, tokenId, eventChoice, debugIgnoreEntryLimits).Then((Promise<ICourseInfoWrapper> p) =>
		{
			ICourseInfoWrapper result = p.Result;
			if (result != null)
			{
				_updatePlayerCourse(result);
			}
		});
	}

	protected virtual void UpdateSideboardForFormat(Client_Deck deck)
	{
		DeckUtilities.UpdateSideboardForFormat(deck, _formatManager.GetSafeFormat(EventUXInfo.DeckSelectFormat));
	}

	public Promise<Client_Deck> DeckFormattedForEventSubmission(Client_Deck deck)
	{
		if (_deckDataProvider != null && _deckDataProvider.HasDeck(deck.Id))
		{
			return _deckDataProvider.GetFullDeck(deck.Id).Convert((Client_Deck fullDeck) =>
			{
				fullDeck = new Client_Deck(fullDeck);
				UpdateSideboardForFormat(fullDeck);
				return fullDeck;
			});
		}
		Client_Deck client_Deck = new Client_Deck(deck);
		UpdateSideboardForFormat(client_Deck);
		return new SimplePromise<Client_Deck>(client_Deck);
	}

	public Promise<Client_Deck> SubmitEventDeck(Client_Deck deck)
	{
		return DeckFormattedForEventSubmission(deck).Then((Promise<Client_Deck> promise) => promise.Successful ? _eventsServiceWrapper.SubmitEventDeck(EventInfo.InternalEventName, promise.Result).Then((Promise<ICourseInfoWrapper> p) =>
		{
			ICourseInfoWrapper result = p.Result;
			Error error = p.Error;
			if (!p.Successful && error.Code == 7000)
			{
				_deckDataProvider?.MarkDirty();
				_deckDataProvider?.GetAllDecks();
			}
			if (result != null)
			{
				_updatePlayerCourse(result);
			}
			return (!p.Successful) ? new SimplePromise<Client_Deck>(p.Error) : new SimplePromise<Client_Deck>(deck);
		}) : new SimplePromise<Client_Deck>(promise.Error));
	}

	public Promise<ICourseInfoWrapper> SubmitEventDeckFromChoices(Client_Deck deck)
	{
		return DeckFormattedForEventSubmission(deck).Then((Promise<Client_Deck> promise) => promise.Successful ? _eventsServiceWrapper.SubmitEventDeckFromChoices(EventInfo.InternalEventName, promise.Result.Id.ToString()).Then((Promise<ICourseInfoWrapper> p) =>
		{
			ICourseInfoWrapper result = p.Result;
			Error error = p.Error;
			if (!p.Successful && error.Code == 7000)
			{
				_deckDataProvider?.MarkDirty();
				_deckDataProvider?.GetAllDecks();
			}
			if (result != null)
			{
				_updatePlayerCourse(result);
			}
		}) : new SimplePromise<ICourseInfoWrapper>(promise.Error));
	}

	public Promise<ICourseInfoWrapper> ResignFromEvent()
	{
		return _eventsServiceWrapper.ResignFromEvent(EventInfo.InternalEventName).Then((Promise<ICourseInfoWrapper> p) =>
		{
			if (p.Result != null)
			{
				_updatePlayerCourse(p.Result);
				onResign();
			}
		});
	}

	protected virtual void onResign()
	{
	}

	protected virtual void onDrop()
	{
	}

	public Promise<ICourseInfoWrapper> DropFromEvent()
	{
		return _eventsServiceWrapper.DropFromEvent(EventInfo.InternalEventName).IfSuccess((Promise<ICourseInfoWrapper> p) =>
		{
			ICourseInfoWrapper result = p.Result;
			onDrop();
			if (result != null)
			{
				_updatePlayerCourse(result);
			}
		});
	}

	public Promise<ICourseInfoWrapper> GetEventCourse()
	{
		return _eventsServiceWrapper.GetEventCourse(EventInfo.InternalEventName).IfSuccess((Promise<ICourseInfoWrapper> p) =>
		{
			_updatePlayerCourse(p.Result);
			return (p.Result.CurrentEventModule != PlayerEventModule.Complete) ? p : DropFromEvent();
		});
	}

	public Promise<ICourseInfoWrapper> ClaimNoGatePrize()
	{
		return _eventsServiceWrapper.ClaimNoGatePrize(EventInfo.InternalEventName).IfSuccess((Promise<ICourseInfoWrapper> p) =>
		{
			ICourseInfoWrapper result = p.Result;
			if (result != null)
			{
				_updatePlayerCourse(result);
			}
		});
	}

	public Promise<Unit> JoinNewMatchQueue()
	{
		return _eventsServiceWrapper.JoinNewMatchQueue(EventInfo.InternalEventName);
	}

	public Promise<ICourseInfoWrapper> ClaimPrize()
	{
		return _eventsServiceWrapper.ClaimPrize(EventInfo.InternalEventName).Then((Promise<ICourseInfoWrapper> p) =>
		{
			if (p.Successful)
			{
				_updatePlayerCourse(p.Result);
			}
			if (_courseInfo.CurrentEventModule == PlayerEventModule.Join)
			{
				onDrop();
			}
		});
	}

	public void UpgradeDeck(UpgradePacket upgradePacket)
	{
	}

	public string GetLocalizedText(EventTextType textType)
	{
		return "";
	}

	public List<Guid> GetEventDeckIds(bool validate, out List<Guid> invalidDecks)
	{
		invalidDecks = null;
		return null;
	}

	public bool TracksGameCount(out int gamesLeft)
	{
		gamesLeft = 0;
		return false;
	}

	public List<RewardDisplayData> GetRewardDisplayData()
	{
		return null;
	}
}
