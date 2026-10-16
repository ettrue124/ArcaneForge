# Local Arena server research build

For publication status, confirmed gaps and required visual acceptance, see [RELEASE-READINESS.md](RELEASE-READINESS.md). Full Arena animation parity is not release-ready.

## Repair builds and check gameplay

Double-click **`Repair-And-Check-Arena.bat`**. Leave the window open until it prints the report path. This can take several minutes, especially after a client update.

The script backs up and refreshes imported client files when the installed game version changes, refreshes the cached card database and generated card/ability/token metadata, rebuilds the Java bridge and Arena protocol host, runs the Python suite and every `engine-bridge/test_*.py` gameplay scenario, and refreshes the animation and gameplay-request inventories. Tests use isolated matches and temporary accounts.

Results and individual logs are under `analysis/server-checks/<run>/`; `analysis/server-checks/latest-report.txt` points to the latest readable report. Failed, timed-out, blocked, or skipped checks are reported separately. The script continues independent checks after a test failure and prevents overlapping repair runs. Metadata backups are stored with the report; full client-update backups are under `data/client-snapshots`.

This repairs known build/data problems and checks implemented animation adapters. Missing adapters still require code changes. A passing report means the automated checks passed; actual Unity visuals, timing, and all card combinations remain unverified. Restart the local server after a successful check to load the rebuilt components. The script does not restart a running match or change the installed game.

Command-line equivalent: `python tools/repair_arena_animations.py`.

After fixing a failed gameplay check, `python tools/recheck_failed_audit.py` reruns failed gameplay scenarios and retains the original failures/logs with links to successful rechecks. Build or data failures require the complete audit. `python tools/create_release_status.py` writes a separate publication-status summary; passing tests alone do not make the full-parity claim ready.

This is an operational account/deck/lobby backend, a recovered Arena protocol host, and an integrated headless Forge rules engine. The local Arena integration supports human gameplay against Forge AI with implemented prompts, actions and animation adapters. It remains experimental; full official-server feature and visual parity is not established.

## Run the local backend

From this folder on Windows with Python 3.11 or newer:

```powershell
.\Start-LocalServer.ps1
```

Or use these commands directly (also suitable for a local Linux checkout with the archive):

```text
python scripts/prepare_client.py MTGA.zip
python -m server
```

Open http://127.0.0.1:8787. Register a local account, search the extracted catalogue, save decks, then pair through the queue or direct challenges. Lobby decks require at least 60 cards. Match records retain `pending_engine` status for interactive Arena gameplay. With Forge configured, the panel can run AI simulations of the paired decks; pairing does not start a player-controlled Arena game.

## Forge integration

With JDK 17+ and Maven installed, run `Setup-Forge.ps1`, then restart the local backend. The server reads `engine-bridge/launch.json` and launches the actual Forge engine as needed. Engine profiles remain inside this project. Forge sources/dependencies are downloaded in bulk; individual cards are not recreated.

```powershell
.\Setup-Forge.ps1
python scripts/forge_coverage.py --timeout 180
python scripts/forge_smoke.py
dotnet run --project protocol-host -- --encode-state data/forge-initial-state.json data/forge-gre-preview
```

Verified Forge commit: `d50cedc565bfbcedab333c67507dc0f5aef00386`. An actual seeded rules fixture finished naturally in 12 turns, with White lions winning. Its 120-card initial state used real IDs from the Arena catalogue and was encoded into recovered GRE protobuf. That fixture deliberately exceeds normal copy limits to exercise rules; the backend still enforces copy limits for saved decks. A second integration test runs legal all-basic-land deck fixtures through the authenticated HTTP match API.

Bulk lookup resolves 20,093 of 20,128 catalogue entries by name. The coverage report flags 695 entries for review, with 19,406 eligible for preliminary Forge simulation and 35 unresolved entries. These counts are not verified rules equivalence. Digital/rebalanced entries are rejected by HTTP simulation until an explicit mapping exists. See `data/forge-coverage.json` and `engine-bridge/README.md`.

The running backend binds to loopback. SQLite account/deck/match data is in `data/server.sqlite3`. Sessions expire after 24 hours and are revocable. Passwords use salted scrypt and session tokens are stored as SHA256 hashes. Restarting preserves accounts, decks, challenges, queues and match snapshots.

## What works and what remains

