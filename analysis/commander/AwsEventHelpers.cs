using System;
using System.Collections.Generic;
using EventPage.Components.NetworkModels;
using Wizards.Arena.Enums.Store;
using Wizards.Arena.Enums.UILayout;
using Wizards.MDN;
using Wizards.Mtga.FrontDoorModels;
using Wizards.Unification.Models.Event;
using Wizards.Unification.Models.Events;
using Wotc.Mtga.Cards.Database;

namespace Core.Shared.Code.Network.Utils;

public static class AwsEventHelpers
{
	public static PurchaseCurrency ToPurchaseCurrency(this EventEntryCurrencyType eventCurrency)
	{
		switch (eventCurrency)
		{
		case EventEntryCurrencyType.Gold:
			return PurchaseCurrency.Gold;
		case EventEntryCurrencyType.Gem:
			return PurchaseCurrency.Gem;
		case EventEntryCurrencyType.SealedToken:
			return PurchaseCurrency.SealedToken;
		case EventEntryCurrencyType.DraftToken:
			return PurchaseCurrency.DraftToken;
		case EventEntryCurrencyType.Free:
		case EventEntryCurrencyType.None:
			return PurchaseCurrency.None;
		case EventEntryCurrencyType.EventToken:
			return PurchaseCurrency.CustomToken;
		default:
			throw new ArgumentOutOfRangeException("eventCurrency", eventCurrency, null);
		}
	}

	public static EventEntryCurrencyType ToEventEntryCurrency(this PurchaseCurrency eventCurrency)
	{
		return eventCurrency switch
		{
			PurchaseCurrency.None => EventEntryCurrencyType.Free, 
			PurchaseCurrency.Gold => EventEntryCurrencyType.Gold, 
			PurchaseCurrency.Gem => EventEntryCurrencyType.Gem, 
			PurchaseCurrency.SealedToken => EventEntryCurrencyType.SealedToken, 
			PurchaseCurrency.DraftToken => EventEntryCurrencyType.DraftToken, 
			PurchaseCurrency.CustomToken => EventEntryCurrencyType.EventToken, 
			_ => throw new ArgumentOutOfRangeException($"Can't convert PurchaseCurrency {eventCurrency} to EventEntryCurrencyType!"), 
		};
	}

	public static UILayoutInfo ToUILayoutInfo(this UILayout layout)
	{
		return new UILayoutInfo
		{
			DeckButtonBehavior = layout.DeckButtonBehavior,
			EventBladeBehavior = (Wizards.MDN.LayoutEventBladeBehavior)layout.EventBladeBehavior,
			ResignBehavior = (Wizards.MDN.LayoutResignBehavior)layout.ResignBehavior,
			TemplateName = layout.ResignBehavior.ToString(),
			WinTrackBehavior = (Wizards.MDN.LayoutWinTrackBehavior)layout.WinTrackBehavior
		};
	}

	private static EventInfoV2 ConvertToEventInfoV2(ActiveAiBotMatch activeEvent)
	{
		EventInfoV2 eventInfoV = new EventInfoV2();
		eventInfoV.PublicEventName = activeEvent.PublicEventName;
		eventInfoV.InternalEventName = activeEvent.InternalEventName;
		eventInfoV.EventState = MDNEventState.Active;
		eventInfoV.FormatType = MDNEFormatType.Constructed;
		eventInfoV.ModuleGlobalData = new ModuleInfo();
		eventInfoV.ModuleGlobalData.MinimumRank = -1;
		eventInfoV.ModuleGlobalData.EntryFees = new List<Wizards.MDN.EventEntryFee>();
		eventInfoV.ModuleGlobalData.EntryFees.Add(new Wizards.MDN.EventEntryFee());
		eventInfoV.ModuleGlobalData.DeckSelect = activeEvent.Format;
		eventInfoV.ModuleGlobalData.RankUpdateType = null;
		eventInfoV.ModuleGlobalData.CollationIds = new List<uint>();
		eventInfoV.ModuleGlobalData.CardPool = new List<uint>();
		eventInfoV.ModuleGlobalData.Prizes = new List<Guid>();
		eventInfoV.ModuleGlobalData.Items = new List<Guid>();
		eventInfoV.StartTime = DateTime.MinValue;
		eventInfoV.LockedTime = DateTime.MaxValue;
		eventInfoV.ClosedTime = DateTime.MaxValue;
		eventInfoV.PastEntries = new Dictionary<EventEntryCurrencyType, int>();
		eventInfoV.DisplayPriority = activeEvent.DisplayPriority;
		eventInfoV.IsArenaPlayModeEvent = true;
		eventInfoV.Emblems = new List<uint>();
		eventInfoV.UILayoutOptions = new UILayoutInfo();
		eventInfoV.UILayoutOptions.TemplateName = activeEvent.InternalEventName;
		eventInfoV.UILayoutOptions.ResignBehavior = Wizards.MDN.LayoutResignBehavior.Undefined;
		eventInfoV.UILayoutOptions.WinTrackBehavior = Wizards.MDN.LayoutWinTrackBehavior.Undefined;
		eventInfoV.UILayoutOptions.EventBladeBehavior = Wizards.MDN.LayoutEventBladeBehavior.Queue;
		eventInfoV.UILayoutOptions.DeckButtonBehavior = LayoutDeckButtonBehavior.Fixed;
		eventInfoV.SkipValidation = false;
		eventInfoV.DoesUpdateQuests = true;
		eventInfoV.DoesUpdateDailyWeeklyRewards = false;
		eventInfoV.AllowUncollectedCards = false;
		eventInfoV.Parameters = new Dictionary<string, string>();
		return eventInfoV;
	}

