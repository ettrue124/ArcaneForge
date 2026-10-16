using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;
using GreClient.CardData;
using Wizards.Arena.DeckValidation.Core.Models;
using Wizards.Arena.Enums.Card;
using Wizards.Arena.Enums.Format;
using Wizards.Arena.Models.Network;
using Wizards.Arena.Promises;
using Wizards.Models;
using Wizards.Mtga.Format;
using Wizards.Mtga.FrontDoorModels;
using Wotc.Mtga.Cards.Database;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Network.ServiceWrappers;

namespace SharedClientCore.SharedClientCore.Meta.Cards;

public class FormatManagerCore : IFormatManager
{
	private const string SortGroupName = "ConstructedSortOrder";

	private const string EvergreenGroupName = "EvergreenFormats";

	private const string BannedPopupGroupName = "BannedFormats";

	private IFormatsServiceWrapper _serviceWrapper;

	private ICardDatabaseAdapter _cardDatabase;

	private readonly List<DeckFormat> _formats = new List<DeckFormat>();

	private Dictionary<string, DeckFormat> _formatsMap = new Dictionary<string, DeckFormat>();

	private DeckFormat _defaultFormat;

	private List<DeckFormat> _evergreenFormats = new List<DeckFormat>();

	private Dictionary<string, SetAvailability> _setAvailabilities;

	private List<DeckFormat> _bannedPopupFormats = new List<DeckFormat>();

	public List<DeckFormat> EvergreenFormats => _evergreenFormats;

	public IReadOnlyList<DeckFormat> BannedPopupFormats => _bannedPopupFormats;

	public virtual void Initialize(ICardDatabaseAdapter cdb, IFormatsServiceWrapper serviceWrapper)
	{
		_cardDatabase = cdb;
		_serviceWrapper = serviceWrapper;
	}

	public Promise<GetFormatsData> RefreshFormats(bool includeDebugFormats)
	{
		return _serviceWrapper.GetFormatData().IfSuccess((Promise<GetFormatsData> p) =>
		{
			SetupFormats(p.Result.Formats, p.Result.FormatGroups, includeDebugFormats);
		});
	}

	protected void SetupFormats(List<FormatConfigInfo> formatInfos, List<Wizards.Models.FormatGroup> formatGroups, bool includeDebugFormats)
	{
		_formats.Clear();
		BuildDeckFormats(formatInfos, _formats, _cardDatabase, this);
		if (includeDebugFormats)
		{
			_formats.Add(AllCardsTestFormat(_cardDatabase, this));
			_formats.Add(AllCardsTestBrawlFormat(_cardDatabase, this));
		}
		for (int i = 0; i < _formats.Count; i++)
		{
			_formats[i].SortOrder = _formats.Count + i;
		}
		_formatsMap = _formats.ToDictionary((DeckFormat x) => x.FormatName);
		if (formatGroups != null)
		{
			Wizards.Models.FormatGroup formatGroup = FormatGroupByName(formatGroups, "ConstructedSortOrder");
			ApplySort(_formatsMap, formatGroup?.FormatNames);
			_evergreenFormats = GetFormatsForFormatGroup("EvergreenFormats", _formatsMap, formatGroups);
			MarkAsEvergreen(EvergreenFormats);
			_bannedPopupFormats = GetFormatsForFormatGroup("BannedFormats", _formatsMap, formatGroups);
		}
		_formats.Sort(FormatUtilities.FormatSortOrderComparator);
		_defaultFormat = _formats.FirstOrDefault();
	}

	private static void ApplySort(Dictionary<string, DeckFormat> formatsMap, List<string> formatSortOrder)
	{
		if (formatSortOrder == null)
		{
			return;
		}
		for (int i = 0; i < formatSortOrder.Count; i++)
		{
			if (formatsMap.TryGetValue(formatSortOrder[i], out var value))
			{
				value.SortOrder = i;
			}
		}
	}

