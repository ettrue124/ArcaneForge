# Arena animation mapping inventory

Generated from installed client DLLs. These are static reference candidates, requiring gameplay validation.

| Annotation | Detail-key candidates | Visual path candidates | Bridge explicit annotation |
|---|---|---|---|
| AbilityExhausted | AbilityGrpId, UniqueAbilityId, UsesRemaining | No direct visual path discovered | Not emitted explicitly |
| AbilityInstanceCreated |  | UpdateCardModelUXEvent, ZoneTransferUXEvent | Yes |
| AbilityInstanceDeleted |  | No direct visual path discovered | Yes |
| AbilityWordActive | AbilityGrpId, AbilityWordName, ExcludedFromCount, ShowOnMiniCDC, choice, colors, for-mode, isActive, manaSpec, threshold, trigger_condition_met, types, value | No direct visual path discovered | Yes |
| AddAbility | UniqueAbilityId, count, effect_id, grpid, originalAbilityObjectZcid, perpetualAffectedId, sourceAbilityGRPID | No direct visual path discovered | Not emitted explicitly |
| AddAbility | effect_id | No direct visual path discovered | Not emitted explicitly |
| Attachment |  | No direct visual path discovered | Yes |
| AttachmentCreated |  | UpdateCardModelUXEvent, WaitForSecondsUXEvent | Yes |
| BoonInfo | TotalUses, UsesRemaining | No direct visual path discovered | Not emitted explicitly |
| CardRevealed | source_zone | No direct visual path discovered | Yes |
| CastingTimeOption | additionalCostGrpId, alternateCostGrpId, castAbilityGrpId, casualtyAbilityGrpId, chosenCostPromptId, color, costGrpId, costType, kickerAbilityGrpId, type, value | No direct visual path discovered | Yes |
| ChoiceResult | Choice_Domain, Choice_Options, Choice_Sentiment, Choice_Value | ChoiceResultUXEvent_General, ChoiceResultUXEvent_Permanent, ChoiceResultUXEvent_Random | Yes |
| ChoosingAttachments | SelectingFor, Selections | No direct visual path discovered | Not emitted explicitly |
| ClassLevel | Level | No direct visual path discovered | Yes |
| CoinFlip | CoinFlipResult | ChooseRandomUXEvent_CoinFlip | Yes |
| ColorProduction |  | No direct visual path discovered | Not emitted explicitly |
| ControllerChanged |  | UpdateCardModelUXEvent, WaitForSecondsUXEvent | Yes |
| CopiedObject | CopyObject, LayeredEffectType, REASON, abilityGrpId, copyFromGrpid | No direct visual path discovered | Yes |
| CopyException | manaCost | No direct visual path discovered | Not emitted explicitly |
| Counter | count, counter_type, perpetual_count | No direct visual path discovered | Yes |
| CounterAdded | transaction_amount | No direct visual path discovered | Yes |
| CounterRemoved | transaction_amount | No direct visual path discovered | Yes |
| CrewedThisTurn |  | No direct visual path discovered | Not emitted explicitly |
| DamageDealt | damage, markDamage, type | UXEventDamageDealt, UpdateCardModelUXEvent, WaitForSecondsUXEvent | Yes |
| DamagedThisTurn |  | No direct visual path discovered | Not emitted explicitly |
| DelayedTriggerAffectees | abilityGrpId, removesFromZone | No direct visual path discovered | Not emitted explicitly |
| Designation | ActivePlayerSpellCount, ColorIdentity, ControllerId, CostIncrease, PreparedCopyZcid, PromptMessage, grpid, skin_code, source_grpid, value | No direct visual path discovered | Yes |
| DieRoll | Faces, Ignored, NaturalResult, Result | DieRollUXEvent, UpdateCardModelUXEvent, WaitForSecondsUXEvent | Yes |
| DieRoll | Faces, Ignored, NaturalResult, Result | No direct visual path discovered | Yes |
| DisplayCardUnderCard | Disable, DisplayUnderObjects | No direct visual path discovered | Not emitted explicitly |
| DisqualifiedEffect |  | DisqualifiedEffectUXEvent | Not emitted explicitly |
| DungeonStatus | AllDungeonsCompleted, CurrentDungeon, CurrentDungeonZCID, CurrentRoom | No direct visual path discovered | Not emitted explicitly |
| DynamicAbility | action_cost_string, base_grpid, cost, grpid, o0, parameter, {} | No direct visual path discovered | Not emitted explicitly |
| EnteredZoneThisTurn |  | No direct visual path discovered | Yes |
| FaceDown | REASON, abilityGrpId | No direct visual path discovered | Yes |
| GainDesignation |  | CardReactionUXEvent, UpdateCardModelUXEvent, WaitForSecondsUXEvent | Not emitted explicitly |
| GamewideHistoryCount | AbilityGrpId, count | No direct visual path discovered | Not emitted explicitly |
| GenericRelationship | category | No direct visual path discovered | Not emitted explicitly |
| HiddenInformation | ManaValue | No direct visual path discovered | Not emitted explicitly |
| HighlightReason | Reason | No direct visual path discovered | Not emitted explicitly |
| InstanceRevealedToOpponent |  | No direct visual path discovered | Yes |
| LayeredEffect | CopyObject, Duration, LayeredEffectType, PreviousFacedownAbilityGrpId, PreviousFacedownReason, abilityGrpId, effect_id, isTop, sourceAbilityGRPID | No direct visual path discovered | Yes |
| LayeredEffectCreated |  | AbilityAddedUXEvent, UpdateCardModelUXEvent, WaitForSecondsUXEvent | Not emitted explicitly |
| LinkInfo | Choice_Domain, Choice_Value, LinkType | CardNamedUXEvent | Yes |
| LinkInfo | ChooseLinkType, LinkType, TypeCategory, sourceAbilityGRPID, value | No direct visual path discovered | Yes |
| LinkedDamage |  | No direct visual path discovered | Not emitted explicitly |
| LoopCount |  | No direct visual path discovered | Not emitted explicitly |
| LoseDesignation |  | UpdateCardModelUXEvent, WaitForSecondsUXEvent | Not emitted explicitly |
| LossOfGame | gameEffectCardGrpId, reason | No direct visual path discovered | Yes |
| LoyaltyActivationsRemaining | ActivationsRemaining | No direct visual path discovered | Yes |
| ManaDetails | Enum_Type, Enum_Value_ | No direct visual path discovered | Not emitted explicitly |
| ManaPaid | color, id, substitution_grpid | ManaProducedUXEvent | Yes |
| MiscContinuousEffect | Chosen_SubType, QualificationSubtype, QualificationType, extra_turns, grpid | No direct visual path discovered | Not emitted explicitly |
| ModifiedColor | Add, Set, color, modificationType | No direct visual path discovered | Yes |
| ModifiedLife | life | LifeTotalUpdateUXEvent | Yes |
| ModifiedPower |  | No direct visual path discovered | Not emitted explicitly |
| ModifiedToughness |  | No direct visual path discovered | Not emitted explicitly |
| ModifiedType | effect_id, sourceAbilityGRPID, temporaryCardType | No direct visual path discovered | Not emitted explicitly |
| MultiplayerNumericAid | value | No direct visual path discovered | Not emitted explicitly |
| MultistepEffectComplete |  | MultistepEffectCompletedUXEvent, MultistepEffectStartedUXEvent | Yes |
| MultistepEffectStarted |  | MultistepEffectCompletedUXEvent, MultistepEffectStartedUXEvent | Yes |
| NewTurnStarted |  | UpdateTurnUXEvent | Yes |
| ObjectIdChanged | new_id, orig_id | No direct visual path discovered | Not emitted explicitly |
| ObjectsSelected |  | No direct visual path discovered | Not emitted explicitly |
| ObjectsToDistinguish |  | No direct visual path discovered | Not emitted explicitly |
| PendingEffect | TransferCounter, count, counter_type, effect_type | CounterProducedUXEvent, CountersChangedUXEvent | Not emitted explicitly |
| PermanentRegenerated |  | RegeneratedCardUXEvent | Yes |
| PhaseOrStepModified | phase, step | UXEventUpdatePhase | Yes |
| PhasedIn |  | UpdateCardModelUXEvent, WaitForSecondsUXEvent | Yes |
| PhasedOut |  | UpdateCardModelUXEvent, WaitForSecondsUXEvent | Yes |
| PlayerSelectingTargets |  | NullVfxProvider, PlayVFXUXEvent | Yes |
| PlayerSubmittedTargets |  | NullVfxProvider, PlayVFXUXEvent | Yes |
| PowerToughnessModCreated | power, toughness | UpdateCardModelUXEvent, WaitForSecondsUXEvent | Not emitted explicitly |
| PredictedDirectDamage | PredictiveDamage, modifierString, value | No direct visual path discovered | Not emitted explicitly |
| Qualification | QualificationSubtype, QualificationType, SourceParent, [Qualification] Tgt:{0}_Src:{1}_Type:{2}, grpid, parent_grpid | No direct visual path discovered | Yes |
| ReferencedCardNames |  | No direct visual path discovered | Not emitted explicitly |
| ReferencedObjects |  | No direct visual path discovered | Not emitted explicitly |
| RegeneratePending |  | No direct visual path discovered | Yes |
| RemainingSelections |  | No direct visual path discovered | Not emitted explicitly |
| RemoveAbility | effect_id | No direct visual path discovered | Not emitted explicitly |
| RemoveAttachment | invalidating_grpid | IllegalAttachmentUXEvent | Yes |
| ReplacementEffect | ReplacementSourceZcid, damageRedirectionDestination, durability, grpid, replacedEffectRecipient, replacedEffectSource, replacementEffectSpawnerType | No direct visual path discovered | Not emitted explicitly |
| ReplacementEffectApplied | IsDamageReplacement, grpid | ReplacementEffectAppliedUXEvent | Yes |
| ReplacementEffectApplied | IsDamageReplacement, grpid | No direct visual path discovered | Yes |
| ResolutionComplete | grpid | ResolutionEffectModel, ResolutionEventEndedUXEvent, ResolutionEventStartedUXEvent | Yes |
| ResolutionComplete |  | No direct visual path discovered | Yes |
| ResolutionStart | grpid | ResolutionEffectModel, ResolutionEventEndedUXEvent, ResolutionEventStartedUXEvent | Yes |
| ResolutionStart |  | No direct visual path discovered | Yes |
| RevealedCardCreated |  | No direct visual path discovered | Not emitted explicitly |
| RevealedCardDeleted |  | No direct visual path discovered | Yes |
| SaddledThisTurn |  | No direct visual path discovered | Not emitted explicitly |
| Scry | bottomIds, effectGrpid, graveyardIds, libraryIds, topIds | ScryResultUXEvent | Yes |
| ShouldntPlay | Reason, ability_grpid | No direct visual path discovered | Not emitted explicitly |
| Shuffle | NewIds, OldIds | DeckShuffleUXEvent | Yes |
| SupplementalText | AbilityGrpId, AbilityWordName, ExcludedFromCount, ShowOnMiniCDC, choice, colors, for-mode, isActive, manaSpec, threshold, trigger_condition_met, types, value | No direct visual path discovered | Not emitted explicitly |
| SuppressedPowerAndToughness | power, toughness | No direct visual path discovered | Not emitted explicitly |
| SuspendLike | abilityGrpid | No direct visual path discovered | Not emitted explicitly |
| SwitchPowerToughness |  | No direct visual path discovered | Not emitted explicitly |
| SyntheticEvent | type | SyntheticEventUXEvent | Not emitted explicitly |
| TappedUntappedPermanent | tapped | UpdateCardModelUXEvent, WaitForSecondsUXEvent | Yes |
| TargetSpec | abilityGrpId, distributions, index, promptId, promptParameters | No direct visual path discovered | Yes |
| TemporaryPermanent |  | No direct visual path discovered | Not emitted explicitly |
| TextChange | effect_id | No direct visual path discovered | Not emitted explicitly |
| TokenCreated |  | UpdateCardModelUXEvent, ZoneTransferUXEvent | Yes |
| TokenDeleted |  | No direct visual path discovered | Yes |
| TokenImmediatelyDied |  | No direct visual path discovered | Not emitted explicitly |
| TriggeringObject | NEW_OBJECT_ID, source_zone | No direct visual path discovered | Not emitted explicitly |
| TurnPermanent |  | No direct visual path discovered | Not emitted explicitly |
| UseOrCostsManaCost |  | No direct visual path discovered | Yes |
| UserActionTaken | abilityGrpId, actionType | UserActionTakenUXEvent | Yes |
| ZoneTransfer | zone_dest, zone_src | ZoneTransferUXEvent | Yes |

## Limits

- Static candidates are not a complete call graph.
- Playback inventory includes every nonnested UXEvent-named class in the DuelScene.UXEvents namespace plus referenced playback types; it includes base classes and tutorial UI.
- Dynamic asset lookups and Unity bundle contents are not enumerated.
- A Forge adapter must supply truthful event source, targets, ordering and details.
- This report does not change the private server or enable animations automatically.

Some playback is generated from state differences without an explicit annotation. A missing annotation entry does not by itself prove a missing feature.
