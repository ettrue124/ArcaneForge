# Backend gap audit (2026-11-17)

## Gameplay (match messages)

Read-only. REA `inspect_managed_members` on the installed
`Wizards.MDN.GreProtobuf.dll` (SHA-256 52bf497a…, MVID 4e4ff3f7…):
- `ClientMessageType` has 47 values (evidence `ev_1aabb3ba…`).
- `GREMessageType` (server to client) was enumerated from the same assembly.

Each value was compared with references in the decompiled client and in the
non-test `protocol-host` sources. Branches were then checked by hand in
`MatchDoorHost.cs`, which dispatches the `ClientToGREMessage` handling.

**Key behaviour:** the dispatch is a long if/else chain with no final `else`.
A message it doesn't recognise is silently ignored, with no reply. Arena can
then sit waiting.

### Arena sends these during a match; the server ignores them
| Message | What it's for | Impact |
|---|---|---|
| Uimessage (`ClientToGreuimessage`) | Emotes and UI signals | In PvP the opponent never sees your emotes. |
| EnterSideboardingReq | Opening the sideboard between games (Bo3) | Client-initiated sideboard view does nothing. Engine-driven `InputSubmitDeck` sideboarding is handled. |
| EffectCostResp | Answer to an effect-cost prompt | Harmless today, because the server never sends that prompt. |
| AutoResp | Client automatic replies | Unverified. Not seen in kept traces. |
| CheckpointReq | Client checkpoint | Unverified purpose. |

