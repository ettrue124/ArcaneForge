# Arena gameplay evidence

## REA inspection

REA CLI 6.4.0 works with Node 24.20.0. Codex registration is healthy, but MCP tools are absent from this running chat. CLI analysis was used without changing registration.

Core.dll identity: SHA-256 947441eecc9a9231503af434ad7970e6af0bc2c56148d56991bba5b6c76458e6. REA evidence ev_0a04660df4abf5abc74e7454633cad013d02980b91e37dd46ce7516a733cd3bb is saved in arena-core-rea-evidence.json.

REA managed member inspection returned no methods because of its 64 MiB representation budget. Evidence ev_004a570306ce7d09977c5fb2d814c88725f0d9c1f74c74384841a2f238dae34b records that limitation. This is not evidence that methods are absent. Existing ILSpy was used for code recovery.

## Observed client behavior in recovered code

- .tools/video-actions-workflow.cs, ApplyInteractionInternal: Actions become active GreInteraction objects; InactiveActions become GreInteraction objects with isActive=false.
- analysis/arena-client-side-interaction.cs, HandleActions: a card with only inactive interactions cannot enter the normal action handler.
- The same class's modal browser construction puts active choices into its selectable list and inactive choices into its nonselectable list.
- analysis/esika-card-data.cs, GetLinkedFaceAtIndex: linked printing data can construct a face instance when an explicit linked instance is absent. Missing explicit child objects alone does not establish the cause of Esika's reported problem.
- Historical trace local-20261011-234954-5bb3ecce contains both Cast and CastMDFC actions for Esika. Later project changes already added face-specific printing and source IDs.

## Server correction

MatchDoorHost now places Cold actions into ActionsAvailableReq.InactiveActions. Previously every offered action was active, including unaffordable spells retained for cost display. This follows the client distinction above and applies to all spell choices, including modal faces.

Static code establishes the active/inactive distinction. It does not prove this fixes the original Esika report or verify Unity rendering. New gameplay confirmation is required. Build and deployment status are reported separately.
