# Battle adapter audit — 2026-10-03

Target: Forge rules and AI inside the installed Arena client's match screen.

Implemented in this pass:

- Native target requests for cards and players, including per-click updates and separate Submit responses.
- Native attacker and blocker requests, preserving selected assignments and routing changes through Forge's controller.
- Automatic mana payment and manual available mana-source actions.
- Explicit Forge ability IDs carried in Arena action selections; playable alternatives, battlefield abilities, and other playable zones are enumerated instead of restricting play to one hand ability.
- Bounded entity/card choices through native SelectN requests; Forge validates choice membership and counts. Choice candidates temporarily expose the printing required by the selection browser.
- Combat presentation fields: summoning sickness, damage, attacking target, blocking assignments.
- Actual Forge win/draw information and game-over stage in GRE state; background MatchDoor polling recognizes game over.
- Match state uses the active Arena match ID rather than the bridge's default ID.
- Correlated engine rejections preserve the active subprocess; stale clicks no longer destroy a match. Malformed envelopes and timeouts still reset transport.
- Snapshot collection retries concurrent modifications for a bounded 200 ms and handles an absent active input.
- Poll failures are logged rather than silently retried forever.

Validation:

- Java bridge compiled against the pinned Forge dependencies.
- .NET BattleAudit build completed with zero warnings/errors; protobuf/frame/TLS and battle request mapping self-tests passed.
- 12 Python engine transport tests passed, including preserving the exact process after a valid rejection.
- Original real Forge test passed: two mulligans, bottoming two after Keep, land play and AI turn progression.
- Real Forge battle test completed a game with casting, mana payment, player targeting and attacking. An earlier symmetric-deck run additionally exercised blocking. Test opponents now use a stable land-only deck to avoid losing before the attack path is exercised.
- Actual Arena launch reached the home/deck-selection screen and submitted an AI match request. Its window disappeared before MatchDoor battle validation. Player.log showed an error joining the ladder queue and an AchievementManager exception during shutdown; this is not evidence of a successful native battle.

Remaining / unverified:

- No complete native Arena battle has been verified. Native action grouping/ability display, targeting interactions, selection browsers, payment controls and combat workflows need screen-level regression.
- Generic blocking Forge prompts (option dialogs, mode lists, confirmations, numeric/text input, ordering and damage distributions) do not yet have complete native GRE adapters.
- Divided targeting is explicitly rejected; advanced mana-cost symbols are omitted rather than represented as free.
- Multiblocking, combat taxes/requirements, special attack recipients, manual combat damage ordering/assignment need dedicated adapters.
- Tokens and unmapped generated cards remain omitted from the encoder; stack abilities, counters, attachments, transformed/copy characteristics and hidden-information workflows remain incomplete.
- Client stops/yields/full control and settings are not fully mapped to Forge.
- Snapshot retries prevent common collection exceptions, but do not establish an atomic game-thread snapshot boundary.
- Match authentication, single-session ownership, reconnect and shutdown cleanup need hardening before multiple users or remote access.

Do not label the installation fully playable or all features fixed based solely on these engine tests.

Recording regression (20261004-0418-37.2592301.mp4):

- Green deck, printed costs and rules text appear in the recording. Arena sends a Forest mana activation, then receives full states without another actionable request.
- Reproduced the cause in real Forge: a blocking getAbilityToPlay picker with exactly one Forest mana ability. Resolve that single option in the bridge; multiple-option prompts still require an adapter.
- Added authoritative floating mana pools to Forge snapshots and GRE PlayerInfo, including color, source and count.
- test_manual_mana.py passed: manual Forest activation, floating green mana, Llanowar Elves cast/resolution and priority restored, with no hidden blocking prompt. Protocol self-tests passed including mana pool translation.
- These are engine and protocol regressions. A fresh native Arena match is still needed to verify the visible controls after this fix.

Arena battle presentation and priority update:

- Legal actions now include Forge dry-run mana sources and fully floating payments. Native ActionExtensions.CanAffordToCast requires AutoTapSolution for nonzero-cost cards; a test against the installed DLL confirms the translated action passes that check.
- Full board states also contain ActionInfo entries, supporting card ability/display refreshes between prompts.
- Per-connection board history generates tap/untap and categorized zone-transfer annotations for native playback. No duplicate tap annotation is emitted for unchanged state.
- SetSettingsReq is acknowledged and its pass mode, phase stops, Full Control and mana-payment stops drive the bridge. Arena's recorded default ResolveMyStackEffects also skips empty priority windows. Normal payment auto-confirms through Forge. Mulligan/target/mode choices and combat with eligible combatants are preserved.
- Snapshot deduplication now considers actual board/input data rather than suppressing every change sharing a button ID.
- Verified: protocol self-tests (including native affordability), real mana source/pool previews and cast regression, and a complete real Forge battle covering cast/payment/target/attack/AI/game-over.
- Native screen playback remains unverified after this update. Event provenance for damage/trigger/stack resolution, tokens, attachments, advanced costs, ability menus, transient-stop lifetime, some pass modes and fine-grained yields are still incomplete; this is not full Arena feature parity.

Fetch land / ability menu update:

- Exact normalized printed activated-ability text maps Forge descriptions to installed Arena ability IDs. Escape Tunnel options map to 88024 and 170692; Terramorphic Expanse maps to 145470. Ambiguous/unmatched text is left unmapped.
- Forge InputConfirm sacrifice decisions now produce native OptionalActionMessage requests with localized prompt 1739 and a source-card reference; Yes/No routes back to the current Forge input.
- Real Terramorphic Expanse regression covers activation, confirmation, library selection and a tapped basic land. Native constructors and ability mapping self-tests pass. Screen behavior needs confirmation in a fresh Arena match.
- Collect-Arena-Trace.bat extracts gameplay JSON from a local Player.log, excluding login and account envelopes. It does not contact official servers or download all animations. Summarized states are unrecoverable.
