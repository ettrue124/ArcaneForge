# Arena client action inventory

Extracted at 2026-10-08T00:31:23.474102+00:00 from `C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed`.

This is the declared protocol and static handler evidence for this client version. It does not prove that every enum value is usable, implement Forge adapters, or recover official server logic.

## Incoming game messages

| Message | Value | Payload | Handler found |
|---|---:|---|---|
| None | 0 | No unique field match | No |
| GameStateMessage | 1 | wotc.mtgo.gre.external.messaging.GameStateMessage | No |
| ActionsAvailableReq | 2 | wotc.mtgo.gre.external.messaging.ActionsAvailableReq | No |
| ChooseStartingPlayerReq | 6 | wotc.mtgo.gre.external.messaging.ChooseStartingPlayerReq | No |
| ConnectResp | 7 | wotc.mtgo.gre.external.messaging.ConnectResp | No |
| GetSettingsResp | 9 | wotc.mtgo.gre.external.messaging.GetSettingsResp | No |
| SetSettingsResp | 10 | wotc.mtgo.gre.external.messaging.SetSettingsResp | No |
| GroupReq | 11 | wotc.mtgo.gre.external.messaging.GroupReq | No |
| IllegalRequest | 12 | No unique field match | No |
| MulliganReq | 15 | wotc.mtgo.gre.external.messaging.MulliganReq | No |
| OrderReq | 17 | wotc.mtgo.gre.external.messaging.OrderReq | No |
| PromptReq | 18 | No unique field match | No |
| RevealHandReq | 21 | No unique field match | No |
| SelectNReq | 22 | wotc.mtgo.gre.external.messaging.SelectNReq | No |
| AllowForceDraw | 24 | No unique field match | No |
| BinaryGameState | 25 | wotc.mtgo.gre.external.messaging.BinaryGameState | No |
| DeclareAttackersReq | 26 | wotc.mtgo.gre.external.messaging.DeclareAttackersReq | No |
| SubmitAttackersResp | 27 | No unique field match | No |
| DeclareBlockersReq | 28 | wotc.mtgo.gre.external.messaging.DeclareBlockersReq | No |
| SubmitBlockersResp | 29 | No unique field match | No |
| AssignDamageReq | 30 | wotc.mtgo.gre.external.messaging.AssignDamageReq | No |
| AssignDamageConfirmation | 31 | No unique field match | No |
| OrderCombatDamageReq | 32 | No unique field match | No |
| OrderDamageConfirmation | 33 | No unique field match | No |
| SelectTargetsReq | 34 | wotc.mtgo.gre.external.messaging.SelectTargetsReq | No |
| SubmitTargetsResp | 35 | No unique field match | No |
| PayCostsReq | 36 | wotc.mtgo.gre.external.messaging.PayCostsReq | No |
| IntermissionReq | 37 | wotc.mtgo.gre.external.messaging.IntermissionReq | No |
| DieRollResultsResp | 38 | wotc.mtgo.gre.external.messaging.DieRollResultsResp | No |
| SelectReplacementReq | 39 | wotc.mtgo.gre.external.messaging.SelectReplacementReq | No |
| SelectNGroupReq | 40 | wotc.mtgo.gre.external.messaging.SelectNGroupReq | No |
| DistributionReq | 42 | wotc.mtgo.gre.external.messaging.DistributionReq | No |
| NumericInputReq | 43 | wotc.mtgo.gre.external.messaging.NumericInputReq | No |
| SearchReq | 44 | wotc.mtgo.gre.external.messaging.SearchReq | No |
| OptionalActionMessage | 45 | wotc.mtgo.gre.external.messaging.OptionalActionMessage | No |
| CastingTimeOptionsReq | 46 | wotc.mtgo.gre.external.messaging.CastingTimeOptionsReq | No |
| SelectFromGroupsReq | 48 | wotc.mtgo.gre.external.messaging.SelectFromGroupsReq | No |
| SearchFromGroupsReq | 49 | wotc.mtgo.gre.external.messaging.SearchFromGroupsReq | No |
| GatherReq | 50 | wotc.mtgo.gre.external.messaging.GatherReq | No |
| QueuedGameStateMessage | 51 | wotc.mtgo.gre.external.messaging.GameStateMessage | No |
| UIMessage | 52 | wotc.mtgo.gre.external.messaging.UIMessage | No |
| SubmitDeckReq | 53 | wotc.mtgo.gre.external.messaging.SubmitDeckReq | No |
| EdictalMessage | 54 | wotc.mtgo.gre.external.messaging.EdictalMessage | No |
| TimeoutMessage | 55 | wotc.mtgo.gre.external.messaging.TimeoutMessage | No |
| TimerStateMessage | 56 | wotc.mtgo.gre.external.messaging.TimerStateMessage | No |
| SubmitDeckConfirmation | 57 | No unique field match | No |
| StringInputReq | 58 | wotc.mtgo.gre.external.messaging.StringInputReq | No |
| SelectCountersReq | 59 | wotc.mtgo.gre.external.messaging.SelectCountersReq | No |
| PredictionResp | 60 | wotc.mtgo.gre.external.messaging.PredictionResp | No |

