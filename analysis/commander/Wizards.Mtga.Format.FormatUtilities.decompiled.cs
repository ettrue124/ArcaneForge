using Wizards.Mtga.FrontDoorModels;

namespace Wizards.Mtga.Format;

public static class FormatUtilities
{
	public const int DEFAULT_MIN_MAIN_DECK_CARDS = 60;

	public const int DEFAULT_MAX_MAIN_DECK_CARDS = 250;

	public const int DEFAULT_MIN_COMMAND_ZONE_CARDS = 0;

	public const int DEFAULT_MAX_COMMAND_ZONE_CARDS = 0;

	public const int DEFAULT_MIN_SIDEBOARD_CARDS = 0;

	public const int DEFAULT_MAX_SIDEBOARD_CARDS = 0;

	public const int DEFAULT_MAX_SINGLE_CARD = 4;

	public const int DEFAULT_MAX_SINGLE_CARD_SINGLETON = 1;

	public const int DEFAULT_MAX_SINGLE_CARD_UNRESTRICTED = 250;

	public const string HISTORIC_FORMAT = "Historic";

	public const string TIMELESS_FORMAT = "Timeless";

	public const string STANDARD_FORMAT = "Standard";

	public const string ALCHEMY_FORMAT = "Alchemy";

	public const string EXPLORER_FORMAT = "Explorer";

	public const string BRAWL_FORMAT = "Brawl";

	public const string ARTISAN_FORMAT = "Artisan";

	public const string HISTORIC_BRAWL_FORMAT = "HistoricBrawl";

	public const string DIRECT_GAME_FORMAT = "DirectGame";

	public const string TRADITIONAL_STANDARD_FORMAT = "TraditionalStandard";

	public const string TRADITIONAL_HISTORIC_FORMAT = "TraditionalHistoric";

	public const string TRADITIONAL_TIMELESS_FORMAT = "TraditionalTimeless";

	public const string TRADITIONAL_ALCHEMY_FORMAT = "TraditionalAlchemy";

	public const string TRADITIONAL_EXPLORER_FORMAT = "TraditionalExplorer";

	public const string PAUPER_FORMAT = "Pauper";

	public const string SINGLETON_FORMAT = "Singleton";

	public const string DIRECT_GAME_LIMITED_FORMAT = "DirectGameLimited";

	public const string DIRECT_GAME_ALCHEMY_FORMAT = "DirectGameAlchemy";

	public const string DIRECT_GAME_LIMITED_REBALANCED_FORMAT = "DirectGameLimitedRebalanced";

	public const string DIRECT_GAME_BRAWL_FORMAT = "DirectGameBrawl";

	public const string DIRECT_GAME_BRAWL_REBALANCED_FORMAT = "DirectGameBrawlRebalanced";

	public const string AMBIGUOUS_FORMAT = "Ambiguous";

	public static bool IsAmbiguous(string formatName)
	{
		return formatName == "Ambiguous";
	}

	public static bool IsLimited(MDNEFormatType formatType)
	{
		if (formatType != MDNEFormatType.Draft)
		{
			return formatType == MDNEFormatType.Sealed;
		}
		return true;
	}

	public static int FormatSortOrderComparator(DeckFormat a, DeckFormat b)
	{
		return a.SortOrder.CompareTo(b.SortOrder);
	}
}