	private static List<DeckFormat> GetFormatsForFormatGroup(string groupName, Dictionary<string, DeckFormat> formatsMap, List<Wizards.Models.FormatGroup> formatGroups)
	{
		return GetFormatsForGroup(formatsMap, FormatGroupByName(formatGroups, groupName)?.FormatNames);
	}

	private static List<DeckFormat> GetFormatsForGroup(Dictionary<string, DeckFormat> formatsMap, List<string> formatNames)
	{
		List<DeckFormat> list = new List<DeckFormat>();
		if (formatNames == null)
		{
			return list;
		}
		list.AddRange(from formatName in formatNames
			select GetSafeFormat(formatsMap, formatName, null) into format
			where format != null
			select format);
		return list;
	}

	private static void MarkAsEvergreen(List<DeckFormat> formats)
	{
		formats.ForEach((DeckFormat format) =>
		{
			format.IsEvergreen = true;
		});
	}

	private static Wizards.Models.FormatGroup FormatGroupByName(List<Wizards.Models.FormatGroup> formatGroups, string groupName)
	{
		return formatGroups.Find(groupName, (Wizards.Models.FormatGroup x, string y) => x.GroupName == y);
	}

	public DeckFormat GetSafeFormat(string formatName)
	{
		return GetSafeFormat(_formatsMap, formatName, _defaultFormat);
	}

	public DeckFormat GetSafeFormat(string formatName, DeckFormat defaultFormat)
	{
		return GetSafeFormat(_formatsMap, formatName, defaultFormat);
	}

	private static DeckFormat GetSafeFormat(Dictionary<string, DeckFormat> formatsMap, string formatName, DeckFormat defaultValue)
	{
		if (string.IsNullOrEmpty(formatName))
		{
			return defaultValue;
		}
		if (formatsMap != null && formatsMap.TryGetValue(formatName, out var value))
		{
			return value;
		}
		return defaultValue;
	}

	public DeckFormat GetDefaultFormat()
	{
		return _defaultFormat;
	}

	public SetAvailability GetCardTitleAvailability(uint titleId, DeckFormat format)
	{
		IReadOnlyList<CardPrintingData> printingsByTitleId = _cardDatabase.DatabaseUtilities.GetPrintingsByTitleId(titleId);
		return format.GetAvailability(printingsByTitleId);
	}

	public SetAvailability GetCardArtAvailability(uint artId, DeckFormat format)
	{
		IReadOnlyList<CardPrintingData> readOnlyList = _cardDatabase.DatabaseUtilities.GetPrintingsByArtId(artId);
		if (readOnlyList.Count > 0)
		{
			readOnlyList = readOnlyList.Concat(_cardDatabase.DatabaseUtilities.GetPrintingsByTitleId(readOnlyList[0].TitleId)).ToArray();
		}
		return format.GetAvailability(readOnlyList);
	}

	public List<DeckFormat> GetAllFormats()
	{
		return _formats;
	}

