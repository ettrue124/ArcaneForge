using System;
using System.Collections.Generic;
using System.Linq;
using GreClient.CardData;
using Wizards.Arena.DeckValidation.Core.Interfaces;
using Wizards.Arena.DeckValidation.Core.Models;
using Wizards.Arena.Enums.Card;
using Wizards.Arena.Enums.Format;
using Wizards.Mtga.FrontDoorModels;
using Wotc.Mtga.Extensions;
using Wotc.Mtga.Loc;
using Wotc.Mtgo.Gre.External.Messaging;

public class DeckFormat : IFormatConfig
{
	private readonly IFormatManager _formatManager;

	private readonly IClientLocProvider _localizationManager;

	private string _formatName;

	public int MinMainDeckCards;

	public int MaxMainDeckCards;

	public int MinMainDeckCardsExcludingCommandZone;

	public int MaxMainDeckCardsExcludingCommandZone;

	public int MinCommandZoneCards;

	public int MaxCommandZoneCards;

	public int MinSideboardCards;

	public int MaxSideboardCards;

	public int MaxTotalCards;

	private HashSet<string> _legalSets;

	private HashSet<string> _filterSets;

	private IReadOnlyList<uint> _suspendedCardTitleIds;

	private IReadOnlyList<uint> _supressedCardTitleIds;

	private HashSet<uint> _bannedTitleIds;

	private Dictionary<uint, Quota> _restrictedTitleIds;

	private HashSet<uint> _allowedCommanders;

	private HashSet<uint> _bannedAsCommanders;

	private HashSet<uint> _legalTitleIds;

	private IReadOnlyCollection<IReadOnlyCollection<Wizards.Arena.Enums.Card.ManaColor>> _restrictToColorIdentity;

	private IReadOnlyList<Wizards.Arena.Enums.Card.CardType> _disallowedCardTypesForMainAndSideboard;

	private IReadOnlyList<Wizards.Arena.Enums.Card.CardType> _restrictToCommandZoneCardType;

	private IReadOnlyDictionary<Rarity, Quota> _rarityPerCardQuotas;

	public bool UseRebalancedCards;

	public MDNEFormatType FormatType;

	private int _maxCardsByTitle;

	public int SortOrder;

	public bool IsEvergreen;

	public FormatSideboardBehavior SideboardBehavior;

	private Dictionary<string, SetAvailability> _setAvailabilities;

	public string FormatName
	{
		get
		{
			return _formatName;
		}
		set
		{
			if (_formatName != value)
			{
				_formatName = value;
				IsStandard = _formatName != null && _formatName.IndexOf("Standard", StringComparison.OrdinalIgnoreCase) >= 0;
				IsAlchemy = _formatName != null && _formatName.IndexOf("Alchemy", StringComparison.OrdinalIgnoreCase) >= 0;
				IsHistoric = _formatName != null && _formatName.IndexOf("Historic", StringComparison.OrdinalIgnoreCase) >= 0;
				IsPauper = _formatName != null && _formatName.IndexOf("Pauper", StringComparison.OrdinalIgnoreCase) >= 0;
				IsDirectChallenge = _formatName != null && _formatName.IndexOf("DirectGame", StringComparison.OrdinalIgnoreCase) >= 0;
				IsArtisan = _formatName != null && _formatName.IndexOf("Artisan", StringComparison.OrdinalIgnoreCase) >= 0;
				IsExplorer = _formatName != null && _formatName.IndexOf("Explorer", StringComparison.OrdinalIgnoreCase) >= 0;
			}
		}
	}

	public bool IsStandard { get; private set; }

	public bool IsAlchemy { get; private set; }

	public bool IsHistoric { get; private set; }

	public bool IsDirectChallenge { get; private set; }

	public bool IsPauper { get; private set; }

	public bool IsArtisan { get; private set; }

	public bool IsSingleton { get; private set; }

	public bool IsExplorer { get; private set; }

	public bool FormatIncludesCommandZone { get; set; }

	public HashSet<string> LegalSets
	{
		get
		{
			return _legalSets ?? (_legalSets = new HashSet<string>());
		}
		set
		{
			_legalSets = value;
		}
	}

	public HashSet<string> FilterSets
	{
		get
		{
			return _filterSets ?? (_filterSets = new HashSet<string>());
		}
		set
		{
			_filterSets = value;
		}
	}