(`OptionalActionResp` is handled through the `OptionalResp` field.
`ConnectReq` is replaced by `ClientToMatchDoorConnectRequest`.
`ForceDrawReq`, `ControlReq`, `AllowForceDrawResp`, `RevealHandResp`,
`OrderCombatDamageResp`, `DrawCardResp`, `TakeTimeoutReq` and `PredictionReq`
aren't referenced by client code.)

### Arena supports these; the server never sends them
| Message | Arena feature not used |
|---|---|
| IllegalRequest | "That action isn't allowed" feedback. The server silently re-sends the current prompt instead. |
| DieRollResultsResp, plus die-roll annotations | Dice-roll result display for roll-a-die cards. Not checked whether annotations alone cover it. |
| RevealHandReq | Arena's dedicated "reveal your hand" prompt. |
| StringInputReq | Typed text input. Arena can answer it (StringInputResp is handled), but the server never asks. How Forge "name a card" choices are presented needs checking. |
| SubmitAttackersResp, SubmitBlockersResp, SubmitTargetsResp, AssignDamageConfirmation | Confirmations after submitting attacks, blocks, targets or damage. The server pushes a fresh state instead. Possible cause of leftover UI. |
| PromptReq, EdictalMessage | Generic prompt text, and notices of actions the server forced. |
| OrderCombatDamageReq, OrderDamageConfirmation | Damage-assignment order. Obsolete under the current combat rules. |
| QueuedGameStateMessage, BinaryGameState, PredictionResp, AllowForceDraw | Transport optimisations and debug. Not needed. |

### Prompt screens: native versus generic (checked 2026-11-17)

Arena chooses a dedicated screen from the request's type and selection
properties (`SelectNWorkflowTranslation`). The server was compared against
that logic, and against the menu titles found in all saved traces in
`analysis/arena-traces`.

- **Fixed:** London mulligan bottoming was a generic "Choose a card" SelectN.
  It now sends `GroupReq` with `GroupingContext.LondonMulligan`, which opens
  Arena's London browser.
- **Already native:** attacks, blocks, targets, mana payment and auto-tap,
  mulligan keep/mull, scry/surveil, card ordering (top/bottom), combat damage,
  distribution, gather, search and search-from-groups, select-from-groups,
  counters, modal spells, mana type, replacement effects, piles, submit deck,
  and Phyrexian/cost choices (CastingTimeOptions). Colours, counter types and
  card/super/sub types use static lists, and card names use `CardNames`.
- **Correctly generic:** votes (Arena has no voting screen); discards (Arena's
  discard browser only opens for 15+ cards with more than 7 to discard,
  otherwise it uses the same in-hand selection).
- **Remaining mismatch: trigger ordering.** "Order your triggers: choose the
  one to resolve first" is a repeated labelled menu. Arena's native
  `TriggeredAbilityWorkflow` (SelectN with `Context TriggeredAbility` and
  `OptionContext Stacking`) shows the triggers as cards to drag into order, but
  it needs a card view for every trigger ID. Forge asks for the order before
  the triggers exist as stack objects, so this needs pending-trigger objects in
  the encoded game state. Not done: risky without a live check.
- **Minor, unverified:** non-mana "Choose a color" (`nativeKind color`) is sent
  as `StaticList.ManaColors`. Arena's colour picker is the same, but its
  header may read as a mana choice. `StaticList.Colors` would be the exact
  match.

### Animations and indicators (checked 2026-11-17)

Arena's `AnnotationType` has 118 values. The server sends 65. Arena's client
has parsers for 42 that the server never sends (`AnnotationParserFactory`).

- **Added:** GainDesignation and LoseDesignation, from the change in persistent
  Designation annotations between boards (monarch, initiative, city's blessing,
  monstrous, renowned...).
- **Deliberately not added:** PowerToughnessModCreated. Arena already animates
  stat changes from board diffs; `BattlePlaybackTests` checks they fire once,
  so a burst annotation would play twice.
- **Added with bridge data (2026-11-17):**
  - TemporaryPermanent: card JSON `temporary`, from Forge SVar `EndOfTurnLeavePlay`
    (unearth, Dash, Blitz, Warp, end-of-turn sacrifice tokens).
  - SuspendLike: `suspended` (exiled, Suspend, time counters), with the Suspend
    keyword ability ID.
  - GamewideHistoryCount: player `stormCount` (all spells cast this turn),
    tagged with the Storm keyword ability ID. Arena shows the badge only where
    its card data configures a count for that ability.
  - Built in `BattlePresentation.StateTemporaryAndSuspend`. Not yet seen in play.
- **Still open** (Forge events that exist but aren't
  forwarded: GameEventCardStatsChanged, GameEventTokenCreated,
  GameEventCardCounters, GameEventDayTimeChanged, GameEventDoorChanged,
  GameEventPlayerControl, GameEventCardForetold, GameEventCardPlotted, ...):
  - one-off animations: TokenImmediatelyDied, RevealedCardCreated,
    LayeredEffectCreated, ModifiedType, RemoveAbility, TurnPermanent,
    LinkedDamage, CopyException, LoopCount, PendingEffect, SyntheticEvent
  - lasting indicators: TemporaryPermanent ("leaves at end of turn"),
    SuspendLike, DungeonStatus, AbilityExhausted, SwitchPowerToughness,
    ColorProduction, GamewideHistoryCount, SupplementalText,
    DelayedTriggerAffectees, ReferencedObjects, GenericRelationship,
    HiddenInformation, ObjectsSelected
  - not relevant: BoonInfo (Alchemy only), ManaDetails, ShouldntPlay,
    HighlightReason, PredictedDirectDamage and other UI hints.

### Suggested gameplay order (not started)
1. Log every match message that falls through the chain, so live play shows
   which gaps actually happen. Then send an IllegalRequest instead of staying
   silent.
2. Relay emotes (UI messages) between players in PvP.
3. Dice-roll results display.
4. Check card-naming and typed-text prompts against StringInputReq.

This is protocol coverage only. Forge rules accuracy and how the server
presents each Forge input are separate. The menu audit is below.

# Arena front-door requests

Read-only audit. No code changed.

## Method and evidence

- Request list: REA `inspect_managed_members` on the installed
  `MTGA_Data/Managed/Wizards.Arena.Models.Protobuf.dll`
  (SHA-256 3e615db8…, MVID a3175fc5…, Arena 0.1.14852.1341982), type
  `Wizards.Arena.Models.Network.CmdType`. Evidence `ev_ab7773c9e93b…`:
  215 fields, i.e. 214 request types plus the enum's value field.
- Which of those the client can send: `CmdType.<Name>` references in the
  decompiled Windows client (`data/ios/windows-reconstruction/managed-projects`,
  excluding the models assembly). **129 of 214** are referenced. The other 85
  look obsolete or server-only. Note that "referenced" means the code can send
  the request, not that a menu reaching it was observed.
- What the server answers: explicit `CmdType.X` branches in `protocol-host`,
  `Startup.BackendCommands` (same set as `server/arena.py` `COMMANDS`), and
  fixture replies from `analysis/startup-fixtures.json` and
  `analysis/deckbuilder-formats.json`.
- Anything else falls through to `Diagnostics.Reply`, which returns
  `MethodNotFound`. Arena shows that as an error or a feature that does nothing.
- Runtime logs: only `LogBusinessEventsV2` and `MercGetStoreStatusV2` appear
  in kept logs, because the host console isn't saved. So the priority below is
  judged from the UI feature each request belongs to, not from observed traffic.

## Result

**80 requests the client can send get "method not found".** (`CardGetCardSet`
is handled through `BackendCommands`.)

### Likely noticed in normal play
| Feature | Missing requests |
|---|---|
| Direct Challenge (friend match from the Social panel) | ChallengeJoin, ChallengeInvite, ChallengeReady, ChallengeUnready, ChallengeSetSettings, ChallengeSetPlayerSettings, ChallengeIssue, ChallengeStartLaunchCountdown, ChallengeClose, ChallengeKick, ChallengeSendMessage, DcIssueChallenge. Only Create and Exit are handled. |
| Cosmetics and card styles | CosmeticsGetPlayerOwnedCosmetics, CosmeticsGetPreferredCosmetics, CosmeticsSetPreferredSleeve, CosmeticsSetPreferredPet, CosmeticsSetPreferredTitle, CosmeticsSetPreferredEmotes, AccountSetAvatar, SetPreferredPrinting, RemovePreferredPrinting |
| Event results and choices | EventClaimPrize, EventGetMatchResultReport, EventSetChoice, EventSetJumpStartPacket |
| Precon decks | DeckGetPreconDeckV2. The list request is a fixture; fetching one deck fails. |

### Drafting (no draft modes work)
BotDraftStartDraft, BotDraftDraftPick, BotDraftDraftStatus, DraftCompleteDraft,
EventJoinDraft, EventJoinDraftQueue, EventLeaveDraftQueue,
EventPlayerDraftReadyPlayer, EventPlayerDraftGetTablePacks,
EventPlayerDraftMakePick, EventPlayerDraftConfirmCardPoolGrant,
EventPlayerDraftReserveCard, EventPlayerDraftClearReservedCard

### Store, currency and progression
MercGetStoreStatusV2, MercGetSkusV3, MercGetListingsV5, MercGetSkusAndListings,
MercPurchaseV2, CurrencyGetCurrencies, BoosterGetOwnedBoosters,
BoosterOpenBoosters, CardRedeemWildCards, CardCompleteVault, QuestSwapQuest,
RenewalRedeemRewards, GraphProcessV2, GraphSetFavoriteNodes

### Rarely used or newer social features
- Gatherings (13): Join, Exit, Invite, Close, ReconnectAll, Kick,
  UpdateMetadata, AssignRole, RemoveRole, GenerateInviteCode, ClearInviteCode,
  JoinByInviteCode, CreateV2
- Tournaments (5): Create, DropPlayer, GetState, PlayerReadyRound,
  PlayerReadyStart
- Mail and notifications: MarkLetterRead, ClaimAttachment,
  SetOrganizationNotificationPreference, SetGlobalWpnNotificationPreference
- Other: ReportPlayer, Attach, GetConflictingAccountsData, MosaicAuxiliary
- Telemetry: LogBusinessEvents. It should just return success; the V2 form is
  already a fixture.

## Answered, but only with fixed sample data (24)

These reply successfully, but nothing is saved per player:

DeckGetAllPreconDecksV3, EventGetActiveEvents(V2), EventGetCourses(V2),
EventGetActiveMatches, CarouselGetCarouselItems, StoreGetEntitlementsV2,
QuestGetQuests, RankGetCombinedRankInfo, RankGetSeasonAndRankDetails,
RankEvaluatePayoutsV2, PeriodicRewardsGetStatus, RenewalGetCurrentRenewal,
GetPlayBladeQueueConfig, GetPlayerPreferences, **SetPlayerPreferences** (client
settings saved to the server are thrown away), LogBusinessEventsV2,
GetNetDeckFolders, GetDesignerMetadata, StaticContent,
GetAllPreferredPrintings, GetAllPrizeWalls, GetKillSwitchData

## Suggested order (not started)

1. Silent success for telemetry and notification-preference requests, so they
   stop producing errors.
2. Save player preferences, and cosmetics/avatar/card-style choices (store
   what the client sends and return it).
3. Direct Challenge flow, for friend matches without the event queue.
4. Event prize and result requests (EventClaimPrize, EventGetMatchResultReport).
5. Bot draft, if drafting is wanted. This is the largest piece.

Not verified: whether each menu actually sends these in this build. Each item
needs a live trace before implementation.