	public static void BuildDeckFormats(IReadOnlyList<FormatConfigInfo> configs, List<DeckFormat> formats, ICardDatabaseAdapter cardDatabase, IFormatManager formatManager)
	{
		IDatabaseUtilities databaseUtilities = cardDatabase.DatabaseUtilities;
		formats.Clear();
		foreach (FormatConfigInfo config in configs)
		{
			DeckFormat deckFormat = new DeckFormat(formatManager, cardDatabase.ClientLocProvider)
			{
				FormatName = config.Name,
				FormatType = ServiceWrapperHelpers.ToMDNFormatType(config.FormatType),
				UseRebalancedCards = config.UseRebalancedCards,
				SideboardBehavior = config.SideboardBehavior,
				MinMainDeckCardsExcludingCommandZone = (config.MainDeckQuota?.Min ?? 60),
				MaxMainDeckCardsExcludingCommandZone = (config.MainDeckQuota?.Max ?? 250),
				MinCommandZoneCards = (config.CommandZoneQuota?.Min ?? 0),
				MaxCommandZoneCards = (config.CommandZoneQuota?.Max ?? 0),
				MinSideboardCards = (config.SideBoardQuota?.Min ?? 0),
				MaxSideboardCards = (config.SideBoardQuota?.Max ?? 0),
				MaxTotalCards = 250
			};
			Wizards.Arena.Models.Network.Quota commandZoneQuota = config.CommandZoneQuota;
			deckFormat.FormatIncludesCommandZone = commandZoneQuota != null && commandZoneQuota.Max > 0;
			DeckFormat deckFormat2 = deckFormat;
			if (deckFormat2.FormatIncludesCommandZone)
			{
				deckFormat2.MinMainDeckCards = deckFormat2.MinMainDeckCardsExcludingCommandZone + deckFormat2.MinCommandZoneCards;
				deckFormat2.MaxMainDeckCards = deckFormat2.MaxMainDeckCardsExcludingCommandZone + deckFormat2.MaxCommandZoneCards;
			}
			else if (config.MainDeckQuota != null)
			{
				deckFormat2.MinMainDeckCards = config.MainDeckQuota.Min;
				deckFormat2.MaxMainDeckCards = config.MainDeckQuota.Max;
			}
			else
			{
				deckFormat2.MinMainDeckCards = 60;
				deckFormat2.MaxMainDeckCards = 250;
			}
			DeckFormat deckFormat3 = deckFormat2;
			deckFormat3.MaxCardsByTitle = config.CardCountRestriction switch
			{
				CardCountRule.Singleton => 1, 
				CardCountRule.UnrestrictedCardCounts => 250, 
				_ => 4, 
			};
			formats.Add(deckFormat2);
		}
		for (int i = 0; i < formats.Count; i++)
		{
			DeckFormat deckFormat4 = formats[i];
			FormatConfigInfo formatConfigInfo = configs[i];
			if (deckFormat4.FormatType != MDNEFormatType.Constructed)
			{
				continue;
			}
			deckFormat4.LegalSets = formatConfigInfo.LegalSets.ToHashSet();
			deckFormat4.FilterSets = formatConfigInfo.FilterSets.ToHashSet();
			RepeatedField<uint> bannedTitleIds = formatConfigInfo.BannedTitleIds;
			if (bannedTitleIds != null && bannedTitleIds.Count > 0)
			{
				deckFormat4.BannedTitleIds.UnionWith(formatConfigInfo.BannedTitleIds);
			}
			RepeatedField<uint> suspendedTitleIds = formatConfigInfo.SuspendedTitleIds;
			if (suspendedTitleIds != null && suspendedTitleIds.Count > 0)
			{
				deckFormat4.SuspendedCardTitleIds = formatConfigInfo.SuspendedTitleIds;
				deckFormat4.BannedTitleIds.UnionWith(formatConfigInfo.SuspendedTitleIds);
			}
			if (formatConfigInfo.SupressedTitleIds.Count > 0)
			{
				deckFormat4.SupressedCardTitleIds = formatConfigInfo.SupressedTitleIds;
			}
			RepeatedField<uint> allowedCommanderTitleIds = formatConfigInfo.AllowedCommanderTitleIds;
			if (allowedCommanderTitleIds != null && allowedCommanderTitleIds.Count > 0)
			{
				deckFormat4.AllowedCommanders.UnionWith(formatConfigInfo.AllowedCommanderTitleIds);
			}
			RepeatedField<uint> bannedAsCommanderTitleIds = formatConfigInfo.BannedAsCommanderTitleIds;
			if (bannedAsCommanderTitleIds != null && bannedAsCommanderTitleIds.Count > 0)
			{
				deckFormat4.BannedAsCommanders.UnionWith(formatConfigInfo.BannedAsCommanderTitleIds);
			}
			MapField<uint, Wizards.Arena.Models.Network.Quota> individualCardQuotas = formatConfigInfo.IndividualCardQuotas;
			if (individualCardQuotas != null && individualCardQuotas.Count > 0)
			{
				deckFormat4.RestrictedTitleIds = FormatDataClientCore.ConvertFromProto(formatConfigInfo.IndividualCardQuotas);
			}
			if (formatConfigInfo.RestrictToColorIdentity.Any())
			{
				deckFormat4.RestrictToColorIdentity = formatConfigInfo.RestrictToColorIdentity.Select((IEnumerable<ManaColor> opt) => opt.ToArray()).ToArray();
			}
			if (formatConfigInfo.DisallowedCardTypesForMainAndSideboard.Any())
			{
				deckFormat4.DisallowedCardTypesForMainAndSideboard = formatConfigInfo.DisallowedCardTypesForMainAndSideboard.ToArray();
			}
			if (formatConfigInfo.RestrictToCommandZoneCardType.Any())
			{
				deckFormat4.RestrictToCommandZoneCardType = formatConfigInfo.RestrictToCommandZoneCardType.ToArray();
			}
			if (formatConfigInfo.RarityPerCardQuotas.Any())
			{
				deckFormat4.RarityPerCardQuotas = formatConfigInfo.RarityPerCardQuotas.ToDictionary((KeyValuePair<Rarity, Wizards.Arena.Models.Network.Quota> kvp) => kvp.Key, (KeyValuePair<Rarity, Wizards.Arena.Models.Network.Quota> kvp) => new Wizards.Arena.DeckValidation.Core.Models.Quota(kvp.Value.Min, kvp.Value.Max));
			}
			bool flag = false;
			for (int num = 0; num < i; num++)
			{
				if (IsSame(formats[num], configs[num], deckFormat4, formatConfigInfo))
				{
					deckFormat4.LegalTitleIds = formats[num].LegalTitleIds;
					flag = true;
					break;
				}
			}
			if (flag)
			{
				continue;
			}
			HashSet<uint> hashSet = new HashSet<uint>();
			hashSet.UnionWith(databaseUtilities.GetTitleIdsLegalForFormatData(deckFormat4.LegalSets, deckFormat4.IsPauper, deckFormat4.IsArtisan, deckFormat4.UseRebalancedCards));
			hashSet.UnionWith(formatConfigInfo.AllowedTitleIds);
			RepeatedField<uint> bannedTitleIds2 = formatConfigInfo.BannedTitleIds;
			if (bannedTitleIds2 == null || bannedTitleIds2.Count <= 0)
			{
				RepeatedField<uint> suspendedTitleIds2 = formatConfigInfo.SuspendedTitleIds;
				if (suspendedTitleIds2 == null || suspendedTitleIds2.Count <= 0)
				{
					goto IL_04dd;
				}
			}
			hashSet.ExceptWith(deckFormat4.BannedTitleIds);
			goto IL_04dd;
			IL_04dd:
			hashSet.TrimExcess();
			deckFormat4.LegalTitleIds = hashSet;
		}
	}