## ActionType

| Wire enum | Value |
|---|---:|
| ActionType_None | 0 |
| ActionType_Cast | 1 |
| ActionType_Activate | 2 |
| ActionType_Play | 3 |
| ActionType_Activate_Mana | 4 |
| ActionType_Pass | 5 |
| ActionType_Activate_Test | 6 |
| ActionType_Special | 7 |
| ActionType_Special_TurnFaceUp | 8 |
| ActionType_ResolutionCost | 9 |
| ActionType_CastLeft | 10 |
| ActionType_CastRight | 11 |
| ActionType_Make_Payment | 12 |
| ActionType_CombatCost | 14 |
| ActionType_OpeningHandAction | 15 |
| ActionType_CastAdventure | 16 |
| ActionType_FloatMana | 17 |
| ActionType_CastMDFC | 18 |
| ActionType_PlayMDFC | 19 |
| ActionType_Special_Payment | 20 |
| ActionType_CastPrototype | 21 |
| ActionType_CastLeftRoom | 22 |
| ActionType_CastRightRoom | 23 |
| ActionType_CastOmen | 24 |
| ActionType_Placeholder25 | 25 |

## CastingTimeOptionType

| Wire enum | Value |
|---|---:|
| CastingTimeOptionType_None | 0 |
| CastingTimeOptionType_Done | 1 |
| CastingTimeOptionType_ChooseX | 2 |
| CastingTimeOptionType_Kicker | 3 |
| CastingTimeOptionType_Multikicker | 4 |
| CastingTimeOptionType_AdditionalCost | 5 |
| CastingTimeOptionType_OptionalCost | 6 |
| CastingTimeOptionType_Replicate | 7 |
| CastingTimeOptionType_Conspire | 8 |
| CastingTimeOptionType_ManaType | 9 |
| CastingTimeOptionType_Modal | 10 |
| CastingTimeOptionType_ChooseOrCost | 11 |
| CastingTimeOptionType_Selection | 12 |
| CastingTimeOptionType_CastThroughAbility | 13 |
| CastingTimeOptionType_TimingPermission | 14 |
| CastingTimeOptionType_Casualty | 15 |
| CastingTimeOptionType_Specialize | 16 |
| CastingTimeOptionType_Bargain | 17 |
| CastingTimeOptionType_OptionalCostPaid | 18 |

## AnnotationType