| Component | Current result |
| --- | --- |
| Client inputs | Updated installed client imported with SHA256 manifest; previous inputs preserved |
| Card catalogue | 20,128 primary non-token entries from the updated installation, preserving client metadata |
| Local account API | Registration, login, expiring tokens, logout |
| Decks | Owned CRUD, known IDs, aggregate copy limits across alternate printings |
| Lobby | Atomic two-player queue pairing, challenges, durable match snapshots |
| Control panel | Local browser UI using the account/deck/lobby API |
| Arena transport | Actual TLS 1.2, recovered framed TCP/protobuf envelopes; see `protocol-host/README.md` |
| Protocol schemas | Nine protobuf file descriptors plus 183 FrontDoor command IDs exported in bulk |
| Stock Arena account/login bootstrap | Incomplete; unverified |
| Inventory, collections, events, store, social service responses | Incomplete |
| Forge rules engine | Integrated; actual AI simulations and bulk card lookup verified |
| GRE full game-state translation | Prototype encoded actual Forge initial state; hidden identities filtered per seat |
| GRE match lifecycle/human choices | Incomplete |
| Arena digital/rebalanced behavior equivalence | Unverified; explicit mappings required |
| Playable duel in the supplied Arena client | Not achieved |

The HTTP control API and Arena protocol service are different interfaces. Account success in the control panel does not establish stock Arena client compatibility.

## Recovered protocol host

Requires .NET SDK 10 and the extracted DLLs in `client-inspection/`:

```text
dotnet run --project protocol-host -- --self-test
dotnet run --project protocol-host -- --export-schema
dotnet run --project protocol-host -- --port 9445
dotnet run --project protocol-host -- --port 9445 --backend-url http://127.0.0.1:8787
dotnet run --project protocol-host -- --match --port 9444
```

The listeners use loopback and reject unsupported commands with the client's actual protobuf error models. With `--backend-url`, FrontDoor authenticates local backend tokens against `/api/me` and produces the recovered session response DTO; this does not implement Arena's normal login bootstrap. MatchDoor authentication/gameplay remain unsupported. An ephemeral self-signed localhost certificate is generated for protocol tests; it does not establish Arena client trust. Supplied PFX support is documented beside the host. No certificate trust store, hosts file, firewall or installed Arena files have been changed.

The exported descriptor set can regenerate message bindings in other languages instead of hand-writing hundreds of protocol messages. It comes from the supplied DLLs, not from a guessed network format. Local extracted/decompiled game binaries and data are ignored by Git.

## Verify

```powershell
.\Test-All.ps1
```

Python tests exercise ownership, login/revocation/expiration, hostile JSON and cross-origin requests, selective archive handling, local catalogue extraction, alternate printing limits, concurrent queue pairing and challenge acceptance. C# self-tests exercise framing versions, partial reads, bounds, actual TLS/protobuf exchange and transaction correlation. These checks cover the implemented components; they do not establish gameplay compatibility.

When `engine-bridge/launch.json` exists, Python tests also start the real Java engine and simulate a paired match over HTTP. The first engine request loads Forge scripts and can take tens of seconds. Protocol tests additionally verify full-state zone references and hidden-card sanitization.

## API

Register with `POST /api/register` and sign in with `POST /api/login`, JSON `{ "username": "...", "password": "..." }`. Authenticated requests use `Authorization: Bearer <token>`.

| Endpoint | Operation |
| --- | --- |
| `GET /api/status` | Public implementation/compatibility status |
| `GET /api/me`, `POST /api/logout` | Current local user, revoke token |
| `GET /api/users?q=...` | Find local challenge opponents |
| `GET /api/cards?q=...&limit=100` | Catalogue search |
| `GET/POST /api/decks` | List/create decks |
| `GET/PUT/DELETE /api/decks/{id}` | Read/update/delete owned deck |
| `POST/DELETE /api/queue` | Queue with `{ "deckId": 1 }`, cancel |
| `GET/POST /api/challenges` | List/create with `{ "opponentId": 2, "deckId": 1 }` |
| `POST /api/challenges/{id}/accept` | Accept with own `{ "deckId": 2 }` |
| `DELETE /api/challenges/{id}` | Cancel a challenge you participate in |
| `GET /api/matches`, `GET /api/matches/{id}` | Participant-only pending match records |
| `GET /api/engine/status` | Initialize/check the configured real Forge engine |
| `POST /api/engine/cards` | Resolve `{ "names": ["Plains"] }` through Forge |
| `POST /api/matches/{id}/simulate` | Participant-only AI rules simulation of the paired decks; optional seed/maxSeconds |

Deck JSON is `{ "name": "My deck", "cards": [{ "grpId": 12345, "quantity": 4 }] }`. Card IDs are from your extracted database. Basic lands are unlimited, ordinary cards default to four copies, and client alternate copy limits are preserved. This is preliminary deck validation: tournament format legality, sideboards and gameplay rules remain separate requirements.

## Remaining engineering