	private static bool IsSame(DeckFormat otherFormat, FormatConfigInfo otherConfig, DeckFormat currentFormat, FormatConfigInfo currentConfig)
	{
		if (otherFormat.IsPauper != currentFormat.IsPauper)
		{
			return false;
		}
		if (otherFormat.IsArtisan != currentFormat.IsArtisan)
		{
			return false;
		}
		if (otherFormat.UseRebalancedCards != currentFormat.UseRebalancedCards)
		{
			return false;
		}
		if (!otherConfig.BannedTitleIds.SequenceEqual(currentConfig.BannedTitleIds))
		{
			return false;
		}
		if (!otherConfig.SuspendedTitleIds.SequenceEqual(currentConfig.SuspendedTitleIds))
		{
			return false;
		}
		if (!otherFormat.LegalSets.SetEquals(currentFormat.LegalSets))
		{
			return false;
		}
		return true;
	}

	public void SetupSetAvailabilities(Dictionary<string, SetAvailability> setAvailabilities)
	{
		_setAvailabilities = setAvailabilities;
	}

	public Dictionary<string, SetAvailability> GetSetAvailabilities()
	{
		return _setAvailabilities;
	}

	public SetAvailability GetSetAvailabilityForPrinting(CardPrintingData printing)
	{
		string key = ((!string.IsNullOrEmpty(printing.DigitalReleaseSet)) ? printing.DigitalReleaseSet : printing.ExpansionCode);
		return _setAvailabilities.GetValueOrDefault(key, SetAvailability.HistoricOnly);
	}