| Wire enum | Value |
|---|---:|
| AnnotationType_None | 0 |
| AnnotationType_ZoneTransfer | 1 |
| AnnotationType_LossOfGame | 2 |
| AnnotationType_DamageDealt | 3 |
| AnnotationType_TappedUntappedPermanent | 4 |
| AnnotationType_ModifiedPower | 5 |
| AnnotationType_ModifiedToughness | 6 |
| AnnotationType_ModifiedColor | 7 |
| AnnotationType_PhaseOrStepModified | 8 |
| AnnotationType_AddAbility | 9 |
| AnnotationType_ModifiedLife | 10 |
| AnnotationType_CreateAttachment | 11 |
| AnnotationType_RemoveAttachment | 12 |
| AnnotationType_ObjectIdChanged | 13 |
| AnnotationType_Counter | 14 |
| AnnotationType_ControllerChanged | 15 |
| AnnotationType_CounterAdded | 16 |
| AnnotationType_CounterRemoved | 17 |
| AnnotationType_LayeredEffectCreated | 18 |
| AnnotationType_LayeredEffectDestroyed | 19 |
| AnnotationType_Attachment | 20 |
| AnnotationType_Haunt | 21 |
| AnnotationType_CopiedObject | 22 |
| AnnotationType_RemoveAbility | 23 |
| AnnotationType_WinTheGame | 24 |
| AnnotationType_ModifiedType | 25 |
| AnnotationType_TargetSpec | 26 |
| AnnotationType_TextChange | 27 |
| AnnotationType_FaceDown | 28 |
| AnnotationType_TurnPermanent | 29 |
| AnnotationType_DynamicAbility | 30 |
| AnnotationType_ObjectsSelected | 31 |
| AnnotationType_TriggeringObject | 32 |
| AnnotationType_DamageSource | 33 |
| AnnotationType_ManaPaid | 34 |
| AnnotationType_TokenCreated | 35 |
| AnnotationType_AbilityInstanceCreated | 36 |
| AnnotationType_AbilityInstanceDeleted | 37 |
| AnnotationType_DisplayCardUnderCard | 38 |
| AnnotationType_AbilityWordActive | 39 |
| AnnotationType_LinkInfo | 40 |
| AnnotationType_TokenDeleted | 41 |
| AnnotationType_Qualification | 42 |
| AnnotationType_ResolutionStart | 43 |
| AnnotationType_ResolutionComplete | 44 |
| AnnotationType_Designation | 45 |
| AnnotationType_GainDesignation | 46 |
| AnnotationType_CardRevealed | 47 |
| AnnotationType_NewTurnStarted | 48 |
| AnnotationType_ManaDetails | 49 |
| AnnotationType_DisqualifiedEffect | 50 |
| AnnotationType_LayeredEffect | 51 |
| AnnotationType_MiscContinuousEffect | 52 |
| AnnotationType_ShouldntPlay | 53 |
| AnnotationType_UseOrCostsManaCost | 54 |
| AnnotationType_RemainingSelections | 55 |
| AnnotationType_Shuffle | 56 |
| AnnotationType_CoinFlip | 57 |
| AnnotationType_ChoiceResult | 58 |
| AnnotationType_RevealedCardCreated | 59 |
| AnnotationType_RevealedCardDeleted | 60 |
| AnnotationType_SuspendLike | 61 |
| AnnotationType_ReplacementEffect | 62 |
| AnnotationType_EnteredZoneThisTurn | 63 |
| AnnotationType_CastingTimeOption | 64 |
| AnnotationType_Scry | 65 |
| AnnotationType_PredictedDirectDamage | 66 |
| AnnotationType_SwitchPowerToughness | 67 |
| AnnotationType_SupplementalText | 68 |
| AnnotationType_PendingEffect | 69 |
| AnnotationType_AttachmentCreated | 70 |
| AnnotationType_PowerToughnessModCreated | 71 |
| AnnotationType_SyntheticEvent | 72 |
| AnnotationType_UserActionTaken | 73 |
| AnnotationType_DelayedTriggerAffectees | 74 |
| AnnotationType_InstanceRevealedToOpponent | 75 |
| AnnotationType_ModifiedName | 76 |
| AnnotationType_ReplacementEffectApplied | 77 |
| AnnotationType_ReferencedObjects | 78 |
| AnnotationType_ChoosingAttachments | 79 |
| AnnotationType_TemporaryPermanent | 80 |
| AnnotationType_GamewideHistoryCount | 81 |
| AnnotationType_AbilityExhausted | 82 |
| AnnotationType_MultistepEffectStarted | 83 |
| AnnotationType_MultistepEffectComplete | 84 |
| AnnotationType_DieRoll | 85 |
| AnnotationType_DungeonStatus | 86 |
| AnnotationType_LinkedDamage | 87 |
| AnnotationType_ClassLevel | 88 |
| AnnotationType_TokenImmediatelyDied | 89 |
| AnnotationType_DamagedThisTurn | 90 |
| AnnotationType_ReferencedCardNames | 91 |
| AnnotationType_PlayerSelectingTargets | 92 |
| AnnotationType_PlayerSubmittedTargets | 93 |
| AnnotationType_CrewedThisTurn | 94 |
| AnnotationType_PhasedOut | 95 |
| AnnotationType_PhasedIn | 96 |
| AnnotationType_LoyaltyActivationsRemaining | 97 |
| AnnotationType_GroupedIds | 98 |
| AnnotationType_RegeneratePending | 99 |
| AnnotationType_PermanentRegenerated | 100 |
| AnnotationType_ObjectsToDistinguish | 101 |
| AnnotationType_BoonInfo | 102 |
| AnnotationType_LoseDesignation | 103 |
| AnnotationType_SaddledThisTurn | 104 |
| AnnotationType_HiddenInformation | 105 |
| AnnotationType_LoopCount | 106 |
| AnnotationType_Vote | 107 |
| AnnotationType_SuppressedPowerAndToughness | 108 |
| AnnotationType_GenericRelationship | 109 |
| AnnotationType_ColorProduction | 110 |
| AnnotationType_CopyException | 111 |
| AnnotationType_SelectNDecoration | 112 |
| AnnotationType_ModifiedCost | 113 |
| AnnotationType_MultiplayerNumericAid | 114 |
| AnnotationType_HighlightReason | 115 |
| AnnotationType_PersistentChoiceResult | 116 |
| AnnotationType_EnteredZoneAtSameTime | 117 |