The existing [Forge engine](https://github.com/Card-Forge/forge) is integrated and supplies actual card scripts/rules. Its dedicated `server` CLI is not implemented upstream; this project invokes its engine APIs through a Java sidecar. Forge's `ProtocolGuiGame`/`IRemote` interfaces offer a next step for forwarding human choices while preserving Forge's existing player controllers.

A complete Arena adapter still must map unsupported cards/printings and digital mechanics; translate human actions, priority and choice prompts; implement state deltas and the full match lifecycle. Stable seats, initial full-state objects and hidden-information filtering have a verified prototype, but Arena rendering is unverified. Account/ring bootstrap, startup-critical FrontDoor responses and supported TLS configuration also remain necessary.

Evidence and exact inspected type names are recorded in `analysis/client-feasibility.md` and `analysis/protocol.md`. The active inputs are installed client `0.1.14403.1326110`, catalogue Data `2026.63.0.270` / GRP `270.1318799`. The original archive still contains the older client and is not the active input. See `analysis/client-update-protocol.md` for the update comparison. Work was performed in this Windows workspace; no remote cloud deployment was performed.

## Commander game modes

X costs now use Arena's numeric selection request with a Forge payment limit; the selected value is retained on the stack. Fight and bite spells select your creature and the enemy creature in separate target groups. Normal priority automatically passes when no non-mana play is available, including at phase stops; full control still pauses. `python engine-bridge/test_fight_x.py` checks Primal Might with X=2 and Rabid Bite against real Forge, alongside the native protocol self-test.

Run `Launch-Server.bat`, then open Arena. Under **Play → Find Match → Play**, choose **Play vs Forge AI**, **Commander — 1 AI**, or **Commander — 3 AIs**. Both Commander queues accept the same Commander deck; the selected queue controls the player count.

The local account has **Sample Commander - Bill Landfall**, a mono-green deck with Bristly Bill in the command zone, 39 lands and 60 singleton spells. It totals **100 cards including the commander**. Basic lands are allowed in multiple copies. An Arena import is also available in `Sample-Commander-Deck.txt`. To add it to another local account, run `python tools/add_sample_deck.py --user-id ID`; this preserves existing decks and any edits to the sample.

Local mode names are added to Arena's localization database with a record of the original rows. Launching the server while Arena is closed reapplies them after an update. `Restore-OfficialClient.ps1` restores those rows along with the service configuration. Restart Arena after changing the format or localization data.

Commander uses installed paper-card definitions and the verified ban list in `data/commander-bans.json`. Unsupported cards absent from Arena cannot be imported. Two/four-seat states, deck rules, command zones and mulligans are checked against Forge and Arena's native parsers; this does not verify every multiplayer animation or card interaction.

## Refresh after a game update

The native gameplay bridge now handles Forge's blocking mode, color, optional, card/entity, ordering, scry, numeric, text, damage and distribution decisions. It preserves Forge's original objects and validates answers instead of selecting the first option automatically. X announcements keep their casting interface; ordinary amounts use numeric requests. Additional casts distinguish Adventure, split halves, modal double-faced backs, Prototype, Rooms and Omen from Forge ability state. These variants still need stock-client visual playtesting.

Run `python engine-bridge/test_prompt_games.py` after compiling the bridge to exercise real Forge scry, modes, divided damage and trample against the native request encoder. `python engine-bridge/test_fight_x.py` verifies actual X/fight effects. `dotnet run --project protocol-host -- --self-test` checks installed client parsers and response mappings. These checks do not substitute for testing all Unity workflows in Arena.

`python tools/export_linked_faces.py` rebuilds the face map from the readable local card database, using only explicit printing links. `python engine-bridge/test_card_faces.py` verifies an MDFC land and an Adventure spell use the correct face and rules metadata while retaining their original printing. Same-name linked printings, such as Prototype, retain the original rather than guessing a face.

Run `python tools/report_action_coverage.py` to write `analysis/action-inventory/adapter-coverage.md` and `.json`. They distinguish native adapters, alternate workflows and outstanding work; the extracted `actions.json` remains an unmodified static inventory. Multiple attackers per blocker have an implemented adapter and regression scenario. Best-of-three sideboarding and full animation parity remain unsupported.

To extract the available client protocol without changing the game or server, double-click `Extract-Arena-Actions.bat`. It runs `python tools/extract_arena_actions.py` against the installed `MTGA_Data/Managed` DLLs and writes `analysis/action-inventory/README.md`, `actions.json`, `incoming-messages.csv`, exact protobuf descriptors and decompiled request/annotation handlers. The JSON retains wire numbers, payload fields, enum values, request/response directions and DLL fingerprints. Declared enum values are distinguished from recovered client handlers; static extraction does not establish live gameplay support. Use `--managed "path/to/Managed"` for another installation, `--schema-only` for descriptors without decompilation, or `--refresh` to regenerate cached sources.

```powershell
python scripts/refresh_client.py "C:\Program Files\Wizards of the Coast\MTGA"
dotnet run --project protocol-host -- --export-schema
python scripts/forge_coverage.py --timeout 180
.\Test-All.ps1
```

Restart the local backend and protocol host after refreshing. The refresh preserves the old inputs under `data/client-snapshots` and imports a new catalogue; local accounts and saved decks survive. A saved deck containing removed card IDs must be revised before pairing. Updating inputs does not redirect the installed client to this service.
