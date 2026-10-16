# Official Arena animation capture comparison

Captured 1459 decoded gameplay messages. Recording stopped.

## Changes made from this recording

- Added UserActionTaken for actual land plays, casts, and activations.
- Added ability source links and source-zone details to AbilityInstanceCreated.
- Added AbilityInstanceDeleted when an ability leaves the local board.
- Included affected IDs in resolution events, matching the official message shape.

## Remaining observed annotation gaps

These are candidates for further adapters, not proof that every annotation drives a visible animation. Counts include repeated persistent state.

| Annotation | Occurrences |
|---|---:|
| EnteredZoneThisTurn | 139 |
| ObjectIdChanged | 129 |
| ManaPaid | 84 |
| TriggeringObject | 17 |
| ColorProduction | 16 |
| LayeredEffectCreated | 14 |
| ModifiedLife | 14 |
| Qualification | 14 |
| SyntheticEvent | 12 |
| ObjectsSelected | 11 |
| MultistepEffectStarted | 7 |
| MultistepEffectComplete | 7 |
| LayeredEffect | 7 |
| ShouldntPlay | 6 |
| LayeredEffectDestroyed | 6 |
| LinkInfo | 5 |
| PowerToughnessModCreated | 4 |
| AddAbility | 4 |
| Scry | 3 |
| ModifiedToughness | 3 |
| ModifiedPower | 3 |
| ClassLevel | 3 |
| GainDesignation | 2 |
| DelayedTriggerAffectees | 2 |
| AbilityExhausted | 2 |
| ChoiceResult | 2 |
| ReplacementEffect | 1 |
| ModifiedCost | 1 |
| TextChange | 1 |
| ModifiedName | 1 |
| RemoveAbility | 1 |
| FaceDown | 1 |
| RevealedCardCreated | 1 |
| RevealedCardDeleted | 1 |
| ModifiedType | 1 |

## Validation limits

Java and .NET builds passed. Protocol self-tests passed. A real Forge cycling-land test confirmed the PlayLand zone event and the new UserActionTaken event in the native protocol output. After the user's request to return Arena to the local server, the configuration was restored and both local services restarted successfully with these fixes loaded. Native visual verification remains outstanding.

The client log contains decoded official gameplay messages. It is not a capture of every encrypted network packet. Official hover messages are recorded; Unity visual playback was not recorded.
