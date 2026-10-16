using System;
using AssetLookupTree;
using EventPage.Components.NetworkModels;
using Wizards.MDN;
using Wizards.MDN.Services.Models.Event;
using Wizards.Mtga.Format;
using Wizards.Mtga.FrontDoorModels;
using Wotc.Mtga.Events;

namespace Wizards.Mtga.PlayBlade.Extensions;

public static class Extensions
{
	public static BladeEventInfo ConvertToBladeEventInfo(this EventContext eventContext, AssetLookupSystem alt, CombinedRankInfo combinedRankInfo)
	{
		IPlayerEvent playerEvent = eventContext.PlayerEvent;
		IEventInfo eventInfo = playerEvent.EventInfo;
		IEventUXInfo eventUXInfo = playerEvent.EventUXInfo;
		bool isLimited = FormatUtilities.IsLimited(eventInfo.FormatType);
		string rankImagePath = RankIconUtils.GetRankImagePath(alt, combinedRankInfo, isLimited);
		if (eventUXInfo.PublicEventName == "AIBotMatch")
		{
			eventUXInfo.EventComponentData.TitleRankText = new LocalizedTextData
			{
				LocKey = "Events/Event_Title_AIBotMatch"
			};
			eventUXInfo.EventComponentData.DescriptionText = new LocalizedTextData
			{
				LocKey = "Events/Event_Desc_AIBotMatch"
			};
		}
		string titleKeyForEvent = EventHelper.GetTitleKeyForEvent(eventUXInfo);
		string descriptionKeyForEvent = EventHelper.GetDescriptionKeyForEvent(eventUXInfo);
		PlayerEventModule currentModule = playerEvent.CourseData.CurrentModule;
		bool flag = currentModule == PlayerEventModule.Draft || currentModule == PlayerEventModule.DeckSelect || currentModule == PlayerEventModule.HumanDraft || currentModule == PlayerEventModule.TransitionToMatches || currentModule == PlayerEventModule.Choice || currentModule == PlayerEventModule.ClaimPrize;
		Enum.TryParse<MatchWinCondition>(playerEvent.WinCondition.ToString(), out var result);
		BladeEventInfo bladeEventInfo = new BladeEventInfo
		{
			EventName = eventInfo.EventId,
			LocShortTitle = eventUXInfo.TitleLocKey,
			BackgroundImagePath = ClientEventDefinitionList.GetBackgroundImagePath(alt, eventContext),
			BladeImagePath = ClientEventDefinitionList.GetBladeImagePath(alt, eventContext),
			RankImagePath = rankImagePath,
			LogoImagePath = ClientEventDefinitionList.GetBladeLogoPath(alt, eventContext),
			LocTitle = titleKeyForEvent,
			LocDescription = descriptionKeyForEvent,
			Format = playerEvent.Format,
			FormatName = eventUXInfo.DeckSelectFormat
		};
		string internalEventName = eventInfo.InternalEventName;
		bladeEventInfo.IsBotMatch = internalEventName == "AIBotMatch" || internalEventName == "AIBotMatch_Rebalanced";
		bladeEventInfo.IsLimited = isLimited;
		bladeEventInfo.IsRanked = eventInfo.IsRanked;
		bladeEventInfo.DeckFormat = eventUXInfo.DeckSelectFormat;
		bladeEventInfo.StartTime = eventInfo.StartTime;
		bladeEventInfo.LockTime = eventInfo.LockedTime;
		bladeEventInfo.CloseTime = eventInfo.ClosedTime;
		bladeEventInfo.TimerType = playerEvent.GetTimerState().ConverToBladeTimerType();
		bladeEventInfo.WinCondition = result;
		bladeEventInfo.IsInProgress = flag;
		bladeEventInfo.DynamicFilterTagIds = eventUXInfo.DynamicFilterTagIds;
		BladeEventInfo bladeEventInfo2 = bladeEventInfo;
		if (flag)
		{
			EventComponentData eventComponentData = eventUXInfo.EventComponentData;
			if (eventComponentData != null && eventComponentData.LossDetailsDisplay?.LossDetailsType == LossDetailsType.LossSlots)
			{
				bladeEventInfo2.TotalProgressPips = eventUXInfo.EventComponentData.LossDetailsDisplay.Games;
				bladeEventInfo2.PlayerProgress = playerEvent.CurrentLosses;
			}
		}
		return bladeEventInfo2;
	}

	private static BladeTimerType ConverToBladeTimerType(this EventTimerState state)
	{
		return state switch
		{
			EventTimerState.Preview => BladeTimerType.Preview, 
			EventTimerState.Unjoined_LockingSoon => BladeTimerType.Unjoined_LockingSoon, 
			EventTimerState.Joined_ClosingSoon => BladeTimerType.Joined_ClosingSoon, 
			EventTimerState.ClosedAndCompleted => BladeTimerType.ClosedAndCompleted, 
			_ => BladeTimerType.Hidden, 
		};
	}
}
