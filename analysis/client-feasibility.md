# Arena client feasibility, inspected 2026-10-03

The supplied Windows client contains a recoverable protocol and complete local card catalog. It does not expose an authoritative Magic rules engine that can simply be run as a private server. A local service implementation and rules bridge remain necessary. Client boot, login, lobby, and a real duel have not been validated.

## Direct evidence

- `Core.dll`, `GreClient.Rules.GreInterfaceFactory.Create`: instantiates the `SharedClientCore.dll` `GreInterface` with a card adapter, message history, logger, and UI callbacks. No rules-process or native-engine invocation occurs.
- `SharedClientCore.dll`, `GreClient.Rules.GreInterface.ParseFullGameStateMessage` and `ProcessMessage`: reconstructs `MtgGameState` from server `GREToClientMessage`/`GameStateMessage`, merges full/diff updates, and produces animation/UI events. The word Rules in the namespace does not imply authoritative card execution.
- `SharedClientCore.dll`, `GreClient.Network.GREConnection.Connect/SendMessage`: connects to MatchDoor, wraps protobuf `ClientToGREMessage` bytes inside `ClientToMatchServiceMessage.Payload`, and receives server game messages.
- `GreClient.Network.ConnectionConfig`: host/port, user ID, access token, client version, and inactivity timeout; no embedded local engine handle.
- ZIP native-plugin/executable inventory: Unity, audio, browser, SQLite, crash reporting, Steam, EOS; no named GRE server or rules engine was found. This negative evidence is bounded to names and inspected managed paths, not a proof about every byte of the archive.
- Offline harness service wrappers exist for inventory, cosmetics, store, and other UI data. `HarnessEnvironmentInitializer` only sets `HostPlatform.Harness`; these are not a recovered match engine.

## Local card catalog

`MTGA/MTGA_Data/Downloads/Raw/Raw_CardDatabase_627ece2b2da56e56f2801a98b5a5b231.mtga` is a standard SQLite database despite its suffix. Read-only inspection found 26,586 Cards rows and 21,131 Abilities rows. Version table: Data `2026.62.0.102`; GRP `102.1309557`. This identifies supplied catalog versions, not verification that the installation is the current public release.

A local copy is at `analysis/decompiled-rules/card-database.mtga`; schema is `card-schema.json` alongside it. `Cards.GrpId` is the Arena identity. `Cards.TitleId` joins `Localizations_enUS.LocId` with `Formatted=0` for card names. Cards contains mana strings, power/toughness, enum-based types/colors, linked faces, ability IDs, rebalancing and printing information. Ability rows carry text and display metadata, not executable effect implementations.

A useful read-only catalog query is:

```sql
SELECT c.GrpId, l.Loc AS Name, c.OldSchoolManaText, c.Power, c.Toughness,
       c.Types, c.Subtypes, c.Colors, c.AbilityIds
FROM Cards c
JOIN Localizations_enUS l ON l.LocId=c.TitleId AND l.Formatted=0;
```

A rules bridge can bulk-map these IDs to another engine's names/printings. Duplicate names, split faces, digital-only and rebalanced cards need explicit handling; matching names alone does not establish behavior compatibility.

## Confirmed configuration path

`Wizards.Mtga.Interfaces.dll`, `Wizards.Mtga.Configuration.DefaultConfigurationLoader.LoadServicesConfiguration` reads `services.conf` under Unity `Application.streamingAssetsPath`, normally `MTGA_Data/StreamingAssets/services.conf` on this Windows build. No such file was present in the supplied ZIP. The loader falls back to an empty configuration; Core's `EnvironmentManager` then selects production defaults.

`ServicesConfigurationConverter.ReadJson` accepts an `environments` object keyed by names and optional `$environments` name array. It inserts the key into each environment's `name` field. An environment supports `fdHost`, `fdPort`, `mdHost`, `mdPort`, `doorbellUri`, `ecoUri`, `accountSystemBaseUri`, `accountSystemEnvironment`, `accountSystemId`, `accountSystemSecret`, platform client IDs, telemetry `bikeUri`, `HostPlatform`, and store transaction type. Omitted URL fields can retain production defaults, so a local test configuration must deliberately cover all relevant services.

`Core.Shared.Code.Providers.EnvironmentManager.GetDoorbellUri` appends `/api/ring` or `/api/v2/ring` according to the `RingDoorbellV2` feature toggle. `InitializeEnvironment` consults `MDNPlayerPrefs.PreviousFDServer`, then the first configured environment. Config files set a debug feature toggle. `tinkerbellUri`, if present, triggers external environment discovery; omit it for local services.

The configuration mechanism is evidence for a possible isolated client configuration without rewriting endpoint IL. It is not proof that a complete local login works. SDK account environment selection, TLS trust and exact responses still need confirmation. Embedded production credential constants were excluded from this report and must not be reused in local configuration.

## Accelerated implementation

The largest shortcut is an existing rules engine. [XMage's official repository](https://github.com/magefree/mage) documents a full rules-enforcement engine and self-hosted, offline servers. [Forge's official repository](https://github.com/Card-Forge/forge) documents a Java rules engine with game modules and existing card scripts. Neither source claims Arena wire compatibility. An adapter must still translate seat IDs, zones, object IDs, actions, priority prompts, timing, state diffs, and hidden information into this client's protobuf model. Coverage of all Arena digital mechanics must be established separately.

Build catalog, account/lobby persistence, FrontDoor protocol, MatchDoor protocol and engine mapping concurrently. Reuse the supplied protobuf schema and catalog in bulk; avoid reimplementing cards individually. A playable game on the engine's own client is materially easier than a full Arena-compatible replacement. A complete Arena private server cannot honestly be marked finished before login and a real duel pass on the supplied client.
