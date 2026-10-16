# Forge integration evidence

Source checkout: `vendor/forge`, inspected 2026-10-03. No AGENTS.md was found in the checkout. Upstream source was not modified.

## Actual executable bridge

`engine-bridge/ForgeEngineMain.java` exposes local JSON-lines requests on stdin and responses on stdout, with all Forge initialization and game logging redirected to stderr. Compile it against the compiled Forge desktop module and dependencies. Its sole CLI argument is the Forge assets **base** directory containing `res/`, normally `vendor/forge/forge-gui` in this checkout. `GuiDesktop.getAssetsDir()` is overridden before initialization, avoiding the desktop's build-version-dependent relative path.

The launcher must isolate `-Duser.home`, APPDATA, and LOCALAPPDATA to project-owned profile directories. `FModel.initialize(null,null)` uses Forge's normal profile and card script initialization; its card metadata is executable Forge rules and is distinct from the Arena catalog.

Operations:

- `{ "id": 1, "op": "status" }`: initializes Forge and returns readiness and the implemented AI-versus-AI mode.
- `{ "id": 2, "op": "cards", "names": ["Forest", "Llanowar Elves"] }`: returns exact Forge name support and selected edition. No automatic substitution of rebalanced or digital variants occurs.
- `{ "id": 3, "op": "simulate", "decks": [{"name":"P1","cards":[{"name":"Forest","quantity":20,"grpId":123}]}, {"name":"P2","cards":[{"name":"Forest","quantity":20,"grpId":123}]}], "seed":42, "maxSeconds":30 }`: constructs actual Forge decks and runs its AI controllers through `Match.startGame`. Maximum runtime is 5–60 seconds with a five-second shutdown allowance. Empty or unsupported decks are rejected; full format legality is not currently enforced by this bridge.

Responses have `id`, `ok`, and either `result` or `error`. Simulation results contain the actual winner (null for a draw), winner seat, turns, end reason, timeout flag, bounded game logs, a final `snapshot`, and `initialSnapshot` captured on the first real phase event after card identities are registered. If the game ends before that phase event, initialSnapshot is null. Timeout is explicitly reported and ends the game as a draw. If Forge fails to stop, the worker marks the process unhealthy and exits after the error response; the launcher must restart it. Seeds influence Forge's RNG but do not promise bit-for-bit determinism across engine versions or threaded behavior.

Snapshot original-card identities are captured at the first-turn hook from supplied deck name-to-Arena-ID mappings. Multiple printings of the same name in one deck are rejected when IDs differ. Unmapped cards and generated tokens are omitted with `omittedUnmappedCards` count, rather than assigned invented Arena IDs. This may make a snapshot incomplete for token/conjure-heavy games. The snapshot is a full engine observation; any player-facing GRE encoder must mask opponent hand and library information. A final snapshot is not a live Arena interaction session.

## Evidence for reuse

`forge.view.Main` recognizes `sim`, calls `SimulateMatch.simulate`, and forces headless AWT for console modes. `SimulateMatch` uses `FModel.initialize`, `DeckSerializer.fromFile`, `RegisteredPlayer`, `GameRules`, `Match.createGame`, `Game.setNoGUIUser`, and `Match.startGame`. Its CLI supports decks, fixed seed, timeout, match length, and quiet result output. The Main `server` branch merely says dedicated server mode is not implemented; it is not a ready dedicated server launcher.

The bridge avoids the Main entry point's Sentry initialization and UI startup. It directly uses the same game APIs and `LobbyPlayerAi` rather than replacing rules with a simplified engine.

## Reusing human input with fewer adapters

Current source provides `forge.gamemodes.net.ProtocolGuiGame`, a concrete `IGuiGame` proxy designed for embedding. It accepts `IRemote`, whose required methods are `send(NetEvent)` and `sendAndWait(IdentifiableNetEvent)`; `write` has a default implementation. It handles prompts and deltas via `GameProtocolSender` and `DeltaSyncManager`, with typed response validation in `ProtocolMethod`.

Use `LobbyPlayerHuman` / `PlayerControllerHuman`, then `setGui(new ProtocolGuiGame(remote))`, and wire game event forwarding as `HostedMatch` does. A bridge implementing `IRemote` can publish prompts and wait for responses without implementing roughly a hundred abstract `PlayerController` choices. Priority/card actions are available through `IGameController.selectButtonOk`, `selectButtonCancel`, `selectCard`, `selectPlayer`, `selectAbility`, `useMana`, and `concede`. `InputProxy` forwards these to the active validated input.

The desktop test sources `HeadlessGuiDesktop` and `HeadlessNetworkGuiGame` demonstrate no-display initialization and networking tests, but their default replies automate choices. Copying their defaults would not produce human-controlled gameplay.

Arena support still needs conversion from these Forge prompts and event/state structures to this client's GRE protobuf, per-player visibility, and exact MatchDoor/FrontDoor sequencing. The Java bridge currently provides real Forge simulation and coverage checking, not an Arena login or human duel.

## Local verification completed

The bridge compiled with JDK 17 against the actual built desktop module and its resolved dependency classpath. A live isolated Forge worker answered status and exact-name support requests, supported Forest/Grizzly Bears, and rejected a deliberately nonexistent name. A seeded AI game (seed 42, 36 Forest plus 24 Grizzly Bears versus 36 Plains plus 24 Savannah Lions) finished naturally at turn 12 with White lions winning seat 2 and no timeout. These deliberately repetitive decks test the rules path, not constructed legality.

After stable-seat correction, a second live run preserved both player records (seat 1 life -2, seat 2 life 18), all 120 original card observations, and the correct owner seat for every supplied name-to-ID mapping. The first captured real phase was UPKEEP on turn 1. Evidence is in `data/forge-stable-seat.jsonl` and `data/forge-stable-seat.stderr.log`; sample IDs in this low-level test are illustrative except Forest, so this is not evidence of valid Arena rendering. The root integration must use real catalogue mappings for its protocol checks.

The first exploratory runtime used all local Maven jars and failed due an obsolete Guava version shadowing Forge's dependency. The resolved `engine-bridge/build/forge-classpath.txt` fixes the environment and was used for successful execution; broad jar globbing is not a supported launch method.
