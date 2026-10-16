# Forge → Arena GRE translation prototype

The recovered protobuf schema provides real message contracts; Forge supplies authoritative rules state. This adapter translates a read-only snapshot into a generated `GREToClientMessage.GameStateMessage`, wrapped in `MatchServiceToClientMessage.GreToClientEvent`. It does not invent action prompts or claim the stock Arena client can complete login/start a match.

## Implemented input contract

Run `dotnet run --project protocol-host -- --encode-state snapshot.json output-prefix` to produce `output-prefix.pb`, `.frame` and `.json`. `.pb` contains the match-service envelope; `.frame` adds actual Arena v4 framing. Output targets one viewer and must be produced separately for each seat.

```json
{
  "matchId": "forge-match-1",
  "gameStateId": 1,
  "msgId": 1,
  "viewerSeat": 1,
  "turn": {"number": 1, "phase": "MAIN1", "activeSeat": 1, "prioritySeat": 1},
  "players": [{"seat": 1, "life": 20}, {"seat": 2, "life": 20}],
  "cards": [{"instanceId": 1001, "grpId": 12345, "ownerSeat": 1,
             "controllerSeat": 1, "zone": "Hand", "tapped": false}]
}
```

Exactly two players with seats 1 and 2 are supported. Engine state IDs and message IDs must be positive. Card IDs must be positive and unique. Visible cards must carry a resolved positive Arena grpId. A card can optionally carry integer `power` and `toughness`. Unknown phases/zones and unresolved visible cards fail explicitly.

## Confirmed GRE mappings

| Forge snapshot | Recovered GRE field |
| --- | --- |
| matchId | GameInfo.matchID |
| gameStateId | outer GREToClientMessage.gameStateId and GameStateMessage.gameStateId |
| viewerSeat | GREToClientMessage.systemSeatIds |
| msgId | GREToClientMessage.msgId |
| player seat/life | PlayerInfo.systemSeatNumber/lifeTotal |
| phase/turn/player | TurnInfo.phase/step/turnNumber/activePlayer/priorityPlayer/decisionPlayer |
| card instanceId | GameObjectInfo.instanceId, ZoneInfo.objectInstanceIds |
| Arena printing grpId | GameObjectInfo.grpId |
| owner/controller | GameObjectInfo.ownerSeatId/controllerSeatId |
| tapped/power/toughness | GameObjectInfo.isTapped/power/toughness |

The prototype emits `GREMessageType_GameStateMessage=1`, `GameStateType_Full=1`, `GameStateUpdate_Send=1`, `GameStage_Play=2`, `GameType_Duel=1`, `GameVariant_Normal=1`, `MatchState_GameInProgress=1`, `PlayerStatus_InGame=1`. These are confirmed enum values. The present encoder only supports ongoing, first-game two-player snapshots; pregame, game-over and match results need additional bridge fields and mapping.

There are no proto2 `required` fields in the recovered messages. IDs, game info, players, zones and object references are semantically necessary for the client state graph; the complete set required by an actual running stock client has not been validated.

| Forge ZoneType | GRE ZoneType number |
| --- | --- |
| Library | 1 |
| Hand | 2 |
| Battlefield | 3 |
| Stack | 4 |
| Graveyard | 5 |
| Exile | 6 |
| Command | 7 |
| Sideboard | 10 |

Prototype zone IDs are stable `ownerSeat * 100 + zoneType`. It emits per-owner zones, including battlefield/stack; whether those should consolidate into shared zones for Arena presentation remains unverified. Empty zones are included. IDs derive from adapter namespace, not Forge zone identity. Forge card IDs should be mapped into a disjoint stable instance namespace (for example 1000 + Forge ID), retained for a full object's lifespan; library reorder and control change must not change identity.

| Forge PhaseType | GRE phase / step |
| --- | --- |
| UNTAP | Beginning 1 / Untap 1 |
| UPKEEP | Beginning 1 / Upkeep 2 |
| DRAW | Beginning 1 / Draw 3 |
| MAIN1 | Main1 2 / None 0 |
| COMBAT_BEGIN | Combat 3 / BeginCombat 4 |
| COMBAT_DECLARE_ATTACKERS | Combat 3 / DeclareAttack 5 |
| COMBAT_DECLARE_BLOCKERS | Combat 3 / DeclareBlock 6 |
| COMBAT_FIRST_STRIKE_DAMAGE | Combat 3 / FirstStrikeDamage 11 |
| COMBAT_DAMAGE | Combat 3 / CombatDamage 7 |
| COMBAT_END | Combat 3 / EndCombat 8 |
| MAIN2 | Main2 4 / None 0 |
| END_OF_TURN | Ending 5 / End 9 |
| CLEANUP | Ending 5 / Cleanup 10 |

Forge source evidence is `vendor/forge/forge-game/src/main/java/forge/game/phase/PhaseType.java`, `zone/ZoneType.java`, `GameView.java`, `player/PlayerView.java`, `card/CardView.java`. GRE evidence is the recovered descriptor set in `analysis/schemas`.

## Hidden information

Each viewer output sanitizes hidden objects before serialization. Library identities are always hidden; opponent hand/sideboard identities are hidden. Those objects retain instance, zone and owner IDs but omit grpId, power/toughness and tap state. Own hand/sideboard remains private; public zones carry visible identities. This prototype does not support revealed library cards, face-down permanents, spectating or other changing knowledge states. Do not expose raw engine snapshots to an untrusted remote player; they can contain both hands.

## Card printing mapping

Forge names are not Arena grpIds. The bridge must map Forge's card name and edition/collector identity against Arena's locally extracted database, reject unsupported entries and keep the selected Arena printing stable. Name-only lookup may select the wrong printing or face; double-faced cards, tokens, adventures and Arena-specific mechanics need richer mapping. Encoder tests use synthetic IDs solely to validate serialization; those fixture IDs are not an engine-produced match.

## Interactive actions still needed

`ActionsAvailableReq` contains repeated `Action` and `inactiveActions`. `ActionType` numbers include Cast 1, Activate 2, Play 3, Activate_Mana 4, Pass 5. `Action` references instanceId/grpId/abilityGrpId and detailed target/mana/cost data. `ClientToGREMessage.PerformActionResp` returns selected actions; `respId` must correlate with GRE request `msgId`, and `gameStateId` must guard stale input.

Forge's blocking PlayerController decisions need a queue of pending prompts translated to these exact GRE request/response types. At minimum map mulligans, optional actions, available casts/land plays, targets, attackers, blockers, replacement choices, payment choices and ordering. A generic pass button does not cover these decisions. These are not implemented by the snapshot encoder. No `ActionsAvailableReq` is emitted until real engine decisions can be represented faithfully.

## Validation

The host self-test parses emitted bytes with the actual recovered generated models; checks life/phase mapping, zone membership, visible cards and hidden identities; and rejects unresolved grpIds and duplicate objects. This validates message construction and sanitization. Real Forge engine snapshots can be serialized once bridge output uses this contract. A stock client rendering or interactive match is a separate integration milestone.
