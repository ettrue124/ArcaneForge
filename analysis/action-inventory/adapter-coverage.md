# Arena adapter coverage

This implementation report is separate from the extracted protocol inventory. A declared enum is not proof of a gameplay feature. No entry claims full visual verification.

Real Forge tests pass for scry placement, mode/card choices, divided damage, trample damage, X/fight spells, MDFC land faces and Adventure spell faces. Treasure/Food/creature tokens, counterspell stack targets and multiple attackers per blocker also pass real Forge tests. The installed Arena request constructors, token animation parsers, player counter state and response mappings are tested without a Unity scene.

| Message | Implementation | Adapter / limitation |
| --- | --- | --- |
| None | declared only / no recovered handler | No adapter claimed. |
| GameStateMessage | native adapter | ForgeEncoder.cs / BattleUpdates.cs |
| ActionsAvailableReq | native adapter | BattlePresentation.cs / BattleActionTypes.java |
| ChooseStartingPlayerReq | native adapter | BattleRequests.cs |
| ConnectResp | native adapter | MatchDoorHost.cs |
| GetSettingsResp | native adapter | BattleFlow.cs |
| SetSettingsResp | native adapter | BattleFlow.cs |
| GroupReq | native adapter | BattleGuiRequests.cs / BattlePrompts.java (scry top/bottom) |
| IllegalRequest | not implemented | No adapter claimed. |
| MulliganReq | native adapter | MatchDoorHost.cs / LondonMulligan.java |
| OrderReq | native adapter | BattleGuiRequests.cs / BattlePrompts.java (card order) |
| PromptReq | not implemented | No adapter claimed. |
| RevealHandReq | declared only / no recovered handler | No adapter claimed. |
| SelectNReq | native adapter | BattleGuiRequests.cs / BattleRequests.cs (entities and labeled options) |
| AllowForceDraw | declared only / no recovered handler | No adapter claimed. |
| BinaryGameState | not implemented | No adapter claimed. |
| DeclareAttackersReq | native adapter | BattleRequests.cs |
| SubmitAttackersResp | declared only / no recovered handler | No adapter claimed. |
| DeclareBlockersReq | native adapter | BattleRequests.cs / BattleInputs.java (multiple attackers per blocker) |
| SubmitBlockersResp | declared only / no recovered handler | No adapter claimed. |
| AssignDamageReq | native adapter | BattleGuiRequests.cs / BattlePrompts.java |
| AssignDamageConfirmation | declared only / no recovered handler | No adapter claimed. |
| OrderCombatDamageReq | declared only / no recovered handler | No adapter claimed. |
| OrderDamageConfirmation | declared only / no recovered handler | No adapter claimed. |
| SelectTargetsReq | native adapter | BattleRequests.cs / BattleInputs.java / BattlePrompts.java (entities and stack spells/abilities) |
| SubmitTargetsResp | declared only / no recovered handler | No adapter claimed. |
| PayCostsReq | alternate workflow | Forge payment input uses mana actions, bounded numbers, card selections, and confirmations. |
| IntermissionReq | native adapter | BattleCompletion.cs (end-of-game result) |
| DieRollResultsResp | declared only / no recovered handler | No adapter claimed. |
| SelectReplacementReq | native adapter | BattleNativeMenus.cs / BattlePrompts.java; native parser/response checks, with labeled fallback where ability identity cannot be resolved. |
| SelectNGroupReq | native adapter | BattleNativeMenus.cs / BattlePrompts.java; native parser/response checks, with labeled fallback where ability identity cannot be resolved. |
| DistributionReq | native adapter | BattleGuiRequests.cs / BattlePrompts.java |
| NumericInputReq | native adapter | BattleRequests.cs / ForgeInteractiveSession.java |
| SearchReq | native adapter | BattleNativeMenus.cs / BattlePrompts.java; native parser/response checks, with labeled fallback where ability identity cannot be resolved. |
| OptionalActionMessage | native adapter | BattleRequests.cs |
| CastingTimeOptionsReq | native adapter | BattleRequests.cs / BattleNativeMenus.cs (X and resolvable modal choices) |
| SelectFromGroupsReq | native adapter | BattleNativeMenus.cs / BattlePrompts.java; native parser/response checks, with labeled fallback where ability identity cannot be resolved. |
| SearchFromGroupsReq | native adapter | BattleNativeMenus.cs / BattlePrompts.java; native parser/response checks, with labeled fallback where ability identity cannot be resolved. |
| GatherReq | native adapter | BattleNativeMenus.cs / BattlePrompts.java; native parser/response checks, with labeled fallback where ability identity cannot be resolved. |
| QueuedGameStateMessage | not implemented | No adapter claimed. |
| UIMessage | not implemented | No adapter claimed. |
| SubmitDeckReq | native adapter | BattleNativeMenus.cs / BattlePrompts.java; native parser/response checks, with labeled fallback where ability identity cannot be resolved. |
| EdictalMessage | not implemented | No adapter claimed. |
| TimeoutMessage | not implemented | No adapter claimed. |
| TimerStateMessage | not implemented | No adapter claimed. |
| SubmitDeckConfirmation | declared only / no recovered handler | No adapter claimed. |
| StringInputReq | native adapter | BattleGuiRequests.cs / BattlePrompts.java |
| SelectCountersReq | native adapter | BattleNativeMenus.cs / BattlePrompts.java; native parser/response checks, with labeled fallback where ability identity cannot be resolved. |
| PredictionResp | declared only / no recovered handler | No adapter claimed. |

## Action values

The bridge now distinguishes ordinary casts/activations/land plays/mana, pass, turning face up, split-card halves, Adventure, modal double-faced backs, Prototype, Rooms and Omen from Forge state. Test/placeholder and specialized payment action values are not sent blindly.

## Remaining

- Stock Arena visual workflows and all card variants need playtesting.
- Best-of-three sideboarding is not supported.
- Generic amount prompts do not transmit per-recipient maximums to Arena; Forge validates them.
- Arbitrary non-card ordering uses click-selection order; its native UI order is unverified.
- No full official-server gameplay/animation parity is claimed.
