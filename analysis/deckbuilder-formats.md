# Local Arena deck-builder format contracts

This audit reads the updated local client assemblies and installed card database. It does not contact official services. `deckbuilder-formats.json` supplies an explicit local sandbox fixture; it does not recover current official Historic legality, bans, suspended cards or rebalanced rules.

## Recovered commands and transport

`Wotc.Mtga.Network.ServiceWrappers.AwsFormatsServiceWrapper.GetFormatData` sends **`CmdType.GetFormats`**, with empty `GetFormatsReq`, and expects `Wizards.Arena.Models.Network.GetFormatsResponse`. The response contains `Formats: List<FormatConfigInfo>` and `FormatGroups: List<FormatGroup>`. There are no observed `FormatGetFormats` or `FormatGetDeckFormats` requests in this wrapper. Do not add guessed command names.

`FrontDoorConnectionAWS.GetCardSet` sends **`CmdType.CardGetCardSet`**, with `{ "SetCode": "MIR" }`, and expects `CardsAndQuantity`, a dictionary of decimal card `grpId` strings to integer owned quantities. The response is the dictionary itself, without a `cards` envelope or cache version. Local `server/arena.py` now implements that exact envelope, selecting known primary non-token cards whose installed `arenaMetadata.ExpansionCode` matches `SetCode`. Set-to-product or DigitalReleaseSet collation mappings were not recovered as part of this command; the adapter does not guess that a product code is an expansion code.

`AwsInventoryServiceWrapper.GetPlayerCards` sends **`CardGetAllCards`** with capitalized `{ "CacheVersion": 0 }`. Its response is `{ "cacheVersion": integer, "cards": { "grpId": quantity } }`. The catalogue already exists locally; these responses grant the dedicated local sandbox collection, rather than downloading definitions of card text or artwork.

`AwsDeckServiceWrapper` uses `DeckGetDeckSummariesV3`, `DeckGetDeck`, `DeckUpsertDeckV3` and `DeckDeleteDeck`. `DeckActionType` has `StringEnumConverter`: native JSON upserts can carry **`"ActionType": "Imported"`** or `"Updated"`, not merely an integer. Adapter support was corrected during this audit. The exact enum names are `None`, `CreatedNew`, `Imported`, `Cloned`, `CopyLimited`, `PreconCopied`, `Exported`, `Updated`; their integer values are -1 through 6.

## Why a nonempty format fixture matters

`FormatManagerCore.SetupFormats` builds a name-to-format map and picks the first sorted format as `_defaultFormat`. It looks for these exact format-group names:

- `ConstructedSortOrder`: orders available formats.
- `EvergreenFormats`: marks formats as evergreen and exposes them to normal deck-building flows.
- `BannedFormats`: controls ban-update popup format selection.

An empty `GetFormatsResponse` leaves the client without a usable default format or dropdown entry. Merely storing deck-summary `{name:"Format", value:"Historic"}` does not create the corresponding format definition. `FormatUtilities.HISTORIC_FORMAT` confirms the `Historic` identifier and `DeckFormat.GetLocalizedName` uses `MainNav/DeckFormat/Historic`.

`FormatManagerCore.BuildDeckFormats` reads these operational fields from `FormatConfigInfo`:

| Field | Recovered behavior |
| --- | --- |
| `Name` | Matches deck summary Format attribute and localization identifier. |
| `FormatTypeInternal` | 3 is `EFormatType.Constructed`; 0 None, 1 Draft, 2 Sealed. |
| `CardCountRestrictionInternal` | 0 None maps to four copies by title; 1 Singleton to one; 2 UnrestrictedCardCounts to 250. |
| `SideboardBehaviorInternal` | 0 Normal; 1 CompanionOnly. |
| `MainDeckQuota` | Client default min 60, max 250. |
| `SideBoardQuota` | Exact capital B in the generated C# property; client default min 0, max 0. |
| `CommandZoneQuota` | Client default min 0, max 0. |
| `LegalSets` | Set of expansion codes used to build legal title IDs. |
| `FilterSets` | Set codes for filtering UI. |
| `AllowedTitleIds` | Additional card title IDs, distinct from grpIds. |
| `BannedTitleIds`, `SuspendedTitleIds` | Removed from computed legal titles; suspended titles also become banned. |
| `SupressedTitleIds` | Exact misspelling in generated DTO; separate suppression metadata. |
| `UseRebalancedCards` | Selects the rebalanced member of linked title/card data. |
| `IndividualCardQuotas` | Map from title ID to min/max quota. |

The sidecar's protobuf JSON fixture uses lower-camel field names such as `formatTypeInternal`, `sideBoardQuota` and numeric internal enum properties. The generated .NET DTO exposes both typed wrappers and those integer backing properties. Preserve this fixture encoding through the protocol host and verify real-client parsing; do not replace it with enum strings of invented names.

## Empty LegalSets does not mean all cards

`DatabaseUtilities.GetTitleIdsLegalForFormatData` enumerates primary printings and applies `FormatManagerUtils.IsCardLegalForFormat`. The predicate first rejects non-primary cards, selects a rebalanced member when linked, always allows unlimited basic lands, applies Pauper/Artisan rarity checks if named accordingly, then checks **`allowedSets.Contains(card.ExpansionCode)`**. Thus an empty `LegalSets` list normally leaves only basic lands legal. The built-in debug `GenAllCardsFormat` constructs an empty list too; its name is not evidence that an empty server list grants every card.

The fixture derives **217 expansion codes from 20,128 primary non-token entries** in the actual extracted `data/catalogue.json`, records its SHA-256, and provides those codes as both `LegalSets` and `FilterSets`. It sets `UseRebalancedCards=false` and provides no invented banned/suspended IDs. The main quota and four-copy baseline come from recovered `FormatUtilities` defaults. The sideboard remains the recovered default of zero: supporting sideboard decks requires an explicit local quota policy, rather than silently labeling a guessed value as official Historic data.

## Limits and integration

The installed `Raw_CardDatabase_8d9a18c7ea752803bb93e190da54707e.mtga` extraction contains no SQLite table whose name includes `format`; no official format configuration was recovered from that source. Network-backed `GetFormats` is the source used by the client. The fixture deliberately uses the recognized `Historic` name for a broad local sandbox entry and must be described as local sandbox policy, not current official Historic legality. Digital-only and unlinked rebalanced cards may still need explicit coverage/legality policy; Forge engine name mapping alone does not verify equivalence.

Protocol fixture-loader shape is `{ "commands": { "GetFormats": { "format": "json", "payload": { ...response... } } } }`, included alongside audit metadata in `analysis/deckbuilder-formats.json`. Other commands remain served by the authenticated Arena adapter. The next native-client checks are: format dropdown/default resolves Historic; collection displays nonbasic cards; deck import sends the recovered string ActionType; save returns the same GUID and survives reload. None of these is evidence of live match playability.
