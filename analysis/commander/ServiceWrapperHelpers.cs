using System;
using System.Collections.Generic;
using System.Linq;
using Core.Shared.Code.ClientModels;
using EventPage.Components.NetworkModels;
using Wizards.Arena.Enums.Event;
using Wizards.Arena.Enums.Format;
using Wizards.Arena.Enums.Player;
using Wizards.Arena.Enums.Rank;
using Wizards.Arena.Enums.Store;
using Wizards.Arena.Enums.UILayout;
using Wizards.MDN;
using Wizards.MDN.Client.Models.Event;
using Wizards.MDN.Objectives;
using Wizards.MDN.Services.Models.Event;
using Wizards.Models;
using Wizards.Mtga.Decks;
using Wizards.Mtga.FrontDoorModels;
using Wizards.Unification.Models.Event;
using Wizards.Unification.Models.Events;
using Wizards.Unification.Models.Events.Components;
using Wizards.Unification.Models.Player;
using Wizards.Unification.Models.Quest;
using Wizards.Unification.Models.Rank;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Network.ServiceWrappers;

public static class ServiceWrapperHelpers
{
	public static EventPage.Components.NetworkModels.EventInfoV3 ConvertToClientEventModelV3(this Wizards.Unification.Models.Events.EventInfoV3 serverEventInfo, ICardDataProvider cardDatabase)
	{
		EventPage.Components.NetworkModels.EventInfoV3 eventInfoV = new EventPage.Components.NetworkModels.EventInfoV3();
		eventInfoV.InternalEventName = serverEventInfo.InternalEventName;
		eventInfoV.EventState = ToMDNEventState(serverEventInfo.EventState);
		eventInfoV.FormatType = ToMDNFormatType(serverEventInfo.FormatType);
		eventInfoV.StartTime = serverEventInfo.StartTime;
		eventInfoV.LockedTime = serverEventInfo.LockedTime;
		eventInfoV.ClosedTime = serverEventInfo.ClosedTime;
		eventInfoV.Flags = new List<EventPage.Components.NetworkModels.EventInfoFlag>();
		eventInfoV.EventTags = serverEventInfo.EventTags;
		eventInfoV.WinCondition = (MatchWinCondition)Enum.Parse(typeof(MatchWinCondition), serverEventInfo.WinCondition.ToString());
		eventInfoV.MinimumRank = serverEventInfo.MinimumRank;
		foreach (Wizards.Unification.Models.Events.EventInfoFlag flag in serverEventInfo.Flags)
		{
			eventInfoV.Flags.Add(ToEventInfoFlag(flag));
		}
		if (serverEventInfo.PastEntries != null)
		{
			eventInfoV.PastEntries = new Dictionary<EventEntryCurrencyType, int>();
			foreach (KeyValuePair<Wizards.Arena.Enums.Store.PurchaseCurrency, TokenTypeAndQuantity> pastEntry in serverEventInfo.PastEntries)
			{
				eventInfoV.PastEntries.Add(ToEventEntryCurrencyType(pastEntry.Key), pastEntry.Value.Quantity);
			}
		}
		List<Wizards.Unification.Models.Event.EventEntryFee> entryFees = serverEventInfo.EntryFees;
		if (entryFees != null && entryFees.Count > 0)
		{
			eventInfoV.EntryFees = new List<Wizards.MDN.EventEntryFee>();
			foreach (Wizards.Unification.Models.Event.EventEntryFee entryFee in serverEventInfo.EntryFees)
			{
				eventInfoV.EntryFees.Add(ToEventEntryFee(entryFee));
			}
		}
		eventInfoV.EventUXInfo = ToEventUXInfo(serverEventInfo.EventUXInfo, cardDatabase);
		List<string> allowedCountryCodes = serverEventInfo.AllowedCountryCodes;
		if (allowedCountryCodes != null && allowedCountryCodes.Count > 0)
		{
			eventInfoV.AllowedCountryCodes = new List<string>(serverEventInfo.AllowedCountryCodes);
		}
		List<string> excludedCountryCodes = serverEventInfo.ExcludedCountryCodes;
		if (excludedCountryCodes != null && excludedCountryCodes.Count > 0)
		{
			eventInfoV.ExcludedCountryCodes = new List<string>(serverEventInfo.ExcludedCountryCodes);
		}
		return eventInfoV;
	}

	public static MDNEventState ToMDNEventState(ActiveState input)
	{
		return input switch
		{
			ActiveState.Active => MDNEventState.Active, 
			ActiveState.ForceActive => MDNEventState.ForceActive, 
			ActiveState.NotActive => MDNEventState.NotActive, 
			_ => MDNEventState.NotActive, 
		};
	}

	public static MDNEFormatType ToMDNFormatType(Wizards.MDN.Client.Models.Event.EFormatType input)
	{
		return input switch
		{
			Wizards.MDN.Client.Models.Event.EFormatType.Constructed => MDNEFormatType.Constructed, 
			Wizards.MDN.Client.Models.Event.EFormatType.Draft => MDNEFormatType.Draft, 
			Wizards.MDN.Client.Models.Event.EFormatType.Sealed => MDNEFormatType.Sealed, 
			Wizards.MDN.Client.Models.Event.EFormatType.None => MDNEFormatType.Invalid, 
			_ => MDNEFormatType.Invalid, 
		};
	}

	public static MDNEFormatType ToMDNFormatType(Wizards.Arena.Enums.Format.EFormatType input)
	{
		return input switch
		{
			Wizards.Arena.Enums.Format.EFormatType.Constructed => MDNEFormatType.Constructed, 
			Wizards.Arena.Enums.Format.EFormatType.Draft => MDNEFormatType.Draft, 
			Wizards.Arena.Enums.Format.EFormatType.Sealed => MDNEFormatType.Sealed, 
			Wizards.Arena.Enums.Format.EFormatType.None => MDNEFormatType.Invalid, 
			_ => MDNEFormatType.Invalid, 
		};
	}

	public static EventPage.Components.NetworkModels.EventInfoFlag ToEventInfoFlag(Wizards.Unification.Models.Events.EventInfoFlag input)
	{
		return input switch
		{
			Wizards.Unification.Models.Events.EventInfoFlag.AllowUncollectedCards => EventPage.Components.NetworkModels.EventInfoFlag.AllowUncollectedCards, 
			Wizards.Unification.Models.Events.EventInfoFlag.IsArenaPlayModeEvent => EventPage.Components.NetworkModels.EventInfoFlag.IsArenaPlayModeEvent, 
			Wizards.Unification.Models.Events.EventInfoFlag.IsPreconEvent => EventPage.Components.NetworkModels.EventInfoFlag.IsPreconEvent, 
			Wizards.Unification.Models.Events.EventInfoFlag.Ranked => EventPage.Components.NetworkModels.EventInfoFlag.Ranked, 
			Wizards.Unification.Models.Events.EventInfoFlag.SkipDeckValidation => EventPage.Components.NetworkModels.EventInfoFlag.SkipDeckValidation, 
			Wizards.Unification.Models.Events.EventInfoFlag.UpdateDailyWeeklyRewards => EventPage.Components.NetworkModels.EventInfoFlag.UpdateDailyWeeklyRewards, 
			Wizards.Unification.Models.Events.EventInfoFlag.UpdateQuests => EventPage.Components.NetworkModels.EventInfoFlag.UpdateQuests, 
			Wizards.Unification.Models.Events.EventInfoFlag.IsAiBotMatch => EventPage.Components.NetworkModels.EventInfoFlag.IsAiBotMatch, 
			_ => EventPage.Components.NetworkModels.EventInfoFlag.Invalid, 
		};
	}