	private static DeckFormat AllCardsTestFormat(ICardDatabaseAdapter cardDatabase, IFormatManager formatManager)
	{
		return GenAllCardsFormat(cardDatabase, formatManager, "AllCardsTestFormat", isBrawl: false);
	}

	private static DeckFormat AllCardsTestBrawlFormat(ICardDatabaseAdapter cardDatabase, IFormatManager formatManager)
	{
		return GenAllCardsFormat(cardDatabase, formatManager, "AllCardsTestBrawlFormat", isBrawl: true);
	}

	private static DeckFormat GenAllCardsFormat(ICardDatabaseAdapter cardDatabase, IFormatManager formatManager, string name, bool isBrawl)
	{
		DeckFormat deckFormat = new DeckFormat(formatManager, cardDatabase.ClientLocProvider)
		{
			FormatName = name,
			FormatType = MDNEFormatType.Constructed,
			UseRebalancedCards = false,
			SideboardBehavior = (isBrawl ? FormatSideboardBehavior.CompanionOnly : FormatSideboardBehavior.Normal),
			MinMainDeckCardsExcludingCommandZone = (isBrawl ? 99 : 60),
			MaxMainDeckCardsExcludingCommandZone = (isBrawl ? 99 : 250),
			MinCommandZoneCards = (isBrawl ? 1 : 0),
			MaxCommandZoneCards = (isBrawl ? 1 : 0),
			MinSideboardCards = (isBrawl ? 0 : 0),
			MaxSideboardCards = (isBrawl ? 1 : 0),
			MaxTotalCards = 250,
			FormatIncludesCommandZone = isBrawl,
			IsEvergreen = true
		};
		if (isBrawl)
		{
			deckFormat.MinMainDeckCards = deckFormat.MinMainDeckCardsExcludingCommandZone + deckFormat.MinCommandZoneCards;
			deckFormat.MaxMainDeckCards = deckFormat.MaxMainDeckCardsExcludingCommandZone + deckFormat.MaxCommandZoneCards;
		}
		else
		{
			deckFormat.MinMainDeckCards = 60;
			deckFormat.MaxMainDeckCards = 250;
		}
		deckFormat.MaxCardsByTitle = (isBrawl ? 1 : 4);
		if (isBrawl)
		{
			deckFormat.RarityPerCardQuotas = new Dictionary<Rarity, Wizards.Arena.DeckValidation.Core.Models.Quota>
			{
				[Rarity.Common] = new Wizards.Arena.DeckValidation.Core.Models.Quota(0, 1),
				[Rarity.Uncommon] = new Wizards.Arena.DeckValidation.Core.Models.Quota(0, 1),
				[Rarity.Rare] = new Wizards.Arena.DeckValidation.Core.Models.Quota(0, 1),
				[Rarity.Mythic] = new Wizards.Arena.DeckValidation.Core.Models.Quota(0, 1)
			};
		}
		deckFormat.LegalTitleIds.UnionWith(cardDatabase.DatabaseUtilities.GetTitleIdsLegalForFormatData(deckFormat.LegalSets, deckFormat.IsPauper, deckFormat.IsArtisan, deckFormat.UseRebalancedCards));
		return deckFormat;
	}
}
