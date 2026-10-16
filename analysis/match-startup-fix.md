# Arena match startup investigation

The latest supplied screenshot reaches the Arena battlefield with Forge AI, then
waits for the server. Player.log confirms MatchDoor TLS and ConnectResp succeed,
followed by a 120-object full state and a pass-only ActionsAvailableReq. Arena
throws KeyNotFoundException for LocalPlayer in CardHolderManager.CreateSubCardHolder.

Decompiled local Core.dll evidence is saved in .tools/match-card-holder-manager.cs.
CreateCardHolder initializes PlayerTypeMap for a zone owner. CreateSubCardHolder
indexes that player entry without checking. ForgeEncoder was emitting shared
zones before any owned zones. The encoder now serializes owned zones first; a
regression test checks both players have owned zones before shared zones appear.
This is a likely fix for the observed exception, still requiring stock-client
verification.

MatchDoor also ignored JSON-format heartbeat frames (version 4, type 2, format 1).
It now echoes pongs before application parsing, ignores inbound pongs, advertises
a 60-second inactivity timeout, and serializes writes through a per-stream lock
so background state writes cannot overlap foreground replies. Tests check both
heartbeat formats and ensure pongs are not answered.

Validation: dotnet build protocol-host -o protocol-host/bin/MatchStartupFix and
the resulting ProtocolHost.dll --self-test both passed. The live build was rebuilt
as well. No actual Arena match was launched or verified during this investigation.

Remaining observed issues in the current adapter include ignored SetSettingsReq,
missing general prompt translation, fabricated pass/cast action availability,
constant game-state IDs, a second poll draining events, unauthenticated interactive
HTTP fallback to user 1, and match authentication accepting unvalidated tickets.
These were not fixed by the narrow startup patch. The first snapshot can precede
the opening hand; a loaded board alone does not establish working gameplay.