	public static EventEntryCurrencyType ToEventEntryCurrencyType(Wizards.Arena.Enums.Store.PurchaseCurrency input)
	{
		return input switch
		{
			Wizards.Arena.Enums.Store.PurchaseCurrency.DraftToken => EventEntryCurrencyType.DraftToken, 
			Wizards.Arena.Enums.Store.PurchaseCurrency.CustomToken => EventEntryCurrencyType.EventToken, 
			Wizards.Arena.Enums.Store.PurchaseCurrency.None => EventEntryCurrencyType.Free, 
			Wizards.Arena.Enums.Store.PurchaseCurrency.Gem => EventEntryCurrencyType.Gem, 
			Wizards.Arena.Enums.Store.PurchaseCurrency.Gold => EventEntryCurrencyType.Gold, 
			Wizards.Arena.Enums.Store.PurchaseCurrency.SealedToken => EventEntryCurrencyType.SealedToken, 
			_ => EventEntryCurrencyType.None, 
		};
	}

	public static EventPage.Components.NetworkModels.LossDetailsType ToLossDetailsType(Wizards.Unification.Models.Events.Components.LossDetailsType input)
	{
		return input switch
		{
			Wizards.Unification.Models.Events.Components.LossDetailsType.LossSlots => EventPage.Components.NetworkModels.LossDetailsType.LossSlots, 
			Wizards.Unification.Models.Events.Components.LossDetailsType.PlayMaxGames => EventPage.Components.NetworkModels.LossDetailsType.PlayMaxGames, 
			Wizards.Unification.Models.Events.Components.LossDetailsType.PlayUntilEventEnds => EventPage.Components.NetworkModels.LossDetailsType.PlayUntilEventEnds, 
			_ => EventPage.Components.NetworkModels.LossDetailsType.LossSlots, 
		};
	}

	public static Wizards.MDN.CardGrantTime ToCardGrantTime(Wizards.Unification.Models.Events.CardGrantTime input)
	{
		return input switch
		{
			Wizards.Unification.Models.Events.CardGrantTime.PostClaimPrize => Wizards.MDN.CardGrantTime.PostClaimPrize, 
			Wizards.Unification.Models.Events.CardGrantTime.PreMatch => Wizards.MDN.CardGrantTime.PreMatch, 
			_ => Wizards.MDN.CardGrantTime.PreMatch, 
		};
	}

	public static Wizards.MDN.EventEntryFee ToEventEntryFee(Wizards.Unification.Models.Event.EventEntryFee input)
	{
		return new Wizards.MDN.EventEntryFee
		{
			CurrencyType = ToEventEntryCurrencyType(input.CurrencyType),
			Quantity = input.Quantity,
			MaxUses = input.MaxUses,
			ReferenceId = input.ReferenceId
		};
	}

	public static Wizards.Mtga.FrontDoorModels.CardInDeck ToCardInDeck(Wizards.Unification.Models.Events.CardInDeck input)
	{
		return new Wizards.Mtga.FrontDoorModels.CardInDeck(input.Id, input.Quantity);
	}

	public static Wizards.Mtga.FrontDoorModels.DeckInfo ToDeckInfoDescription(Wizards.Unification.Models.Events.DeckInfo input, ICardDataProvider cardDatabase)
	{
		Wizards.Mtga.FrontDoorModels.DeckInfo deckInfo = new Wizards.Mtga.FrontDoorModels.DeckInfo();
		deckInfo.id = input.id;
		deckInfo.name = input.name;
		deckInfo.format = input.format;
		deckInfo.description = input.description;
		deckInfo.deckTileId = input.deckTileId;
		deckInfo.deckArtId = input.deckArtId;
		deckInfo.lastUpdated = input.lastUpdated;
		deckInfo.mainDeck = new List<Wizards.Mtga.FrontDoorModels.CardInDeck>();
		foreach (Wizards.Unification.Models.Events.CardInDeck item in input.mainDeck)
		{
			deckInfo.mainDeck.Add(ToCardInDeck(item));
		}
		deckInfo.sideboard = new List<Wizards.Mtga.FrontDoorModels.CardInDeck>();
		foreach (Wizards.Unification.Models.Events.CardInDeck item2 in input.sideboard)
		{
			deckInfo.sideboard.Add(ToCardInDeck(item2));
		}
		deckInfo.cardBack = input.cardBack;
		deckInfo.commandZone = new List<Wizards.Mtga.FrontDoorModels.CardInDeck>();
		foreach (Wizards.Unification.Models.Events.CardInDeck item3 in input.commandZone)
		{
			deckInfo.commandZone.Add(ToCardInDeck(item3));
		}
		deckInfo.attributes = input.attributes;
		deckInfo.companion = ToCardInDeck(input.companion);
		deckInfo.isCompanionValid = input.isCompanionValid;
		deckInfo.isLoaded = input.isLoaded;
		if (deckInfo.attributes.TryGetValue(DeckAttributeKeys.LAST_PLAYED_KEY, out var value) && DateTime.TryParse(value, out var result))
		{
			deckInfo.LastPlayed = result;
		}
		if (deckInfo.attributes.TryGetValue(DeckAttributeKeys.FAVORITE_KEY, out var value2) && bool.TryParse(value2, out var result2))
		{
			deckInfo.IsFavorite = result2;
		}
		deckInfo.ManaColors = DeckUtilities.GetDeckColors(deckInfo, cardDatabase);
		return deckInfo;
	}