	private static EventInfoV2 GetChallengeEvent()
	{
		EventInfoV2 eventInfoV = new EventInfoV2();
		eventInfoV.InternalEventName = "DirectGame";
		eventInfoV.EventState = MDNEventState.Active;
		eventInfoV.FormatType = MDNEFormatType.Constructed;
		eventInfoV.ModuleGlobalData = new ModuleInfo();
		eventInfoV.ModuleGlobalData.MinimumRank = -1;
		eventInfoV.ModuleGlobalData.EntryFees = new List<Wizards.MDN.EventEntryFee>();
		eventInfoV.ModuleGlobalData.EntryFees.Add(new Wizards.MDN.EventEntryFee());
		eventInfoV.ModuleGlobalData.DeckSelect = "";
		eventInfoV.ModuleGlobalData.RankUpdateType = null;
		eventInfoV.ModuleGlobalData.CollationIds = new List<uint>();
		eventInfoV.ModuleGlobalData.CardPool = new List<uint>();
		eventInfoV.ModuleGlobalData.Prizes = new List<Guid>();
		eventInfoV.ModuleGlobalData.Items = new List<Guid>();
		eventInfoV.StartTime = DateTime.MinValue;
		eventInfoV.LockedTime = DateTime.MaxValue;
		eventInfoV.ClosedTime = DateTime.MaxValue;
		eventInfoV.PastEntries = new Dictionary<EventEntryCurrencyType, int>();
		eventInfoV.DisplayPriority = 99999;
		eventInfoV.IsArenaPlayModeEvent = false;
		eventInfoV.Emblems = new List<uint>();
		eventInfoV.UILayoutOptions = new UILayoutInfo();
		eventInfoV.UILayoutOptions.TemplateName = eventInfoV.InternalEventName;
		eventInfoV.UILayoutOptions.ResignBehavior = Wizards.MDN.LayoutResignBehavior.Undefined;
		eventInfoV.UILayoutOptions.WinTrackBehavior = Wizards.MDN.LayoutWinTrackBehavior.Undefined;
		eventInfoV.UILayoutOptions.EventBladeBehavior = Wizards.MDN.LayoutEventBladeBehavior.Hidden;
		eventInfoV.UILayoutOptions.DeckButtonBehavior = LayoutDeckButtonBehavior.Editable;
		eventInfoV.SkipValidation = false;
		eventInfoV.DoesUpdateQuests = false;
		eventInfoV.DoesUpdateDailyWeeklyRewards = false;
		eventInfoV.AllowUncollectedCards = false;
		eventInfoV.Parameters = new Dictionary<string, string>();
		return eventInfoV;
	}

	public static List<EventPage.Components.NetworkModels.EventInfoV3> ConvertToEventsList(ICardDataProvider cardDatabase, ActiveEventsResponseV2 response)
	{
		List<EventPage.Components.NetworkModels.EventInfoV3> list = new List<EventPage.Components.NetworkModels.EventInfoV3>();
		if (response?.Events != null)
		{
			foreach (Wizards.Unification.Models.Events.EventInfoV3 @event in response.Events)
			{
				list.Add(@event.ConvertToClientEventModelV3(cardDatabase));
			}
		}
		if (response?.Challenges != null)
		{
			EventInfoV2 challengeEvent = GetChallengeEvent();
			list.Add(challengeEvent.ConvertToV3());
		}
		if (response?.AiBotMatches != null)
		{
			foreach (ActiveAiBotMatch aiBotMatch in response.AiBotMatches)
			{
				EventInfoV2 oldEventInfo = ConvertToEventInfoV2(aiBotMatch);
				list.Add(oldEventInfo.ConvertToV3());
			}
		}
		return list;
	}
}
