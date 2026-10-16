# Protocol audit after installed Arena update

Compared baseline client `0.1.14123.1316311` with installed client `0.1.14403.1326110`. Baseline descriptor exports are preserved under `data/client-snapshots/0.1.14123.1316311-20261003T180624Z/schemas`; refreshed descriptors are under `analysis/schemas`.

## Transport and authentication

Local ILSpy decompilation of installed `BufferMessageReader`, `TcpConnection`, `AwsAuthServiceWrapper` and `GREConnection` produced identical SHA-256 text hashes to the earlier extracted types. TCP version 4, packed type/format framing, little-endian body length, TLS 1.2, certificate validation and ping/pong behavior therefore remain unchanged in these inspected implementations. The FrontDoor auth fields and MatchDoor auth/connect sequences remain unchanged.

The installed `Wizards.Arena.TcpConnection.dll` and `Google.Protobuf.dll` are binary-identical to the baseline extracted copies. Arena Models/Enums and GRE generated assemblies changed. Cmd/Response field numbers and the GameStateMessage fields used by the snapshot translator remain unchanged.

## Command inventory

The enum inventory grew from 179 to 183 names. No surviving command name changed numeric ID.

| Change | ID |
| --- | --- |
| Rename GetEventLinkNotificationPreferences → GetWpnNotificationPreferences | 2350 |
| Add SetGlobalWpnNotificationPreference | 2352 |
| Add GatheringDeclineInvite | 2915 |
| Add GatheringCheckText | 2916 |
| Add MosaicAuxiliary | 4300 |

The diagnostic host derives command names from the new generated enum and rejects unsupported commands explicitly. The renamed ID and new commands do not require changes to current local authentication or state-export behavior.

## GRE descriptor differences

Compared every top-level and nested message/enum by qualified name and field/enum number. The following semantic changes were found; no existing message was removed and no existing field wire type/number was changed:

- GroupSpecification adds repeated uint32 `hot_ids` at field 7.
- PlayerConfig adds repeated ToastTuple `toasts` at field 14.
- New ToastTuple: uint32 `toastID` field 1 and `toastParam` field 2.
- AbilityType renames Placeholder419 to EmpowerJace (419), adds Placeholder432 (432).
- PromptMessage renames Placeholder200 to EmpowerJace (200).
- AbilityWord adds Placeholder58 (58).
- CardType adds Conspiracy (15).
- MiscellaneousTerm adds placeholders 33 and 34.
- QualificationType adds PlayWithZoneRevealed (96).
- SubType renames PlaceholderSubType489 to Heartwood (489) and PlaceholderSubType490 to Sculpture (490), adds PlaceholderSubType536 (536).

Machine-readable detail is saved as `analysis/client-update-protocol-diff.json`. Nine protobuf descriptor files were exported again from the refreshed installed assemblies.

## .NET harness compatibility fix

The installed `Wizards.Arena.Models.Protobuf.dll` references `Wizards.Arena.Enums, Version=3.15.0.0`, while the actual installed Enums assembly identifies itself as version `3.9.0.0`. Unity tolerates this local cross-version reference; the standalone .NET host initially failed to resolve it even after a clean rebuild.

Added `protocol-host/ClientAssemblyResolver.cs`: a module initializer resolves a fixed allowlist of six locally referenced client assembly names from the executable directory, including already loaded local assemblies. It performs no network loads, rewrites no DLLs and does not change the client installation. This allows the harness to use the actual installed assembly set despite the version metadata mismatch.

## Verification and limits

The refreshed host builds cleanly. Updated descriptor/command export succeeds. Host self-tests pass for Forge snapshot serialization and hidden identities, TLS 1.2 framing/ping/protobuf, local auth bridging and typed rejection envelopes.

This establishes compatibility of the local transport harness and snapshot encoder with the updated generated contracts. It does not implement account/doorbell bootstrap, full FrontDoor commands or bidirectional Forge/GRE gameplay, and it does not establish a playable stock-client private server.