	public static EventPage.Components.NetworkModels.EventUXInfo ToEventUXInfo(Wizards.Unification.Models.Events.EventUXInfo input, ICardDataProvider cardDatabase)
	{
		EventPage.Components.NetworkModels.EventUXInfo eventUXInfo = new EventPage.Components.NetworkModels.EventUXInfo();
		eventUXInfo.PublicEventName = input.PublicEventName;
		eventUXInfo.DisplayPriority = input.DisplayPriority;
		eventUXInfo.PrioritizeBannerIfPlayerHasToken = input.PrioritizeBannerIfPlayerHasToken;
		eventUXInfo.Parameters = input.Parameters;
		eventUXInfo.Group = input.Group;
		eventUXInfo.EventBladeBehavior = input.EventBladeBehavior;
		eventUXInfo.DeckSelectFormat = input.DeckSelectFormat;
		eventUXInfo.Prizes = input.Prizes;
		eventUXInfo.EventComponentData = ToEventComponentData(input.EventComponentData, cardDatabase);
		eventUXInfo.DynamicFilterTagIds = input.DynamicFilterTagIds;
		eventUXInfo.Joinability = input.Joinability;
		eventUXInfo.FactionSealedUXInfo = new List<FactionSealedUXInfo>();
		foreach (FactionSealedUXInfo item in input.FactionSealedUXInfo)
		{
			FactionSealedUXInfo factionSealedUXInfo = new FactionSealedUXInfo
			{
				EventPageImage = item.EventPageImage,
				FactionInternalName = item.FactionInternalName,
				EventPageDescriptionLoc = item.EventPageDescriptionLoc,
				FactionEventNameLoc = item.FactionEventNameLoc,
				FactionSelectThumbnailImage = item.FactionSelectThumbnailImage,
				FactionSelectShortNameLoc = (item.FactionSelectShortNameLoc ?? ""),
				FactionCollations = new List<FactionCollation>()
			};
			foreach (FactionCollation factionCollation in item.FactionCollations)
			{
				factionSealedUXInfo.FactionCollations.Add(new FactionCollation
				{
					CollationId = factionCollation.CollationId,
					HangarLoc = factionCollation.HangarLoc
				});
			}
			eventUXInfo.FactionSealedUXInfo.Add(factionSealedUXInfo);
		}
		return eventUXInfo;
	}

	public static EventPage.Components.NetworkModels.EventComponentData ToEventComponentData(Wizards.Unification.Models.Events.EventComponentData input, ICardDataProvider cardDatabase)
	{
		return new EventPage.Components.NetworkModels.EventComponentData
		{
			AvgQueueTimeDisplay = ToAvgQueueTimeDisplayData(input.AvgQueueTimeDisplay),
			BoosterPacksDisplay = ToBoosterPacksDisplayData(input.BoosterPacksDisplay),
			StickerDisplay = ToStickerDisplayData(input.StickerDisplay),
			EmblemDisplay = ToEmblemDisplayData(input.EmblemDisplay),
			CardDisplay = ToCardDisplayData(input.CardDisplay),
			CardsDisplay = ToCardsDisplayData(input.CardsDisplay),
			LossDetailsDisplay = ToLossDetailsDisplayData(input.LossDetailsDisplay),
			TimerDisplay = ToEmptyData(input.TimerDisplay),
			DescriptionText = ToLocalizedTextData(input.DescriptionText),
			SubtitleText = ToLocalizedTextData(input.SubtitleText),
			TitleRankText = ToLocalizedTextData(input.TitleRankText),
			ChestWidget = ToChestWidgetData(input.ChestWidget),
			InspectPreconDecksWidget = ToInspectPreconDecksWidgetData(input.InspectPreconDecksWidget),
			InspectSingleDeckWidget = ToInspectSingleDeckWidgetData(input.InspectSingleDeckWidget, cardDatabase),
			PreviewEventWidget = ToPreviewEventWidgetData(input.PreviewEventWidget),
			ResignWidget = ToEmptyData(input.ResignWidget),
			SelectedDeckWidget = ToSelectedDeckWidgetData(input.SelectedDeckWidget),
			ViewCardPoolWidget = ToViewCardPoolWidgetData(input.ViewCardPoolWidget),
			MainButtonWidget = ToEmptyData(input.MainButtonWidget),
			ByCourseObjectiveTrack = ToObjectiveTrackData(input.ByCourseObjectiveTrack),
			CumulativeObjectiveTrack = ToObjectiveTrackData(input.CumulativeObjectiveTrack),
			HiddenBubblesObjectiveTrack = ToObjectiveTrackData(input.HiddenBubblesObjectiveTrack),
			DraftScreenUI = ToDraftScreenUIData(input.DraftScreenUI),
			DirectGameUI = ToDirectGameUIData(input.DirectGameUI),
			PrizeWallData = ToPrizeWallData(input.PrizeWallData)
		};
	}

	public static EventPage.Components.NetworkModels.AvgQueueTimeDisplayData ToAvgQueueTimeDisplayData(Wizards.Unification.Models.Events.AvgQueueTimeDisplayData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.AvgQueueTimeDisplayData
		{
			Seconds = input.Seconds
		};
	}

	public static EventPage.Components.NetworkModels.BoosterPacksDisplayData ToBoosterPacksDisplayData(Wizards.Unification.Models.Events.BoosterPacksDisplayData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.BoosterPacksDisplayData
		{
			CardGrantTime = ToCardGrantTime(input.CardGrantTime),
			CollationIds = input.CollationIds
		};
	}

	public static EventPage.Components.NetworkModels.StickerDisplayData ToStickerDisplayData(Wizards.Unification.Models.Events.StickerDisplayData input)
	{
		if (input == null)
		{
			return null;
		}
		if (input.Stickers.Count == 0)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.StickerDisplayData
		{
			Stickers = input.Stickers.ToList()
		};
	}

	public static EventPage.Components.NetworkModels.EmblemDisplayData ToEmblemDisplayData(Wizards.Unification.Models.Events.EmblemDisplayData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.EmblemDisplayData
		{
			EmblemIDs = input.EmblemIDs
		};
	}

	public static EventPage.Components.NetworkModels.CardDisplayData ToCardDisplayData(Wizards.Unification.Models.Events.CardDisplayData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.CardDisplayData
		{
			GrpID = input.GrpID
		};
	}

	public static EventPage.Components.NetworkModels.CardsDisplayData ToCardsDisplayData(Wizards.Unification.Models.Events.CardsDisplayData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.CardsDisplayData
		{
			GrpIds = input.GrpIds,
			HeaderLocKey = input.HeaderLocKey,
			DescriptionLocKey = input.DescriptionLocKey
		};
	}

	public static EventPage.Components.NetworkModels.LossDetailsDisplayData ToLossDetailsDisplayData(Wizards.Unification.Models.Events.LossDetailsDisplayData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.LossDetailsDisplayData
		{
			Games = input.Games,
			LossDetailsType = ToLossDetailsType(input.LossDetailsType)
		};
	}

	public static EventPage.Components.NetworkModels.EmptyData ToEmptyData(Wizards.Unification.Models.Events.EmptyData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.EmptyData();
	}

	public static EventPage.Components.NetworkModels.LocalizedTextData ToLocalizedTextData(Wizards.Unification.Models.Events.Components.LocalizedTextData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.LocalizedTextData
		{
			LocKey = input.LocKey
		};
	}

	public static EventPage.Components.NetworkModels.ChestWidgetData ToChestWidgetData(Wizards.Unification.Models.Events.Components.ChestWidgetData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.ChestWidgetData
		{
			CardSkin = input.CardSkin
		};
	}