	public IReadOnlyList<uint> SuspendedCardTitleIds
	{
		get
		{
			return _suspendedCardTitleIds ?? (_suspendedCardTitleIds = Array.Empty<uint>());
		}
		set
		{
			_suspendedCardTitleIds = value;
		}
	}

	public IReadOnlyList<uint> SupressedCardTitleIds
	{
		get
		{
			return _supressedCardTitleIds ?? (_supressedCardTitleIds = Array.Empty<uint>());
		}
		set
		{
			_supressedCardTitleIds = value;
		}
	}

	public HashSet<uint> BannedTitleIds
	{
		get
		{
			return _bannedTitleIds ?? (_bannedTitleIds = new HashSet<uint>());
		}
		set
		{
			_bannedTitleIds = value;
		}
	}

	public Dictionary<uint, Quota> RestrictedTitleIds
	{
		get
		{
			return _restrictedTitleIds ?? (_restrictedTitleIds = new Dictionary<uint, Quota>());
		}
		set
		{
			_restrictedTitleIds = value;
		}
	}

	public HashSet<uint> AllowedCommanders
	{
		get
		{
			return _allowedCommanders ?? (_allowedCommanders = new HashSet<uint>());
		}
		set
		{
			_allowedCommanders = value;
		}
	}

	public HashSet<uint> BannedAsCommanders
	{
		get
		{
			return _bannedAsCommanders ?? (_bannedAsCommanders = new HashSet<uint>());
		}
		set
		{
			_bannedAsCommanders = value;
		}
	}

	public HashSet<uint> LegalTitleIds
	{
		get
		{
			return _legalTitleIds ?? (_legalTitleIds = new HashSet<uint>());
		}
		set
		{
			_legalTitleIds = value;
		}
	}

	public IReadOnlyCollection<IReadOnlyCollection<Wizards.Arena.Enums.Card.ManaColor>> RestrictToColorIdentity
	{
		get
		{
			return _restrictToColorIdentity ?? (_restrictToColorIdentity = Array.Empty<IReadOnlyCollection<Wizards.Arena.Enums.Card.ManaColor>>());
		}
		set
		{
			_restrictToColorIdentity = value;
		}
	}

	public IReadOnlyList<Wizards.Arena.Enums.Card.CardType> DisallowedCardTypesForMainAndSideboard
	{
		get
		{
			return _disallowedCardTypesForMainAndSideboard ?? (_disallowedCardTypesForMainAndSideboard = Array.Empty<Wizards.Arena.Enums.Card.CardType>());
		}
		set
		{
			_disallowedCardTypesForMainAndSideboard = value;
		}
	}

	public IReadOnlyList<Wizards.Arena.Enums.Card.CardType> RestrictToCommandZoneCardType
	{
		get
		{
			return _restrictToCommandZoneCardType ?? (_restrictToCommandZoneCardType = Array.Empty<Wizards.Arena.Enums.Card.CardType>());
		}
		set
		{
			_restrictToCommandZoneCardType = value;
		}
	}

	public IReadOnlyDictionary<Rarity, Quota> RarityPerCardQuotas
	{
		get
		{
			return _rarityPerCardQuotas ?? (_rarityPerCardQuotas = new Dictionary<Rarity, Quota>());
		}
		set
		{
			_rarityPerCardQuotas = value;
		}
	}

	public int MaxCardsByTitle
	{
		get
		{
			return _maxCardsByTitle;
		}
		set
		{
			_maxCardsByTitle = value;
			IsSingleton = value == 1;
		}
	}

	string IFormatConfig.Name => FormatName;

	double IFormatConfig.DeckWeightScalar => 0.0;

	double IFormatConfig.DeckWeightQuotaMin => 0.0;

	double IFormatConfig.DeckWeightQuotaMax => 0.0;

	FormatType IFormatConfig.FormatType => FormatType switch
	{
		MDNEFormatType.Invalid => Wizards.Arena.Enums.Format.FormatType.None, 
		MDNEFormatType.Draft => Wizards.Arena.Enums.Format.FormatType.Draft, 
		MDNEFormatType.Sealed => Wizards.Arena.Enums.Format.FormatType.Sealed, 
		MDNEFormatType.Constructed => Wizards.Arena.Enums.Format.FormatType.Constructed, 
		_ => throw new ArgumentOutOfRangeException(), 
	};

