# Forge rules engine bridge

This sidecar invokes the real [Forge](https://github.com/Card-Forge/forge) card database, rules engine and AI controllers. It does not recreate card effects. Forge runs headlessly, with an isolated profile under `data/forge-profile`, and its diagnostics are separated from the JSON-lines protocol.

Run `Setup-Forge.ps1` with Java/JDK 17+ and Maven installed. The script downloads Forge into `vendor/forge`, builds its desktop/engine dependencies, compiles `ForgeEngineMain.java`, and writes the local absolute launch configuration `launch.json`. `-SkipBuild` reuses an already built Forge checkout. Maven logs are in `data/forge-build.log`. Forge's exact Git commit is recorded in the launch configuration.

The Python `server.engine.Engine` reads this configuration, starts Java and exchanges correlated requests. Methods are `status`, `cards` (bulk name lookup) and `simulate` (two AI-controlled decks through the actual rules engine). Human interaction through Arena is not implemented by simulation.

Example requests:

```json
{"id":"1","op":"status"}
{"id":"2","op":"cards","names":["Plains","Grizzly Bears"]}
{"id":"3","op":"simulate","seed":42,"maxSeconds":30,"decks":[{"name":"White","cards":[{"name":"Plains","quantity":36,"grpId":12345},{"name":"Savannah Lions","quantity":24,"grpId":12346}]},{"name":"Green","cards":[{"name":"Forest","quantity":36,"grpId":12347},{"name":"Grizzly Bears","quantity":24,"grpId":12348}]}]}
```

The illustrative IDs above must be replaced with actual catalogue IDs before exporting Arena messages. `simulate` is a rules test and does not enforce constructed format legality; deck API validation remains separate. Unsupported card names produce errors, not replacement cards. Time-limited simulations report timeout separately from a played outcome.

Replies are `{id,ok:true,result:...}` or `{id,ok:false,error:"..."}`. Only replies go to stdout. Simulations return actual outcomes/logs and a first-turn snapshot for protocol mapping. Snapshot card identities derive from submitted Arena IDs; unmapped/generated objects are explicitly counted. Snapshot export is a translation prototype, not verified stock Arena rendering.

`protocol-host --encode-state input.json output-prefix` turns bridge snapshots into GRE protobuf and framed transport messages, filters hidden identities and rejects visible objects without usable Arena IDs. Blocking decisions are described by `BattlePrompts.java` and answered with native wire IDs through `interactiveAnswer`. Mode/color options retain labels, optional choices can be declined, card order and top/bottom groups preserve actual Forge objects, and damage allocations validate totals and recipients. Numbers retain range and sparse-value validation. Payment, mulligan, targeting, incremental state and terminal outcomes have dedicated adapters. Reconnect and complete stock-client visual parity remain incomplete.

Forge is GPL-3.0; see the upstream `vendor/forge/LICENSE` and source. This bridge source is offered under GPL-3.0-or-later. Extracted Arena binaries are local input and are not part of a Forge distribution.