	public static EventPage.Components.NetworkModels.InspectPreconDecksWidgetData ToInspectPreconDecksWidgetData(Wizards.Unification.Models.Events.InspectPreconDecksWidgetData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.InspectPreconDecksWidgetData
		{
			DeckIds = input.DeckIds
		};
	}

	public static EventPage.Components.NetworkModels.InspectSingleDeckWidgetData ToInspectSingleDeckWidgetData(Wizards.Unification.Models.Events.InspectSingleDeckWidgetData input, ICardDataProvider cardDatabase)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.InspectSingleDeckWidgetData
		{
			Deck = ToDeckInfoDescription(input.Deck, cardDatabase)
		};
	}

	public static EventPage.Components.NetworkModels.PreviewEventWidgetData ToPreviewEventWidgetData(Wizards.Unification.Models.Events.PreviewEventWidgetData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.PreviewEventWidgetData
		{
			LinkedEventName = input.LinkedEventName,
			LocKey = input.LocKey
		};
	}

	public static SelectedDeckWidgetData ToSelectedDeckWidgetData(SelectedDeckWidgetData input)
	{
		if (input == null)
		{
			return null;
		}
		return new SelectedDeckWidgetData
		{
			DeckButtonBehavior = ((input.DeckButtonBehavior == LayoutDeckButtonBehavior.Undefined) ? LayoutDeckButtonBehavior.Editable : input.DeckButtonBehavior),
			ShowCopyDeckButton = input.ShowCopyDeckButton
		};
	}

	public static EventPage.Components.NetworkModels.ViewCardPoolWidgetData ToViewCardPoolWidgetData(Wizards.Unification.Models.Events.ViewCardPoolWidgetData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.ViewCardPoolWidgetData
		{
			CardPool = input.CardPool
		};
	}

	public static EventPage.Components.NetworkModels.ObjectiveTrackData ToObjectiveTrackData(Wizards.Unification.Models.Events.Components.ObjectiveTrackData input)
	{
		EventPage.Components.NetworkModels.ObjectiveTrackData objectiveTrackData = new EventPage.Components.NetworkModels.ObjectiveTrackData();
		objectiveTrackData.ChestDescriptions = new List<EventPage.Components.NetworkModels.EventChestDescription>();
		if (input == null)
		{
			return objectiveTrackData;
		}
		foreach (Wizards.Unification.Models.Events.Components.EventChestDescription chestDescription in input.ChestDescriptions)
		{
			EventPage.Components.NetworkModels.EventChestDescription eventChestDescription = new EventPage.Components.NetworkModels.EventChestDescription();
			eventChestDescription.ChestDescription = ToClientChestDescription(chestDescription.ChestDescription);
			eventChestDescription.Wins = chestDescription.Wins;
			objectiveTrackData.ChestDescriptions.Add(eventChestDescription);
		}
		return objectiveTrackData;
	}

	public static EventPage.Components.NetworkModels.DraftScreenUIData ToDraftScreenUIData(Wizards.Unification.Models.Events.DraftScreenUIData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.DraftScreenUIData
		{
			TimeoutDiscoSec = input.TimeoutDiscoSec,
			TimeoutWarnSec = input.TimeoutWarnSec
		};
	}

	public static EventPage.Components.NetworkModels.DirectGameUIData ToDirectGameUIData(Wizards.Unification.Models.Events.DirectGameUIData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.DirectGameUIData
		{
			DirectGameFormats = input.DirectGameFormats
		};
	}

	public static EventPage.Components.NetworkModels.PrizeWallData ToPrizeWallData(Wizards.Unification.Models.Events.PrizeWallData input)
	{
		if (input == null)
		{
			return null;
		}
		return new EventPage.Components.NetworkModels.PrizeWallData
		{
			PrizeWallId = input.PrizeWallId
		};
	}

	public static ClientChestDescription ToClientChestDescription(ChestDescription input)
	{
		return new ClientChestDescription
		{
			image1 = input.image1,
			image2 = input.image2,
			image3 = input.image3,
			prefab = input.prefab,
			referenceId = input.referenceId,
			headerLocKey = input.headerLocKey,
			descriptionLocKey = input.descriptionLocKey,
			quantity = input.quantity,
			locParams = new Dictionary<string, int>(input.locParams),
			availableDate = input.availableDate
		};
	}

	public static Client_SeasonAndRankInfo ToClientSeasonAndRankInfo(SeasonAndRankInfo rankInfo)
	{
		Client_SeasonAndRankInfo client_SeasonAndRankInfo = new Client_SeasonAndRankInfo();
		client_SeasonAndRankInfo.currentSeason = ToClientSeasonInfo(rankInfo.currentSeason);
		client_SeasonAndRankInfo.limitedRankInfo = new List<Client_RankDefinition>();
		foreach (Wizards.Unification.Models.Rank.RankInfo item in rankInfo.limitedRankInfo)
		{
			client_SeasonAndRankInfo.limitedRankInfo.Add(ToClientRankInfo(item));
		}
		client_SeasonAndRankInfo.constructedRankInfo = new List<Client_RankDefinition>();
		foreach (Wizards.Unification.Models.Rank.RankInfo item2 in rankInfo.constructedRankInfo)
		{
			client_SeasonAndRankInfo.constructedRankInfo.Add(ToClientRankInfo(item2));
		}
		return client_SeasonAndRankInfo;
	}

	public static Client_SeasonInfo ToClientSeasonInfo(SeasonInfo info)
	{
		Client_SeasonInfo client_SeasonInfo = new Client_SeasonInfo();
		client_SeasonInfo.seasonOrdinal = info.seasonOrdinal;
		client_SeasonInfo.seasonStartTime = info.seasonStartTime;
		client_SeasonInfo.seasonEndTime = info.seasonEndTime;
		client_SeasonInfo.seasonLimitedRewards = new Dictionary<string, Client_ChestData>();
		foreach (KeyValuePair<string, ClientChestDescription> seasonLimitedReward in info.seasonLimitedRewards)
		{
			client_SeasonInfo.seasonLimitedRewards.Add(seasonLimitedReward.Key, new Client_ChestData(seasonLimitedReward.Value));
		}
		client_SeasonInfo.seasonConstructedRewards = new Dictionary<string, Client_ChestData>();
		foreach (KeyValuePair<string, ClientChestDescription> seasonConstructedReward in info.seasonConstructedRewards)
		{
			client_SeasonInfo.seasonConstructedRewards.Add(seasonConstructedReward.Key, new Client_ChestData(seasonConstructedReward.Value));
		}
		client_SeasonInfo.minMatches = info.minMatches;
		return client_SeasonInfo;
	}

	public static EventAndSeasonPayouts ToClientEventAndSeasonPayouts(SeasonPayoutResponseV2 payouts)
	{
		if (payouts == null)
		{
			return null;
		}
		EventAndSeasonPayouts eventAndSeasonPayouts = new EventAndSeasonPayouts();
		eventAndSeasonPayouts.eventPayouts = new List<EventPayoutData>();
		eventAndSeasonPayouts.seasonPayout = new SeasonPayoutData();
		eventAndSeasonPayouts.seasonPayout.currentSeasonOrdinal = payouts.currentSeasonOrdinal;
		eventAndSeasonPayouts.seasonPayout.oldConstructedOrdinal = payouts.oldConstructedOrdinal;
		eventAndSeasonPayouts.seasonPayout.oldLimitedOrdinal = payouts.oldLimitedOrdinal;
		eventAndSeasonPayouts.seasonPayout.constructedReset = ToClientRankProgress(payouts.constructedReset);
		eventAndSeasonPayouts.seasonPayout.limitedReset = ((payouts.limitedReset == null) ? null : ToClientRankProgress(payouts.limitedReset));
		if (payouts.constructedPayout != null)
		{
			eventAndSeasonPayouts.seasonPayout.constructedDelta = new List<Wizards.Mtga.FrontDoorModels.ClientInventoryUpdateReportItem> { payouts.constructedPayout.ToUpdateReportItem() };
		}
		if (payouts.limitedPayout != null)
		{
			eventAndSeasonPayouts.seasonPayout.limitedDelta = new List<Wizards.Mtga.FrontDoorModels.ClientInventoryUpdateReportItem> { payouts.limitedPayout.ToUpdateReportItem() };
		}
		if (payouts.inventoryInfoWithNoDeltas != null)
		{
			eventAndSeasonPayouts.seasonPayout.inventoryInfoWithNoDeltas = AWSInventoryConversions.ConvertInventoryInfo(payouts.inventoryInfoWithNoDeltas);
		}
		return eventAndSeasonPayouts;
	}

	public static Client_RankDefinition ToClientRankInfo(Wizards.Unification.Models.Rank.RankInfo rankInfo)
	{
		return new Client_RankDefinition
		{
			rankClass = ToRankingClassType(rankInfo.rankClass),
			level = rankInfo.level,
			steps = rankInfo.steps
		};
	}

	public static ClientChestDescription ToClientChestDescription(ClientChestDescription desc)
	{
		ClientChestDescription clientChestDescription = new ClientChestDescription();
		clientChestDescription.image1 = desc.image1;
		clientChestDescription.image2 = desc.image2;
		clientChestDescription.image3 = desc.image3;
		clientChestDescription.prefab = desc.prefab;
		clientChestDescription.referenceId = desc.referenceId;
		clientChestDescription.headerLocKey = desc.headerLocKey;
		clientChestDescription.descriptionLocKey = desc.descriptionLocKey;
		clientChestDescription.quantity = desc.quantity;
		clientChestDescription.locParams = new Dictionary<string, int>();
		foreach (KeyValuePair<string, int> locParam in desc.locParams)
		{
			clientChestDescription.locParams.Add(locParam.Key, locParam.Value);
		}
		clientChestDescription.availableDate = desc.availableDate;
		return clientChestDescription;
	}

	public static RankProgress ToClientRankProgress(PlayerRankProgress rankProgress)
	{
		return new RankProgress
		{
			playerId = "",
			seasonOrdinal = rankProgress.seasonOrdinal,
			newClass = ToRankingClassType(rankProgress.newClass),
			oldClass = ToRankingClassType(rankProgress.oldClass),
			newLevel = rankProgress.newLevel,
			oldLevel = rankProgress.oldLevel,
			oldStep = rankProgress.oldStep,
			newStep = rankProgress.newStep,
			wasLossProtected = rankProgress.wasLossProtected,
			rankUpdateType = rankProgress.rankUpdateType.ToString()
		};
	}

	public static MythicRatingUpdated ToClientMythicRatingUpdates(Client_MythicRatingUpdates mythicRatingUpdates)
	{
		return new MythicRatingUpdated
		{
			oldMythicPercentile = (mythicRatingUpdates?.oldMythicPercentile ?? 0f),
			newMythicPercentile = (mythicRatingUpdates?.newMythicPercentile ?? 0f),
			newMythicLeaderboardPlacement = (mythicRatingUpdates?.newMythicLeaderboardPlacement ?? 0)
		};
	}

	public static RankingClassType ToRankingClassType(Wizards.Arena.Enums.Rank.RankClass rankclass)
	{
		return rankclass switch
		{
			Wizards.Arena.Enums.Rank.RankClass.Spark => RankingClassType.Spark, 
			Wizards.Arena.Enums.Rank.RankClass.Bronze => RankingClassType.Bronze, 
			Wizards.Arena.Enums.Rank.RankClass.Silver => RankingClassType.Silver, 
			Wizards.Arena.Enums.Rank.RankClass.Gold => RankingClassType.Gold, 
			Wizards.Arena.Enums.Rank.RankClass.Platinum => RankingClassType.Platinum, 
			Wizards.Arena.Enums.Rank.RankClass.Diamond => RankingClassType.Diamond, 
			Wizards.Arena.Enums.Rank.RankClass.Mythic => RankingClassType.Mythic, 
			_ => RankingClassType.None, 
		};
	}

	public static CombinedRankInfo ToCombinedRankInfo(PlayerRankingInfo rankInfo)
	{
		return new CombinedRankInfo
		{
			constructedSeasonOrdinal = rankInfo.constructedSeasonOrdinal,
			constructedClass = ToRankingClassType(rankInfo.constructedClass),
			constructedLevel = rankInfo.constructedLevel,
			constructedStep = rankInfo.constructedStep,
			constructedMatchesWon = rankInfo.constructedMatchesWon,
			constructedMatchesLost = rankInfo.constructedMatchesLost,
			constructedMatchesDrawn = rankInfo.constructedMatchesDrawn,
			limitedSeasonOrdinal = rankInfo.limitedSeasonOrdinal,
			limitedClass = ToRankingClassType(rankInfo.limitedClass),
			limitedLevel = rankInfo.limitedLevel,
			limitedStep = rankInfo.limitedStep,
			limitedMatchesWon = rankInfo.limitedMatchesWon,
			limitedMatchesLost = rankInfo.limitedMatchesLost,
			limitedMatchesDrawn = rankInfo.limitedMatchesDrawn,
			constructedPercentile = (float)rankInfo.constructedPercentile,
			constructedLeaderboardPlace = rankInfo.constructedLeaderboardPlace,
			limitedPercentile = (float)rankInfo.limitedPercentile,
			limitedLeaderboardPlace = rankInfo.limitedLeaderboardPlace
		};
	}

	public static EventPage.Components.NetworkModels.EventInfoV3 ConvertToV3(this EventInfoV2 oldEventInfo)
	{
		EventPage.Components.NetworkModels.EventInfoV3 eventInfoV = new EventPage.Components.NetworkModels.EventInfoV3();
		eventInfoV.InternalEventName = oldEventInfo.InternalEventName;
		eventInfoV.EventState = oldEventInfo.EventState;
		eventInfoV.FormatType = oldEventInfo.FormatType;
		eventInfoV.StartTime = oldEventInfo.StartTime;
		eventInfoV.LockedTime = oldEventInfo.LockedTime;
		eventInfoV.ClosedTime = oldEventInfo.ClosedTime;
		eventInfoV.PastEntries = oldEventInfo.PastEntries;
		eventInfoV.EntryFees = oldEventInfo.ModuleGlobalData.EntryFees;
		eventInfoV.Flags = new List<EventPage.Components.NetworkModels.EventInfoFlag>();
		if (!string.IsNullOrWhiteSpace(oldEventInfo.ModuleGlobalData.RankUpdateType) && !oldEventInfo.ModuleGlobalData.RankUpdateType.Equals(Wizards.MDN.FormatType.None.ToString(), StringComparison.CurrentCultureIgnoreCase))
		{
			eventInfoV.Flags.Add(EventPage.Components.NetworkModels.EventInfoFlag.Ranked);
		}
		if (oldEventInfo.DoesUpdateDailyWeeklyRewards)
		{
			eventInfoV.Flags.Add(EventPage.Components.NetworkModels.EventInfoFlag.UpdateDailyWeeklyRewards);
		}
		if (oldEventInfo.DoesUpdateQuests)
		{
			eventInfoV.Flags.Add(EventPage.Components.NetworkModels.EventInfoFlag.UpdateQuests);
		}
		if (oldEventInfo.SkipValidation)
		{
			eventInfoV.Flags.Add(EventPage.Components.NetworkModels.EventInfoFlag.SkipDeckValidation);
		}
		if (oldEventInfo.AllowUncollectedCards)
		{
			eventInfoV.Flags.Add(EventPage.Components.NetworkModels.EventInfoFlag.AllowUncollectedCards);
		}
		if (oldEventInfo.IsArenaPlayModeEvent)
		{
			eventInfoV.Flags.Add(EventPage.Components.NetworkModels.EventInfoFlag.IsArenaPlayModeEvent);
		}
		if (oldEventInfo.ModuleGlobalData.IsPreconEvent)
		{
			eventInfoV.Flags.Add(EventPage.Components.NetworkModels.EventInfoFlag.IsPreconEvent);
		}
		eventInfoV.EventUXInfo = new EventPage.Components.NetworkModels.EventUXInfo();
		eventInfoV.EventUXInfo.PublicEventName = oldEventInfo.PublicEventName;
		eventInfoV.EventUXInfo.DisplayPriority = oldEventInfo.DisplayPriority;
		eventInfoV.EventUXInfo.Parameters = oldEventInfo.Parameters;
		eventInfoV.EventUXInfo.Group = oldEventInfo.Group;
		UILayoutInfo uILayoutOptions = oldEventInfo.UILayoutOptions;
		if (uILayoutOptions != null && uILayoutOptions.EventBladeBehavior == Wizards.MDN.LayoutEventBladeBehavior.Hidden)
		{
			eventInfoV.EventUXInfo.EventBladeBehavior = EventBladeBehavior.Hidden;
		}
		else
		{
			UILayoutInfo uILayoutOptions2 = oldEventInfo.UILayoutOptions;
			if (uILayoutOptions2 != null && uILayoutOptions2.EventBladeBehavior == Wizards.MDN.LayoutEventBladeBehavior.Queue)
			{
				eventInfoV.EventUXInfo.EventBladeBehavior = EventBladeBehavior.Queue;
			}
			else
			{
				eventInfoV.EventUXInfo.EventBladeBehavior = EventBladeBehavior.EventPage;
			}
		}
		if (eventInfoV.EventUXInfo.EventBladeBehavior == EventBladeBehavior.EventPage)
		{
			eventInfoV.EventUXInfo.EventComponentData = GetEventComponentData(oldEventInfo);
		}
		else
		{
			eventInfoV.EventUXInfo.EventComponentData = new EventPage.Components.NetworkModels.EventComponentData();
			Dictionary<string, string> directGameFormats = oldEventInfo.ModuleGlobalData.DirectGameFormats;
			if (directGameFormats != null && directGameFormats.Count > 0)
			{
				eventInfoV.EventUXInfo.EventComponentData.DirectGameUI = new EventPage.Components.NetworkModels.DirectGameUIData
				{
					DirectGameFormats = oldEventInfo.ModuleGlobalData.DirectGameFormats
				};
			}
		}
		eventInfoV.EventUXInfo.DeckSelectFormat = oldEventInfo.ModuleGlobalData.DeckSelect;
		ModuleInfo moduleGlobalData = oldEventInfo.ModuleGlobalData;
		if (moduleGlobalData != null && moduleGlobalData.Prizes?.Count > 0)
		{
			eventInfoV.EventUXInfo.Prizes = new Dictionary<int, Guid>();
			for (int i = 0; i < oldEventInfo.ModuleGlobalData.Prizes.Count; i++)
			{
				if (oldEventInfo.ModuleGlobalData.Prizes[i] != Guid.Empty)
				{
					eventInfoV.EventUXInfo.Prizes[i] = oldEventInfo.ModuleGlobalData.Prizes[i];
				}
			}
		}
		return eventInfoV;
	}

	public static EventPage.Components.NetworkModels.EventComponentData GetEventComponentData(EventInfoV2 eventInfo)
	{
		EventPage.Components.NetworkModels.EventComponentData eventComponentData = new EventPage.Components.NetworkModels.EventComponentData();
		ModuleInfo moduleGlobalData = eventInfo.ModuleGlobalData;
		if (eventInfo.ModuleGlobalData.TimeoutDiscoSec > 0 && eventInfo.ModuleGlobalData.TimeoutWarnSec > 0)
		{
			eventComponentData.DraftScreenUI = new EventPage.Components.NetworkModels.DraftScreenUIData
			{
				TimeoutDiscoSec = eventInfo.ModuleGlobalData.TimeoutDiscoSec,
				TimeoutWarnSec = eventInfo.ModuleGlobalData.TimeoutWarnSec
			};
		}
		if (moduleGlobalData != null && moduleGlobalData.AvgPodmakingSec >= 0)
		{
			eventComponentData.AvgQueueTimeDisplay = new EventPage.Components.NetworkModels.AvgQueueTimeDisplayData
			{
				Seconds = moduleGlobalData.AvgPodmakingSec
			};
		}
		if (moduleGlobalData != null && moduleGlobalData.CollationIds?.Count > 0)
		{
			eventComponentData.BoosterPacksDisplay = new EventPage.Components.NetworkModels.BoosterPacksDisplayData
			{
				CollationIds = moduleGlobalData.CollationIds,
				CardGrantTime = moduleGlobalData.cardGrantTime.GetValueOrDefault()
			};
		}
		if (moduleGlobalData != null && moduleGlobalData.StickerIds.Count > 0)
		{
			eventComponentData.StickerDisplay = new EventPage.Components.NetworkModels.StickerDisplayData
			{
				Stickers = moduleGlobalData.StickerIds.ToList()
			};
		}
		List<uint> emblems = eventInfo.Emblems;
		if (emblems != null && emblems.Count > 0)
		{
			eventComponentData.EmblemDisplay = new EventPage.Components.NetworkModels.EmblemDisplayData
			{
				EmblemIDs = eventInfo.Emblems
			};
		}
		EventPage.Components.NetworkModels.EventComponentData eventComponentData2 = eventComponentData;
		string internalEventName = eventInfo.InternalEventName;
		EventPage.Components.NetworkModels.PreviewEventWidgetData previewEventWidget;
		if (internalEventName == "ArenaOpen_Day1_20200801")
		{
			previewEventWidget = new EventPage.Components.NetworkModels.PreviewEventWidgetData
			{
				LocKey = "Events/EventPreview_Preview_Day_2",
				LinkedEventName = "ArenaOpen_Day2_20200802"
			};
		}
		else
		{
			previewEventWidget = ((!(internalEventName == "ArenaOpen_Day2_20200802")) ? eventComponentData.PreviewEventWidget : new EventPage.Components.NetworkModels.PreviewEventWidgetData
			{
				LocKey = "Events/EventPreview_Back_To_Day_1",
				LinkedEventName = "ArenaOpen_Day1_20200801"
			});
		}
		eventComponentData2.PreviewEventWidget = previewEventWidget;
		if (eventInfo.UILayoutOptions == null || eventInfo.UILayoutOptions.ResignBehavior != Wizards.MDN.LayoutResignBehavior.Hidden)
		{
			eventComponentData.ResignWidget = new EventPage.Components.NetworkModels.EmptyData();
		}
		if (string.IsNullOrEmpty(moduleGlobalData?.DeckSelect) && (moduleGlobalData == null || !moduleGlobalData.IsPreconEvent))
		{
			UILayoutInfo uILayoutOptions = eventInfo.UILayoutOptions;
			if (uILayoutOptions == null || uILayoutOptions.DeckButtonBehavior != LayoutDeckButtonBehavior.Fixed)
			{
				goto IL_0238;
			}
		}
		LayoutDeckButtonBehavior deckButtonBehavior = GetDeckButtonBehavior(eventInfo);
		SelectedDeckWidgetData selectedDeckWidget = new SelectedDeckWidgetData
		{
			DeckButtonBehavior = deckButtonBehavior,
			ShowCopyDeckButton = (deckButtonBehavior == LayoutDeckButtonBehavior.Editable || deckButtonBehavior == LayoutDeckButtonBehavior.Fixed)
		};
		eventComponentData.SelectedDeckWidget = selectedDeckWidget;
		goto IL_0238;
		IL_0238:
		Wizards.MDN.LayoutWinTrackBehavior layoutWinTrackBehavior = eventInfo.UILayoutOptions?.WinTrackBehavior ?? Wizards.MDN.LayoutWinTrackBehavior.Undefined;
		List<Guid> prizes = eventInfo.ModuleGlobalData.Prizes;
		if ((prizes != null && prizes.Count > 0) || layoutWinTrackBehavior != Wizards.MDN.LayoutWinTrackBehavior.Undefined)
		{
			EventPage.Components.NetworkModels.ObjectiveTrackData objectiveTrackData = new EventPage.Components.NetworkModels.ObjectiveTrackData
			{
				ChestDescriptions = CreateChestDescriptions(moduleGlobalData)
			};
			switch (layoutWinTrackBehavior)
			{
			case Wizards.MDN.LayoutWinTrackBehavior.Undefined:
			case Wizards.MDN.LayoutWinTrackBehavior.ByCourse:
				eventComponentData.ByCourseObjectiveTrack = objectiveTrackData;
				break;
			case Wizards.MDN.LayoutWinTrackBehavior.Cumulative:
				eventComponentData.CumulativeObjectiveTrack = objectiveTrackData;
				break;
			case Wizards.MDN.LayoutWinTrackBehavior.HiddenBubbles:
				eventComponentData.HiddenBubblesObjectiveTrack = objectiveTrackData;
				break;
			}
		}
		List<uint> cardPool = moduleGlobalData.CardPool;
		if (cardPool != null && cardPool.Count > 0 && eventInfo.UILayoutOptions?.TemplateName == "ViewPoolLimitedEventTemplate")
		{
			eventComponentData.ViewCardPoolWidget = new EventPage.Components.NetworkModels.ViewCardPoolWidgetData
			{
				CardPool = moduleGlobalData.CardPool
			};
		}
		List<Guid> items = moduleGlobalData.Items;
		if (items != null && items.Count > 0)
		{
			eventComponentData.InspectPreconDecksWidget = new EventPage.Components.NetworkModels.InspectPreconDecksWidgetData
			{
				DeckIds = moduleGlobalData.Items
			};
		}
		if (moduleGlobalData.MaxGames > 0)
		{
			eventComponentData.LossDetailsDisplay = new EventPage.Components.NetworkModels.LossDetailsDisplayData
			{
				Games = moduleGlobalData.MaxGames,
				LossDetailsType = EventPage.Components.NetworkModels.LossDetailsType.PlayMaxGames
			};
		}
		else if (moduleGlobalData.MaxLosses > 0)
		{
			eventComponentData.LossDetailsDisplay = new EventPage.Components.NetworkModels.LossDetailsDisplayData
			{
				Games = moduleGlobalData.MaxLosses,
				LossDetailsType = EventPage.Components.NetworkModels.LossDetailsType.LossSlots
			};
		}
		else if ((eventInfo.UILayoutOptions?.WinTrackBehavior ?? Wizards.MDN.LayoutWinTrackBehavior.Undefined) == Wizards.MDN.LayoutWinTrackBehavior.Cumulative)
		{
			eventComponentData.LossDetailsDisplay = new EventPage.Components.NetworkModels.LossDetailsDisplayData
			{
				Games = 0,
				LossDetailsType = EventPage.Components.NetworkModels.LossDetailsType.PlayUntilEventEnds
			};
		}
		eventComponentData.DescriptionText = new EventPage.Components.NetworkModels.LocalizedTextData
		{
			LocKey = "Events/Event_Desc_" + eventInfo.PublicEventName
		};
		eventComponentData.TitleRankText = new EventPage.Components.NetworkModels.LocalizedTextData
		{
			LocKey = "Events/Event_Title_" + eventInfo.PublicEventName
		};
		eventComponentData.TimerDisplay = new EventPage.Components.NetworkModels.EmptyData();
		eventComponentData.MainButtonWidget = new EventPage.Components.NetworkModels.EmptyData();
		return eventComponentData;
	}

	private static LayoutDeckButtonBehavior GetDeckButtonBehavior(EventInfoV2 eventInfo)
	{
		if (eventInfo.UILayoutOptions != null)
		{
			return eventInfo.UILayoutOptions.DeckButtonBehavior;
		}
		MDNEFormatType formatType = eventInfo.FormatType;
		if ((uint)(formatType - 1) > 1u)
		{
			_ = 3;
			return LayoutDeckButtonBehavior.Fixed;
		}
		return LayoutDeckButtonBehavior.Editable;
	}

	private static List<EventPage.Components.NetworkModels.EventChestDescription> CreateChestDescriptions(ModuleInfo moduleInfo)
	{
		List<EventPage.Components.NetworkModels.EventChestDescription> list = new List<EventPage.Components.NetworkModels.EventChestDescription>();
		List<ClientChestDescription> winNoGateChestDescriptions = moduleInfo.WinNoGateChestDescriptions;
		if (winNoGateChestDescriptions != null && winNoGateChestDescriptions.Count > 0)
		{
			for (int i = 0; i < moduleInfo.WinNoGateChestDescriptions.Count; i++)
			{
				if (moduleInfo.WinNoGateRewards[i].Wins != 0)
				{
					EventPage.Components.NetworkModels.EventChestDescription item = new EventPage.Components.NetworkModels.EventChestDescription
					{
						ChestDescription = moduleInfo.WinNoGateChestDescriptions[i],
						Wins = moduleInfo.WinNoGateRewards[i].Wins
					};
					list.Add(item);
				}
			}
		}
		else
		{
			for (int j = 0; j < moduleInfo.ChestDescriptions.Count; j++)
			{
				EventPage.Components.NetworkModels.EventChestDescription item2 = new EventPage.Components.NetworkModels.EventChestDescription
				{
					ChestDescription = moduleInfo.ChestDescriptions[j],
					Wins = (uint)j
				};
				list.Add(item2);
			}
		}
		return list;
	}

	public static Wizards.Mtga.FrontDoorModels.ClientInventoryUpdateReportItem ConvertToAzure(this Wizards.Unification.Models.Quest.ClientInventoryUpdateReportItem awsInventoryUpdateReport)
	{
		return new Wizards.Mtga.FrontDoorModels.ClientInventoryUpdateReportItem
		{
			context = awsInventoryUpdateReport.context.ConvertToAzure(),
			delta = awsInventoryUpdateReport.delta.ConvertToAzure(),
			xpGained = awsInventoryUpdateReport.xpGained
		};
	}

	private static Wizards.Mtga.FrontDoorModels.InventoryUpdateContext ConvertToAzure(this Wizards.Unification.Models.Player.InventoryUpdateContext awsUpdateContext)
	{
		return new Wizards.Mtga.FrontDoorModels.InventoryUpdateContext
		{
			source = awsUpdateContext.source.ConvertToAzure(),
			sourceId = awsUpdateContext.sourceId
		};
	}

	private static InventoryUpdateSource ConvertToAzure(this InventoryChangeSource awsSource)
	{
		return awsSource switch
		{
			InventoryChangeSource.Unknown => InventoryUpdateSource.Unknown, 
			InventoryChangeSource.QuestReward => InventoryUpdateSource.QuestReward, 
			InventoryChangeSource.DailyWins => InventoryUpdateSource.DailyWins, 
			InventoryChangeSource.WeeklyWins => InventoryUpdateSource.WeeklyWins, 
			InventoryChangeSource.LoginGrant => InventoryUpdateSource.LoginGrant, 
			InventoryChangeSource.IdEmpotentLoginGrant => InventoryUpdateSource.LoginGrant, 
			InventoryChangeSource.BattlePassLevelUp => InventoryUpdateSource.BattlePassLevelUp, 
			InventoryChangeSource.BattlePassLevelMasteryTree => InventoryUpdateSource.BattlePassLevelMasteryTree, 
			InventoryChangeSource.EarlyPlayerProgressionLevelUp => InventoryUpdateSource.EarlyPlayerProgressionLevelUp, 
			InventoryChangeSource.EarlyPlayerProgressionMasteryTree => InventoryUpdateSource.EarlyPlayerProgressionMasteryTree, 
			InventoryChangeSource.ProgressionRewardTierAdd => InventoryUpdateSource.ProgressionRewardTierAdd, 
			InventoryChangeSource.RenewalReward => InventoryUpdateSource.RenewalReward, 
			InventoryChangeSource.MercantilePurchase => InventoryUpdateSource.MercantilePurchase, 
			InventoryChangeSource.MercantileChestPurchase => InventoryUpdateSource.MercantileChestPurchase, 
			InventoryChangeSource.MercantileBoosterPurchase => InventoryUpdateSource.MercantileBoosterPurchase, 
			InventoryChangeSource.EventReward => InventoryUpdateSource.EventReward, 
			InventoryChangeSource.RedeemVoucher => InventoryUpdateSource.RedeemVoucher, 
			InventoryChangeSource.ModifyPlayerInventory => InventoryUpdateSource.ModifyPlayerInventory, 
			InventoryChangeSource.OpenChest => InventoryUpdateSource.OpenChest, 
			InventoryChangeSource.MassOpenChest => InventoryUpdateSource.MassOpenChest, 
			InventoryChangeSource.BasicLandSetUpdate => InventoryUpdateSource.BasicLandSetUpdate, 
			InventoryChangeSource.CompleteVault => InventoryUpdateSource.CompleteVault, 
			InventoryChangeSource.CosmeticPurchase => InventoryUpdateSource.CosmeticPurchase, 
			InventoryChangeSource.WildCardRedemption => InventoryUpdateSource.WildCardRedemption, 
			InventoryChangeSource.BoosterOpen => InventoryUpdateSource.BoosterOpen, 
			InventoryChangeSource.StarterDeckUpgrade => InventoryUpdateSource.StarterDeckUpgrade, 
			InventoryChangeSource.RankedSeasonReward => InventoryUpdateSource.RankedSeasonReward, 
			InventoryChangeSource.EventPayEntry => InventoryUpdateSource.EventPayEntry, 
			InventoryChangeSource.BannedCardGrant => InventoryUpdateSource.BannedCardGrant, 
			InventoryChangeSource.RestrictedCardGrant => InventoryUpdateSource.RestrictedCardGrant, 
			InventoryChangeSource.CustomerSupportGrant => InventoryUpdateSource.Unknown, 
			InventoryChangeSource.EventGrantCardPool => InventoryUpdateSource.Unknown, 
			InventoryChangeSource.EntryReward => InventoryUpdateSource.EventReward, 
			InventoryChangeSource.CampaignGraphPayoutNode => InventoryUpdateSource.Unknown, 
			_ => throw new ArgumentOutOfRangeException("Unable to translate type " + awsSource.ToString() + " to InventoryChangeSource"), 
		};
	}
}