	FormatSideboardBehavior IFormatConfig.SideboardBehavior => SideboardBehavior switch
	{
		FormatSideboardBehavior.Normal => FormatSideboardBehavior.Normal, 
		FormatSideboardBehavior.CompanionOnly => FormatSideboardBehavior.CompanionOnly, 
		_ => throw new ArgumentOutOfRangeException(), 
	};

	bool IFormatConfig.UseRebalancedCards => UseRebalancedCards;

	IReadOnlyList<string> IFormatConfig.LegalSets => LegalSets.ToList();

	IReadOnlyCollection<IReadOnlyCollection<Wizards.Arena.Enums.Card.ManaColor>> IFormatConfig.RestrictToColorIdentity => RestrictToColorIdentity;

	Quota IFormatConfig.MainDeckQuota => new Quota(MinMainDeckCardsExcludingCommandZone, MaxMainDeckCardsExcludingCommandZone);

	Quota IFormatConfig.CommandZoneQuota => new Quota(MinCommandZoneCards, MaxCommandZoneCards);

	Quota IFormatConfig.SideboardQuota => new Quota(MinSideboardCards, MaxSideboardCards);

	IReadOnlyList<Wizards.Arena.Enums.Card.CardType> IFormatConfig.DisallowedCardTypesForMainAndSideboard => DisallowedCardTypesForMainAndSideboard;

	IReadOnlyList<Wizards.Arena.Enums.Card.CardType> IFormatConfig.RestrictToCommandZoneCardType => RestrictToCommandZoneCardType;

	IReadOnlyDictionary<Rarity, Quota> IFormatConfig.RarityPerCardQuotas => RarityPerCardQuotas;

	IReadOnlyDictionary<uint, Quota> IFormatConfig.IndividualCardQuotas => RestrictedTitleIds.ToDictionary((KeyValuePair<uint, Quota> kvp) => kvp.Key, (KeyValuePair<uint, Quota> kvp) => new Quota(kvp.Value.Min, kvp.Value.Max));

	IReadOnlyCollection<uint> IFormatConfig.AllowedCardTitleIds => LegalTitleIds.ToList();

	IReadOnlyCollection<uint> IFormatConfig.BannedCardTitleIds => BannedTitleIds;

	IReadOnlyCollection<uint> IFormatConfig.SuspendedCardTitleIds => SuspendedCardTitleIds;

	IReadOnlyCollection<uint> IFormatConfig.AllowedCommanderTitleIds => AllowedCommanders;

	IReadOnlyCollection<uint> IFormatConfig.BannedAsCommanderTitleIds => BannedAsCommanders;

	public DeckFormat(IFormatManager formatManager, IClientLocProvider localizationManager)
	{
		_formatManager = formatManager;
		_localizationManager = localizationManager;
	}

	public bool IsCardBanned(uint titleId)
	{
		return BannedTitleIds.Contains(titleId);
	}

	public bool IsCardBannedAsCommander(uint titleId)
	{
		return BannedAsCommanders.Contains(titleId);
	}

	public bool IsCardRestricted(uint titleId)
	{
		return RestrictedTitleIds.ContainsKey(titleId);
	}

	public int GetRestrictedQuotaMax(uint titleId)
	{
		if (RestrictedTitleIds.TryGetValue(titleId, out var value))
		{
			return value.Max;
		}
		return -1;
	}

	public bool IsCardLegal(uint titleId)
	{
		if (LegalTitleIds.Count != 0)
		{
			return LegalTitleIds.Contains(titleId);
		}
		return true;
	}

	public static bool CardMatchesCommanderColors(CardPrintingData card, CardColorFlags commanderColors)
	{
		if (commanderColors == CardColorFlags.None && card.IsBasicLand)
		{
			return true;
		}
		return (card.ColorIdentityFlags & ~commanderColors) == 0;
	}

	public bool AllowedCommander(CardPrintingData commander)
	{
		HashSet<uint> allowedCommanders = AllowedCommanders;
		if (allowedCommanders != null && allowedCommanders.Count > 0 && !AllowedCommanders.Contains(commander.TitleId))
		{
			return false;
		}
		return CardCanBeCommander(commander);
	}