## ClientMessageType

| Wire enum | Value |
|---|---:|
| ClientMessageType_None | 0 |
| ClientMessageType_ConnectReq | 1 |
| ClientMessageType_CancelActionReq | 5 |
| ClientMessageType_ConcedeReq | 7 |
| ClientMessageType_EnterSideboardingReq | 8 |
| ClientMessageType_ForceDrawReq | 9 |
| ClientMessageType_CheckpointReq | 10 |
| ClientMessageType_GetSettingsReq | 11 |
| ClientMessageType_GroupResp | 12 |
| ClientMessageType_MulliganResp | 13 |
| ClientMessageType_OrderResp | 14 |
| ClientMessageType_PerformActionResp | 15 |
| ClientMessageType_ControlReq | 17 |
| ClientMessageType_SelectNResp | 18 |
| ClientMessageType_SetSettingsReq | 20 |
| ClientMessageType_UndoReq | 22 |
| ClientMessageType_ChooseStartingPlayerResp | 24 |
| ClientMessageType_OptionalActionResp | 25 |
| ClientMessageType_AllowForceDrawResp | 26 |
| ClientMessageType_RevealHandResp | 28 |
| ClientMessageType_DeclareAttackersResp | 30 |
| ClientMessageType_SubmitAttackersReq | 31 |
| ClientMessageType_DeclareBlockersResp | 32 |
| ClientMessageType_SubmitBlockersReq | 33 |
| ClientMessageType_OrderCombatDamageResp | 34 |
| ClientMessageType_AssignDamageResp | 35 |
| ClientMessageType_SelectTargetsResp | 36 |
| ClientMessageType_SubmitTargetsReq | 37 |
| ClientMessageType_DrawCardResp | 38 |
| ClientMessageType_SelectReplacementResp | 39 |
| ClientMessageType_SelectNGroupResp | 40 |
| ClientMessageType_DistributionResp | 42 |
| ClientMessageType_NumericInputResp | 43 |
| ClientMessageType_SearchResp | 44 |
| ClientMessageType_EffectCostResp | 45 |
| ClientMessageType_CastingTimeOptionsResp | 46 |
| ClientMessageType_SelectFromGroupsResp | 48 |
| ClientMessageType_SearchFromGroupsResp | 49 |
| ClientMessageType_GatherResp | 50 |
| ClientMessageType_AutoResp | 52 |
| ClientMessageType_UIMessage | 53 |
| ClientMessageType_SubmitDeckResp | 54 |
| ClientMessageType_TakeTimeoutReq | 55 |
| ClientMessageType_PerformAutoTapActionsResp | 56 |
| ClientMessageType_StringInputResp | 57 |
| ClientMessageType_SelectCountersResp | 58 |
| ClientMessageType_PredictionReq | 59 |

## Files

- `actions.json`: complete message/field/enum catalogue, handler references, fingerprints and limitations.
- `incoming-messages.csv`: filterable server-to-client message list.
- `arena-descriptor-set.pb` / `.json`: exact recovered protobuf descriptors.
- `sources/`: decompiled handlers and their request classes.

Gameplay messages are wrapped in `GREToClientMessage`, then `GreToClientEvent`, then `MatchServiceToClientMessage`. State IDs, message IDs, game objects, seats and response correlation must agree. See the exact fields in the JSON catalogue.

ActionType describes entries offered in ActionsAvailableReq; animation/game-state annotations are separate. ClientMessageType contains replies sent by Arena, not additional server-to-client commands.

## Extraction limitations

- Enum declarations include unused/reserved/unsupported entries; handler presence is recorded separately.
- Payload fields use structural name matches or member accesses recovered from the handler; unmatched entries require inspection.
- Decompiler references are static evidence, not proof of successful live gameplay.
- Each command needs truthful rules state, legal choices, sequencing and matching responses.
- Unity assets, all animation variants and official server implementations are outside this inventory.
