# Native Arena deckbuilder startup

The target is the installed Unity client's own deck creator. The fixtures in `startup-fixtures.json` are explicitly synthetic local account initialization data, not a recovered official campaign or functioning store. They never call official services. Live native-client validation is still required; source-compatible JSON is only one step.

## Confirmed cause of the initial black screen

`CampaignGraphManager.Initialized` checks `_definitions.Count > 0`. Returning `{GraphDefinitions:[]}` leaves this false. `NodeNpeStrategy.Initialize` waits up to 15 seconds for that condition, then records Error without setting its `Initialized` property. `Bootstrap.Coroutine_PostLogInSequence` waits indefinitely for `npe.Initialized`, so no StartHook request follows. This exactly explains a client that authenticates, requests GraphGetGraphDefinitions, and then stays black.

The local fixture supplies one real-schema graph named `NPE_Tutorial`, type `NPETutorial`, with a completed local `ManualComplete` node. `NodeGraphUtil.IsCompleted` checks whether any node referenced by the `Completed_Group` has state `Completed`. The matching GraphGetGraphState fixture supplies that state. `NodeNpeStrategy.configureFromState` then marks its Complete flag, and `TutorialRequired` becomes false unless `npe.force_tutorial` is explicitly enabled.

The graph has no rewards, matches, purchases, or tutorial implementation. `LocalCompleted` names a local completed-account marker; it is not claimed to be official tutorial content. No GraphProcess mutation is needed. The host must restrict GraphGetGraphState to its fixture's `requiredGraphId` and reject other graph IDs rather than return a universal fake success. The manager's later NewPlayerExperience/ColorChallenge strategies may request additional graphs; those must be investigated if observed, rather than blanket fulfilled.

## StartHookResponseV2 prerequisites

`AwsStartHookServiceWrapper.StartDataFromResponse` in `.tools/connection-StartHook.cs` converts the typed protobuf response. `InventoryInfo` must be nonnull: the startup controller aborts on a null resolved Inventory. The generated inventory collections/maps are initialized automatically, but include an empty CosmeticsClient to keep owned cosmetics coherent. PreferredCosmetics should also be nonnull: `CosmeticsProvider.SetVanitySelections` replaces its default object directly, so passing null can break later consumers. An empty PreferredCosmetics gives the built-in default emote order and safely nullable avatar/sleeve/pet selections.

Use a positive DeckLimit (for example 100), initial cache versions 1 for delivered sections, and current UTC ServerTimeInternal. Deck summaries and contents must come from the actual local saved deck backend. Inventory collection is fetched separately through CardGetAllCards. KillSwitchNotification, CardMetadataInfo, HomePageAchievements, TimewalkInfo, and ClientPeriodicRewards have conversion null guards. If periodic rewards are provided, their daily/weekly chest maps and valid timestamp wrappers must be populated. No fake economy is necessary for a local collection.

CardGetAllCards is a JSON CardsAndCacheVersion, exactly `{cacheVersion:1,cards:{"<grpId>":quantity}}`; both field names are lower camel case. CardsAndQuantity extends Dictionary<uint,int>. The request has `CacheVersion` capitalized. For the full local collection grant counts from the actual catalogue. The source card metadata distinguishes AlternateDeckLimit and basic land semantics; ownership counts are separate from deck legality.

## StaticContent format

StaticContent is **legacy JSON**, `Wizards.Unification.Models.Event.StaticContentResponse`, not a Network protobuf message. Each field is an optional `HashableObject<T>` with `{Hash:<int>,Object:<value>}`. Requests provide hashes: AvailableCosmeticsHash, SurveyConfigHash, CardNicknamesHash, EmergencyCardBansHash, AchievementsMetadataHash, QueueTipsHash. The concrete startup wrapper requests all six categories.

The fixture provides version/hash 1 for AvailableCosmetics (nonnull object with empty lists), SurveyConfigs, CardNicknames, EmergencyBannedCardTitleIds, and QueueTips. AchievementsMetadata remains absent because its post-processing consults achievement campaign sets and is unnecessary for the deck creator. Provider code checks each nullable `.Object` before using it. These are empty local content categories, not official service data.

## Verified additional startup commands

The fixture map includes only commands verified in exact service wrappers:

- GetPlayerPreferences returns `{Preferences:{Experience:"Advanced"}}`; the provider creates an empty dictionary if null, but Advanced activates the client's own skip-tour path when entering from NPE.
- GetPlayBladeQueueConfig and GetNetDeckFolders return empty lists.
- GetAllPreferredPrintings returns an empty dictionary.
- GetVoucherDefinitions returns `{VoucherDefinitions:[]}` in the supported legacy JSON branch.
- DeckGetAllPreconDecksV3 returns `{CacheVersion:1,PreconDecks:[]}`.
- RenewalGetCurrentRenewal requires `definitionId:null` and `currentId:null` to disable renewal cleanly. Empty strings are insufficient: RenewalManager considers any nonnull definition ID upcoming when ready=false.
- RankGetCombinedRankInfo uses PlayerRankingInfo with lower-camel fields. RankClass is Spark=-1, Bronze=0, Silver=1 onward; zero is not None. The fixture supplies local Bronze/tier4 with zero matches.
- RankGetSeasonAndRankDetails requires currentSeason nonnull, limitedRankInfo/constructedRankInfo arrays, and both season reward dictionaries. The fixture uses synthetic October 2026 dates and no prizes.
- StoreGetEntitlementsV2 is legacy JSON EntitlementsResponse, confirmed from IL. Its InventoryInfo is nonnull and empty economy, with ContainsFirstPurchase=false. This is not the protobuf EntitlementsResponse with a similar name.

## Feature toggles and exclusions

ClientFeatureToggleDataProvider defaults ClientPlayerInbox, ClientPrizeWall, WPNFavoriteStoreStatus, AchievementSceneStatus, SocialBladeStatus, and SocialChallengeStatus on. Its KillSwitch processing interprets true as disabled. Exact mapping: ClientPlayerInbox -> InboxStatus; Tournament -> TournamentStatus; most other keys remain unchanged. Disable only documented unsupported local features using these known keys. Do not guess a generic store-disabled key: it was not established by these sources.

Keep `npe.force_tutorial` false. Turning NPE_V2 off is not a substitute for the valid completed tutorial graph and can choose a different unsupported onboarding strategy. RingDoorbellV2 is true by default in this build; the local doorbell must handle `/api/v2/ring` or explicitly configure the toggle.

Advanced preference alone does not unblock the early bootstrap wait. The completed tutorial graph is required first. Later SparkyTourState depends on NewPlayerExperience and ColorChallenge strategies; it may also refresh events/courses when skipping. Missing graph data must be handled from observed exact requests and source, not guessed campaign success.

The fixture file's evidence links point to local ILSpy outputs under `.tools/rules-*.cs`. Those temporary inspection files and proprietary binaries remain local. No claim is made yet that the complete native deck editor has loaded or saved a deck; that requires the root agent's current UI verification.