	public static bool CardCanBeCommander(CardPrintingData commander)
	{
		if (commander.Supertypes.Contains(Wotc.Mtgo.Gre.External.Messaging.SuperType.Legendary))
		{
			if (commander.Types.Any((Wotc.Mtgo.Gre.External.Messaging.CardType t) => t == Wotc.Mtgo.Gre.External.Messaging.CardType.Artifact) && commander.Subtypes.Any((Wotc.Mtgo.Gre.External.Messaging.SubType t) => t == Wotc.Mtgo.Gre.External.Messaging.SubType.Vehicle || t == Wotc.Mtgo.Gre.External.Messaging.SubType.Spacecraft) && (commander.Power.DefinedValue.HasValue || !string.IsNullOrEmpty(commander.Power.RawText)) && (commander.Toughness.DefinedValue.HasValue || !string.IsNullOrEmpty(commander.Toughness.RawText)))
			{
				return true;
			}
			if (commander.Types.Any((Wotc.Mtgo.Gre.External.Messaging.CardType t) => t == Wotc.Mtgo.Gre.External.Messaging.CardType.Planeswalker || t == Wotc.Mtgo.Gre.External.Messaging.CardType.Creature))
			{
				return true;
			}
		}
		return false;
	}

	public static bool CardCanBePartner(CardPrintingData partner)
	{
		if (partner.Supertypes.Contains(Wotc.Mtgo.Gre.External.Messaging.SuperType.Legendary))
		{
			return partner.HasPartnerAbility();
		}
		return false;
	}

	public string GetLocalizedName()
	{
		string key = "MainNav/DeckFormat/" + FormatName;
		string text = _localizationManager.GetLocalizedText(key);
		if (string.IsNullOrEmpty(text))
		{
			text = "LOCME: " + FormatName;
		}
		return text;
	}

	public string GetLocalizedInfo()
	{
		string key = "MainNav/DeckFormatInfo/" + FormatName;
		string text = _localizationManager.GetLocalizedText(key);
		if (string.IsNullOrEmpty(text) || text.Contains("DeckFormatInfo"))
		{
			text = "";
		}
		return text;
	}

	public SetAvailability GetAvailability(IReadOnlyList<CardPrintingData> printings)
	{
		if (_setAvailabilities == null)
		{
			_setAvailabilities = ((_formatManager != null) ? _formatManager.GetSetAvailabilities() : new Dictionary<string, SetAvailability>());
		}
		List<CardPrintingData> list = printings.Where((CardPrintingData x) => _setAvailabilities.ContainsKey(x.DigitalReleaseSet) || _setAvailabilities.ContainsKey(x.ExpansionCode)).ToList();
		if (list.Count <= 0)
		{
			return SetAvailability.EternalOnly;
		}
		SetAvailability setAvailability = SetAvailability.HistoricOnly;
		foreach (CardPrintingData item in list)
		{
			SetAvailability setAvailability2 = _formatManager?.GetSetAvailabilityForPrinting(item) ?? SetAvailability.HistoricOnly;
			switch (setAvailability2)
			{
			case SetAvailability.AlchemyNotStandard:
				if (setAvailability == SetAvailability.StandardNotAlchemy)
				{
					goto IL_00a9;
				}
				if (setAvailability != SetAvailability.EternalOnly && setAvailability != SetAvailability.HistoricOnly)
				{
					break;
				}
				goto IL_00d6;
			case SetAvailability.StandardNotAlchemy:
				if (setAvailability == SetAvailability.AlchemyNotStandard)
				{
					goto IL_00a9;
				}
				if (setAvailability != SetAvailability.EternalOnly && setAvailability != SetAvailability.HistoricOnly)
				{
					break;
				}
				goto IL_00d6;
			case SetAvailability.RotatingOutSoonStandard:
			case SetAvailability.RotatingOutSoonAlchemy:
				setAvailability = setAvailability2;
				break;
			case SetAvailability.Available:
				if (setAvailability == SetAvailability.AlchemyNotStandard || setAvailability == SetAvailability.StandardNotAlchemy || setAvailability == SetAvailability.EternalOnly || setAvailability == SetAvailability.HistoricOnly)
				{
					setAvailability = setAvailability2;
				}
				break;
			case SetAvailability.EternalOnly:
				{
					if (setAvailability == SetAvailability.HistoricOnly)
					{
						setAvailability = setAvailability2;
					}
					break;
				}
				IL_00d6:
				setAvailability = setAvailability2;
				break;
				IL_00a9:
				setAvailability = SetAvailability.Available;
				break;
			}
		}
		return setAvailability;
	}
}
