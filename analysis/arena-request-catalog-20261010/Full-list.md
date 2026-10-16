# Arena incoming requests and messages

Extracted from the installed Arena client on October 10, 2026.

Scope: server-to-client gameplay messages, match-service envelopes, their complete protobuf fields, and all extracted supporting message and enum definitions. This is the client-declared protocol, not a claim that every legacy value opens a working menu. Some messages have no dedicated payload field. Request legality also depends on current game state. Annotation names describe protocol events; exact animation behavior may additionally depend on detail keys and assets, which protobuf descriptors do not specify.

## Complete incoming gameplay message list

| Wire value | Message | Envelope payload field | Payload type |
|---:|---|---|---|
| 0 | None | No dedicated field | Envelope fields only / unspecified |
| 1 | GameStateMessage | gameStateMessage | GameStateMessage |
| 2 | ActionsAvailableReq | actionsAvailableReq | ActionsAvailableReq |
| 6 | ChooseStartingPlayerReq | chooseStartingPlayerReq | ChooseStartingPlayerReq |
| 7 | ConnectResp | connectResp | ConnectResp |
| 9 | GetSettingsResp | getSettingsResp | GetSettingsResp |
| 10 | SetSettingsResp | setSettingsResp | SetSettingsResp |
| 11 | GroupReq | groupReq | GroupReq |
| 12 | IllegalRequest | illegalRequestMessage | IllegalRequestMessage |
| 15 | MulliganReq | mulliganReq | MulliganReq |
| 17 | OrderReq | orderReq | OrderReq |
| 18 | PromptReq | No dedicated field | Envelope fields only / unspecified |
| 21 | RevealHandReq | No dedicated field | Envelope fields only / unspecified |
| 22 | SelectNReq | selectNReq | SelectNReq |
| 24 | AllowForceDraw | No dedicated field | Envelope fields only / unspecified |
| 25 | BinaryGameState | binaryGameState | BinaryGameState |
| 26 | DeclareAttackersReq | declareAttackersReq | DeclareAttackersReq |
| 27 | SubmitAttackersResp | No dedicated field | Envelope fields only / unspecified |
| 28 | DeclareBlockersReq | declareBlockersReq | DeclareBlockersReq |
| 29 | SubmitBlockersResp | No dedicated field | Envelope fields only / unspecified |
| 30 | AssignDamageReq | assignDamageReq | AssignDamageReq |
| 31 | AssignDamageConfirmation | No dedicated field | Envelope fields only / unspecified |
| 32 | OrderCombatDamageReq | No dedicated field | Envelope fields only / unspecified |
| 33 | OrderDamageConfirmation | No dedicated field | Envelope fields only / unspecified |
| 34 | SelectTargetsReq | selectTargetsReq | SelectTargetsReq |
| 35 | SubmitTargetsResp | No dedicated field | Envelope fields only / unspecified |
| 36 | PayCostsReq | payCostsReq | PayCostsReq |
| 37 | IntermissionReq | intermissionReq | IntermissionReq |
| 38 | DieRollResultsResp | dieRollResultsResp | DieRollResultsResp |
| 39 | SelectReplacementReq | selectReplacementReq | SelectReplacementReq |
| 40 | SelectNGroupReq | selectNGroupReq | SelectNGroupReq |
| 42 | DistributionReq | distributionReq | DistributionReq |
| 43 | NumericInputReq | numericInputReq | NumericInputReq |
| 44 | SearchReq | searchReq | SearchReq |
| 45 | OptionalActionMessage | optionalActionMessage | OptionalActionMessage |
| 46 | CastingTimeOptionsReq | castingTimeOptionsReq | CastingTimeOptionsReq |
| 48 | SelectFromGroupsReq | selectFromGroupsReq | SelectFromGroupsReq |
| 49 | SearchFromGroupsReq | searchFromGroupsReq | SearchFromGroupsReq |
| 50 | GatherReq | gatherReq | GatherReq |
| 51 | QueuedGameStateMessage | gameStateMessage | GameStateMessage |
| 52 | UIMessage | uiMessage | UIMessage |
| 53 | SubmitDeckReq | submitDeckReq | SubmitDeckReq |
| 54 | EdictalMessage | edictalMessage | EdictalMessage |
| 55 | TimeoutMessage | timeoutMessage | TimeoutMessage |
| 56 | TimerStateMessage | timerStateMessage | TimerStateMessage |
| 57 | SubmitDeckConfirmation | No dedicated field | Envelope fields only / unspecified |
| 58 | StringInputReq | stringInputReq | StringInputReq |
| 59 | SelectCountersReq | selectCountersReq | SelectCountersReq |
| 60 | PredictionResp | predictionResp | PredictionResp |

## How requests are delivered

`MatchServiceToClientMessage.greToClientEvent.greToClientMessages` carries `GREToClientMessage` entries. Each entry supplies its message type, recipient seats, message ID, game-state ID, prompt and applicable payload. A matching game state must describe any referenced card/player IDs. Do not treat message IDs, prompt IDs, enum values, and card IDs as interchangeable.

## Request and envelope fields

### wotc.mtgo.gre.external.messaging.MatchServiceToClientMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | transactionId | string |  |  |
| 3 | requestId | int32 |  |  |
| 4 | timestamp | int64 |  |  |
| 7 | error | wotc.mtgo.gre.external.messaging.MatchServiceError |  | message |
| 8 | greToClientEvent | wotc.mtgo.gre.external.messaging.GreToClientEvent |  | message |
| 9 | matchGameRoomStateChangedEvent | wotc.mtgo.gre.external.messaging.MatchGameRoomStateChangedEvent |  | message |
| 100 | authenticateResponse | wotc.mtgo.gre.external.messaging.AuthenticateResponse |  | message |
| 201 | createMatchGameRoomResponseV2 | wotc.mtgo.gre.external.messaging.CreateMatchGameRoomResponseV2 |  | message |
| 1000 | echoResponse | wotc.mtgo.gre.external.messaging.EchoResponse |  | message |

### wotc.mtgo.gre.external.messaging.GreToClientEvent

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | greToClientMessages | wotc.mtgo.gre.external.messaging.GREToClientMessage | yes |  |

### wotc.mtgo.gre.external.messaging.GREToClientMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | type | wotc.mtgo.gre.external.messaging.GREMessageType |  |  |
| 2 | systemSeatIds | uint32 | yes |  |
| 3 | msgId | uint32 |  |  |
| 4 | gameStateId | uint32 |  |  |
| 6 | prompt | wotc.mtgo.gre.external.messaging.Prompt |  |  |
| 5 | gameStateMessage | wotc.mtgo.gre.external.messaging.GameStateMessage |  | message |
| 7 | chooseStartingPlayerReq | wotc.mtgo.gre.external.messaging.ChooseStartingPlayerReq |  | message |
| 8 | optionalActionMessage | wotc.mtgo.gre.external.messaging.OptionalActionMessage |  | message |
| 9 | actionsAvailableReq | wotc.mtgo.gre.external.messaging.ActionsAvailableReq |  | message |
| 10 | orderReq | wotc.mtgo.gre.external.messaging.OrderReq |  | message |
| 11 | groupReq | wotc.mtgo.gre.external.messaging.GroupReq |  | message |
| 12 | selectNReq | wotc.mtgo.gre.external.messaging.SelectNReq |  | message |
| 14 | mulliganReq | wotc.mtgo.gre.external.messaging.MulliganReq |  | message |
| 16 | getSettingsResp | wotc.mtgo.gre.external.messaging.GetSettingsResp |  | message |
| 17 | setSettingsResp | wotc.mtgo.gre.external.messaging.SetSettingsResp |  | message |
| 18 | connectResp | wotc.mtgo.gre.external.messaging.ConnectResp |  | message |
| 19 | illegalRequestMessage | wotc.mtgo.gre.external.messaging.IllegalRequestMessage |  | message |
| 23 | binaryGameState | wotc.mtgo.gre.external.messaging.BinaryGameState |  | message |
| 24 | declareAttackersReq | wotc.mtgo.gre.external.messaging.DeclareAttackersReq |  | message |
| 26 | declareBlockersReq | wotc.mtgo.gre.external.messaging.DeclareBlockersReq |  | message |
| 28 | assignDamageReq | wotc.mtgo.gre.external.messaging.AssignDamageReq |  | message |
| 32 | selectTargetsReq | wotc.mtgo.gre.external.messaging.SelectTargetsReq |  | message |
| 34 | payCostsReq | wotc.mtgo.gre.external.messaging.PayCostsReq |  | message |
| 36 | intermissionReq | wotc.mtgo.gre.external.messaging.IntermissionReq |  | message |
| 37 | dieRollResultsResp | wotc.mtgo.gre.external.messaging.DieRollResultsResp |  | message |
| 38 | selectReplacementReq | wotc.mtgo.gre.external.messaging.SelectReplacementReq |  | message |
| 39 | selectNGroupReq | wotc.mtgo.gre.external.messaging.SelectNGroupReq |  | message |
| 41 | distributionReq | wotc.mtgo.gre.external.messaging.DistributionReq |  | message |
| 42 | numericInputReq | wotc.mtgo.gre.external.messaging.NumericInputReq |  | message |
| 43 | searchReq | wotc.mtgo.gre.external.messaging.SearchReq |  | message |
| 44 | castingTimeOptionsReq | wotc.mtgo.gre.external.messaging.CastingTimeOptionsReq |  | message |
| 47 | selectFromGroupsReq | wotc.mtgo.gre.external.messaging.SelectFromGroupsReq |  | message |
| 48 | searchFromGroupsReq | wotc.mtgo.gre.external.messaging.SearchFromGroupsReq |  | message |
| 49 | gatherReq | wotc.mtgo.gre.external.messaging.GatherReq |  | message |
| 51 | uiMessage | wotc.mtgo.gre.external.messaging.UIMessage |  | message |
| 52 | submitDeckReq | wotc.mtgo.gre.external.messaging.SubmitDeckReq |  | message |
| 53 | edictalMessage | wotc.mtgo.gre.external.messaging.EdictalMessage |  | message |
| 54 | timeoutMessage | wotc.mtgo.gre.external.messaging.TimeoutMessage |  | message |
| 55 | timerStateMessage | wotc.mtgo.gre.external.messaging.TimerStateMessage |  | message |
| 57 | stringInputReq | wotc.mtgo.gre.external.messaging.StringInputReq |  | message |
| 58 | selectCountersReq | wotc.mtgo.gre.external.messaging.SelectCountersReq |  | message |
| 59 | predictionResp | wotc.mtgo.gre.external.messaging.PredictionResp |  | message |
| 45 | informationalUseOnly | bool |  |  |
| 50 | allowCancel | wotc.mtgo.gre.external.messaging.AllowCancel |  |  |
| 56 | allowUndo | bool |  |  |
| 60 | allowPrediction | bool |  |  |

### wotc.mtgo.gre.external.messaging.UIMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seatIds | uint32 | yes |  |
| 2 | onSelect | wotc.mtgo.gre.external.messaging.OnSelect |  | message |
| 3 | onHover | wotc.mtgo.gre.external.messaging.OnHover |  | message |
| 4 | onShuffle | wotc.mtgo.gre.external.messaging.OnShuffle |  | message |
| 5 | onChat | wotc.mtgo.gre.external.messaging.OnChat |  | message |
| 6 | onGenericEvent | wotc.mtgo.gre.external.messaging.OnGenericEvent |  | message |

### wotc.mtgo.gre.external.messaging.GameStateMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | type | wotc.mtgo.gre.external.messaging.GameStateType |  |  |
| 2 | gameStateId | uint32 |  |  |
| 3 | gameInfo | wotc.mtgo.gre.external.messaging.GameInfo |  |  |
| 4 | teams | wotc.mtgo.gre.external.messaging.TeamInfo | yes |  |
| 5 | players | wotc.mtgo.gre.external.messaging.PlayerInfo | yes |  |
| 6 | turnInfo | wotc.mtgo.gre.external.messaging.TurnInfo |  |  |
| 7 | zones | wotc.mtgo.gre.external.messaging.ZoneInfo | yes |  |
| 8 | gameObjects | wotc.mtgo.gre.external.messaging.GameObjectInfo | yes |  |
| 9 | annotations | wotc.mtgo.gre.external.messaging.AnnotationInfo | yes |  |
| 10 | diffDeletedInstanceIds | uint32 | yes |  |
| 11 | pendingMessageCount | uint32 |  |  |
| 12 | prevGameStateId | uint32 |  |  |
| 13 | timers | wotc.mtgo.gre.external.messaging.TimerInfo | yes |  |
| 14 | update | wotc.mtgo.gre.external.messaging.GameStateUpdate |  |  |
| 15 | actions | wotc.mtgo.gre.external.messaging.ActionInfo | yes |  |
| 16 | persistentAnnotations | wotc.mtgo.gre.external.messaging.AnnotationInfo | yes |  |
| 17 | diffDeletedPersistentAnnotationIds | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.AnnotationInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | id | uint32 |  |  |
| 2 | affectorId | uint32 |  |  |
| 3 | affectedIds | uint32 | yes |  |
| 4 | type | wotc.mtgo.gre.external.messaging.AnnotationType | yes |  |
| 6 | details | wotc.mtgo.gre.external.messaging.KeyValuePairInfo | yes |  |
| 7 | redactAffector | bool |  |  |
| 8 | excludedSeatIds | uint32 | yes |  |
| 9 | redactAffected | bool |  |  |
| 10 | temporarilyExcludedSeatIds | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.Prompt

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | promptId | uint32 |  |  |
| 2 | parameters | wotc.mtgo.gre.external.messaging.PromptParameter | yes |  |

### wotc.mtgo.gre.external.messaging.ActionsAvailableReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | actions | wotc.mtgo.gre.external.messaging.Action | yes |  |
| 2 | inactiveActions | wotc.mtgo.gre.external.messaging.Action | yes |  |

### wotc.mtgo.gre.external.messaging.ChooseStartingPlayerReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | teamType | wotc.mtgo.gre.external.messaging.TeamType |  |  |
| 2 | systemSeatIds | uint32 | yes |  |
| 3 | teamIds | uint32 | yes |  |
| 4 | prompt | wotc.mtgo.gre.external.messaging.Prompt |  |  |

### wotc.mtgo.gre.external.messaging.ConnectResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | status | wotc.mtgo.gre.external.messaging.ConnectionStatus |  |  |
| 6 | protoVer | wotc.mtgo.gre.external.messaging.ProtoVersion |  |  |
| 8 | settings | wotc.mtgo.gre.external.messaging.SettingsMessage |  |  |
| 9 | deckMessage | wotc.mtgo.gre.external.messaging.DeckMessage |  |  |
| 10 | grpVersion | wotc.mtgo.gre.external.messaging.Version |  |  |
| 11 | greVersion | wotc.mtgo.gre.external.messaging.Version |  |  |
| 12 | skins | wotc.mtgo.gre.external.messaging.CardSkinTuple | yes |  |
| 13 | grpChangelist | uint32 |  |  |
| 14 | greChangelist | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.GetSettingsResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | settings | wotc.mtgo.gre.external.messaging.SettingsMessage |  |  |

### wotc.mtgo.gre.external.messaging.SetSettingsResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | settings | wotc.mtgo.gre.external.messaging.SettingsMessage |  |  |

### wotc.mtgo.gre.external.messaging.GroupReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | instanceIds | uint32 | yes |  |
| 2 | groupSpecs | wotc.mtgo.gre.external.messaging.GroupSpecification | yes |  |
| 5 | groupType | wotc.mtgo.gre.external.messaging.GroupType |  |  |
| 6 | context | wotc.mtgo.gre.external.messaging.GroupingContext |  |  |
| 7 | sourceId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.IllegalRequestMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | invalidMessage | wotc.mtgo.gre.external.messaging.ClientToGREMessage |  |  |
| 2 | reason | wotc.mtgo.gre.external.messaging.FailureReason |  |  |

### wotc.mtgo.gre.external.messaging.MulliganReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | mulliganType | wotc.mtgo.gre.external.messaging.MulliganType |  |  |
| 2 | freeMulliganCount | uint32 |  |  |
| 3 | mulliganCount | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.OrderReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ids | uint32 | yes |  |
| 3 | orderingContext | wotc.mtgo.gre.external.messaging.OrderingContext |  |  |

### wotc.mtgo.gre.external.messaging.SelectNReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | minSel | int32 |  |  |
| 2 | maxSel | uint32 |  |  |
| 3 | context | wotc.mtgo.gre.external.messaging.SelectionContext |  |  |
| 5 | optionContext | wotc.mtgo.gre.external.messaging.OptionContext |  |  |
| 6 | listType | wotc.mtgo.gre.external.messaging.SelectionListType |  |  |
| 7 | ids | uint32 | yes |  |
| 8 | weights | int32 | yes |  |
| 9 | staticList | wotc.mtgo.gre.external.messaging.StaticList |  |  |
| 11 | prompt | wotc.mtgo.gre.external.messaging.Prompt |  |  |
| 12 | idType | wotc.mtgo.gre.external.messaging.IdType |  |  |
| 14 | unfiltered_ids | uint32 | yes |  |
| 15 | sourceId | uint32 |  |  |
| 16 | hot_ids | uint32 | yes |  |
| 17 | validationType | wotc.mtgo.gre.external.messaging.SelectionValidationType |  |  |
| 18 | zone_ids | uint32 | yes |  |
| 19 | shouldCancel | bool |  |  |
| 20 | minWeight | int32 |  |  |
| 21 | maxWeight | int32 |  |  |

### wotc.mtgo.gre.external.messaging.BinaryGameState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | gameStateMsg | wotc.mtgo.gre.external.messaging.GameStateMessage |  |  |
| 2 | instanceData | bytes |  |  |
| 4 | internalGREData | bytes |  |  |

### wotc.mtgo.gre.external.messaging.DeclareAttackersReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | attackers | wotc.mtgo.gre.external.messaging.Attacker | yes |  |
| 2 | hasRequirements | bool |  |  |
| 3 | hasRestrictions | bool |  |  |
| 4 | AttackWarnings | wotc.mtgo.gre.external.messaging.AttackWarning | yes |  |
| 5 | manaCost | wotc.mtgo.gre.external.messaging.ManaRequirement | yes |  |
| 7 | qualifiedAttackers | wotc.mtgo.gre.external.messaging.Attacker | yes |  |
| 8 | canSubmitAttackers | bool |  |  |

### wotc.mtgo.gre.external.messaging.DeclareBlockersReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | blockers | wotc.mtgo.gre.external.messaging.Blocker | yes |  |
| 2 | hasRequirements | bool |  |  |
| 3 | hasRestrictions | bool |  |  |
| 4 | blockWarnings | wotc.mtgo.gre.external.messaging.BlockWarning | yes |  |
| 5 | manaCost | wotc.mtgo.gre.external.messaging.ManaRequirement | yes |  |

### wotc.mtgo.gre.external.messaging.AssignDamageReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | damageAssigners | wotc.mtgo.gre.external.messaging.DamageAssigner | yes |  |

### wotc.mtgo.gre.external.messaging.SelectTargetsReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | targets | wotc.mtgo.gre.external.messaging.TargetSelection | yes |  |
| 2 | sourceId | uint32 |  |  |
| 3 | abilityGrpId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.PayCostsReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | manaCost | wotc.mtgo.gre.external.messaging.ManaRequirement | yes |  |
| 2 | paymentActions | wotc.mtgo.gre.external.messaging.ActionsAvailableReq |  |  |
| 3 | paymentSelection | wotc.mtgo.gre.external.messaging.SelectNReq |  |  |
| 4 | effectCostReq | wotc.mtgo.gre.external.messaging.EffectCostReq |  |  |
| 6 | autoTapActionsReq | wotc.mtgo.gre.external.messaging.AutoTapActionsAvailableReq |  |  |
| 7 | shouldCancel | bool |  |  |

### wotc.mtgo.gre.external.messaging.IntermissionReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | options | wotc.mtgo.gre.external.messaging.UserOption | yes |  |
| 2 | intermissionPrompt | wotc.mtgo.gre.external.messaging.Prompt |  |  |
| 5 | result | wotc.mtgo.gre.external.messaging.ResultSpec |  |  |

### wotc.mtgo.gre.external.messaging.DieRollResultsResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | playerDieRolls | wotc.mtgo.gre.external.messaging.PlayerDieRoll | yes |  |

### wotc.mtgo.gre.external.messaging.SelectReplacementReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | replacements | wotc.mtgo.gre.external.messaging.ReplacementEffect | yes |  |
| 2 | isOptional | bool |  |  |
| 3 | replacementsType | wotc.mtgo.gre.external.messaging.SelectReplacementsType |  |  |
| 4 | gameObjectSelections | wotc.mtgo.gre.external.messaging.ReplacementSelectionByZcid | yes |  |

### wotc.mtgo.gre.external.messaging.SelectNGroupReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | minSel | uint32 |  |  |
| 2 | maxSel | uint32 |  |  |
| 3 | groups | wotc.mtgo.gre.external.messaging.Group | yes |  |
| 5 | sourceId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.DistributionReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | minAmount | uint32 |  |  |
| 3 | maxAmount | uint32 |  |  |
| 4 | minPerTarget | uint32 |  |  |
| 5 | targetIds | uint32 | yes |  |
| 6 | existingDistributionValues | uint32 | yes |  |
| 7 | requiredDistributionValues | uint32 | yes |  |
| 8 | validSelectedTargetIds | uint32 | yes |  |
| 9 | sourceId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.NumericInputReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | minValue | uint32 |  |  |
| 3 | maxValue | uint32 |  |  |
| 4 | stepSize | uint32 |  |  |
| 5 | sourceId | uint32 |  |  |
| 6 | numericInputType | wotc.mtgo.gre.external.messaging.NumericInputType |  |  |
| 7 | disallowedValues | uint32 | yes |  |
| 8 | suggestedValues | uint32 | yes |  |
| 9 | disallowEven | bool |  |  |
| 10 | disallowOdd | bool |  |  |

### wotc.mtgo.gre.external.messaging.SearchReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | minFind | uint32 |  |  |
| 3 | maxFind | uint32 |  |  |
| 4 | zonesToSearch | uint32 | yes |  |
| 5 | itemsToSearch | uint32 | yes |  |
| 6 | itemsSought | uint32 | yes |  |
| 7 | sourceId | uint32 |  |  |
| 8 | additionalZones | uint32 | yes |  |
| 9 | allowFailToFind | wotc.mtgo.gre.external.messaging.AllowFailToFind |  |  |

### wotc.mtgo.gre.external.messaging.OptionalActionMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | prompt | wotc.mtgo.gre.external.messaging.Prompt |  |  |
| 3 | sourceId | uint32 |  |  |
| 4 | optionalActionTypes | wotc.mtgo.gre.external.messaging.CardMechanicType | yes |  |
| 5 | recipientIds | uint32 | yes |  |
| 6 | highlight | wotc.mtgo.gre.external.messaging.HighlightType |  |  |
| 7 | highlightIds | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.CastingTimeOptionsReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | castingTimeOptionReq | wotc.mtgo.gre.external.messaging.CastingTimeOptionReq | yes |  |

### wotc.mtgo.gre.external.messaging.SelectFromGroupsReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 3 | groups | wotc.mtgo.gre.external.messaging.Group | yes |  |
| 5 | minTotalSel | uint32 |  |  |
| 6 | maxTotalSel | uint32 |  |  |
| 7 | groupingStyle | wotc.mtgo.gre.external.messaging.GroupingStyle |  |  |
| 8 | unfiltered_ids | uint32 | yes |  |
| 9 | sourceId | uint32 |  |  |
| 10 | zone_ids | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.SearchFromGroupsReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | minFind | uint32 |  |  |
| 3 | maxFind | uint32 |  |  |
| 4 | zonesToSearch | uint32 | yes |  |
| 5 | groups | wotc.mtgo.gre.external.messaging.Group | yes |  |
| 6 | groupingStyle | wotc.mtgo.gre.external.messaging.GroupingStyle |  |  |
| 7 | sourceId | uint32 |  |  |
| 8 | allowFailToFind | wotc.mtgo.gre.external.messaging.AllowFailToFind |  |  |
| 9 | additionalZones | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.GatherReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | destinationId | uint32 |  |  |
| 3 | sources | wotc.mtgo.gre.external.messaging.GatherSource | yes |  |
| 4 | amountToGather | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.SubmitDeckReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | deck | wotc.mtgo.gre.external.messaging.DeckMessage |  |  |

### wotc.mtgo.gre.external.messaging.EdictalMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | edictMessage | wotc.mtgo.gre.external.messaging.ClientToGREMessage |  |  |

### wotc.mtgo.gre.external.messaging.TimeoutMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seatId | uint32 |  |  |
| 2 | timeoutCount | uint32 |  |  |
| 3 | timer | wotc.mtgo.gre.external.messaging.TimerInfo |  |  |

### wotc.mtgo.gre.external.messaging.TimerStateMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seatId | uint32 |  |  |
| 2 | timers | wotc.mtgo.gre.external.messaging.TimerInfo | yes |  |

### wotc.mtgo.gre.external.messaging.StringInputReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | sourceId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.SelectCountersReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | minSel | uint32 |  |  |
| 2 | maxSel | uint32 |  |  |
| 3 | context | wotc.mtgo.gre.external.messaging.SelectionContext |  |  |
| 5 | optionContext | wotc.mtgo.gre.external.messaging.OptionContext |  |  |
| 6 | listType | wotc.mtgo.gre.external.messaging.SelectionListType |  |  |
| 7 | counterPairs | wotc.mtgo.gre.external.messaging.CounterPair | yes |  |
| 9 | sourceId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.PredictionResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | gameStateMessages | wotc.mtgo.gre.external.messaging.GameStateMessage | yes |  |
| 2 | suppressedTriggeredAbilities | wotc.mtgo.gre.external.messaging.TriggeredAbility | yes |  |
| 3 | greRequests | wotc.mtgo.gre.external.messaging.GREToClientMessage | yes |  |

### Wizards.Arena.Models.Network.AccumulativePayoutNodeState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Count | int32 |  |  |

### Wizards.Arena.Models.Network.ActiveMatchInfoV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | MatchEndpointHost | string |  |  |
| 2 | MatchEndpointPort | int32 |  |  |
| 3 | MatchId | string |  |  |
| 4 | McFabricId | string |  |  |
| 5 | EventId | string |  |  |
| 6 | Battlefield | string |  |  |
| 7 | AllowReconnect | bool |  |  |
| 8 | YourSeat | int32 |  |  |
| 9 | ClientMetadata | Wizards.Arena.Models.Network.KeyValuePair_String_String | yes |  |
| 10 | PlayerInfos | Wizards.Arena.Models.Network.ClientMatchPlayerInfo | yes |  |

### Wizards.Arena.Models.Network.ActiveMatchInfoV3

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | MatchEndpointHost | string |  |  |
| 2 | MatchEndpointPort | int32 |  |  |
| 3 | MatchId | string |  |  |
| 4 | McFabricId | string |  |  |
| 5 | EventId | string |  |  |
| 6 | Battlefield | string |  |  |
| 7 | AllowReconnect | bool |  |  |
| 8 | YourSeat | int32 |  |  |
| 9 | ClientMetadata | Wizards.Arena.Models.Network.KeyValuePair_String_String | yes |  |
| 10 | PlayerInfos | Wizards.Arena.Models.Network.ClientMatchPlayerInfoV2 | yes |  |

### Wizards.Arena.Models.Network.BoosterStack

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | CollationId | int32 |  |  |
| 2 | SetCode | string |  |  |
| 3 | Count | int32 |  |  |

### Wizards.Arena.Models.Network.CPCMessageNodeState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | CurrentProgress | int32 |  |  |

### Wizards.Arena.Models.Network.CardMetadataInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | NonCraftableCardList | int32 | yes |  |
| 2 | NonCollectibleCardList | int32 | yes |  |
| 3 | UnreleasedSets | string | yes |  |

### Wizards.Arena.Models.Network.CardSkin

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GrpId | int64 |  |  |
| 2 | CCV | string |  |  |

### Wizards.Arena.Models.Network.CardsAndQuantity

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | QuantityByCardGrpId | Wizards.Arena.Models.Network.CardsAndQuantity.QuantityByCardGrpIdEntry | yes |  |

### Wizards.Arena.Models.Network.CardsAndQuantity.QuantityByCardGrpIdEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | uint32 |  |  |
| 2 | value | int32 |  |  |

### Wizards.Arena.Models.Network.Challenge

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |
| 2 | ChallengeTitle | string |  |  |
| 3 | ChallengeName | string |  |  |
| 4 | PlayFirstInternal | int32 |  |  |
| 5 | WinConditionInternal | int32 |  |  |
| 6 | Players | Wizards.Arena.Models.Network.ChallengePlayer | yes |  |
| 7 | OwnerPlayerId | string |  |  |
| 8 | ChallengeLimit | int32 |  |  |

### Wizards.Arena.Models.Network.ChallengeCloseReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |

### Wizards.Arena.Models.Network.ChallengeCreateReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeName | string |  |  |
| 2 | ChallengePassword | string |  |  |

### Wizards.Arena.Models.Network.ChallengeExit

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |

### Wizards.Arena.Models.Network.ChallengeExitReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |

### Wizards.Arena.Models.Network.ChallengeInviteReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |
| 2 | PlayerId | string |  |  |

### Wizards.Arena.Models.Network.ChallengeIssueReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |
| 2 | ChallengeName | string |  |  |
| 3 | DeckId | string |  |  |

### Wizards.Arena.Models.Network.ChallengeJoinReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |

### Wizards.Arena.Models.Network.ChallengeKickReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |
| 2 | PlayerId | string |  |  |

### Wizards.Arena.Models.Network.ChallengeMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | PlayerId | string |  |  |
| 2 | MessageId | string |  |  |
| 3 | Timestamp | google.protobuf.Timestamp |  |  |
| 4 | Message | string |  |  |

### Wizards.Arena.Models.Network.ChallengeMessageResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |
| 2 | Message | Wizards.Arena.Models.Network.ChallengeMessage |  |  |

### Wizards.Arena.Models.Network.ChallengeNotification

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeNotificationType | Wizards.Arena.Models.Network.EChallengeNotificationType |  |  |
| 2 | ChallengeId | string |  |  |
| 3 | Challenge | Wizards.Arena.Models.Network.Challenge |  |  |
| 4 | ChatHistory | Wizards.Arena.Models.Network.ChallengeMessage | yes |  |
| 5 | ChatMessage | Wizards.Arena.Models.Network.ChallengeMessage |  |  |
| 6 | ChallengeName | string |  |  |
| 7 | PlayerCount | int32 |  |  |
| 8 | InviterName | string |  |  |
| 9 | CountdownSeconds | int32 |  |  |

### Wizards.Arena.Models.Network.ChallengePlayer

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | PlayerId | string |  |  |
| 2 | DisplayName | string |  |  |
| 3 | IsWotc | bool |  |  |
| 4 | Ready | bool |  |  |
| 5 | PresenceInternal | int32 |  |  |
| 6 | Cosmetics | Wizards.Arena.Models.Network.PreferredCosmeticsOnDeck |  |  |
| 7 | DeckArtId | uint32 |  |  |
| 8 | DeckTileId | uint32 |  |  |
| 9 | TeamId | int32 |  |  |

### Wizards.Arena.Models.Network.ChallengeReadyReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |
| 2 | DeckId | string |  |  |

### Wizards.Arena.Models.Network.ChallengeReconnectAllResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Challenge | Wizards.Arena.Models.Network.Challenge | yes |  |

### Wizards.Arena.Models.Network.ChallengeSendMessageReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |
| 2 | Message | string |  |  |

### Wizards.Arena.Models.Network.ChallengeSetPlayerSettingsReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |
| 2 | TeamId | int32 |  |  |

### Wizards.Arena.Models.Network.ChallengeSetSettingsReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |
| 2 | ChallengeName | string |  |  |
| 3 | PlayFirstInternal | int32 |  |  |
| 4 | WinConditionInternal | int32 |  |  |
| 5 | ChallengeLimit | int32 |  |  |

### Wizards.Arena.Models.Network.ChallengeSettings

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Password | string |  |  |
| 2 | FormatConfigName | string |  |  |

### Wizards.Arena.Models.Network.ChallengeStartLaunchCountdownReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |

### Wizards.Arena.Models.Network.ChallengeStatusResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Challenge | Wizards.Arena.Models.Network.Challenge |  |  |
| 2 | History | Wizards.Arena.Models.Network.ChallengeMessage | yes |  |

### Wizards.Arena.Models.Network.ChallengeUnreadyReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ChallengeId | string |  |  |

### Wizards.Arena.Models.Network.ClaimAttachmentReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LetterIdInternal | string |  |  |

### Wizards.Arena.Models.Network.ClaimAttachmentResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ModifiedLetter | Wizards.Arena.Models.Network.Letter |  |  |
| 2 | InventoryInfo | Wizards.Arena.Models.Network.InventoryInfo |  |  |

### Wizards.Arena.Models.Network.ClientCampaignGraphState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | NodeStates | Wizards.Arena.Models.Network.ClientCampaignGraphState.NodeStatesEntry | yes |  |
| 2 | MilestoneStates | Wizards.Arena.Models.Network.ClientCampaignGraphState.MilestoneStatesEntry | yes |  |

### Wizards.Arena.Models.Network.ClientCampaignGraphState.MilestoneStatesEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | bool |  |  |

### Wizards.Arena.Models.Network.ClientCampaignGraphState.NodeStatesEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | Wizards.Arena.Models.Network.ClientNodeState |  |  |

### Wizards.Arena.Models.Network.ClientChestDescription

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | image1 | string |  |  |
| 2 | image2 | string |  |  |
| 3 | image3 | string |  |  |
| 4 | prefab | string |  |  |
| 5 | referenceId | string |  |  |
| 6 | headerLocKey | string |  |  |
| 7 | descriptionLocKey | string |  |  |
| 8 | quantity | string |  |  |
| 9 | locParams | Wizards.Arena.Models.Network.ClientChestDescription.LocParamsEntry | yes |  |
| 10 | AvailableDateInternal | google.protobuf.Timestamp |  |  |

### Wizards.Arena.Models.Network.ClientChestDescription.LocParamsEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | int32 |  |  |

### Wizards.Arena.Models.Network.ClientMatchInfoV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | MatchEndpointHost | string |  |  |
| 2 | MatchEndpointPort | int32 |  |  |
| 3 | MatchId | string |  |  |
| 4 | McFabricId | string |  |  |
| 5 | EventId | string |  |  |
| 6 | Battlefield | string |  |  |
| 7 | MatchTypeInternal | int32 |  |  |
| 8 | YourSeat | int32 |  |  |
| 9 | ClientMetadata | Wizards.Arena.Models.Network.KeyValuePair_String_String | yes |  |
| 10 | PlayerInfos | Wizards.Arena.Models.Network.ClientMatchPlayerInfo | yes |  |

### Wizards.Arena.Models.Network.ClientMatchInfoV4

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | MatchEndpointHost | string |  |  |
| 2 | MatchEndpointPort | int32 |  |  |
| 3 | MatchId | string |  |  |
| 4 | McFabricId | string |  |  |
| 5 | EventId | string |  |  |
| 6 | Battlefield | string |  |  |
| 7 | MatchTypeInternal | int32 |  |  |
| 8 | YourSeat | int32 |  |  |
| 9 | ClientMetadata | Wizards.Arena.Models.Network.KeyValuePair_String_String | yes |  |
| 10 | PlayerInfos | Wizards.Arena.Models.Network.ClientMatchPlayerInfoV2 | yes |  |

### Wizards.Arena.Models.Network.ClientMatchPlayerInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ScreenName | string |  |  |
| 2 | RankingClass | string |  |  |
| 3 | RankingTier | int32 |  |  |
| 4 | MythicPercentile | double |  |  |
| 5 | MythicLeaderboardPlace | int32 |  |  |
| 6 | IsWotc | bool |  |  |
| 7 | SeatId | int32 |  |  |
| 8 | TeamId | int32 |  |  |
| 9 | CommanderGrpIds | uint32 | yes |  |
| 10 | CosmeticsSelection | Wizards.Arena.Models.Network.PreferredCosmetics |  |  |

### Wizards.Arena.Models.Network.ClientMatchPlayerInfoV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ScreenName | string |  |  |
| 2 | RankingClass | string |  |  |
| 3 | RankingTier | int32 |  |  |
| 4 | MythicPercentile | double |  |  |
| 5 | MythicLeaderboardPlace | int32 |  |  |
| 6 | IsWotc | bool |  |  |
| 7 | SeatId | int32 |  |  |
| 8 | TeamId | int32 |  |  |
| 9 | Commanders | Wizards.Arena.Models.Network.CommanderInfo | yes |  |
| 10 | CosmeticsSelection | Wizards.Arena.Models.Network.PreferredCosmetics |  |  |

### Wizards.Arena.Models.Network.ClientNodeState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | StatusInternal | int32 |  |  |
| 2 | ProgressNodeState | Wizards.Arena.Models.Network.ClientProgressNodeState |  |  |
| 3 | TierRewardNodeState | Wizards.Arena.Models.Network.ClientTierRewardNodeState |  |  |
| 4 | QuestNodeState | Wizards.Arena.Models.Network.QuestNodeState |  |  |
| 5 | AccumulativePayoutState | Wizards.Arena.Models.Network.AccumulativePayoutNodeState |  |  |
| 6 | TimerState | Wizards.Arena.Models.Network.TimerNodeState |  |  |
| 7 | FamiliarMatchState | Wizards.Arena.Models.Network.FamiliarMatchNodeState |  |  |
| 8 | QueueMatchState | Wizards.Arena.Models.Network.QueueNodeState |  |  |
| 9 | ProgressionHistoryStateDataState | Wizards.Arena.Models.Network.ProgressionHistoryStateDataState |  |  |
| 10 | MatchAchievementNodeState | Wizards.Arena.Models.Network.MatchAchievementNodeState |  |  |
| 11 | CPCMessageNodeState | Wizards.Arena.Models.Network.CPCMessageNodeState |  |  |
| 12 | ThresholdNodeState | Wizards.Arena.Models.Network.ThresholdNodeState |  |  |

### Wizards.Arena.Models.Network.ClientNodeStateMap

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | NodeStates | Wizards.Arena.Models.Network.ClientNodeStateMap.NodeStatesEntry | yes |  |

### Wizards.Arena.Models.Network.ClientNodeStateMap.NodeStatesEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | Wizards.Arena.Models.Network.ClientNodeState |  |  |

### Wizards.Arena.Models.Network.ClientPeriodicRewards

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | DailyRewardSequenceId | int32 |  |  |
| 2 | DailyRewardResetTimestampInternal | google.protobuf.Timestamp |  |  |
| 3 | WeeklyRewardSequenceId | int32 |  |  |
| 4 | WeeklyRewardResetTimestampInternal | google.protobuf.Timestamp |  |  |
| 5 | DailyRewardChestDescriptions | Wizards.Arena.Models.Network.ClientPeriodicRewards.DailyRewardChestDescriptionsEntry | yes |  |
| 6 | WeeklyRewardChestDescriptions | Wizards.Arena.Models.Network.ClientPeriodicRewards.WeeklyRewardChestDescriptionsEntry | yes |  |

### Wizards.Arena.Models.Network.ClientPeriodicRewards.DailyRewardChestDescriptionsEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | int32 |  |  |
| 2 | value | Wizards.Arena.Models.Network.ClientChestDescription |  |  |

### Wizards.Arena.Models.Network.ClientPeriodicRewards.WeeklyRewardChestDescriptionsEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | int32 |  |  |
| 2 | value | Wizards.Arena.Models.Network.ClientChestDescription |  |  |

### Wizards.Arena.Models.Network.ClientProgressNodeState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | CurrentProgress | int32 |  |  |
| 2 | CompletionCount | int32 |  |  |

### Wizards.Arena.Models.Network.ClientTierRewardNodeState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | CurrentTiers | string | yes |  |

### Wizards.Arena.Models.Network.CommanderInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GrpId | uint32 |  |  |
| 2 | StyleCode | string |  |  |

### Wizards.Arena.Models.Network.CosmeticArtStyleEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Id | string |  |  |
| 2 | ArtId | uint32 |  |  |
| 3 | Variant | string |  |  |
| 4 | ExplicitGrpIds | uint32 | yes |  |
| 5 | AcquisitionFlagsInternal | int32 |  |  |

### Wizards.Arena.Models.Network.CosmeticEmoteEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Id | string |  |  |
| 2 | PageInternal | int32 |  |  |
| 3 | FlipTypeInternal | int32 |  |  |
| 4 | Category | string |  |  |
| 5 | Treatment | string |  |  |
| 6 | AcquisitionFlagsInternal | int32 |  |  |

### Wizards.Arena.Models.Network.CosmeticPetEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Id | string |  |  |
| 2 | Name | string |  |  |
| 3 | Variant | string |  |  |
| 4 | AcquisitionFlagsInternal | int32 |  |  |

### Wizards.Arena.Models.Network.CosmeticTitleEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Id | string |  |  |
| 2 | InternalName | string |  |  |
| 3 | LocKey | string |  |  |
| 4 | AcquisitionFlagsInternal | int32 |  |  |

### Wizards.Arena.Models.Network.CosmeticsAvatarEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Id | string |  |  |
| 2 | AcquisitionFlagsInternal | int32 |  |  |

### Wizards.Arena.Models.Network.CosmeticsClient

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ArtStyles | Wizards.Arena.Models.Network.CosmeticArtStyleEntry | yes |  |
| 2 | Avatars | Wizards.Arena.Models.Network.CosmeticsAvatarEntry | yes |  |
| 3 | Pets | Wizards.Arena.Models.Network.CosmeticPetEntry | yes |  |
| 4 | Sleeves | Wizards.Arena.Models.Network.CosmeticsSleeveEntry | yes |  |
| 5 | Emotes | Wizards.Arena.Models.Network.CosmeticEmoteEntry | yes |  |
| 6 | Titles | Wizards.Arena.Models.Network.CosmeticTitleEntry | yes |  |

### Wizards.Arena.Models.Network.CosmeticsSleeveEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Id | string |  |  |
| 2 | AcquisitionFlagsInternal | int32 |  |  |

### Wizards.Arena.Models.Network.CreateTournamentReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | TournamentId | string |  |  |
| 2 | PairingTypeInternal | int32 |  |  |
| 3 | MaxRounds | int32 |  |  |
| 4 | EventId | string |  |  |
| 5 | TournamentPlayers | Wizards.Arena.Models.Network.TournamentPlayer | yes |  |

### Wizards.Arena.Models.Network.Deck

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | MainDeck | Wizards.Arena.Models.Network.DeckCard | yes |  |
| 2 | Sideboard | Wizards.Arena.Models.Network.DeckCard | yes |  |
| 3 | CommandZone | Wizards.Arena.Models.Network.DeckCard | yes |  |
| 4 | Companions | Wizards.Arena.Models.Network.DeckCard | yes |  |
| 5 | Conspiracies | Wizards.Arena.Models.Network.DeckCard | yes |  |
| 6 | CardSkins | Wizards.Arena.Models.Network.CardSkin | yes |  |

### Wizards.Arena.Models.Network.DeckAttribute

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | name | string |  |  |
| 2 | value | string |  |  |

### Wizards.Arena.Models.Network.DeckCard

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | cardId | uint32 |  |  |
| 2 | quantity | uint32 |  |  |

### Wizards.Arena.Models.Network.DeckCardList

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Cards | Wizards.Arena.Models.Network.DeckCard | yes |  |

### Wizards.Arena.Models.Network.DeckSummaryV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | DeckIdInternal | string |  |  |
| 2 | Mana | string |  |  |
| 3 | Name | string |  |  |
| 4 | Attributes | Wizards.Arena.Models.Network.DeckAttribute | yes |  |
| 5 | Description | string |  |  |
| 6 | DeckTileId | google.protobuf.UInt32Value |  |  |
| 7 | DeckArtId | google.protobuf.UInt32Value |  |  |
| 8 | IsCompanionValid | bool |  |  |
| 9 | FormatLegalities | Wizards.Arena.Models.Network.DeckSummaryV2.FormatLegalitiesEntry | yes |  |
| 10 | PreferredCosmetics | Wizards.Arena.Models.Network.PreferredCosmeticsOnDeck |  |  |
| 11 | DeckValidationSummaries | Wizards.Arena.Models.Network.FormatValidationSummary | yes |  |
| 12 | UnownedCardsInternal | Wizards.Arena.Models.Network.DeckSummaryV2.UnownedCardsInternalEntry | yes |  |
| 13 | NetDeckFolderIdInternal | google.protobuf.StringValue |  |  |
| 14 | IsNetDeck | bool |  |  |
| 15 | checkInbox | bool |  |  |

### Wizards.Arena.Models.Network.DeckSummaryV2.FormatLegalitiesEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | bool |  |  |

### Wizards.Arena.Models.Network.DeckSummaryV2.UnownedCardsInternalEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | int32 |  |  |
| 2 | value | int32 |  |  |

### Wizards.Arena.Models.Network.DeckSummaryV3

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | DeckIdInternal | string |  |  |
| 2 | Mana | string |  |  |
| 3 | Name | string |  |  |
| 4 | Attributes | Wizards.Arena.Models.Network.DeckAttribute | yes |  |
| 5 | Description | string |  |  |
| 6 | DeckTileId | google.protobuf.UInt32Value |  |  |
| 7 | DeckArtId | google.protobuf.UInt32Value |  |  |
| 8 | IsCompanionValid | bool |  |  |
| 9 | PreferredCosmetics | Wizards.Arena.Models.Network.PreferredCosmeticsOnDeck |  |  |
| 10 | NetDeckFolderIdInternal | google.protobuf.StringValue |  |  |
| 11 | IsNetDeck | bool |  |  |
| 12 | checkInbox | bool |  |  |

### Wizards.Arena.Models.Network.ExternalChatAuthGetTokenReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | AuthCode | string |  |  |
| 2 | RedirectUrl | string |  |  |
| 3 | CodeVerifier | string |  |  |

### Wizards.Arena.Models.Network.ExternalChatAuthResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | AccessToken | string |  |  |

### Wizards.Arena.Models.Network.ExternalChatGetUserIdReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | PlayerId | string |  |  |

### Wizards.Arena.Models.Network.ExternalChatGetUserIdResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | UserId | uint64 |  |  |

### Wizards.Arena.Models.Network.FamiliarMatchNodeState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | MatchesPlayed | int32 |  |  |

### Wizards.Arena.Models.Network.FormatConfigInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | name | string |  |  |
| 2 | sets | string | yes |  |
| 3 | legalSets | string | yes |  |
| 4 | filterSets | string | yes |  |
| 5 | bannedTitleIds | uint32 | yes |  |
| 6 | suspendedTitleIds | uint32 | yes |  |
| 7 | allowedTitleIds | uint32 | yes |  |
| 8 | supressedTitleIds | uint32 | yes |  |
| 9 | individualCardQuotas | Wizards.Arena.Models.Network.FormatConfigInfo.IndividualCardQuotasEntry | yes |  |
| 10 | CardCountRestrictionInternal | int32 |  |  |
| 11 | FormatTypeInternal | int32 |  |  |
| 12 | mainDeckQuota | Wizards.Arena.Models.Network.Quota |  |  |
| 13 | sideBoardQuota | Wizards.Arena.Models.Network.Quota |  |  |
| 14 | commandZoneQuota | Wizards.Arena.Models.Network.Quota |  |  |
| 15 | SideboardBehaviorInternal | int32 |  |  |
| 16 | useRebalancedCards | bool |  |  |
| 17 | AllowedCommanderTitleIds | uint32 | yes |  |
| 18 | RestrictToColorIdentityInternal | Wizards.Arena.Models.Network.RestrictedColorIdentityOption | yes |  |
| 19 | DisallowedCardTypesForMainAndSideboardInternal | int32 | yes |  |
| 20 | RestrictToCommandZoneCardTypeInternal | int32 | yes |  |
| 21 | RarityPerCardQuotasInternal | Wizards.Arena.Models.Network.FormatConfigInfo.RarityPerCardQuotasInternalEntry | yes |  |
| 22 | RarityTotalQuotasInternal | Wizards.Arena.Models.Network.FormatConfigInfo.RarityTotalQuotasInternalEntry | yes |  |
| 23 | BannedAsCommanderTitleIds | uint32 | yes |  |

### Wizards.Arena.Models.Network.FormatConfigInfo.IndividualCardQuotasEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | uint32 |  |  |
| 2 | value | Wizards.Arena.Models.Network.Quota |  |  |

### Wizards.Arena.Models.Network.FormatConfigInfo.RarityPerCardQuotasInternalEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | int32 |  |  |
| 2 | value | Wizards.Arena.Models.Network.Quota |  |  |

### Wizards.Arena.Models.Network.FormatConfigInfo.RarityTotalQuotasInternalEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | int32 |  |  |
| 2 | value | Wizards.Arena.Models.Network.Quota |  |  |

### Wizards.Arena.Models.Network.FormatGroup

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GroupName | string |  |  |
| 2 | FormatNames | string | yes |  |

### Wizards.Arena.Models.Network.FormatValidationSummary

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Format | string |  |  |
| 2 | InvalidCardCount | uint32 |  |  |
| 3 | NonFormatCardCount | uint32 |  |  |
| 4 | BannedCardCount | uint32 |  |  |
| 5 | UnownedCardCount | uint32 |  |  |
| 6 | DeckTooSmallCardCount | uint32 |  |  |
| 7 | CardsOverRestrictedListQuotaCount | uint32 |  |  |

### Wizards.Arena.Models.Network.Gathering

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | GatheringName | string |  |  |
| 3 | Players | Wizards.Arena.Models.Network.GatheringPlayer | yes |  |
| 4 | OwnerPlayerId | string |  |  |
| 5 | Icon | string |  |  |
| 6 | Description | string |  |  |
| 7 | LanguageInternal | int32 |  |  |
| 8 | FocusesInternal | int32 | yes |  |
| 9 | InviteCode | string |  |  |
| 10 | ExternalChatLobbyId | uint64 |  |  |

### Wizards.Arena.Models.Network.GatheringAdminUpdateOwnerReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | OwnerPlayerId | string |  |  |

### Wizards.Arena.Models.Network.GatheringAssignRoleReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | TargetPlayerId | string |  |  |
| 3 | RollName | string |  |  |

### Wizards.Arena.Models.Network.GatheringCheckTextReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringName | string |  |  |
| 2 | Description | string |  |  |

### Wizards.Arena.Models.Network.GatheringClearInviteCodeReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |

### Wizards.Arena.Models.Network.GatheringCloseReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |

### Wizards.Arena.Models.Network.GatheringCreateReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringName | string |  |  |
| 2 | GatheringPassword | string |  |  |

### Wizards.Arena.Models.Network.GatheringCreateReqV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringName | string |  |  |
| 2 | Description | string |  |  |
| 3 | Icon | string |  |  |
| 4 | LanguageInternal | int32 |  |  |
| 8 | FocusesInternal | int32 | yes |  |

### Wizards.Arena.Models.Network.GatheringCreateReqV3

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringName | string |  |  |
| 2 | Description | string |  |  |
| 3 | Icon | string |  |  |
| 4 | LanguageInternal | int32 |  |  |
| 8 | FocusInternal | int32 |  |  |

### Wizards.Arena.Models.Network.GatheringDeclineInviteReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |

### Wizards.Arena.Models.Network.GatheringExit

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |

### Wizards.Arena.Models.Network.GatheringExitReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |

### Wizards.Arena.Models.Network.GatheringGenerateInviteCodeReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |

### Wizards.Arena.Models.Network.GatheringInviteReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | PlayerId | string |  |  |

### Wizards.Arena.Models.Network.GatheringJoinByInviteCodeReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringInviteCode | string |  |  |

### Wizards.Arena.Models.Network.GatheringJoinReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |

### Wizards.Arena.Models.Network.GatheringKickReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | PlayerId | string |  |  |

### Wizards.Arena.Models.Network.GatheringMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | PlayerId | string |  |  |
| 2 | MessageId | string |  |  |
| 3 | Timestamp | google.protobuf.Timestamp |  |  |
| 4 | Message | string |  |  |

### Wizards.Arena.Models.Network.GatheringMessageResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | Message | Wizards.Arena.Models.Network.GatheringMessage |  |  |

### Wizards.Arena.Models.Network.GatheringNotification

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringNotificationType | Wizards.Arena.Models.Network.EGatheringNotificationType |  |  |
| 2 | GatheringId | string |  |  |
| 3 | Gathering | Wizards.Arena.Models.Network.Gathering |  |  |
| 4 | ChatHistory | Wizards.Arena.Models.Network.GatheringMessage | yes |  |
| 5 | ChatMessage | Wizards.Arena.Models.Network.GatheringMessage |  |  |
| 6 | GatheringName | string |  |  |
| 7 | PlayerCount | int32 |  |  |
| 8 | InviterName | string |  |  |

### Wizards.Arena.Models.Network.GatheringNotificationV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringNotificationType | Wizards.Arena.Models.Network.EGatheringNotificationType |  |  |
| 2 | GatheringId | string |  |  |
| 3 | Gathering | Wizards.Arena.Models.Network.GatheringV2 |  |  |
| 4 | ChatHistory | Wizards.Arena.Models.Network.GatheringMessage | yes |  |
| 5 | ChatMessage | Wizards.Arena.Models.Network.GatheringMessage |  |  |
| 6 | GatheringName | string |  |  |
| 7 | PlayerCount | int32 |  |  |
| 8 | InviterName | string |  |  |

### Wizards.Arena.Models.Network.GatheringPlayer

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | PlayerId | string |  |  |
| 2 | DisplayName | string |  |  |
| 3 | PresenceInternal | int32 |  |  |
| 4 | RoleInternal | int32 |  |  |
| 5 | JoinedAt | google.protobuf.Timestamp |  |  |

### Wizards.Arena.Models.Network.GatheringReconnectAllResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Gatherings | Wizards.Arena.Models.Network.Gathering | yes |  |
| 2 | InvitedGatherings | Wizards.Arena.Models.Network.InvitedGathering | yes |  |

### Wizards.Arena.Models.Network.GatheringReconnectAllRespV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Gatherings | Wizards.Arena.Models.Network.GatheringV2 | yes |  |
| 2 | InvitedGatherings | Wizards.Arena.Models.Network.InvitedGatheringV2 | yes |  |

### Wizards.Arena.Models.Network.GatheringRemoveRoleReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | TargetPlayerId | string |  |  |
| 3 | RollName | string |  |  |

### Wizards.Arena.Models.Network.GatheringSendMessageReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | Message | string |  |  |

### Wizards.Arena.Models.Network.GatheringSettings

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Password | string |  |  |
| 2 | FormatConfigName | string |  |  |

### Wizards.Arena.Models.Network.GatheringStatusResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Gathering | Wizards.Arena.Models.Network.Gathering |  |  |
| 2 | History | Wizards.Arena.Models.Network.GatheringMessage | yes |  |

### Wizards.Arena.Models.Network.GatheringStatusRespV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Gathering | Wizards.Arena.Models.Network.GatheringV2 |  |  |
| 2 | History | Wizards.Arena.Models.Network.GatheringMessage | yes |  |

### Wizards.Arena.Models.Network.GatheringUpdateMetadataReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | GatheringName | string |  |  |
| 3 | Icon | string |  |  |
| 4 | Description | string |  |  |
| 5 | LanguageInternal | int32 |  |  |
| 6 | FocusesInternal | int32 | yes |  |

### Wizards.Arena.Models.Network.GatheringUpdateMetadataReqV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | GatheringName | string |  |  |
| 3 | Icon | string |  |  |
| 4 | Description | string |  |  |
| 5 | LanguageInternal | int32 |  |  |
| 6 | FocusInternal | int32 |  |  |

### Wizards.Arena.Models.Network.GatheringV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | GatheringName | string |  |  |
| 3 | Players | Wizards.Arena.Models.Network.GatheringPlayer | yes |  |
| 4 | OwnerPlayerId | string |  |  |
| 5 | Icon | string |  |  |
| 6 | Description | string |  |  |
| 7 | LanguageInternal | int32 |  |  |
| 8 | FocusInternal | int32 |  |  |
| 9 | InviteCode | string |  |  |
| 10 | ExternalChatLobbyId | uint64 |  |  |

### Wizards.Arena.Models.Network.GetCompleteVaultReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### Wizards.Arena.Models.Network.GetFormatsReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### Wizards.Arena.Models.Network.GetFormatsResponse

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Formats | Wizards.Arena.Models.Network.FormatConfigInfo | yes |  |
| 2 | FormatGroups | Wizards.Arena.Models.Network.FormatGroup | yes |  |

### Wizards.Arena.Models.Network.GetPlayerInboxReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### Wizards.Arena.Models.Network.GetPlayerInboxResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Inbox | Wizards.Arena.Models.Network.Letter | yes |  |

### Wizards.Arena.Models.Network.GetPlayerOwnedCosmeticsReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### Wizards.Arena.Models.Network.GetPlayerOwnedCosmeticsResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | OwnedCosmetics | Wizards.Arena.Models.Network.CosmeticsClient |  |  |

### Wizards.Arena.Models.Network.GetPreferredCosmeticsRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### Wizards.Arena.Models.Network.GetSetsReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### Wizards.Arena.Models.Network.GetTournamentStateReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | TournamentId | string |  |  |

### Wizards.Arena.Models.Network.GetVoucherDefinitionsReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### Wizards.Arena.Models.Network.GetVoucherDefinitionsResponse

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | VoucherDefinitions | Wizards.Arena.Models.Network.VoucherDefinition | yes |  |

### Wizards.Arena.Models.Network.GetWpnNotificationPreferencesReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### Wizards.Arena.Models.Network.GetWpnNotificationPreferencesResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | DefaultWpnNotificationPreference | Wizards.Arena.Models.Network.WpnNotificationPreference |  |  |
| 2 | GlobalNotificationPreference | Wizards.Arena.Models.Network.WpnNotificationPreference |  |  |
| 3 | OrganizationNotificationPreferences | Wizards.Arena.Models.Network.GetWpnNotificationPreferencesResp.OrganizationNotificationPreferencesEntry | yes |  |

### Wizards.Arena.Models.Network.GetWpnNotificationPreferencesResp.OrganizationNotificationPreferencesEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | int32 |  |  |
| 2 | value | Wizards.Arena.Models.Network.WpnNotificationPreference |  |  |

### Wizards.Arena.Models.Network.GrantedCard

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GrpId | int32 |  |  |
| 2 | CardAdded | bool |  |  |
| 3 | SetCode | string |  |  |
| 4 | IsGrantedWithDeck | bool |  |  |
| 5 | Gems | int32 |  |  |
| 6 | VaultProgress | int32 |  |  |

### Wizards.Arena.Models.Network.GraphIdNodeId

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GraphId | string |  |  |
| 2 | NodeId | string |  |  |

### Wizards.Arena.Models.Network.HomePageAchievements

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Claimable | Wizards.Arena.Models.Network.GraphIdNodeId | yes |  |
| 2 | Favorites | Wizards.Arena.Models.Network.GraphIdNodeId | yes |  |
| 3 | CloseToComplete | Wizards.Arena.Models.Network.GraphIdNodeId | yes |  |
| 4 | RecentlyProgressed | Wizards.Arena.Models.Network.GraphIdNodeId | yes |  |
| 5 | OneShots | Wizards.Arena.Models.Network.GraphIdNodeId | yes |  |
| 6 | SparseStatesInternal | Wizards.Arena.Models.Network.HomePageAchievements.SparseStatesInternalEntry | yes |  |

### Wizards.Arena.Models.Network.HomePageAchievements.SparseStatesInternalEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | Wizards.Arena.Models.Network.ClientNodeStateMap |  |  |

### Wizards.Arena.Models.Network.InventoryChange

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | SourceInternal | int32 |  |  |
| 2 | SourceId | string |  |  |
| 3 | InventoryGold | int32 |  |  |
| 4 | InventoryGems | int32 |  |  |
| 5 | InventoryCustomTokens | Wizards.Arena.Models.Network.InventoryChange.InventoryCustomTokensEntry | yes |  |
| 6 | ArtStyles | Wizards.Arena.Models.Network.CosmeticArtStyleEntry | yes |  |
| 7 | Avatars | string | yes |  |
| 8 | Sleeves | string | yes |  |
| 9 | Pets | string | yes |  |
| 10 | Emotes | string | yes |  |
| 11 | Titles | string | yes |  |
| 12 | WildCardTrackUnCommons | int32 |  |  |
| 13 | WildCardTrackRares | int32 |  |  |
| 14 | WildCardTrackMythics | int32 |  |  |
| 15 | SetMasteryProgress | int32 |  |  |
| 16 | WildCardCommons | int32 |  |  |
| 17 | WildCardUnCommons | int32 |  |  |
| 18 | WildCardRares | int32 |  |  |
| 19 | WildCardMythics | int32 |  |  |
| 20 | DecksV2 | Wizards.Arena.Models.Network.DeckSummaryV2 | yes |  |
| 21 | DecksV3 | Wizards.Arena.Models.Network.DeckSummaryV3 | yes |  |
| 22 | DeckCardsInternal | Wizards.Arena.Models.Network.InventoryChange.DeckCardsInternalEntry | yes |  |
| 23 | Boosters | Wizards.Arena.Models.Network.BoosterStack | yes |  |
| 24 | GrantedCards | Wizards.Arena.Models.Network.GrantedCard | yes |  |
| 25 | VouchersInternal | Wizards.Arena.Models.Network.InventoryChange.VouchersInternalEntry | yes |  |
| 26 | NewLetters | Wizards.Arena.Models.Network.Letter | yes |  |
| 27 | PrizeWallsUnlockedInternal | string | yes |  |
| 28 | VouchersV2Internal | Wizards.Arena.Models.Network.InventoryChange.VouchersV2InternalEntry | yes |  |

### Wizards.Arena.Models.Network.InventoryChange.DeckCardsInternalEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | Wizards.Arena.Models.Network.DeckCardList |  |  |

### Wizards.Arena.Models.Network.InventoryChange.InventoryCustomTokensEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | int32 |  |  |

### Wizards.Arena.Models.Network.InventoryChange.VouchersInternalEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | int32 |  |  |

### Wizards.Arena.Models.Network.InventoryChange.VouchersV2InternalEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | int32 |  |  |

### Wizards.Arena.Models.Network.InventoryInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | SeqId | int32 |  |  |
| 2 | Changes | Wizards.Arena.Models.Network.InventoryChange | yes |  |
| 3 | Gems | int32 |  |  |
| 4 | Gold | int32 |  |  |
| 5 | TotalVaultProgress | int32 |  |  |
| 6 | WcTrackPosition | int32 |  |  |
| 7 | WildCardCommons | int32 |  |  |
| 8 | WildCardUnCommons | int32 |  |  |
| 9 | WildCardRares | int32 |  |  |
| 10 | WildCardMythics | int32 |  |  |
| 11 | CustomTokens | Wizards.Arena.Models.Network.InventoryInfo.CustomTokensEntry | yes |  |
| 12 | Boosters | Wizards.Arena.Models.Network.BoosterStack | yes |  |
| 13 | VouchersInternal | Wizards.Arena.Models.Network.InventoryInfo.VouchersInternalEntry | yes |  |
| 14 | PrizeWallsUnlockedInternal | string | yes |  |
| 15 | Cosmetics | Wizards.Arena.Models.Network.CosmeticsClient |  |  |
| 16 | VouchersV2Internal | Wizards.Arena.Models.Network.InventoryInfo.VouchersV2InternalEntry | yes |  |

### Wizards.Arena.Models.Network.InventoryInfo.CustomTokensEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | int32 |  |  |

### Wizards.Arena.Models.Network.InventoryInfo.VouchersInternalEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | int32 |  |  |

### Wizards.Arena.Models.Network.InventoryInfo.VouchersV2InternalEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | int32 |  |  |

### Wizards.Arena.Models.Network.InvitedGathering

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | GatheringName | string |  |  |
| 3 | Icon | string |  |  |
| 4 | Description | string |  |  |
| 5 | LanguageInternal | int32 |  |  |
| 6 | FocusesInternal | int32 | yes |  |

### Wizards.Arena.Models.Network.InvitedGatheringV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GatheringId | string |  |  |
| 2 | GatheringName | string |  |  |
| 3 | Icon | string |  |  |
| 4 | Description | string |  |  |
| 5 | LanguageInternal | int32 |  |  |
| 6 | FocusInternal | int32 |  |  |

### Wizards.Arena.Models.Network.JoinTournamentReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | PlayerId | string |  |  |
| 2 | DisplayName | string |  |  |
| 3 | TournamentId | string |  |  |
| 4 | AvatarId | string |  |  |

### Wizards.Arena.Models.Network.KeyValuePair_String_List_Int32

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Key | string |  |  |
| 2 | Value | int32 | yes |  |

### Wizards.Arena.Models.Network.KeyValuePair_String_String

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Key | string |  |  |
| 2 | Value | string |  |  |

### Wizards.Arena.Models.Network.KillSwitchNotification

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | KillSwitches | Wizards.Arena.Models.Network.KillSwitchNotification.KillSwitchesEntry | yes |  |
| 2 | UxKillSwitches | Wizards.Arena.Models.Network.KillSwitchNotification.UxKillSwitchesEntry | yes |  |

### Wizards.Arena.Models.Network.KillSwitchNotification.KillSwitchesEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | bool |  |  |

### Wizards.Arena.Models.Network.KillSwitchNotification.UxKillSwitchesEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | bool |  |  |

### Wizards.Arena.Models.Network.Letter

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | IdInternal | string |  |  |
| 2 | LetterTypeInternal | int32 |  |  |
| 3 | CreatedDateInternal | google.protobuf.Timestamp |  |  |
| 4 | UpdatedDateInternal | google.protobuf.Timestamp |  |  |
| 5 | ExpiryDateInternal | google.protobuf.Timestamp |  |  |
| 6 | ChangeSourceInternal | int32 |  |  |
| 7 | Content | Wizards.Arena.Models.Network.LetterContent |  |  |
| 8 | State | Wizards.Arena.Models.Network.LetterState |  |  |

### Wizards.Arena.Models.Network.LetterContent

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Title | string |  |  |
| 2 | Body | string |  |  |
| 3 | FallbackTitle | string |  |  |
| 4 | FallbackBody | string |  |  |
| 5 | Attachments | Wizards.Arena.Models.Network.TreasureItem | yes |  |
| 6 | ArtContentTypeInternal | int32 |  |  |
| 7 | ArtContentReferenceId | string |  |  |
| 8 | MoreInfoHyperlink | string |  |  |

### Wizards.Arena.Models.Network.LetterState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ReadDateInternal | google.protobuf.Timestamp |  |  |
| 2 | ClaimDateInternal | google.protobuf.Timestamp |  |  |
| 3 | DeleteDateInternal | google.protobuf.Timestamp |  |  |

### Wizards.Arena.Models.Network.Lobby

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LobbyId | string |  |  |
| 2 | LobbyName | string |  |  |
| 3 | Players | Wizards.Arena.Models.Network.LobbyPlayer | yes |  |
| 4 | OwnerPlayerId | string |  |  |

### Wizards.Arena.Models.Network.LobbyCloseReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LobbyId | string |  |  |

### Wizards.Arena.Models.Network.LobbyCreateReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LobbyName | string |  |  |
| 2 | LobbyPassword | string |  |  |

### Wizards.Arena.Models.Network.LobbyExit

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LobbyId | string |  |  |

### Wizards.Arena.Models.Network.LobbyExitReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LobbyId | string |  |  |

### Wizards.Arena.Models.Network.LobbyInviteReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LobbyId | string |  |  |
| 2 | PlayerId | string |  |  |

### Wizards.Arena.Models.Network.LobbyJoinReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LobbyName | string |  |  |
| 2 | LobbyPassword | string |  |  |
| 3 | LobbyId | string |  |  |

### Wizards.Arena.Models.Network.LobbyKickReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LobbyId | string |  |  |
| 2 | PlayerId | string |  |  |

### Wizards.Arena.Models.Network.LobbyMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | PlayerId | string |  |  |
| 2 | MessageId | string |  |  |
| 3 | Timestamp | google.protobuf.Timestamp |  |  |
| 4 | Message | string |  |  |

### Wizards.Arena.Models.Network.LobbyNotification

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LobbyNotificationType | Wizards.Arena.Models.Network.ELobbyNotificationType |  |  |
| 2 | LobbyId | string |  |  |
| 3 | Lobby | Wizards.Arena.Models.Network.Lobby |  |  |
| 4 | ChatHistory | Wizards.Arena.Models.Network.LobbyMessage | yes |  |
| 5 | ChatMessage | Wizards.Arena.Models.Network.LobbyMessage |  |  |
| 6 | LobbyName | string |  |  |
| 7 | PlayerCount | int32 |  |  |
| 8 | InviterName | string |  |  |

### Wizards.Arena.Models.Network.LobbyPlayer

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | PlayerId | string |  |  |
| 2 | DisplayName | string |  |  |
| 3 | PresenceInternal | int32 |  |  |
| 4 | OpponentId | string |  |  |

### Wizards.Arena.Models.Network.LobbySendMessageReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LobbyId | string |  |  |
| 2 | Message | string |  |  |

### Wizards.Arena.Models.Network.LobbySettings

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Password | string |  |  |
| 2 | FormatConfigName | string |  |  |

### Wizards.Arena.Models.Network.LobbyStatusResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Lobby | Wizards.Arena.Models.Network.Lobby |  |  |
| 2 | History | Wizards.Arena.Models.Network.LobbyMessage | yes |  |

### Wizards.Arena.Models.Network.MarkLetterReadReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LetterIdInternal | string |  |  |

### Wizards.Arena.Models.Network.MarkLetterReadResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ModifiedLetter | Wizards.Arena.Models.Network.Letter |  |  |

### Wizards.Arena.Models.Network.MatchAchievementNodeState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | CurrentProgress | int32 |  |  |

### Wizards.Arena.Models.Network.MosaicAuxReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | FavoriteStores | int64 | yes |  |

### Wizards.Arena.Models.Network.MosaicAuxResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | CarouselEligibleStoreIds | int64 | yes |  |

### Wizards.Arena.Models.Network.OpenBoostersReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | SetCode | string |  |  |
| 2 | BoosterCollationId | int32 |  |  |
| 3 | BoosterCount | int32 |  |  |

### Wizards.Arena.Models.Network.PreferredCosmetics

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Avatar | Wizards.Arena.Models.Network.CosmeticsAvatarEntry |  |  |
| 2 | Sleeve | Wizards.Arena.Models.Network.CosmeticsSleeveEntry |  |  |
| 3 | Pet | Wizards.Arena.Models.Network.CosmeticPetEntry |  |  |
| 4 | Title | Wizards.Arena.Models.Network.CosmeticTitleEntry |  |  |
| 5 | Emotes | Wizards.Arena.Models.Network.CosmeticEmoteEntry | yes |  |

### Wizards.Arena.Models.Network.PreferredCosmeticsOnDeck

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Avatar | string |  |  |
| 2 | Sleeve | string |  |  |
| 3 | Pet | string |  |  |
| 4 | Title | string |  |  |
| 5 | Emotes | string | yes |  |

### Wizards.Arena.Models.Network.ProgressionHistoryStateDataState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | AvailableDateTimeUTCInternal | google.protobuf.Timestamp |  |  |
| 2 | ProgressedDateTimeUTCInternal | google.protobuf.Timestamp |  |  |

### Wizards.Arena.Models.Network.QuestNodeState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | CurrentProgress | int32 |  |  |

### Wizards.Arena.Models.Network.QueueNodeState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | SuccessfulAttempts | int32 |  |  |

### Wizards.Arena.Models.Network.Quota

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | min | int32 |  |  |
| 2 | max | int32 |  |  |

### Wizards.Arena.Models.Network.ReportPlayerReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ReporteeId | string |  |  |
| 2 | ReporteeScreenName | string |  |  |
| 3 | CategoryInternal | int32 |  |  |
| 4 | Comment | string |  |  |
| 5 | Timestamp | google.protobuf.Timestamp |  |  |
| 6 | MessageId | google.protobuf.StringValue |  |  |
| 7 | SessionId | google.protobuf.StringValue |  |  |

### Wizards.Arena.Models.Network.ReportPlayerResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### Wizards.Arena.Models.Network.ReportTournamentMatchResultReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | TournamentId | string |  |  |
| 2 | PlayerId | string |  |  |
| 3 | TournamentMatchResultInternal | int32 |  |  |

### Wizards.Arena.Models.Network.RestrictedColorIdentityOption

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Colors | int32 | yes |  |

### Wizards.Arena.Models.Network.SetCollation

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | CollationCode | string |  |  |
| 2 | CollationId | int32 |  |  |
| 3 | FlavorId | string |  |  |

### Wizards.Arena.Models.Network.SetGlobalWpnNotificationPreferenceReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | NotificationPreference | Wizards.Arena.Models.Network.WpnNotificationPreference |  |  |

### Wizards.Arena.Models.Network.SetGlobalWpnNotificationPreferenceResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | DefaultWpnNotificationPreference | Wizards.Arena.Models.Network.WpnNotificationPreference |  |  |
| 2 | GlobalNotificationPreference | Wizards.Arena.Models.Network.WpnNotificationPreference |  |  |
| 3 | OrganizationNotificationPreferences | Wizards.Arena.Models.Network.SetGlobalWpnNotificationPreferenceResp.OrganizationNotificationPreferencesEntry | yes |  |

### Wizards.Arena.Models.Network.SetGlobalWpnNotificationPreferenceResp.OrganizationNotificationPreferencesEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | int32 |  |  |
| 2 | value | Wizards.Arena.Models.Network.WpnNotificationPreference |  |  |

### Wizards.Arena.Models.Network.SetGroup

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | GroupName | string |  |  |
| 2 | Sets | string | yes |  |

### Wizards.Arena.Models.Network.SetMetadata

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | SetCode | string |  |  |
| 2 | Collations | Wizards.Arena.Models.Network.SetCollation | yes |  |
| 3 | AvailabilityInternal | int32 |  |  |
| 4 | ReleaseDate | google.protobuf.Timestamp |  |  |
| 5 | IsBonusSheet | bool |  |  |
| 6 | IsPublished | bool |  |  |
| 7 | IsMajorCardset | bool |  |  |
| 8 | SetCodeAliases | string | yes |  |
| 9 | BonusSheetSetCodes | string | yes |  |
| 10 | DigitalReleaseSetCodes | string | yes |  |
| 11 | RelatedSets | Wizards.Arena.Models.Network.SubsetMetadata | yes |  |
| 12 | GameVersion | string |  |  |

### Wizards.Arena.Models.Network.SetMetadataResponse

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | SetDatas | Wizards.Arena.Models.Network.SetMetadata | yes |  |
| 2 | SetGroups | Wizards.Arena.Models.Network.SetGroup | yes |  |
| 3 | StoreSetGroups | Wizards.Arena.Models.Network.StorePackGroup | yes |  |
| 4 | SetCollectionGroup | Wizards.Arena.Models.Network.KeyValuePair_String_List_Int32 | yes |  |

### Wizards.Arena.Models.Network.SetOrganizationNotificationPreferenceReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | OrganizationId | int32 |  |  |
| 2 | NotificationPreference | Wizards.Arena.Models.Network.WpnNotificationPreference |  |  |

### Wizards.Arena.Models.Network.SetOrganizationNotificationPreferenceResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | DefaultWPNNotificationPreference | Wizards.Arena.Models.Network.WpnNotificationPreference |  |  |
| 2 | GlobalNotificationPreference | Wizards.Arena.Models.Network.WpnNotificationPreference |  |  |
| 3 | OrganizationNotificationPreferences | Wizards.Arena.Models.Network.SetOrganizationNotificationPreferenceResp.OrganizationNotificationPreferencesEntry | yes |  |

### Wizards.Arena.Models.Network.SetOrganizationNotificationPreferenceResp.OrganizationNotificationPreferencesEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | int32 |  |  |
| 2 | value | Wizards.Arena.Models.Network.WpnNotificationPreference |  |  |

### Wizards.Arena.Models.Network.SetPreferredAvatarCosmeticRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Id | string |  |  |

### Wizards.Arena.Models.Network.SetPreferredEmotesCosmeticRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Ids | string | yes |  |

### Wizards.Arena.Models.Network.SetPreferredPetCosmeticRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Id | string |  |  |

### Wizards.Arena.Models.Network.SetPreferredSleeveCosmeticRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Id | string |  |  |

### Wizards.Arena.Models.Network.SetPreferredTitleCosmeticRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Id | string |  |  |

### Wizards.Arena.Models.Network.StartHookReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | DeckSummariesCacheVersion | int32 |  |  |
| 2 | DecksCacheVersion | int32 |  |  |
| 3 | PreferredCosmeticsCacheVersion | int32 |  |  |
| 4 | TokenDefinitionsCacheVersion | int32 |  |  |
| 5 | KillSwitchNotificationCacheVersion | int32 |  |  |
| 6 | ClientPeriodicRewardsCacheVersion | int32 |  |  |

### Wizards.Arena.Models.Network.StartHookResponseV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | InventoryInfo | Wizards.Arena.Models.Network.InventoryInfo |  |  |
| 2 | DeckSummariesCacheVersion | int32 |  |  |
| 3 | DeckSummaries | Wizards.Arena.Models.Network.DeckSummaryV3 | yes |  |
| 4 | DecksCacheVersion | int32 |  |  |
| 5 | DecksInternal | Wizards.Arena.Models.Network.StartHookResponseV2.DecksInternalEntry | yes |  |
| 6 | SystemMessages | Wizards.Arena.Models.Network.SystemMessage | yes |  |
| 8 | PreferredCosmeticsCacheVersion | int32 |  |  |
| 9 | PreferredCosmetics | Wizards.Arena.Models.Network.PreferredCosmetics |  |  |
| 10 | HomePageAchievements | Wizards.Arena.Models.Network.HomePageAchievements |  |  |
| 11 | DeckLimit | int32 |  |  |
| 12 | TimewalkInfo | Wizards.Arena.Models.Network.TimewalkInfo |  |  |
| 13 | TokenDefinitionsCacheVersion | int32 |  |  |
| 14 | TokenDefinitions | Wizards.Arena.Models.Network.TokenDefinition | yes |  |
| 15 | KillSwitchNotificationCacheVersion | int32 |  |  |
| 16 | KillSwitchNotification | Wizards.Arena.Models.Network.KillSwitchNotification |  |  |
| 17 | CardMetadataInfo | Wizards.Arena.Models.Network.CardMetadataInfo |  |  |
| 18 | ClientPeriodicRewardsCacheVersion | int32 |  |  |
| 19 | ClientPeriodicRewards | Wizards.Arena.Models.Network.ClientPeriodicRewards |  |  |
| 20 | UpdatedGraphs | Wizards.Arena.Models.Network.StartHookResponseV2.UpdatedGraphsEntry | yes |  |
| 21 | ServerTimeInternal | google.protobuf.Timestamp |  |  |

### Wizards.Arena.Models.Network.StartHookResponseV2.DecksInternalEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | Wizards.Arena.Models.Network.Deck |  |  |

### Wizards.Arena.Models.Network.StartHookResponseV2.UpdatedGraphsEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | Wizards.Arena.Models.Network.ClientCampaignGraphState |  |  |

### Wizards.Arena.Models.Network.StorePackGroup

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | SetSymbol | string |  |  |
| 2 | Sets | string | yes |  |
| 3 | TagsInternal | int32 | yes |  |

### Wizards.Arena.Models.Network.SubsetMetadata

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | SetCode | string |  |  |
| 2 | SetName | string |  |  |
| 3 | ReleaseDate | google.protobuf.Timestamp |  |  |
| 4 | IsPublished | bool |  |  |
| 5 | GameVersion | string |  |  |
| 6 | AvailabilityInternal | int32 |  |  |

### Wizards.Arena.Models.Network.SystemMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Title | string |  |  |
| 2 | Message | string |  |  |
| 3 | Parameters | Wizards.Arena.Models.Network.SystemMessage.ParametersEntry | yes |  |
| 4 | TitleLocalized | bool |  |  |
| 5 | MessageLocalized | bool |  |  |

### Wizards.Arena.Models.Network.SystemMessage.ParametersEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | string |  |  |

### Wizards.Arena.Models.Network.ThresholdNodeState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | LatestProgress | double |  |  |

### Wizards.Arena.Models.Network.TimerNodeState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | UnlockTimeInternal | google.protobuf.Timestamp |  |  |

### Wizards.Arena.Models.Network.TimewalkInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | offsetInTicks | int64 |  |  |

### Wizards.Arena.Models.Network.TokenDefinition

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | TokenId | string |  |  |
| 2 | ExpirationDateInternal | google.protobuf.Timestamp |  |  |
| 3 | TokenTypeInternal | int32 |  |  |
| 4 | ThumbnailImageName | string |  |  |
| 5 | PrefabName | string |  |  |
| 6 | HeaderLocKey | string |  |  |
| 7 | DescriptionLocKey | string |  |  |
| 8 | DisplayPriority | int32 |  |  |

### Wizards.Arena.Models.Network.TournamentDropPlayerReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | TournamentId | string |  |  |
| 2 | PlayerId | string |  |  |

### Wizards.Arena.Models.Network.TournamentNotification

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | TournamentNotificationType | Wizards.Arena.Models.Network.ETournamentNotificationType |  |  |
| 2 | TournamentId | string |  |  |
| 3 | OpponentNames | string | yes |  |
| 4 | ReadiedPlayerIds | string | yes |  |
| 5 | TournamentState | Wizards.Arena.Models.Network.TournamentState |  |  |

### Wizards.Arena.Models.Network.TournamentOpponent

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | PlayerId | string |  |  |
| 2 | DisplayName | string |  |  |
| 3 | ResultInternal | int32 |  |  |

### Wizards.Arena.Models.Network.TournamentPairing

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | PlayerOneDisplayName | string |  |  |
| 2 | PlayerOneId | string |  |  |
| 3 | PlayerTwoDisplayName | string |  |  |
| 4 | PlayerTwoId | string |  |  |
| 5 | TournamentMatchResultInternal | int32 |  |  |
| 6 | Round | int32 |  |  |

### Wizards.Arena.Models.Network.TournamentPlayer

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | PlayerId | string |  |  |
| 2 | DisplayName | string |  |  |
| 3 | TeamId | int32 |  |  |
| 4 | PreviousOpponents | Wizards.Arena.Models.Network.TournamentOpponent | yes |  |
| 5 | MatchPoints | int32 |  |  |
| 6 | TiebreakerOpponentMatchWinPercentage | float |  |  |
| 7 | TiebreakerGameWinPercentage | float |  |  |
| 8 | TiebreakerOpponentGameWinPercentage | float |  |  |
| 9 | DeckId | string |  |  |
| 10 | AvatarId | string |  |  |
| 11 | TournamentPlayerStateInternal | int32 |  |  |

### Wizards.Arena.Models.Network.TournamentPlayerReadyRoundReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | TournamentId | string |  |  |
| 2 | PlayerId | string |  |  |
| 3 | AvatarId | string |  |  |

### Wizards.Arena.Models.Network.TournamentPlayerReadyStartReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | TournamentId | string |  |  |
| 2 | PlayerId | string |  |  |
| 3 | AvatarId | string |  |  |
| 4 | DeckId | string |  |  |

### Wizards.Arena.Models.Network.TournamentState

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | TournamentNotificationType | Wizards.Arena.Models.Network.TournamentCmdType |  |  |
| 2 | TournamentId | string |  |  |
| 3 | TournamentPlayers | Wizards.Arena.Models.Network.TournamentPlayer | yes |  |
| 4 | TournamentPairings | Wizards.Arena.Models.Network.TournamentPairing | yes |  |
| 5 | MaxRounds | int32 |  |  |
| 6 | FormatName | string |  |  |
| 7 | PairingTypeInternal | int32 |  |  |
| 8 | TimeoutSec | int32 |  |  |
| 9 | OpponentDisplayName | string |  |  |
| 10 | InactivePlayers | Wizards.Arena.Models.Network.TournamentPlayer | yes |  |

### Wizards.Arena.Models.Network.TournamentStateResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | TournamentState | Wizards.Arena.Models.Network.TournamentState |  |  |

### Wizards.Arena.Models.Network.TreasureItem

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | TreasureTypeInternal | int32 |  |  |
| 2 | ReferenceId | string |  |  |
| 3 | Quantity | int32 |  |  |

### Wizards.Arena.Models.Network.VoucherDefinition

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | CollationId | string |  |  |
| 2 | CollationIds | string | yes |  |
| 3 | LocKey | string |  |  |
| 4 | EndTime | google.protobuf.Timestamp |  |  |
| 5 | PrefabName | string |  |  |
| 6 | VoucherId | string |  |  |

### Wizards.Arena.Models.Network.WildCardBulkRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | CardsAndQuantity | Wizards.Arena.Models.Network.CardsAndQuantity |  |  |

### Wizards.Arena.Models.Network.WpnNotificationPreference

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | AnyMessagesEnabled | bool |  |  |
| 2 | MarketingMessagesEnabled | bool |  |  |
| 3 | CarouselsEnabled | bool |  |  |

### Wizards.Arena.Protocol.Cmd

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Type | Wizards.Arena.Models.Network.CmdType |  |  |
| 2 | RawTransId | string |  |  |
| 3 | ProtobufPayload | google.protobuf.Any |  | Payload |
| 4 | JsonPayload | bytes |  | Payload |
| 5 | Compressed | bool |  |  |

### Wizards.Arena.Protocol.Request

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | Type | Wizards.Arena.Protocol.RequestType |  |  |
| 2 | RawTransId | string |  |  |
| 3 | Key | string |  |  |
| 4 | ProtobufPayload | google.protobuf.Any |  | Payload |
| 5 | JsonPayload | bytes |  | Payload |
| 6 | SessionInfo | Wizards.Arena.Protocol.SessionInfo |  |  |
| 7 | Compressed | bool |  |  |

### Wizards.Arena.Protocol.Response

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | RawTransId | string |  |  |
| 2 | ProtobufPayload | google.protobuf.Any |  | Payload |
| 3 | JsonPayload | bytes |  | Payload |
| 4 | Error | Wizards.Arena.Protocol.ServerError |  |  |
| 5 | Compressed | bool |  |  |

### Wizards.Arena.Protocol.ServerError

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | code | int32 |  |  |
| 2 | message | string |  |  |
| 3 | data | google.protobuf.Struct |  |  |

### Wizards.Arena.Protocol.SessionInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | SessionId | string |  |  |
| 2 | PlatformId | string |  |  |

### google.protobuf.Any

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | type_url | string |  |  |
| 2 | value | bytes |  |  |

### google.protobuf.BoolValue

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | bool |  |  |

### google.protobuf.BytesValue

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | bytes |  |  |

### google.protobuf.DoubleValue

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | double |  |  |

### google.protobuf.FloatValue

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | float |  |  |

### google.protobuf.Int32Value

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | int32 |  |  |

### google.protobuf.Int64Value

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | int64 |  |  |

### google.protobuf.ListValue

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | values | google.protobuf.Value | yes |  |

### google.protobuf.StringValue

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | string |  |  |

### google.protobuf.Struct

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | fields | google.protobuf.Struct.FieldsEntry | yes |  |

### google.protobuf.Struct.FieldsEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | google.protobuf.Value |  |  |

### google.protobuf.Timestamp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seconds | int64 |  |  |
| 2 | nanos | int32 |  |  |

### google.protobuf.UInt32Value

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | uint32 |  |  |

### google.protobuf.UInt64Value

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | uint64 |  |  |

### google.protobuf.Value

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | null_value | google.protobuf.NullValue |  | kind |
| 2 | number_value | double |  | kind |
| 3 | string_value | string |  | kind |
| 4 | bool_value | bool |  | kind |
| 5 | struct_value | google.protobuf.Struct |  | kind |
| 6 | list_value | google.protobuf.ListValue |  | kind |

### wotc.mtgo.gre.external.messaging.AIConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | matchConfig | wotc.mtgo.gre.external.messaging.MatchConfig |  |  |
| 2 | defaultSettings | wotc.mtgo.gre.external.messaging.SettingsMessage |  |  |
| 3 | deckConfigs | wotc.mtgo.gre.external.messaging.DeckConfig | yes |  |
| 4 | responderConfigs | wotc.mtgo.gre.external.messaging.ResponderConfig | yes |  |
| 5 | checkpointFilePath | string |  |  |

### wotc.mtgo.gre.external.messaging.AchievementConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seatId | uint32 |  |  |
| 2 | name | string |  |  |
| 3 | grpId | uint32 |  |  |
| 4 | parameters | wotc.mtgo.gre.external.messaging.AchievementParameter | yes |  |

### wotc.mtgo.gre.external.messaging.AchievementInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | id | uint32 |  |  |
| 2 | seatId | uint32 |  |  |
| 3 | name | string |  |  |
| 4 | count | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.AchievementParameter

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | uint32 |  |  |
| 2 | value | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.AchievementStateMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | id | uint32 |  |  |
| 2 | matchId | string |  |  |
| 3 | playerAchievements | wotc.mtgo.gre.external.messaging.PlayerAchievementInfo | yes |  |
| 4 | gameNumber | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.Action

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | actionType | wotc.mtgo.gre.external.messaging.ActionType |  |  |
| 2 | grpId | uint32 |  |  |
| 3 | instanceId | uint32 |  |  |
| 4 | facetId | uint32 |  |  |
| 6 | abilityGrpId | uint32 |  |  |
| 8 | sourceId | uint32 |  |  |
| 9 | alternativeGrpId | uint32 |  |  |
| 10 | manaPaymentOptions | wotc.mtgo.gre.external.messaging.ManaPaymentOption | yes |  |
| 11 | manaCost | wotc.mtgo.gre.external.messaging.ManaRequirement | yes |  |
| 12 | shouldStop | bool |  |  |
| 13 | disqualifyingSourceId | uint32 |  |  |
| 14 | selectionType | uint32 |  |  |
| 15 | selection | uint32 |  |  |
| 17 | alternativeSourceZcid | uint32 |  |  |
| 18 | visibility | wotc.mtgo.gre.external.messaging.Visibility |  |  |
| 19 | targets | wotc.mtgo.gre.external.messaging.TargetSelection | yes |  |
| 20 | manaPaymentConditions | wotc.mtgo.gre.external.messaging.ManaPaymentCondition | yes |  |
| 21 | autoTapSolution | wotc.mtgo.gre.external.messaging.AutoTapSolution |  |  |
| 22 | auxiliaryManaCosts | wotc.mtgo.gre.external.messaging.CostBundle | yes |  |
| 24 | maxActivations | uint32 |  |  |
| 25 | isBatchable | bool |  |  |
| 26 | twobridGenericManaCost | wotc.mtgo.gre.external.messaging.ManaRequirement |  |  |
| 27 | highlight | wotc.mtgo.gre.external.messaging.HighlightType |  |  |
| 28 | costs | wotc.mtgo.gre.external.messaging.Cost | yes |  |
| 30 | timingSourceGrpid | uint32 |  |  |
| 31 | uniqueAbilityId | uint32 |  |  |
| 32 | consumableCostAdjustmentQualificationIds | uint32 | yes |  |
| 33 | isManaAbilityWithSideEffect | bool |  |  |
| 34 | assumeCanBePaidFor | bool |  |  |
| 35 | manaSelections | wotc.mtgo.gre.external.messaging.ManaSelection | yes |  |
| 36 | manaSpentMatters | bool |  |  |

### wotc.mtgo.gre.external.messaging.ActionInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | actionId | uint32 |  |  |
| 2 | seatId | uint32 |  |  |
| 3 | action | wotc.mtgo.gre.external.messaging.Action |  |  |

### wotc.mtgo.gre.external.messaging.AndCost

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | costs | wotc.mtgo.gre.external.messaging.Cost | yes |  |

### wotc.mtgo.gre.external.messaging.AnyMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | timestamp | int64 |  |  |
| 2 | ServerToGREMessage | wotc.mtgo.gre.external.messaging.ServerToGREMessage |  | message |
| 3 | GREToServerMessage | wotc.mtgo.gre.external.messaging.GREToServerMessage |  | message |
| 4 | ClientToGREMessage | wotc.mtgo.gre.external.messaging.ClientToGREMessage |  | message |
| 5 | GREToClientMessage | wotc.mtgo.gre.external.messaging.GREToClientMessage |  | message |

### wotc.mtgo.gre.external.messaging.AnyToGREMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | timestamp | int64 |  |  |
| 2 | ServerToGREMessage | wotc.mtgo.gre.external.messaging.ServerToGREMessage |  | message |
| 3 | ClientToGREMessage | wotc.mtgo.gre.external.messaging.ClientToGREMessage |  | message |

### wotc.mtgo.gre.external.messaging.AssignDamageResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | assigners | wotc.mtgo.gre.external.messaging.DamageAssigner | yes |  |

### wotc.mtgo.gre.external.messaging.AttackInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | targetId | uint32 |  |  |
| 2 | damageOrdered | bool |  |  |
| 3 | damageAssigned | bool |  |  |
| 4 | orderedBlockers | wotc.mtgo.gre.external.messaging.OrderedDamageAssignment | yes |  |
| 5 | alternativeGrpId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.AttackWarning

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | instanceId | uint32 |  |  |
| 2 | type | wotc.mtgo.gre.external.messaging.AttackWarningType |  |  |
| 3 | warningPromptId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.Attacker

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | attackerInstanceId | uint32 |  |  |
| 2 | legalDamageRecipients | wotc.mtgo.gre.external.messaging.DamageRecipient | yes |  |
| 3 | selectedDamageRecipient | wotc.mtgo.gre.external.messaging.DamageRecipient |  |  |
| 4 | alternativeGrpId | uint32 |  |  |
| 6 | mustAttack | bool |  |  |

### wotc.mtgo.gre.external.messaging.AuthenticateRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | clientId | string |  |  |
| 2 | clientAuthToken | bytes |  |  |
| 3 | playerName | string |  |  |
| 4 | playFabSessionTicket | string |  |  |
| 5 | inactivityTimeoutMs | int32 |  |  |
| 10 | clientInfo | wotc.mtgo.gre.external.messaging.ClientInfo |  |  |

### wotc.mtgo.gre.external.messaging.AuthenticateResponse

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | clientId | string |  |  |
| 2 | sessionId | string |  |  |
| 3 | inactivityTimeoutMs | int32 |  |  |
| 10 | screenName | string |  |  |

### wotc.mtgo.gre.external.messaging.AutoAnswer

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | promptGrpId | uint32 |  |  |
| 2 | abilityGrpId | uint32 |  |  |
| 3 | cardTitleId | uint32 |  |  |
| 4 | appliesTo | wotc.mtgo.gre.external.messaging.SettingScope |  |  |
| 5 | status | wotc.mtgo.gre.external.messaging.SettingStatus |  |  |
| 6 | answer | wotc.mtgo.gre.external.messaging.Answer |  |  |

### wotc.mtgo.gre.external.messaging.AutoRespondPermission

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seatIds | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.AutoTapAction

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | instanceId | uint32 |  |  |
| 2 | abilityGrpId | uint32 |  |  |
| 3 | manaId | uint32 |  |  |
| 4 | manaPaymentOption | wotc.mtgo.gre.external.messaging.ManaPaymentOption |  |  |

### wotc.mtgo.gre.external.messaging.AutoTapActionsAvailableReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | autoTapSolutions | wotc.mtgo.gre.external.messaging.AutoTapSolution | yes |  |

### wotc.mtgo.gre.external.messaging.AutoTapManaPayment

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | manaColor | wotc.mtgo.gre.external.messaging.ManaColor |  |  |
| 2 | manaSpecTypeBits | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.AutoTapSolution

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | autoTapActions | wotc.mtgo.gre.external.messaging.AutoTapAction | yes |  |
| 2 | manaPaymentConditions | wotc.mtgo.gre.external.messaging.ManaPaymentCondition | yes |  |
| 3 | selectedManaColors | wotc.mtgo.gre.external.messaging.ManaColor | yes |  |
| 4 | manaPayments | wotc.mtgo.gre.external.messaging.AutoTapManaPayment | yes |  |

### wotc.mtgo.gre.external.messaging.AutoYield

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | abilityGrpId | uint32 |  |  |
| 2 | cardTitleId | uint32 |  |  |
| 3 | appliesTo | wotc.mtgo.gre.external.messaging.SettingScope |  |  |
| 4 | status | wotc.mtgo.gre.external.messaging.SettingStatus |  |  |

### wotc.mtgo.gre.external.messaging.BlockInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | attackerIds | uint32 | yes |  |
| 2 | damageOrdered | bool |  |  |
| 3 | damageAssigned | bool |  |  |
| 4 | orderedAttackers | wotc.mtgo.gre.external.messaging.OrderedDamageAssignment | yes |  |

### wotc.mtgo.gre.external.messaging.BlockWarning

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | instanceId | uint32 |  |  |
| 2 | type | wotc.mtgo.gre.external.messaging.BlockWarningType |  |  |
| 3 | warningPromptId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.Blocker

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | blockerInstanceId | uint32 |  |  |
| 2 | attackerInstanceIds | uint32 | yes |  |
| 3 | selectedAttackerInstanceIds | uint32 | yes |  |
| 4 | minAttackers | uint32 |  |  |
| 5 | maxAttackers | uint32 |  |  |
| 6 | mustBlock | bool |  |  |

### wotc.mtgo.gre.external.messaging.BoolValue

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | bool |  |  |

### wotc.mtgo.gre.external.messaging.BoolValues

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | values | bool | yes |  |

### wotc.mtgo.gre.external.messaging.CLIPSConfiguration

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | enableWatch | bool |  |  |
| 2 | enableMetrics | bool |  |  |
| 3 | enableLogging | bool |  |  |
| 4 | initFile | string |  |  |
| 7 | metricsFile | string |  |  |
| 8 | logFile | string |  |  |
| 9 | metricsFileAppend | bool |  |  |
| 10 | logFileAppend | bool |  |  |
| 11 | enableDynamicConstraintChecking | bool |  |  |

### wotc.mtgo.gre.external.messaging.CancelActionReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### wotc.mtgo.gre.external.messaging.CardConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | count | uint32 |  |  |
| 2 | title | string |  |  |
| 3 | expansionCode | string |  |  |
| 4 | rarity | string |  |  |
| 5 | cardPoolFilePath | string |  |  |

### wotc.mtgo.gre.external.messaging.CardSkinTuple

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | catalogId | uint32 |  |  |
| 2 | skinCode | string |  |  |

### wotc.mtgo.gre.external.messaging.CastingTimeOptionReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ctoId | uint32 |  |  |
| 2 | castingTimeOptionType | wotc.mtgo.gre.external.messaging.CastingTimeOptionType |  |  |
| 3 | affectedId | uint32 |  |  |
| 4 | affectorId | uint32 |  |  |
| 5 | grpId | uint32 |  |  |
| 6 | playerIdToPrompt | uint32 |  |  |
| 7 | isRequired | bool |  |  |
| 8 | prompt | wotc.mtgo.gre.external.messaging.Prompt |  |  |
| 9 | numericInputReq | wotc.mtgo.gre.external.messaging.NumericInputReq |  | message |
| 10 | selectManaTypeReq | wotc.mtgo.gre.external.messaging.SelectManaTypeReq |  | message |
| 11 | modalReq | wotc.mtgo.gre.external.messaging.ModalReq |  | message |
| 12 | selectNReq | wotc.mtgo.gre.external.messaging.SelectNReq |  | message |
| 13 | manaCost | wotc.mtgo.gre.external.messaging.ManaRequirement | yes |  |
| 15 | autoTapSolution | wotc.mtgo.gre.external.messaging.AutoTapSolution |  |  |

### wotc.mtgo.gre.external.messaging.CastingTimeOptionResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ctoId | uint32 |  |  |
| 2 | castingTimeOptionType | wotc.mtgo.gre.external.messaging.CastingTimeOptionType |  |  |
| 3 | numericInputResp | wotc.mtgo.gre.external.messaging.NumericInputResp |  | message |
| 4 | selectManaTypeResp | wotc.mtgo.gre.external.messaging.SelectManaTypeResp |  | message |
| 5 | chooseModalResp | wotc.mtgo.gre.external.messaging.ChooseModalResp |  | message |
| 6 | selectNResp | wotc.mtgo.gre.external.messaging.SelectNResp |  | message |

### wotc.mtgo.gre.external.messaging.CastingTimeOptionsResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | castingTimeOptionResp | wotc.mtgo.gre.external.messaging.CastingTimeOptionResp |  |  |
| 2 | castingTimeOptionResps | wotc.mtgo.gre.external.messaging.CastingTimeOptionResp | yes |  |

### wotc.mtgo.gre.external.messaging.CheckpointConfiguration

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | enabled | bool |  |  |

### wotc.mtgo.gre.external.messaging.ChooseModalResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | grpIds | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.ChooseStartingPlayerResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | teamType | wotc.mtgo.gre.external.messaging.TeamType |  |  |
| 2 | systemSeatId | uint32 |  |  |
| 3 | teamId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.ClientInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | clientId | string |  |  |
| 2 | clientType | wotc.mtgo.gre.external.messaging.ClientType |  |  |
| 10 | clientVersion | string |  |  |
| 11 | clientLocation | string |  |  |
| 12 | clientLanguage | string |  |  |

### wotc.mtgo.gre.external.messaging.ClientToGREMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | type | wotc.mtgo.gre.external.messaging.ClientMessageType |  |  |
| 2 | systemSeatId | uint32 |  |  |
| 3 | gameStateId | uint32 |  |  |
| 4 | respId | uint32 |  |  |
| 5 | connectReq | wotc.mtgo.gre.external.messaging.ConnectReq |  | message |
| 6 | cancelActionReq | wotc.mtgo.gre.external.messaging.CancelActionReq |  | message |
| 8 | concedeReq | wotc.mtgo.gre.external.messaging.ConcedeReq |  | message |
| 9 | forceDrawReq | wotc.mtgo.gre.external.messaging.ForceDrawReq |  | message |
| 10 | groupResp | wotc.mtgo.gre.external.messaging.GroupResp |  | message |
| 11 | mulliganResp | wotc.mtgo.gre.external.messaging.MulliganResp |  | message |
| 12 | optionalResp | wotc.mtgo.gre.external.messaging.OptionalResp |  | message |
| 13 | orderResp | wotc.mtgo.gre.external.messaging.OrderResp |  | message |
| 14 | performActionResp | wotc.mtgo.gre.external.messaging.PerformActionResp |  | message |
| 15 | selectNResp | wotc.mtgo.gre.external.messaging.SelectNResp |  | message |
| 16 | setSettingsReq | wotc.mtgo.gre.external.messaging.SetSettingsReq |  | message |
| 21 | chooseStartingPlayerResp | wotc.mtgo.gre.external.messaging.ChooseStartingPlayerResp |  | message |
| 22 | declareAttackersResp | wotc.mtgo.gre.external.messaging.DeclareAttackersResp |  | message |
| 23 | declareBlockersResp | wotc.mtgo.gre.external.messaging.DeclareBlockersResp |  | message |
| 25 | assignDamageResp | wotc.mtgo.gre.external.messaging.AssignDamageResp |  | message |
| 26 | selectTargetsResp | wotc.mtgo.gre.external.messaging.SelectTargetsResp |  | message |
| 27 | selectReplacementResp | wotc.mtgo.gre.external.messaging.SelectReplacementResp |  | message |
| 28 | selectNGroupResp | wotc.mtgo.gre.external.messaging.SelectNGroupResp |  | message |
| 30 | distributionResp | wotc.mtgo.gre.external.messaging.DistributionResp |  | message |
| 31 | numericInputResp | wotc.mtgo.gre.external.messaging.NumericInputResp |  | message |
| 32 | searchResp | wotc.mtgo.gre.external.messaging.SearchResp |  | message |
| 33 | effectCostResp | wotc.mtgo.gre.external.messaging.EffectCostResp |  | message |
| 34 | castingTimeOptionsResp | wotc.mtgo.gre.external.messaging.CastingTimeOptionsResp |  | message |
| 36 | selectFromGroupsResp | wotc.mtgo.gre.external.messaging.SelectFromGroupsResp |  | message |
| 37 | searchFromGroupsResp | wotc.mtgo.gre.external.messaging.SearchFromGroupsResp |  | message |
| 38 | gatherResp | wotc.mtgo.gre.external.messaging.GatherResp |  | message |
| 40 | uiMessage | wotc.mtgo.gre.external.messaging.UIMessage |  | message |
| 41 | submitDeckResp | wotc.mtgo.gre.external.messaging.SubmitDeckResp |  | message |
| 42 | controlReq | wotc.mtgo.gre.external.messaging.ControlReq |  | message |
| 43 | performAutoTapActionsResp | wotc.mtgo.gre.external.messaging.PerformAutoTapActionsResp |  | message |
| 44 | stringInputResp | wotc.mtgo.gre.external.messaging.StringInputResp |  | message |
| 45 | selectCountersResp | wotc.mtgo.gre.external.messaging.SelectCountersResp |  | message |
| 46 | predictionReq | wotc.mtgo.gre.external.messaging.PredictionReq |  | message |
| 100 | timerId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.ClientToMatchDoorConnectRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | matchId | string |  |  |
| 2 | mcFabricUri | string |  |  |
| 10 | clientToGreMessageBytes | bytes |  |  |

### wotc.mtgo.gre.external.messaging.ClientToMatchServiceMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | requestId | int32 |  |  |
| 2 | clientToMatchServiceMessageType | wotc.mtgo.gre.external.messaging.ClientToMatchServiceMessageType |  |  |
| 3 | timestamp | int64 |  |  |
| 4 | transactionId | string |  |  |
| 100 | payload | bytes |  |  |

### wotc.mtgo.gre.external.messaging.ConcedeReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | scope | wotc.mtgo.gre.external.messaging.MatchScope |  |  |
| 2 | gameNumber | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.ConnectReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 5 | defaultSettings | wotc.mtgo.gre.external.messaging.SettingsMessage |  |  |
| 6 | protoVer | wotc.mtgo.gre.external.messaging.ProtoVersion |  |  |
| 8 | grpVersion | wotc.mtgo.gre.external.messaging.Version |  |  |
| 9 | grpChangelist | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.ControlReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | type | wotc.mtgo.gre.external.messaging.ControllerType |  |  |
| 2 | controllerId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.Cost

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | type | wotc.mtgo.gre.external.messaging.CostType |  |  |
| 2 | id | uint32 |  |  |
| 3 | objectId | uint32 |  |  |
| 4 | abilityGrpId | uint32 |  |  |
| 5 | index | uint32 |  |  |
| 7 | manaCost | wotc.mtgo.gre.external.messaging.ManaCost |  | message |
| 8 | effectCost | wotc.mtgo.gre.external.messaging.EffectCost |  | message |
| 9 | lifeCost | wotc.mtgo.gre.external.messaging.LifeCost |  | message |
| 10 | loyaltyCost | wotc.mtgo.gre.external.messaging.LoyaltyCost |  | message |
| 11 | orCost | wotc.mtgo.gre.external.messaging.OrCost |  | message |
| 12 | andCost | wotc.mtgo.gre.external.messaging.AndCost |  | message |

### wotc.mtgo.gre.external.messaging.CostBundle

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | costs | wotc.mtgo.gre.external.messaging.Cost | yes |  |

### wotc.mtgo.gre.external.messaging.Counter

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | id | uint32 |  |  |
| 2 | type | wotc.mtgo.gre.external.messaging.CounterType |  |  |
| 3 | count | uint32 |  |  |
| 4 | sourceId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.CounterPair

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | instanceId | uint32 |  |  |
| 2 | counterType | wotc.mtgo.gre.external.messaging.CounterType |  |  |
| 3 | count | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.CreateMatchGameRoomRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | gameRoomConfig | wotc.mtgo.gre.external.messaging.MatchGameRoomConfig |  |  |

### wotc.mtgo.gre.external.messaging.CreateMatchGameRoomResponseV2

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | mcFabricUri | string |  |  |

### wotc.mtgo.gre.external.messaging.CubeConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | cardIds | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.DamageAssigner

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | instanceId | uint32 |  |  |
| 2 | totalDamage | uint32 |  |  |
| 3 | assignments | wotc.mtgo.gre.external.messaging.DamageAssignment | yes |  |
| 4 | decisionPrompt | wotc.mtgo.gre.external.messaging.Prompt |  |  |
| 5 | canIgnoreBlockers | bool |  |  |
| 7 | damageAffectedByReplacements | bool |  |  |

### wotc.mtgo.gre.external.messaging.DamageAssignment

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | instanceId | uint32 |  |  |
| 2 | minDamage | uint32 |  |  |
| 3 | maxDamage | uint32 |  |  |
| 4 | assignedDamage | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.DamageRecipient

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | type | wotc.mtgo.gre.external.messaging.DamageRecType |  |  |
| 2 | teamId | uint32 |  | id |
| 3 | playerSystemSeatId | uint32 |  | id |
| 4 | planeswalkerInstanceId | uint32 |  | id |

### wotc.mtgo.gre.external.messaging.DeckConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seatId | uint32 |  |  |
| 2 | deckCards | wotc.mtgo.gre.external.messaging.CardConfig | yes |  |
| 3 | sideboardCards | wotc.mtgo.gre.external.messaging.CardConfig | yes |  |
| 4 | commanderCards | wotc.mtgo.gre.external.messaging.CardConfig | yes |  |
| 5 | deckFilePath | string |  |  |

### wotc.mtgo.gre.external.messaging.DeckConstraintInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | minDeckSize | uint32 |  |  |
| 2 | maxDeckSize | uint32 |  |  |
| 3 | minSideboardSize | uint32 |  |  |
| 4 | maxSideboardSize | uint32 |  |  |
| 5 | minCommanderSize | uint32 |  |  |
| 6 | maxCommanderSize | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.DeckMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | deckCards | uint32 | yes |  |
| 2 | sideboardCards | uint32 | yes |  |
| 3 | commanderCards | uint32 | yes |  |
| 4 | deckMessageFieldFour | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.DeclareAttackersResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | selectedAttackers | wotc.mtgo.gre.external.messaging.Attacker | yes |  |
| 2 | autoDeclare | bool |  |  |
| 3 | autoDeclareDamageRecipient | wotc.mtgo.gre.external.messaging.DamageRecipient |  |  |

### wotc.mtgo.gre.external.messaging.DeclareBlockersResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | selectedBlockers | wotc.mtgo.gre.external.messaging.Blocker | yes |  |

### wotc.mtgo.gre.external.messaging.Distribution

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | instanceId | uint32 |  |  |
| 2 | amount | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.DistributionResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | distributions | wotc.mtgo.gre.external.messaging.Distribution | yes |  |

### wotc.mtgo.gre.external.messaging.DoubleValue

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | double |  |  |

### wotc.mtgo.gre.external.messaging.DoubleValues

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | values | double | yes |  |

### wotc.mtgo.gre.external.messaging.EchoRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | message | string |  |  |

### wotc.mtgo.gre.external.messaging.EchoResponse

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | message | string |  |  |

### wotc.mtgo.gre.external.messaging.EffectCost

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | cardMechanicTypes | wotc.mtgo.gre.external.messaging.CardMechanicType | yes |  |
| 2 | effectCostType | wotc.mtgo.gre.external.messaging.EffectCostType |  |  |
| 3 | count | int32 |  |  |

### wotc.mtgo.gre.external.messaging.EffectCostReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 3 | effectCostType | wotc.mtgo.gre.external.messaging.EffectCostType |  |  |
| 4 | costSelection | wotc.mtgo.gre.external.messaging.SelectNReq |  | message |
| 5 | counterSelection | wotc.mtgo.gre.external.messaging.SelectCountersReq |  | message |
| 6 | groupSelection | wotc.mtgo.gre.external.messaging.SelectFromGroupsReq |  | message |
| 7 | gatherReq | wotc.mtgo.gre.external.messaging.GatherReq |  | message |

### wotc.mtgo.gre.external.messaging.EffectCostResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | effectCostType | wotc.mtgo.gre.external.messaging.EffectCostType |  |  |
| 3 | costSelection | wotc.mtgo.gre.external.messaging.SelectNResp |  | message |
| 4 | counterSelection | wotc.mtgo.gre.external.messaging.SelectCountersResp |  | message |
| 5 | groupSelection | wotc.mtgo.gre.external.messaging.SelectFromGroupsResp |  | message |
| 38 | gatherResp | wotc.mtgo.gre.external.messaging.GatherResp |  | message |

### wotc.mtgo.gre.external.messaging.FinalMatchResult

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | matchId | string |  |  |
| 2 | matchForceResult | wotc.mtgo.gre.external.messaging.ResultSpec |  |  |
| 3 | matchCompletedReason | wotc.mtgo.gre.external.messaging.MatchCompletedReasonType |  |  |
| 5 | resultList | wotc.mtgo.gre.external.messaging.ResultSpec | yes |  |
| 10 | errorMessage | string |  |  |

### wotc.mtgo.gre.external.messaging.FloatValue

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | float |  |  |

### wotc.mtgo.gre.external.messaging.FloatValues

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | values | float | yes |  |

### wotc.mtgo.gre.external.messaging.ForceDrawReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | scope | wotc.mtgo.gre.external.messaging.MatchScope |  |  |

### wotc.mtgo.gre.external.messaging.GREAssignPriorityMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### wotc.mtgo.gre.external.messaging.GRECancelScheduledMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | timerId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.GRECheckpointRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### wotc.mtgo.gre.external.messaging.GREConfigRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | greConfiguration | wotc.mtgo.gre.external.messaging.GREConfiguration |  |  |

### wotc.mtgo.gre.external.messaging.GREConfiguration

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | gameStateRedactorConfiguration | wotc.mtgo.gre.external.messaging.GameStateRedactorConfiguration |  |  |
| 2 | clipsConfiguration | wotc.mtgo.gre.external.messaging.CLIPSConfiguration |  |  |
| 3 | checkpointConfiguration | wotc.mtgo.gre.external.messaging.CheckpointConfiguration |  |  |

### wotc.mtgo.gre.external.messaging.GRECreateRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | matchID | string |  |  |

### wotc.mtgo.gre.external.messaging.GREDequeuedMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | message | wotc.mtgo.gre.external.messaging.ClientToGREMessage |  |  |

### wotc.mtgo.gre.external.messaging.GREDestroyRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### wotc.mtgo.gre.external.messaging.GREElapseTimeRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | timerId | uint32 |  |  |
| 2 | timeMs | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.GREEnqueuedMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | message | wotc.mtgo.gre.external.messaging.ServerToGREMessage |  |  |

### wotc.mtgo.gre.external.messaging.GREExpireTimerRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | timerId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.GREForceResultRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | result | wotc.mtgo.gre.external.messaging.ResultSpec |  |  |

### wotc.mtgo.gre.external.messaging.GREInterpretRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | clips | string |  |  |

### wotc.mtgo.gre.external.messaging.GREMetrics

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | kvps | wotc.mtgo.gre.external.messaging.KeyValuePair | yes |  |

### wotc.mtgo.gre.external.messaging.GRERestoreRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | binaryGameState | wotc.mtgo.gre.external.messaging.BinaryGameState |  |  |

### wotc.mtgo.gre.external.messaging.GREResumeMatchRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | binaryGameState | wotc.mtgo.gre.external.messaging.BinaryGameState |  |  |

### wotc.mtgo.gre.external.messaging.GREScheduledMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | timerId | uint32 |  |  |
| 2 | delayMs | int32 |  |  |
| 3 | message | wotc.mtgo.gre.external.messaging.ServerToGREMessage |  |  |

### wotc.mtgo.gre.external.messaging.GREStartDelayedTimerMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seatId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.GREStartMatchRequest

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | matchConfig | wotc.mtgo.gre.external.messaging.MatchConfig |  |  |

### wotc.mtgo.gre.external.messaging.GRETickMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### wotc.mtgo.gre.external.messaging.GRETimeoutMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seatId | uint32 |  |  |
| 2 | type | wotc.mtgo.gre.external.messaging.TimeoutType |  |  |

### wotc.mtgo.gre.external.messaging.GRETimerExpiredMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | expiredTimerId | uint32 |  |  |
| 2 | serverToGREMessage | wotc.mtgo.gre.external.messaging.ServerToGREMessage |  | message |
| 3 | clientToGREMessage | wotc.mtgo.gre.external.messaging.ClientToGREMessage |  | message |

### wotc.mtgo.gre.external.messaging.GREToAnyMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | timestamp | int64 |  |  |
| 2 | GREToServerMessage | wotc.mtgo.gre.external.messaging.GREToServerMessage |  | message |
| 3 | GREToClientMessage | wotc.mtgo.gre.external.messaging.GREToClientMessage |  | message |

### wotc.mtgo.gre.external.messaging.GREToServerMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | msgId | uint32 |  |  |
| 2 | binaryGameState | wotc.mtgo.gre.external.messaging.BinaryGameState |  | message |
| 3 | reportResultMessage | wotc.mtgo.gre.external.messaging.ReportResultMessage |  | message |
| 4 | scheduledMessage | wotc.mtgo.gre.external.messaging.GREScheduledMessage |  | message |
| 5 | cancelScheduledMessage | wotc.mtgo.gre.external.messaging.GRECancelScheduledMessage |  | message |
| 6 | gameMetrics | wotc.mtgo.gre.external.messaging.GameMetrics |  | message |
| 7 | greMetrics | wotc.mtgo.gre.external.messaging.GREMetrics |  | message |
| 8 | achievementStateMessage | wotc.mtgo.gre.external.messaging.AchievementStateMessage |  | message |
| 9 | enqueuedMessage | wotc.mtgo.gre.external.messaging.GREEnqueuedMessage |  | message |

### wotc.mtgo.gre.external.messaging.GameInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | matchID | string |  |  |
| 3 | gameNumber | uint32 |  |  |
| 5 | stage | wotc.mtgo.gre.external.messaging.GameStage |  |  |
| 6 | type | wotc.mtgo.gre.external.messaging.GameType |  |  |
| 7 | variant | wotc.mtgo.gre.external.messaging.GameVariant |  |  |
| 8 | matchState | wotc.mtgo.gre.external.messaging.MatchState |  |  |
| 9 | matchWinCondition | wotc.mtgo.gre.external.messaging.MatchWinCondition |  |  |
| 10 | maxTimeoutCount | uint32 |  |  |
| 11 | maxPipCount | uint32 |  |  |
| 12 | timeoutDurationSec | uint32 |  |  |
| 13 | results | wotc.mtgo.gre.external.messaging.ResultSpec | yes |  |
| 14 | superFormat | wotc.mtgo.gre.external.messaging.SuperFormat |  |  |
| 15 | mulliganType | wotc.mtgo.gre.external.messaging.MulliganType |  |  |
| 16 | freeMulliganCount | uint32 |  |  |
| 17 | deckConstraintInfo | wotc.mtgo.gre.external.messaging.DeckConstraintInfo |  |  |
| 18 | sideboardLoadingEnabled | bool |  |  |

### wotc.mtgo.gre.external.messaging.GameMetrics

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | playerMetrics | wotc.mtgo.gre.external.messaging.PlayerMetrics | yes |  |
| 2 | gameNumber | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.GameObjectInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | instanceId | uint32 |  |  |
| 2 | grpId | uint32 |  |  |
| 4 | type | wotc.mtgo.gre.external.messaging.GameObjectType |  |  |
| 5 | zoneId | uint32 |  |  |
| 6 | visibility | wotc.mtgo.gre.external.messaging.Visibility |  |  |
| 7 | ownerSeatId | uint32 |  |  |
| 8 | controllerSeatId | uint32 |  |  |
| 9 | superTypes | wotc.mtgo.gre.external.messaging.SuperType | yes |  |
| 10 | cardTypes | wotc.mtgo.gre.external.messaging.CardType | yes |  |
| 11 | subtypes | wotc.mtgo.gre.external.messaging.SubType | yes |  |
| 12 | color | wotc.mtgo.gre.external.messaging.CardColor | yes |  |
| 13 | power | wotc.mtgo.gre.external.messaging.Int32Value |  |  |
| 14 | toughness | wotc.mtgo.gre.external.messaging.Int32Value |  |  |
| 16 | isCopy | bool |  |  |
| 17 | isTapped | bool |  |  |
| 18 | hasSummoningSickness | bool |  |  |
| 19 | attackState | wotc.mtgo.gre.external.messaging.AttackState |  |  |
| 20 | blockState | wotc.mtgo.gre.external.messaging.BlockState |  |  |
| 23 | damage | uint32 |  |  |
| 24 | attackInfo | wotc.mtgo.gre.external.messaging.AttackInfo |  |  |
| 25 | blockInfo | wotc.mtgo.gre.external.messaging.BlockInfo |  |  |
| 26 | viewers | uint32 | yes |  |
| 27 | loyalty | wotc.mtgo.gre.external.messaging.UInt32Value |  |  |
| 28 | objectSourceGrpId | uint32 |  |  |
| 29 | name | uint32 |  |  |
| 31 | parentId | uint32 |  |  |
| 32 | overlayGrpId | uint32 |  |  |
| 33 | isFacedown | bool |  |  |
| 34 | skinCode | string |  |  |
| 35 | loyaltyUsed | wotc.mtgo.gre.external.messaging.UInt32Value |  |  |
| 36 | abilityOriginalCardGrpIds | uint32 | yes |  |
| 37 | baseSkinCode | string |  |  |
| 38 | removed_subtypes | wotc.mtgo.gre.external.messaging.SubType | yes |  |
| 39 | defense | wotc.mtgo.gre.external.messaging.UInt32Value |  |  |
| 40 | othersideGrpId | uint32 |  |  |
| 41 | uniqueAbilities | wotc.mtgo.gre.external.messaging.UniqueAbilityInfo | yes |  |

### wotc.mtgo.gre.external.messaging.GameStateRedactorConfiguration

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | enableRedaction | bool |  |  |
| 2 | enableForceDiff | bool |  |  |
| 3 | enableZoneRedaction | bool |  |  |

### wotc.mtgo.gre.external.messaging.GatherResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | gatherings | wotc.mtgo.gre.external.messaging.Gathering | yes |  |

### wotc.mtgo.gre.external.messaging.GatherSource

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | sourceId | uint32 |  |  |
| 2 | minAmount | uint32 |  |  |
| 3 | maxAmount | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.Gathering

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | instanceId | uint32 |  |  |
| 2 | amount | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.Group

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ids | uint32 | yes |  |
| 2 | groupId | int32 |  |  |
| 3 | zoneType | wotc.mtgo.gre.external.messaging.ZoneType |  |  |
| 4 | subZoneType | wotc.mtgo.gre.external.messaging.SubZoneType |  |  |
| 5 | minSelect | int32 |  |  |
| 6 | maxSelect | int32 |  |  |
| 7 | isFacedown | bool |  |  |

### wotc.mtgo.gre.external.messaging.GroupResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | groups | wotc.mtgo.gre.external.messaging.Group | yes |  |
| 3 | groupType | wotc.mtgo.gre.external.messaging.GroupType |  |  |

### wotc.mtgo.gre.external.messaging.GroupSpecification

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | lowerBound | uint32 |  |  |
| 2 | upperBound | uint32 |  |  |
| 3 | zoneType | wotc.mtgo.gre.external.messaging.ZoneType |  |  |
| 4 | subZoneType | wotc.mtgo.gre.external.messaging.SubZoneType |  |  |
| 6 | isFacedown | bool |  |  |
| 7 | hot_ids | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.IRLoaderConfiguration

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | enableLimitedIRLoading | bool |  |  |

### wotc.mtgo.gre.external.messaging.Int32Value

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | int32 |  |  |

### wotc.mtgo.gre.external.messaging.Int32Values

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | values | int32 | yes |  |

### wotc.mtgo.gre.external.messaging.Int64Value

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | int64 |  |  |

### wotc.mtgo.gre.external.messaging.Int64Values

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | values | int64 | yes |  |

### wotc.mtgo.gre.external.messaging.KeyValuePair

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | single_value | wotc.mtgo.gre.external.messaging.SingleValue |  | value |
| 3 | repeated_value | wotc.mtgo.gre.external.messaging.RepeatedValue |  | value |

### wotc.mtgo.gre.external.messaging.KeyValuePairInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | type | wotc.mtgo.gre.external.messaging.KeyValuePairValueType |  |  |
| 3 | value_uint32 | uint32 | yes |  |
| 4 | value_int32 | int32 | yes |  |
| 5 | value_uint64 | uint64 | yes |  |
| 6 | value_int64 | int64 | yes |  |
| 7 | value_bool | bool | yes |  |
| 8 | value_string | string | yes |  |
| 9 | value_float | float | yes |  |
| 10 | value_double | double | yes |  |

### wotc.mtgo.gre.external.messaging.LifeCost

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | count | int32 |  |  |

### wotc.mtgo.gre.external.messaging.LoyaltyCost

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | count | int32 |  |  |

### wotc.mtgo.gre.external.messaging.ManaColorCount

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | color | wotc.mtgo.gre.external.messaging.ManaColor |  |  |
| 2 | count | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.ManaCost

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | color | wotc.mtgo.gre.external.messaging.ManaColor | yes |  |
| 2 | count | int32 |  |  |

### wotc.mtgo.gre.external.messaging.ManaInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | manaId | uint32 |  |  |
| 2 | color | wotc.mtgo.gre.external.messaging.ManaColor |  |  |
| 3 | srcInstanceId | uint32 |  |  |
| 4 | specs | wotc.mtgo.gre.external.messaging.ManaInfo.Spec | yes |  |
| 5 | abilityGrpId | uint32 |  |  |
| 6 | linkedSelections | uint32 | yes |  |
| 7 | count | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.ManaInfo.Spec

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | type | wotc.mtgo.gre.external.messaging.ManaSpecType |  |  |

### wotc.mtgo.gre.external.messaging.ManaPaymentCondition

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | colors | wotc.mtgo.gre.external.messaging.ManaColor | yes |  |
| 2 | specs | wotc.mtgo.gre.external.messaging.ManaSpecType | yes |  |
| 3 | abilityGrpId | uint32 |  |  |
| 4 | type | wotc.mtgo.gre.external.messaging.ManaPaymentConditionType |  |  |

### wotc.mtgo.gre.external.messaging.ManaPaymentOption

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | mana | wotc.mtgo.gre.external.messaging.ManaInfo | yes |  |
| 4 | optionIndex | uint32 |  |  |
| 5 | haveReplacementsBeenApplied | bool |  |  |
| 6 | haveTriggersBeenApplied | bool |  |  |
| 7 | isUnpredictable | bool |  |  |

### wotc.mtgo.gre.external.messaging.ManaRequirement

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | color | wotc.mtgo.gre.external.messaging.ManaColor | yes |  |
| 2 | count | int32 |  |  |
| 3 | costId | uint32 |  |  |
| 4 | objectId | uint32 |  |  |
| 5 | abilityGrpId | uint32 |  |  |
| 6 | specs | wotc.mtgo.gre.external.messaging.ManaCostSpecType | yes |  |

### wotc.mtgo.gre.external.messaging.ManaSelection

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | instanceId | uint32 |  |  |
| 2 | abilityGrpId | uint32 |  |  |
| 3 | retriggerUniqueAbilityid | uint32 |  |  |
| 4 | selectionCount | uint32 |  |  |
| 5 | validationType | wotc.mtgo.gre.external.messaging.SelectionValidationType |  |  |
| 6 | options | wotc.mtgo.gre.external.messaging.ManaSelectionOption | yes |  |

### wotc.mtgo.gre.external.messaging.ManaSelectionOption

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | selectedColor | wotc.mtgo.gre.external.messaging.ManaColor |  |  |
| 2 | isConstrainedToAlreadyProduced | bool |  |  |
| 3 | mana | wotc.mtgo.gre.external.messaging.ManaColorCount | yes |  |

### wotc.mtgo.gre.external.messaging.MatchConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | gameType | wotc.mtgo.gre.external.messaging.GameType |  |  |
| 2 | gameVariant | wotc.mtgo.gre.external.messaging.GameVariant |  |  |
| 3 | winCondition | wotc.mtgo.gre.external.messaging.MatchWinCondition |  |  |
| 5 | maxPlayerHandSize | uint32 |  |  |
| 6 | teams | wotc.mtgo.gre.external.messaging.TeamConfig | yes |  |
| 7 | spectators | wotc.mtgo.gre.external.messaging.SpectatorConfig | yes |  |
| 9 | testConfig | wotc.mtgo.gre.external.messaging.TestConfig |  |  |
| 10 | mulliganType | wotc.mtgo.gre.external.messaging.MulliganType |  |  |
| 11 | timerConfigs | wotc.mtgo.gre.external.messaging.TimerConfig | yes |  |
| 12 | timerPackage | wotc.mtgo.gre.external.messaging.TimerPackage |  |  |
| 13 | timeoutConfig | wotc.mtgo.gre.external.messaging.TimeoutConfig |  |  |
| 15 | superFormat | wotc.mtgo.gre.external.messaging.SuperFormat |  |  |
| 16 | deckConstraintInfo | wotc.mtgo.gre.external.messaging.DeckConstraintInfo |  |  |
| 17 | cubeConfig | wotc.mtgo.gre.external.messaging.CubeConfig |  |  |
| 18 | achievementConfigs | wotc.mtgo.gre.external.messaging.AchievementConfig | yes |  |
| 19 | matchConfigFieldNineteen | bool |  |  |

### wotc.mtgo.gre.external.messaging.MatchGameRoomConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | eventId | string |  |  |
| 8 | reservedPlayers | wotc.mtgo.gre.external.messaging.MatchGameRoomPlayerInfo | yes |  |
| 11 | matchId | string |  |  |
| 12 | matchConfig | wotc.mtgo.gre.external.messaging.MatchConfig |  |  |
| 13 | greConfig | wotc.mtgo.gre.external.messaging.GREConfiguration |  |  |
| 14 | checkpoint | wotc.mtgo.gre.external.messaging.BinaryGameState |  |  |
| 20 | isVisible | bool |  |  |
| 21 | isDebug | bool |  |  |
| 31 | greHostLoggerLevel | string |  |  |
| 35 | joinRoomTimeoutSecs | int32 |  |  |
| 36 | playerDisconnectTimeoutSecs | int32 |  |  |
| 37 | serviceMetadata | wotc.mtgo.gre.external.messaging.MatchGameRoomConfig.ServiceMetadataEntry | yes |  |
| 38 | clientMetadata | wotc.mtgo.gre.external.messaging.MatchGameRoomConfig.ClientMetadataEntry | yes |  |

### wotc.mtgo.gre.external.messaging.MatchGameRoomConfig.ClientMetadataEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | string |  |  |

### wotc.mtgo.gre.external.messaging.MatchGameRoomConfig.ServiceMetadataEntry

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | string |  |  |

### wotc.mtgo.gre.external.messaging.MatchGameRoomInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | gameRoomConfig | wotc.mtgo.gre.external.messaging.MatchGameRoomConfig |  |  |
| 3 | stateType | wotc.mtgo.gre.external.messaging.MatchGameRoomStateType |  |  |
| 4 | finalMatchResult | wotc.mtgo.gre.external.messaging.FinalMatchResult |  |  |
| 6 | players | wotc.mtgo.gre.external.messaging.MatchGameRoomPlayerInfo | yes |  |
| 7 | playerMetrics | wotc.mtgo.gre.external.messaging.MatchPlayerMetrics | yes |  |

### wotc.mtgo.gre.external.messaging.MatchGameRoomPlayerInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | userId | string |  |  |
| 2 | playerName | string |  |  |
| 3 | systemSeatId | int32 |  |  |
| 4 | teamId | int32 |  |  |
| 5 | connectionInfo | wotc.mtgo.gre.external.messaging.UserConnectionInfo |  |  |
| 10 | courseId | string |  |  |
| 11 | deckId | string |  |  |
| 13 | sessionId | string |  |  |
| 14 | isWotc | bool |  |  |
| 15 | platformId | string |  |  |
| 16 | isBotPlayer | bool |  |  |
| 17 | eventId | string |  |  |

### wotc.mtgo.gre.external.messaging.MatchGameRoomStateChangedEvent

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | gameRoomInfo | wotc.mtgo.gre.external.messaging.MatchGameRoomInfo |  |  |

### wotc.mtgo.gre.external.messaging.MatchPlayerMetrics

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | userId | string |  |  |
| 2 | metrics | wotc.mtgo.gre.external.messaging.PlayerMetricKeyValuePair | yes |  |

### wotc.mtgo.gre.external.messaging.MatchServiceError

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | errorCode | wotc.mtgo.gre.external.messaging.MatchServiceErrorCode |  |  |
| 3 | errorMessage | string |  |  |

### wotc.mtgo.gre.external.messaging.ModalOption

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | grpId | uint32 |  |  |
| 2 | modeCost | wotc.mtgo.gre.external.messaging.Cost | yes |  |
| 3 | weight | uint32 |  |  |
| 4 | advisability | wotc.mtgo.gre.external.messaging.ModalChoiceAdvisability |  |  |
| 5 | advisabilityPromptId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.ModalReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | modalOptions | wotc.mtgo.gre.external.messaging.ModalOption | yes |  |
| 2 | abilityGrpId | uint32 |  |  |
| 3 | minSel | uint32 |  |  |
| 4 | maxSel | uint32 |  |  |
| 5 | repeatedSelectAllowed | bool |  |  |
| 6 | excludedOptions | wotc.mtgo.gre.external.messaging.ModalOption | yes |  |
| 7 | otherSel | uint32 | yes |  |
| 8 | modeCostSolutions | wotc.mtgo.gre.external.messaging.ModeCostSolution | yes |  |
| 9 | isWeighted | bool |  |  |

### wotc.mtgo.gre.external.messaging.ModeCostSolution

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | modeGrpIds | uint32 | yes |  |
| 2 | autoTapSolution | wotc.mtgo.gre.external.messaging.AutoTapSolution |  |  |

### wotc.mtgo.gre.external.messaging.MulliganResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | decision | wotc.mtgo.gre.external.messaging.MulliganOption |  |  |

### wotc.mtgo.gre.external.messaging.NumericInputResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | numericInputValue | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.OnChat

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | text | string |  |  |

### wotc.mtgo.gre.external.messaging.OnGenericEvent

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | category | string |  |  |
| 2 | payload | string |  |  |

### wotc.mtgo.gre.external.messaging.OnHover

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | objectId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.OnSelect

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | objectId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.OnShuffle

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | objectIds | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.OptionalResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | response | wotc.mtgo.gre.external.messaging.OptionResponse |  |  |
| 2 | persistence | wotc.mtgo.gre.external.messaging.ChoicePersistence |  |  |
| 3 | appliesTo | wotc.mtgo.gre.external.messaging.SettingScope |  |  |
| 4 | mapTo | wotc.mtgo.gre.external.messaging.SettingKey |  |  |

### wotc.mtgo.gre.external.messaging.OrCost

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | costs | wotc.mtgo.gre.external.messaging.Cost | yes |  |
| 2 | isOptional | bool |  |  |

### wotc.mtgo.gre.external.messaging.OrderResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | ids | uint32 | yes |  |
| 2 | ordering | wotc.mtgo.gre.external.messaging.OrderingType |  |  |

### wotc.mtgo.gre.external.messaging.OrderedDamageAssignment

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | instanceId | uint32 |  |  |
| 2 | assignedDamage | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.PerformActionResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | actions | wotc.mtgo.gre.external.messaging.Action | yes |  |
| 2 | autoPassPriority | wotc.mtgo.gre.external.messaging.AutoPassPriority |  |  |
| 3 | setYield | wotc.mtgo.gre.external.messaging.SettingStatus |  |  |
| 4 | appliesTo | wotc.mtgo.gre.external.messaging.SettingScope |  |  |
| 5 | mapTo | wotc.mtgo.gre.external.messaging.SettingKey |  |  |

### wotc.mtgo.gre.external.messaging.PerformAutoTapActionsResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | index | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.PlayerAchievementInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seatId | uint32 |  |  |
| 2 | playerId | string |  |  |
| 3 | achievements | wotc.mtgo.gre.external.messaging.AchievementInfo | yes |  |

### wotc.mtgo.gre.external.messaging.PlayerConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | systemSeatId | uint32 |  |  |
| 2 | deckCards | uint32 | yes |  |
| 3 | sideboardCards | uint32 | yes |  |
| 6 | startingLifeTotal | uint32 |  |  |
| 7 | startingHandSizeSpecified | bool |  |  |
| 8 | startingHandSize | uint32 |  |  |
| 9 | commandEmblems | uint32 | yes |  |
| 10 | jazzMusicians | uint32 | yes |  |
| 11 | skins | wotc.mtgo.gre.external.messaging.CardSkinTuple | yes |  |
| 12 | shuffleRestriction | wotc.mtgo.gre.external.messaging.ShuffleRestriction |  |  |
| 13 | playerConfigFieldThirteen | uint32 |  |  |
| 14 | toasts | wotc.mtgo.gre.external.messaging.ToastTuple | yes |  |

### wotc.mtgo.gre.external.messaging.PlayerDieRoll

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | systemSeatId | uint32 |  |  |
| 2 | rollValue | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.PlayerInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | lifeTotal | int32 |  |  |
| 2 | systemSeatNumber | uint32 |  |  |
| 3 | status | wotc.mtgo.gre.external.messaging.PlayerStatus |  |  |
| 6 | manaPool | wotc.mtgo.gre.external.messaging.ManaInfo | yes |  |
| 7 | maxHandSize | uint32 |  |  |
| 8 | mulliganCount | uint32 |  |  |
| 9 | turnNumber | uint32 |  |  |
| 10 | teamId | uint32 |  |  |
| 11 | timerIds | uint32 | yes |  |
| 12 | controllerSeatId | uint32 |  |  |
| 13 | controllerType | wotc.mtgo.gre.external.messaging.ControllerType |  |  |
| 14 | timeoutCount | uint32 |  |  |
| 15 | pipCount | uint32 |  |  |
| 16 | pendingMessageType | wotc.mtgo.gre.external.messaging.ClientMessageType |  |  |
| 17 | startingLifeTotal | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.PlayerMetric

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.PlayerMetricKeyValuePair

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | key | string |  |  |
| 2 | value | int32 |  |  |

### wotc.mtgo.gre.external.messaging.PlayerMetrics

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | playerId | uint32 |  |  |
| 3 | metricsV2 | wotc.mtgo.gre.external.messaging.PlayerMetric | yes |  |

### wotc.mtgo.gre.external.messaging.PredictionReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| — | No fields | | | |

### wotc.mtgo.gre.external.messaging.PromptParameter

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | parameterName | string |  |  |
| 2 | type | wotc.mtgo.gre.external.messaging.ParameterType |  |  |
| 3 | reference | wotc.mtgo.gre.external.messaging.Reference |  | value |
| 4 | stringValue | string |  | value |
| 5 | numberValue | int32 |  | value |
| 7 | promptId | int32 |  | value |

### wotc.mtgo.gre.external.messaging.Reference

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | type | wotc.mtgo.gre.external.messaging.ReferenceType |  |  |
| 2 | id | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.RepeatedValue

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | uint32_values | wotc.mtgo.gre.external.messaging.UInt32Values |  | values |
| 2 | int32_values | wotc.mtgo.gre.external.messaging.Int32Values |  | values |
| 3 | uint64_values | wotc.mtgo.gre.external.messaging.UInt64Values |  | values |
| 4 | int64_values | wotc.mtgo.gre.external.messaging.Int64Values |  | values |
| 5 | bool_values | wotc.mtgo.gre.external.messaging.BoolValues |  | values |
| 6 | string_values | wotc.mtgo.gre.external.messaging.StringValues |  | values |
| 7 | float_values | wotc.mtgo.gre.external.messaging.FloatValues |  | values |
| 8 | double_values | wotc.mtgo.gre.external.messaging.DoubleValues |  | values |

### wotc.mtgo.gre.external.messaging.ReplacementEffect

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | objectInstance | uint32 |  |  |
| 2 | uniqueAbilityId | uint32 |  |  |
| 4 | abilityGrpId | uint32 |  |  |
| 5 | affectedObject | uint32 |  |  |
| 6 | replacementEffectId | uint32 |  |  |
| 7 | conferringObjectZcid | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.ReplacementSelectionByZcid

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | selectableZcid | uint32 |  |  |
| 2 | replacementEffectId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.ReportResultMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | result | wotc.mtgo.gre.external.messaging.ResultSpec |  |  |

### wotc.mtgo.gre.external.messaging.ResponderConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seatId | uint32 |  |  |
| 2 | responderFilePath | string |  |  |
| 3 | disableMetaResponses | bool |  |  |
| 4 | disableManaActions | bool |  |  |
| 5 | disableColdActions | bool |  |  |
| 6 | disableColdTargetSelection | bool |  |  |
| 7 | enableSmartPlayActions | bool |  |  |

### wotc.mtgo.gre.external.messaging.ResultSpec

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | scope | wotc.mtgo.gre.external.messaging.MatchScope |  |  |
| 2 | result | wotc.mtgo.gre.external.messaging.ResultType |  |  |
| 3 | winningTeamId | uint32 |  |  |
| 4 | reason | wotc.mtgo.gre.external.messaging.ResultReason |  |  |

### wotc.mtgo.gre.external.messaging.SearchFromGroupsResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 3 | groups | wotc.mtgo.gre.external.messaging.Group | yes |  |
| 4 | addZoneToSearchScope | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.SearchResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | itemsFound | uint32 | yes |  |
| 3 | addZoneToSearchScope | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.SelectCountersResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 5 | selections | wotc.mtgo.gre.external.messaging.CounterPair | yes |  |

### wotc.mtgo.gre.external.messaging.SelectFromGroupsResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 3 | groups | wotc.mtgo.gre.external.messaging.Group | yes |  |

### wotc.mtgo.gre.external.messaging.SelectManaTypeReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | manaColors | wotc.mtgo.gre.external.messaging.ManaColor | yes |  |
| 3 | sourceId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.SelectManaTypeResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 3 | manaColor | wotc.mtgo.gre.external.messaging.ManaColor |  |  |

### wotc.mtgo.gre.external.messaging.SelectNGroupResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 2 | selectedgroupids | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.SelectNResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 5 | ids | uint32 | yes |  |
| 6 | useArbitrary | wotc.mtgo.gre.external.messaging.OrderingType |  |  |

### wotc.mtgo.gre.external.messaging.SelectReplacementResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | replacement | wotc.mtgo.gre.external.messaging.ReplacementEffect |  |  |

### wotc.mtgo.gre.external.messaging.SelectTargetsResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | target | wotc.mtgo.gre.external.messaging.TargetSelection |  |  |

### wotc.mtgo.gre.external.messaging.ServerToGREMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | createRequest | wotc.mtgo.gre.external.messaging.GRECreateRequest |  | message |
| 2 | configRequest | wotc.mtgo.gre.external.messaging.GREConfigRequest |  | message |
| 3 | startMatchRequest | wotc.mtgo.gre.external.messaging.GREStartMatchRequest |  | message |
| 4 | resumeMatchRequest | wotc.mtgo.gre.external.messaging.GREResumeMatchRequest |  | message |
| 5 | checkpointRequest | wotc.mtgo.gre.external.messaging.GRECheckpointRequest |  | message |
| 6 | forceResultRequest | wotc.mtgo.gre.external.messaging.GREForceResultRequest |  | message |
| 7 | destroyRequest | wotc.mtgo.gre.external.messaging.GREDestroyRequest |  | message |
| 8 | timerExpired | wotc.mtgo.gre.external.messaging.GRETimerExpiredMessage |  | message |
| 9 | tick | wotc.mtgo.gre.external.messaging.GRETickMessage |  | message |
| 10 | timeout | wotc.mtgo.gre.external.messaging.GRETimeoutMessage |  | message |
| 11 | interpretRequest | wotc.mtgo.gre.external.messaging.GREInterpretRequest |  | message |
| 12 | expireTimerRequest | wotc.mtgo.gre.external.messaging.GREExpireTimerRequest |  | message |
| 13 | elapseTimeRequest | wotc.mtgo.gre.external.messaging.GREElapseTimeRequest |  | message |
| 14 | startDelayedTimerMessage | wotc.mtgo.gre.external.messaging.GREStartDelayedTimerMessage |  | message |
| 15 | restoreRequest | wotc.mtgo.gre.external.messaging.GRERestoreRequest |  | message |
| 16 | dequeued | wotc.mtgo.gre.external.messaging.GREDequeuedMessage |  | message |
| 17 | assignPriority | wotc.mtgo.gre.external.messaging.GREAssignPriorityMessage |  | message |

### wotc.mtgo.gre.external.messaging.SetSettingsReq

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | settings | wotc.mtgo.gre.external.messaging.SettingsMessage |  |  |
| 2 | turnNumber | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.SettingsConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seatId | uint32 |  |  |
| 2 | defaultSettings | wotc.mtgo.gre.external.messaging.SettingsMessage |  |  |
| 3 | settingsFilePath | string |  |  |

### wotc.mtgo.gre.external.messaging.SettingsMessage

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | stops | wotc.mtgo.gre.external.messaging.Stop | yes |  |
| 2 | yields | wotc.mtgo.gre.external.messaging.AutoYield | yes |  |
| 3 | answers | wotc.mtgo.gre.external.messaging.AutoAnswer | yes |  |
| 4 | autoPassOption | wotc.mtgo.gre.external.messaging.AutoPassOption |  |  |
| 5 | graveyardOrder | wotc.mtgo.gre.external.messaging.OrderingType |  |  |
| 6 | clearAllStops | wotc.mtgo.gre.external.messaging.SettingStatus |  |  |
| 7 | clearAllYields | wotc.mtgo.gre.external.messaging.SettingStatus |  |  |
| 8 | clearAllAnswers | wotc.mtgo.gre.external.messaging.SettingStatus |  |  |
| 9 | manaSelectionType | wotc.mtgo.gre.external.messaging.ManaSelectionType |  |  |
| 10 | defaultAutoPassOption | wotc.mtgo.gre.external.messaging.AutoPassOption |  |  |
| 11 | smartStopsSetting | wotc.mtgo.gre.external.messaging.SmartStopsSetting |  |  |
| 12 | autoTapStopsSetting | wotc.mtgo.gre.external.messaging.AutoTapStopsSetting |  |  |
| 13 | autoOptionalPaymentCancellationSetting | wotc.mtgo.gre.external.messaging.Setting |  |  |
| 14 | manaPaymentStrategyType | wotc.mtgo.gre.external.messaging.ManaPaymentStrategyType |  |  |
| 15 | transientStops | wotc.mtgo.gre.external.messaging.Stop | yes |  |
| 17 | autoSelectReplacementSetting | wotc.mtgo.gre.external.messaging.Setting |  |  |
| 19 | stackAutoPassOption | wotc.mtgo.gre.external.messaging.AutoPassOption |  |  |

### wotc.mtgo.gre.external.messaging.SingleValue

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | uint32_value | uint32 |  | value |
| 2 | int32_value | int32 |  | value |
| 3 | uint64_value | uint64 |  | value |
| 4 | int64_value | int64 |  | value |
| 5 | bool_value | bool |  | value |
| 6 | string_value | string |  | value |
| 7 | float_value | float |  | value |
| 8 | double_value | double |  | value |

### wotc.mtgo.gre.external.messaging.SpectatorConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | seatId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.Stop

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | stopType | wotc.mtgo.gre.external.messaging.StopType |  |  |
| 2 | appliesTo | wotc.mtgo.gre.external.messaging.SettingScope |  |  |
| 3 | status | wotc.mtgo.gre.external.messaging.SettingStatus |  |  |

### wotc.mtgo.gre.external.messaging.StringInputResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | inputValue | string |  |  |

### wotc.mtgo.gre.external.messaging.StringValue

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | string |  |  |

### wotc.mtgo.gre.external.messaging.StringValues

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | values | string | yes |  |

### wotc.mtgo.gre.external.messaging.SubmitDeckResp

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | deck | wotc.mtgo.gre.external.messaging.DeckMessage |  |  |

### wotc.mtgo.gre.external.messaging.Target

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | targetInstanceId | uint32 |  |  |
| 2 | legalAction | wotc.mtgo.gre.external.messaging.SelectAction |  |  |
| 3 | highlight | wotc.mtgo.gre.external.messaging.HighlightType |  |  |

### wotc.mtgo.gre.external.messaging.TargetInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | targetType | wotc.mtgo.gre.external.messaging.TargetType |  |  |
| 2 | targetId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.TargetSelection

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | targetIdx | uint32 |  |  |
| 2 | targets | wotc.mtgo.gre.external.messaging.Target | yes |  |
| 3 | minTargets | uint32 |  |  |
| 4 | maxTargets | uint32 |  |  |
| 5 | selectedTargets | uint32 |  |  |
| 6 | prompt | wotc.mtgo.gre.external.messaging.Prompt |  |  |
| 7 | targetSourceZoneId | uint32 |  |  |
| 8 | targetingAbilityGrpId | uint32 |  |  |
| 9 | targetingPlayer | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.TeamConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | teamID | uint32 |  |  |
| 2 | players | wotc.mtgo.gre.external.messaging.PlayerConfig | yes |  |

### wotc.mtgo.gre.external.messaging.TeamInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | id | uint32 |  |  |
| 2 | playerIds | uint32 | yes |  |
| 3 | status | wotc.mtgo.gre.external.messaging.TeamStatus |  |  |

### wotc.mtgo.gre.external.messaging.TestConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | shuffleRestriction | wotc.mtgo.gre.external.messaging.ShuffleRestriction |  |  |
| 2 | startingPlayer | uint32 |  |  |
| 3 | useSpecifiedSeed | bool |  |  |
| 4 | randomSeed | uint32 |  |  |
| 5 | useZeroManaCostForCasting | bool |  |  |
| 6 | useMaxLandsPerTurn | uint32 |  |  |
| 7 | treeOfCongress | wotc.mtgo.gre.external.messaging.TreeOfCongress |  |  |
| 8 | enableAchievements | bool |  |  |
| 9 | disableHiFiGameStateQueueing | bool |  |  |
| 10 | disableGameStateQueueingDuringCancelableActions | bool |  |  |
| 11 | disableHiFiGameStates | bool |  |  |
| 12 | disableTimers | bool |  |  |
| 13 | randomSeeds | uint32 | yes |  |
| 14 | freeMulliganCount | uint32 |  |  |
| 15 | randomDrawCount | uint32 |  |  |
| 16 | minRandomDrawDistance | float |  |  |
| 17 | maxRandomDrawDistance | float |  |  |
| 18 | enableAutoAcceptHand | bool |  |  |
| 19 | enableAutoPlay | bool |  |  |
| 20 | disableUnexpectedMessageRecovery | bool |  |  |
| 21 | disableSideboarding | bool |  |  |
| 22 | candidateShuffleCount | uint32 |  |  |
| 23 | bestCandidateShuffleCount | uint32 |  |  |
| 24 | autoRespondPermission | wotc.mtgo.gre.external.messaging.AutoRespondPermission |  |  |
| 25 | disableDeferredActionCostPayment | bool |  |  |
| 26 | disableIndividualDamageAssignments | bool |  |  |
| 27 | disableHiFiSettings | bool |  |  |
| 28 | enableSideboardLoading | bool |  |  |
| 29 | disableUniqueAbilityActivations | bool |  |  |
| 30 | enablePredictions | bool |  |  |
| 31 | enableDamageOrdering | bool |  |  |
| 32 | disableHiFiAchievements | bool |  |  |
| 33 | disableAmortizedAutoPassing | bool |  |  |
| 34 | isReplaying | bool |  |  |

### wotc.mtgo.gre.external.messaging.TimeoutConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | maxTimeoutCount | uint32 |  |  |
| 2 | startingTimeoutCount | uint32 |  |  |
| 3 | maxPipCount | uint32 |  |  |
| 4 | startingPipCount | uint32 |  |  |
| 5 | durationSec | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.TimerConfig

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | type | wotc.mtgo.gre.external.messaging.TimerType |  |  |
| 4 | durationSec | uint32 |  |  |
| 5 | behavior | wotc.mtgo.gre.external.messaging.TimerBehavior |  |  |
| 6 | controls | wotc.mtgo.gre.external.messaging.TimerControl | yes |  |
| 7 | warningThresholdSec | uint32 |  |  |
| 8 | maxDurationSec | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.TimerControl

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | event | wotc.mtgo.gre.external.messaging.TimerEvent |  |  |
| 2 | behavior | wotc.mtgo.gre.external.messaging.TimerBehavior |  |  |

### wotc.mtgo.gre.external.messaging.TimerInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | timerId | uint32 |  |  |
| 2 | type | wotc.mtgo.gre.external.messaging.TimerType |  |  |
| 3 | durationSec | uint32 |  |  |
| 4 | elapsedSec | uint32 |  |  |
| 5 | running | bool |  |  |
| 6 | behavior | wotc.mtgo.gre.external.messaging.TimerBehavior |  |  |
| 7 | warningThresholdSec | uint32 |  |  |
| 8 | elapsedMs | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.ToastTuple

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | toastID | uint32 |  |  |
| 2 | toastParam | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.TreeOfCongress

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | systemSeatId | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.TriggeredAbility

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | abilityGrpId | uint32 |  |  |
| 2 | sourceZcid | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.TurnInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | phase | wotc.mtgo.gre.external.messaging.Phase |  |  |
| 2 | step | wotc.mtgo.gre.external.messaging.Step |  |  |
| 3 | turnNumber | uint32 |  |  |
| 4 | activePlayer | uint32 |  |  |
| 5 | priorityPlayer | uint32 |  |  |
| 6 | decisionPlayer | uint32 |  |  |
| 8 | nextPhase | wotc.mtgo.gre.external.messaging.Phase |  |  |
| 9 | nextStep | wotc.mtgo.gre.external.messaging.Step |  |  |

### wotc.mtgo.gre.external.messaging.UInt32Value

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.UInt32Values

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | values | uint32 | yes |  |

### wotc.mtgo.gre.external.messaging.UInt64Value

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | value | uint64 |  |  |

### wotc.mtgo.gre.external.messaging.UInt64Values

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | values | uint64 | yes |  |

### wotc.mtgo.gre.external.messaging.UniqueAbilityInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | id | uint32 |  |  |
| 2 | grpId | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.UserConnectionInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | connectionState | wotc.mtgo.gre.external.messaging.ConnectionState |  |  |
| 10 | lastConnectTimestamp | int64 |  |  |
| 11 | lastDisconnectTimestamp | int64 |  |  |

### wotc.mtgo.gre.external.messaging.UserOption

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | optionPrompt | wotc.mtgo.gre.external.messaging.Prompt |  |  |
| 2 | responseType | wotc.mtgo.gre.external.messaging.ClientMessageType |  |  |

### wotc.mtgo.gre.external.messaging.Version

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | majorVersion | uint32 |  |  |
| 2 | minorVersion | uint32 |  |  |
| 3 | revisionVersion | uint32 |  |  |
| 4 | buildVersion | uint32 |  |  |

### wotc.mtgo.gre.external.messaging.ZoneInfo

| Field number | Field | Type | Repeated | Oneof |
|---:|---|---|---|---|
| 1 | zoneId | uint32 |  |  |
| 2 | type | wotc.mtgo.gre.external.messaging.ZoneType |  |  |
| 3 | visibility | wotc.mtgo.gre.external.messaging.Visibility |  |  |
| 4 | ownerSeatId | uint32 |  |  |
| 5 | objectInstanceIds | uint32 | yes |  |
| 6 | viewers | uint32 | yes |  |

## Complete enum values

### wotc.mtgo.gre.external.messaging.GREMessageType

| Value | Name |
|---:|---|
| 0 | GREMessageType_None |
| 1 | GREMessageType_GameStateMessage |
| 2 | GREMessageType_ActionsAvailableReq |
| 6 | GREMessageType_ChooseStartingPlayerReq |
| 7 | GREMessageType_ConnectResp |
| 9 | GREMessageType_GetSettingsResp |
| 10 | GREMessageType_SetSettingsResp |
| 11 | GREMessageType_GroupReq |
| 12 | GREMessageType_IllegalRequest |
| 15 | GREMessageType_MulliganReq |
| 17 | GREMessageType_OrderReq |
| 18 | GREMessageType_PromptReq |
| 21 | GREMessageType_RevealHandReq |
| 22 | GREMessageType_SelectNReq |
| 24 | GREMessageType_AllowForceDraw |
| 25 | GREMessageType_BinaryGameState |
| 26 | GREMessageType_DeclareAttackersReq |
| 27 | GREMessageType_SubmitAttackersResp |
| 28 | GREMessageType_DeclareBlockersReq |
| 29 | GREMessageType_SubmitBlockersResp |
| 30 | GREMessageType_AssignDamageReq |
| 31 | GREMessageType_AssignDamageConfirmation |
| 32 | GREMessageType_OrderCombatDamageReq |
| 33 | GREMessageType_OrderDamageConfirmation |
| 34 | GREMessageType_SelectTargetsReq |
| 35 | GREMessageType_SubmitTargetsResp |
| 36 | GREMessageType_PayCostsReq |
| 37 | GREMessageType_IntermissionReq |
| 38 | GREMessageType_DieRollResultsResp |
| 39 | GREMessageType_SelectReplacementReq |
| 40 | GREMessageType_SelectNGroupReq |
| 42 | GREMessageType_DistributionReq |
| 43 | GREMessageType_NumericInputReq |
| 44 | GREMessageType_SearchReq |
| 45 | GREMessageType_OptionalActionMessage |
| 46 | GREMessageType_CastingTimeOptionsReq |
| 48 | GREMessageType_SelectFromGroupsReq |
| 49 | GREMessageType_SearchFromGroupsReq |
| 50 | GREMessageType_GatherReq |
| 51 | GREMessageType_QueuedGameStateMessage |
| 52 | GREMessageType_UIMessage |
| 53 | GREMessageType_SubmitDeckReq |
| 54 | GREMessageType_EdictalMessage |
| 55 | GREMessageType_TimeoutMessage |
| 56 | GREMessageType_TimerStateMessage |
| 57 | GREMessageType_SubmitDeckConfirmation |
| 58 | GREMessageType_StringInputReq |
| 59 | GREMessageType_SelectCountersReq |
| 60 | GREMessageType_PredictionResp |

### wotc.mtgo.gre.external.messaging.AnnotationType

| Value | Name |
|---:|---|
| 0 | AnnotationType_None |
| 1 | AnnotationType_ZoneTransfer |
| 2 | AnnotationType_LossOfGame |
| 3 | AnnotationType_DamageDealt |
| 4 | AnnotationType_TappedUntappedPermanent |
| 5 | AnnotationType_ModifiedPower |
| 6 | AnnotationType_ModifiedToughness |
| 7 | AnnotationType_ModifiedColor |
| 8 | AnnotationType_PhaseOrStepModified |
| 9 | AnnotationType_AddAbility |
| 10 | AnnotationType_ModifiedLife |
| 11 | AnnotationType_CreateAttachment |
| 12 | AnnotationType_RemoveAttachment |
| 13 | AnnotationType_ObjectIdChanged |
| 14 | AnnotationType_Counter |
| 15 | AnnotationType_ControllerChanged |
| 16 | AnnotationType_CounterAdded |
| 17 | AnnotationType_CounterRemoved |
| 18 | AnnotationType_LayeredEffectCreated |
| 19 | AnnotationType_LayeredEffectDestroyed |
| 20 | AnnotationType_Attachment |
| 21 | AnnotationType_Haunt |
| 22 | AnnotationType_CopiedObject |
| 23 | AnnotationType_RemoveAbility |
| 24 | AnnotationType_WinTheGame |
| 25 | AnnotationType_ModifiedType |
| 26 | AnnotationType_TargetSpec |
| 27 | AnnotationType_TextChange |
| 28 | AnnotationType_FaceDown |
| 29 | AnnotationType_TurnPermanent |
| 30 | AnnotationType_DynamicAbility |
| 31 | AnnotationType_ObjectsSelected |
| 32 | AnnotationType_TriggeringObject |
| 33 | AnnotationType_DamageSource |
| 34 | AnnotationType_ManaPaid |
| 35 | AnnotationType_TokenCreated |
| 36 | AnnotationType_AbilityInstanceCreated |
| 37 | AnnotationType_AbilityInstanceDeleted |
| 38 | AnnotationType_DisplayCardUnderCard |
| 39 | AnnotationType_AbilityWordActive |
| 40 | AnnotationType_LinkInfo |
| 41 | AnnotationType_TokenDeleted |
| 42 | AnnotationType_Qualification |
| 43 | AnnotationType_ResolutionStart |
| 44 | AnnotationType_ResolutionComplete |
| 45 | AnnotationType_Designation |
| 46 | AnnotationType_GainDesignation |
| 47 | AnnotationType_CardRevealed |
| 48 | AnnotationType_NewTurnStarted |
| 49 | AnnotationType_ManaDetails |
| 50 | AnnotationType_DisqualifiedEffect |
| 51 | AnnotationType_LayeredEffect |
| 52 | AnnotationType_MiscContinuousEffect |
| 53 | AnnotationType_ShouldntPlay |
| 54 | AnnotationType_UseOrCostsManaCost |
| 55 | AnnotationType_RemainingSelections |
| 56 | AnnotationType_Shuffle |
| 57 | AnnotationType_CoinFlip |
| 58 | AnnotationType_ChoiceResult |
| 59 | AnnotationType_RevealedCardCreated |
| 60 | AnnotationType_RevealedCardDeleted |
| 61 | AnnotationType_SuspendLike |
| 62 | AnnotationType_ReplacementEffect |
| 63 | AnnotationType_EnteredZoneThisTurn |
| 64 | AnnotationType_CastingTimeOption |
| 65 | AnnotationType_Scry |
| 66 | AnnotationType_PredictedDirectDamage |
| 67 | AnnotationType_SwitchPowerToughness |
| 68 | AnnotationType_SupplementalText |
| 69 | AnnotationType_PendingEffect |
| 70 | AnnotationType_AttachmentCreated |
| 71 | AnnotationType_PowerToughnessModCreated |
| 72 | AnnotationType_SyntheticEvent |
| 73 | AnnotationType_UserActionTaken |
| 74 | AnnotationType_DelayedTriggerAffectees |
| 75 | AnnotationType_InstanceRevealedToOpponent |
| 76 | AnnotationType_ModifiedName |
| 77 | AnnotationType_ReplacementEffectApplied |
| 78 | AnnotationType_ReferencedObjects |
| 79 | AnnotationType_ChoosingAttachments |
| 80 | AnnotationType_TemporaryPermanent |
| 81 | AnnotationType_GamewideHistoryCount |
| 82 | AnnotationType_AbilityExhausted |
| 83 | AnnotationType_MultistepEffectStarted |
| 84 | AnnotationType_MultistepEffectComplete |
| 85 | AnnotationType_DieRoll |
| 86 | AnnotationType_DungeonStatus |
| 87 | AnnotationType_LinkedDamage |
| 88 | AnnotationType_ClassLevel |
| 89 | AnnotationType_TokenImmediatelyDied |
| 90 | AnnotationType_DamagedThisTurn |
| 91 | AnnotationType_ReferencedCardNames |
| 92 | AnnotationType_PlayerSelectingTargets |
| 93 | AnnotationType_PlayerSubmittedTargets |
| 94 | AnnotationType_CrewedThisTurn |
| 95 | AnnotationType_PhasedOut |
| 96 | AnnotationType_PhasedIn |
| 97 | AnnotationType_LoyaltyActivationsRemaining |
| 98 | AnnotationType_GroupedIds |
| 99 | AnnotationType_RegeneratePending |
| 100 | AnnotationType_PermanentRegenerated |
| 101 | AnnotationType_ObjectsToDistinguish |
| 102 | AnnotationType_BoonInfo |
| 103 | AnnotationType_LoseDesignation |
| 104 | AnnotationType_SaddledThisTurn |
| 105 | AnnotationType_HiddenInformation |
| 106 | AnnotationType_LoopCount |
| 107 | AnnotationType_Vote |
| 108 | AnnotationType_SuppressedPowerAndToughness |
| 109 | AnnotationType_GenericRelationship |
| 110 | AnnotationType_ColorProduction |
| 111 | AnnotationType_CopyException |
| 112 | AnnotationType_SelectNDecoration |
| 113 | AnnotationType_ModifiedCost |
| 114 | AnnotationType_MultiplayerNumericAid |
| 115 | AnnotationType_HighlightReason |
| 116 | AnnotationType_PersistentChoiceResult |
| 117 | AnnotationType_EnteredZoneAtSameTime |

### wotc.mtgo.gre.external.messaging.OrderingContext

| Value | Name |
|---:|---|
| 0 | OrderingContext_None |
| 1 | OrderingContext_OrderingForBottom |
| 2 | OrderingContext_OrderingForTop |

### wotc.mtgo.gre.external.messaging.GroupingContext

| Value | Name |
|---:|---|
| 0 | GroupingContext_None |
| 1 | GroupingContext_Scry |
| 2 | GroupingContext_Surveil |
| 3 | GroupingContext_LondonMulligan |

### wotc.mtgo.gre.external.messaging.SelectionContext

| Value | Name |
|---:|---|
| 0 | SelectionContext_None |
| 1 | SelectionContext_Discard |
| 2 | SelectionContext_ManaPool |
| 3 | SelectionContext_Resolution |
| 4 | SelectionContext_TriggeredAbility |
| 5 | SelectionContext_Modal |
| 6 | SelectionContext_Replacement |
| 7 | SelectionContext_NonMana_Payment |
| 8 | SelectionContext_ManaFromAbility |
| 9 | SelectionContext_Voting |

### wotc.mtgo.gre.external.messaging.OptionContext

| Value | Name |
|---:|---|
| 0 | OptionContext_None |
| 1 | OptionContext_ManaAbility |
| 2 | OptionContext_Payment |
| 3 | OptionContext_Stacking |
| 4 | OptionContext_Targeting |
| 5 | OptionContext_Resolution |
| 6 | OptionContext_TurnBased |
| 7 | OptionContext_Replacement |
| 8 | OptionContext_ActivateCast |
| 9 | OptionContext_Special |
| 10 | OptionContext_Special_TurnFaceUp |

### wotc.mtgo.gre.external.messaging.SelectionValidationType

| Value | Name |
|---:|---|
| 0 | SelectionValidationType_None |
| 1 | SelectionValidationType_NonRepeatable |
| 2 | SelectionValidationType_ArbitraryRepeats |
| 3 | SelectionValidationType_ConstrainedRepeats |

### wotc.mtgo.gre.external.messaging.AllowCancel

| Value | Name |
|---:|---|
| 0 | AllowCancel_None |
| 1 | AllowCancel_Continue |
| 2 | AllowCancel_Abort |
| 3 | AllowCancel_No |

### Wizards.Arena.Models.Network.CmdType

| Value | Name |
|---:|---|
| 0 | Authenticate |
| 1 | StartHook |
| 2 | Scaling_Passthrough |
| 3 | TencentSecurity_AntiHackData |
| 4 | Tencent_RmtCreateOrder |
| 5 | Attach |
| 6 | GetFormats |
| 7 | ForceDetach |
| 8 | GetConflictingAccountsData |
| 100 | FriendRequestNotification |
| 300 | Grants_ListPendinGrants |
| 301 | Grants_ClaimPendingGrants |
| 400 | Deck_GetDeck |
| 401 | Deck_GetDeckSummaries |
| 402 | Deck_UpsertDeck |
| 403 | Deck_DeleteDeck |
| 404 | Deck_GetAllPreconDecks |
| 405 | Deck_GetPreconDeck |
| 406 | Deck_UpsertDeckV2 |
| 407 | Deck_GetDeckSummariesV2 |
| 408 | Deck_GetAllPreconDecksV2 |
| 409 | Deck_GetPreconDeckV2 |
| 410 | Deck_GetAllPreconDecksV3 |
| 411 | Deck_GetDeckSummariesV3 |
| 412 | Deck_UpsertDeckV3 |
| 500 | DC_IssueChallenge |
| 501 | DC_CancelChallenge |
| 550 | Card_GetCardSet |
| 551 | Card_GetAllCards |
| 552 | Card_RedeemWildCards |
| 553 | Card_CompleteVault |
| 600 | Event_Join |
| 601 | Event_Drop |
| 602 | Event_SetDeck |
| 603 | Event_EnterPairing |
| 604 | Event_GetActiveEvents |
| 605 | Event_GetCourses |
| 606 | Event_LeavePairing |
| 607 | Event_ClaimPrize |
| 608 | Event_GetMatchResultReport |
| 609 | Event_Resign |
| 610 | Event_GetChoices |
| 611 | Event_SetChoice |
| 612 | Event_AiBotMatch |
| 613 | Event_GetActiveMatches |
| 614 | Event_SetJumpStartPacket |
| 615 | Event_JoinDraftQueue |
| 616 | Event_LeaveDraftQueue |
| 617 | Event_PlayerDraftReadyPlayer |
| 618 | Event_PlayerDraftGetTablePacks |
| 619 | Event_JoinDraft |
| 620 | Event_PlayerDraftMakePick |
| 621 | Event_PlayerDraftConfirmCardPoolGrant |
| 622 | Event_SetDeckV2 |
| 623 | Event_GetCoursesV2 |
| 624 | Event_GetActiveEventsV2 |
| 625 | Event_PlayerDraftReserveCard |
| 626 | Event_PlayerDraftClearReservedCard |
| 627 | Event_SetDeckV3 |
| 703 | Store_GetEntitlements |
| 704 | Carousel_GetCarouselItems |
| 705 | Merc_GetSkus |
| 706 | Merc_GetListingsV2 |
| 707 | Merc_PurchaseV2 |
| 708 | Merc_GetStoreStatusV2 |
| 709 | Merc_GetListingsV3 |
| 710 | Merc_GetListingsV4 |
| 711 | Merc_GetSkusV2 |
| 712 | Store_GetEntitlementsV2 |
| 713 | Merc_GetSkusV3 |
| 714 | Merc_GetListingsV5 |
| 715 | Merc_GetSkusAndListings |
| 800 | Currency_GetCurrencies |
| 900 | Booster_OpenBoosters |
| 901 | Booster_GetOwnedBoosters |
| 1000 | Quest_GetQuests |
| 1001 | Quest_SwapQuest |
| 1100 | Rank_GetCombinedRankInfo |
| 1101 | Rank_EvaluatePayouts |
| 1102 | Rank_GetSeasonAndRankDetails |
| 1103 | Rank_GetSeasonHistory |
| 1104 | Rank_GetSeasonAndRewardHistory |
| 1105 | Rank_EvaluatePayoutsV2 |
| 1200 | PeriodicRewards_GetStatus |
| 1201 | Renewal_GetCurrentRenewal |
| 1202 | Renewal_RedeemRewards |
| 1520 | GetVoucherDefinitions |
| 1521 | GetSets |
| 1600 | Account_GetTencentScreenName |
| 1601 | Account_SetTencentScreenName |
| 1602 | Account_GetAvatar |
| 1603 | Account_SetAvatar |
| 1700 | Graph_GetGraphDefinitions |
| 1701 | Graph_GetGraphState |
| 1702 | Graph_Process |
| 1703 | Graph_ProcessV2 |
| 1704 | Graph_SetFavoriteNodes |
| 1800 | BotDraft_StartDraft |
| 1801 | BotDraft_DraftPick |
| 1802 | BotDraft_DraftStatus |
| 1900 | Cosmetics_GetPlayerOwnedCosmetics |
| 1901 | Cosmetics_GetPreferredCosmetics |
| 1902 | Cosmetics_SetPreferredAvatar |
| 1903 | Cosmetics_SetPreferredSleeve |
| 1904 | Cosmetics_SetPreferredPet |
| 1905 | Cosmetics_SetPreferredTitle |
| 1906 | Cosmetics_PurchaseCosmetic |
| 1907 | BotDraft_Debug_UnloadCache |
| 1908 | Draft_CompleteDraft |
| 1909 | Cosmetics_SetPreferredEmotes |
| 1910 | GetPlayBladeQueueConfig |
| 1911 | GetPlayerPreferences |
| 1912 | SetPlayerPreferences |
| 1913 | LogBusinessEvents |
| 1914 | LogBusinessEventsV2 |
| 2200 | GetNetDeckFolders |
| 2300 | GetPlayerInbox |
| 2301 | MarkLetterRead |
| 2302 | ClaimAttachment |
| 2350 | GetWpnNotificationPreferences |
| 2351 | SetOrganizationNotificationPreference |
| 2352 | SetGlobalWpnNotificationPreference |
| 2400 | GetDesignerMetadata |
| 2500 | StaticContent |
| 2600 | GetAllPreferredPrintings |
| 2601 | SetPreferredPrinting |
| 2602 | RemovePreferredPrinting |
| 2650 | TournamentCreate |
| 2651 | TournamentJoin |
| 2652 | TournamentDropPlayer |
| 2653 | TournamentGetState |
| 2654 | TournamentPlayerReadyRound |
| 2655 | TournamentReportMatchResult |
| 2656 | TournamentPlayerReadyStart |
| 2700 | GetAllPrizeWalls |
| 2800 | LobbyJoin |
| 2801 | LobbyCreate |
| 2802 | LobbySendMessage |
| 2803 | LobbyExit |
| 2804 | LobbyInvite |
| 2805 | LobbyClose |
| 2806 | LobbyReconnectAll |
| 2807 | LobbyKick |
| 2900 | GatheringJoin |
| 2901 | GatheringCreate |
| 2902 | GatheringSendMessage |
| 2903 | GatheringExit |
| 2904 | GatheringInvite |
| 2905 | GatheringClose |
| 2906 | GatheringReconnectAll |
| 2907 | GatheringKick |
| 2908 | GatheringUpdateMetadata |
| 2909 | GatheringAssignRole |
| 2910 | GatheringRemoveRole |
| 2911 | GatheringGenerateInviteCode |
| 2912 | GatheringClearInviteCode |
| 2913 | GatheringJoinByInviteCode |
| 2914 | GatheringCreateV2 |
| 2915 | GatheringDeclineInvite |
| 2916 | GatheringCheckText |
| 2917 | GatheringCreateV3 |
| 2918 | GatheringUpdateMetadataV2 |
| 2919 | GatheringReconnectAllV2 |
| 3000 | ChallengeJoin |
| 3001 | ChallengeCreate |
| 3002 | ChallengeSendMessage |
| 3003 | ChallengeExit |
| 3004 | ChallengeInvite |
| 3005 | ChallengeClose |
| 3006 | ChallengeReconnectAll |
| 3007 | ChallengeKick |
| 3008 | ChallengeReady |
| 3009 | ChallengeUnready |
| 3010 | ChallengeSetSettings |
| 3011 | ChallengeIssue |
| 3012 | ChallengeStartLaunchCountdown |
| 3013 | ChallengeSetPlayerSettings |
| 4000 | ExternalChatLogin |
| 4001 | ExternalChatAuthGetToken |
| 4002 | ExternalChatMerge |
| 4003 | ExternalChatUnmerge |
| 4004 | ExternalChatGetUserId |
| 4100 | ReportPlayer |
| 4200 | GetKillSwitchData |
| 4300 | MosaicAuxiliary |
| 9991 | ProtobufTest |

### Wizards.Arena.Models.Network.EChallengeNotificationType

| Value | Name |
|---:|---|
| 0 | Invalid |
| 1 | ChallengeStatus |
| 2 | ChallengeChatMessage |
| 3 | ChallengeInvite |
| 4 | ChallengeClose |
| 5 | ChallengeKick |
| 6 | ChallengeStartLaunchCountdown |

### Wizards.Arena.Models.Network.EGatheringNotificationType

| Value | Name |
|---:|---|
| 0 | Invalid |
| 1 | GatheringStatus |
| 2 | GatheringChatMessage |
| 3 | GatheringInvite |
| 4 | GatheringClose |
| 5 | GatheringKick |

### Wizards.Arena.Models.Network.ELobbyNotificationType

| Value | Name |
|---:|---|
| 0 | Invalid |
| 1 | LobbyStatus |
| 2 | LobbyChatMessage |
| 3 | LobbyInvite |
| 4 | LobbyClose |
| 5 | LobbyKick |

### Wizards.Arena.Models.Network.ETournamentNotificationType

| Value | Name |
|---:|---|
| 0 | Invalid |
| 1 | TournamentIsReady |
| 2 | RoundStarted |
| 3 | RoundReadyUpdate |
| 4 | OtherMatchCompletedUpdate |

### Wizards.Arena.Models.Network.TournamentCmdType

| Value | Name |
|---:|---|
| 0 | CreateTournament |
| 1 | JoinTournament |
| 2 | DropPlayerFromTournament |
| 3 | GetTournamentState |
| 4 | PlayerIsReadyReq |
| 5 | ReportTournamentMatchResult |

### Wizards.Arena.Protocol.RequestType

| Value | Name |
|---:|---|
| 0 | RequestType_Default |
| 1 | RequestType_ForceDetach |

### google.protobuf.NullValue

| Value | Name |
|---:|---|
| 0 | NULL_VALUE |

### wotc.mtgo.gre.external.messaging.AbilityCategory

| Value | Name |
|---:|---|
| 0 | AbilityCategory_None |
| 1 | AbilityCategory_Activated |
| 2 | AbilityCategory_Triggered |
| 3 | AbilityCategory_Static |
| 4 | AbilityCategory_Spell |
| 5 | AbilityCategory_AdditionalCost |
| 6 | AbilityCategory_ActivatedTest |
| 7 | AbilityCategory_Chained |
| 8 | AbilityCategory_AlternativeCost |
| 9 | AbilityCategory_OpeningHandAction |
| 10 | AbilityCategory_Spliced |
| 11 | AbilityCategory_Placeholder11 |

### wotc.mtgo.gre.external.messaging.AbilityPaymentType

| Value | Name |
|---:|---|
| 0 | AbilityPaymentType_None |
| 1 | AbilityPaymentType_TapSymbol |
| 2 | AbilityPaymentType_Loyalty |

### wotc.mtgo.gre.external.messaging.AbilityPresentation

| Value | Name |
|---:|---|
| 0 | AbilityPresentation_None |
| 1 | AbilityPresentation_Badge |
| 2 | AbilityPresentation_Icon |
| 3 | AbilityPresentation_Count |

### wotc.mtgo.gre.external.messaging.AbilitySubCategory

| Value | Name |
|---:|---|
| 0 | AbilitySubCategory_None |
| 1 | AbilitySubCategory_Mana |
| 2 | AbilitySubCategory_Loyalty |
| 3 | AbilitySubCategory_Payment |
| 4 | AbilitySubCategory_Crew |
| 5 | AbilitySubCategory_Embalm |
| 6 | AbilitySubCategory_Cycling |
| 7 | AbilitySubCategory_Transform |
| 8 | AbilitySubCategory_Explore |
| 9 | AbilitySubCategory_Eternalize |
| 10 | AbilitySubCategory_EnterTheBattlefield |
| 11 | AbilitySubCategory_Surveil |
| 12 | AbilitySubCategory_Adapt |
| 13 | AbilitySubCategory_Amass |
| 14 | AbilitySubCategory_Proliferate |
| 15 | AbilitySubCategory_Scry |
| 16 | AbilitySubCategory_Enrage |
| 17 | AbilitySubCategory_Learn |
| 18 | AbilitySubCategory_Investigate |
| 19 | AbilitySubCategory_DependentTrigger |
| 20 | AbilitySubCategory_ClassLevel |
| 21 | AbilitySubCategory_ClassAbilityGranting |
| 22 | AbilitySubCategory_CompleteDungeon |
| 23 | AbilitySubCategory_NonRedundantActivatedAbility |
| 24 | AbilitySubCategory_Draft |
| 25 | AbilitySubCategory_PreciseCombatDamageDealtToPlayerIsRelevant |
| 26 | AbilitySubCategory_Connive |
| 27 | AbilitySubCategory_ToSolveCase |
| 28 | AbilitySubCategory_SolvedCase |
| 29 | AbilitySubCategory_CastingTimeOption |
| 30 | AbilitySubCategory_Monstrosity |
| 31 | AbilitySubCategory_OptionalCost |
| 32 | AbilitySubCategory_StationIntrinsicLevel |
| 33 | AbilitySubCategory_LevelUpAbilityGranting |
| 34 | AbilitySubCategory_Stackless |

### wotc.mtgo.gre.external.messaging.AbilityType

| Value | Name |
|---:|---|
| 0 | AbilityType_None |
| 1 | AbilityType_Deathtouch |
| 2 | AbilityType_Defender |
| 3 | AbilityType_DoubleStrike |
| 4 | AbilityType_Enchant |
| 5 | AbilityType_Equip |
| 6 | AbilityType_FirstStrike |
| 7 | AbilityType_Flash |
| 8 | AbilityType_Flying |
| 9 | AbilityType_Haste |
| 10 | AbilityType_Hexproof |
| 11 | AbilityType_Intimidate |
| 12 | AbilityType_Lifelink |
| 13 | AbilityType_Reach |
| 14 | AbilityType_Trample |
| 15 | AbilityType_Vigilance |
| 16 | AbilityType_Landwalk |
| 21 | AbilityType_ProtectionFrom |
| 22 | AbilityType_Shroud |
| 23 | AbilityType_Banding |
| 24 | AbilityType_Rampage |
| 25 | AbilityType_CumulativeUpkeep |
| 26 | AbilityType_Flanking |
| 27 | AbilityType_Phasing |
| 28 | AbilityType_Buyback |
| 29 | AbilityType_Shadow |
| 30 | AbilityType_Cycling |
| 31 | AbilityType_Echo |
| 32 | AbilityType_Horsemanship |
| 33 | AbilityType_Fading |
| 34 | AbilityType_Kicker |
| 35 | AbilityType_Flashback |
| 36 | AbilityType_Madness |
| 37 | AbilityType_Morph |
| 38 | AbilityType_Fear |
| 39 | AbilityType_Amplify |
| 40 | AbilityType_Provoke |
| 41 | AbilityType_Storm |
| 42 | AbilityType_AffinityFor |
| 43 | AbilityType_Entwine |
| 44 | AbilityType_Modular |
| 45 | AbilityType_Sunburst |
| 46 | AbilityType_Bushido |
| 47 | AbilityType_Soulshift |
| 48 | AbilityType_Splice |
| 49 | AbilityType_Offering |
| 50 | AbilityType_Ninjutsu |
| 51 | AbilityType_Epic |
| 52 | AbilityType_Convoke |
| 53 | AbilityType_Dredge |
| 54 | AbilityType_Transmute |
| 55 | AbilityType_Bloodthirst |
| 56 | AbilityType_Haunt |
| 57 | AbilityType_Replicate |
| 58 | AbilityType_Forecast |
| 59 | AbilityType_Graft |
| 60 | AbilityType_Recover |
| 61 | AbilityType_Ripple |
| 62 | AbilityType_SplitSecond |
| 63 | AbilityType_Suspend |
| 64 | AbilityType_Vanishing |
| 65 | AbilityType_Absorb |
| 66 | AbilityType_AuraSwap |
| 67 | AbilityType_Delve |
| 68 | AbilityType_Fortify |
| 69 | AbilityType_Frenzy |
| 70 | AbilityType_Gravestorm |
| 71 | AbilityType_Poisonous |
| 72 | AbilityType_Transfigure |
| 73 | AbilityType_Champion |
| 74 | AbilityType_Changeling |
| 75 | AbilityType_Evoke |
| 76 | AbilityType_Hideaway |
| 77 | AbilityType_Prowl |
| 78 | AbilityType_Reinforce |
| 79 | AbilityType_Conspire |
| 80 | AbilityType_Persist |
| 81 | AbilityType_Wither |
| 82 | AbilityType_Retrace |
| 83 | AbilityType_Devour |
| 84 | AbilityType_Exalted |
| 85 | AbilityType_Unearth |
| 86 | AbilityType_Cascade |
| 87 | AbilityType_Annihilator |
| 88 | AbilityType_LevelUp |
| 89 | AbilityType_Rebound |
| 90 | AbilityType_UmbraArmor |
| 91 | AbilityType_Infect |
| 92 | AbilityType_BattleCry |
| 93 | AbilityType_LivingWeapon |
| 94 | AbilityType_Undying |
| 95 | AbilityType_Miracle |
| 96 | AbilityType_Soulbond |
| 97 | AbilityType_Overload |
| 98 | AbilityType_Scavenge |
| 99 | AbilityType_Unleash |
| 100 | AbilityType_Cipher |
| 101 | AbilityType_Evolve |
| 102 | AbilityType_Extort |
| 103 | AbilityType_Fuse |
| 104 | AbilityType_Indestructible |
| 105 | AbilityType_Regenerate |
| 106 | AbilityType_EnterZone_Tapped |
| 107 | AbilityType_EnterZone_Controlled |
| 108 | AbilityType_EnterZone_Attached |
| 109 | AbilityType_Level |
| 110 | AbilityType_EnterZone_WithCounters |
| 111 | AbilityType_TurnFaceUp_WithCounters |
| 114 | AbilityType_PlaneswalkerLoyaltyReplacement |
| 115 | AbilityType_CastCommanderFromCommandZone |
| 116 | AbilityType_CommanderZoneTransferReplacement |
| 122 | AbilityType_Multikicker |
| 123 | AbilityType_BasicLandcycling |
| 124 | AbilityType_Forestcycling |
| 125 | AbilityType_Islandcycling |
| 126 | AbilityType_Mountaincycling |
| 127 | AbilityType_Plainscycling |
| 128 | AbilityType_Slivercycling |
| 129 | AbilityType_Swampcycling |
| 130 | AbilityType_Wizardcycling |
| 134 | AbilityType_Monstrosity |
| 135 | AbilityType_Tribute |
| 136 | AbilityType_Outlast |
| 137 | AbilityType_Prowess |
| 138 | AbilityType_Bolster |
| 139 | AbilityType_PlaneswalkerDamageRedirection |
| 140 | AbilityType_OrderObjectsEnteringGraveyard |
| 141 | AbilityType_OrderObjectsEnteringLibrary |
| 142 | AbilityType_Menace |
| 143 | AbilityType_Skulk |
| 144 | AbilityType_Ingest |
| 145 | AbilityType_Manifest |
| 147 | AbilityType_Emerge |
| 148 | AbilityType_Deliverance |
| 149 | AbilityType_CastWithoutPayingManaCost |
| 150 | AbilityType_Megamorph |
| 151 | AbilityType_Devoid |
| 152 | AbilityType_SacrificeClue |
| 153 | AbilityType_Escalate |
| 154 | AbilityType_Fabricate |
| 156 | AbilityType_Crew |
| 157 | AbilityType_Improvise |
| 158 | AbilityType_ChooseZoneForMutuallyExclusiveZoneTransfers |
| 159 | AbilityType_Aftermath |
| 160 | AbilityType_Embalm |
| 161 | AbilityType_TapForImprovise |
| 162 | AbilityType_Exert |
| 163 | AbilityType_Eternalize |
| 164 | AbilityType_Afflict |
| 165 | AbilityType_Ascend |
| 166 | AbilityType_SagaChapter |
| 167 | AbilityType_SagaLoreReplacement |
| 168 | AbilityType_SagaLoreTurn |
| 169 | AbilityType_Surveil |
| 170 | AbilityType_Jumpstart |
| 171 | AbilityType_Mentor |
| 172 | AbilityType_TapForConvoke |
| 173 | AbilityType_Afterlife |
| 174 | AbilityType_Spectacle |
| 175 | AbilityType_Riot |
| 176 | AbilityType_Adapt |
| 177 | AbilityType_AffinityForArtifacts |
| 178 | AbilityType_Scry1 |
| 179 | AbilityType_Scry2 |
| 180 | AbilityType_Scry3 |
| 181 | AbilityType_Scry4 |
| 182 | AbilityType_ScryX |
| 183 | AbilityType_SacrificeTreasure |
| 184 | AbilityType_ProtectionFromChosenColor |
| 185 | AbilityType_ProtectionFromW |
| 186 | AbilityType_ProtectionFromU |
| 187 | AbilityType_ProtectionFromB |
| 188 | AbilityType_ProtectionFromR |
| 189 | AbilityType_ProtectionFromG |
| 190 | AbilityType_HexproofFromChosen |
| 191 | AbilityType_HexproofFromW |
| 192 | AbilityType_HexproofFromU |
| 193 | AbilityType_HexproofFromB |
| 194 | AbilityType_HexproofFromR |
| 195 | AbilityType_HexproofFromG |
| 196 | AbilityType_Adventure |
| 197 | AbilityType_SacrificeFood |
| 198 | AbilityType_SacrificeGold |
| 199 | AbilityType_Escape |
| 200 | AbilityType_Fight |
| 201 | AbilityType_UntapOptional |
| 202 | AbilityType_Companion |
| 203 | AbilityType_Mutate |
| 204 | AbilityType_ProtectionFromDogs |
| 205 | AbilityType_Mill |
| 206 | AbilityType_ProtectionFromChosenName |
| 207 | AbilityType_MDFC |
| 208 | AbilityType_Foretell |
| 209 | AbilityType_Boast |
| 210 | AbilityType_SacrificeShard |
| 211 | AbilityType_Ward |
| 212 | AbilityType_Learn |
| 213 | AbilityType_ReboundDelayedTrigger |
| 214 | AbilityType_Decayed |
| 215 | AbilityType_Disturb |
| 216 | AbilityType_Daybound |
| 217 | AbilityType_Nightbound |
| 218 | AbilityType_VentureIntoTheDungeon |
| 219 | AbilityType_RollD20 |
| 220 | AbilityType_Training |
| 221 | AbilityType_Cleave |
| 222 | AbilityType_Exploit |
| 223 | AbilityType_Accept |
| 224 | AbilityType_Conjure |
| 225 | AbilityType_Perpetual |
| 226 | AbilityType_Seek |
| 227 | AbilityType_WinsTheFight |
| 228 | AbilityType_Proliferate |
| 229 | AbilityType_Support2 |
| 230 | AbilityType_Support3 |
| 231 | AbilityType_Support6 |
| 232 | AbilityType_Plainswalk |
| 233 | AbilityType_Islandwalk |
| 234 | AbilityType_Swampwalk |
| 235 | AbilityType_Mountainwalk |
| 236 | AbilityType_Forestwalk |
| 237 | AbilityType_Reconfigure |
| 238 | AbilityType_Compleated |
| 239 | AbilityType_SacrificeBlood |
| 240 | AbilityType_Blitz |
| 241 | AbilityType_Casualty |
| 242 | AbilityType_Draft |
| 243 | AbilityType_Reconfigure_Attach |
| 244 | AbilityType_Reconfigure_Unattach |
| 245 | AbilityType_Connive |
| 246 | AbilityType_ConniveX |
| 247 | AbilityType_BlitzDelayedTrigger |
| 248 | AbilityType_BlitzDeathTrigger |
| 249 | AbilityType_StartingIntensity |
| 250 | AbilityType_AllAbilities |
| 251 | AbilityType_ShieldCounter_PreventDestroy |
| 252 | AbilityType_ShieldCounter_PreventDamage |
| 253 | AbilityType_DoubleTeam |
| 254 | AbilityType_Specialize |
| 255 | AbilityType_PayLifeInsteadOfManaCost |
| 256 | AbilityType_CommanderClearPerpetualReplacement |
| 257 | AbilityType_ProtectionFromDemons |
| 258 | AbilityType_ProtectionFromDevils |
| 259 | AbilityType_PowerstoneMana |
| 260 | AbilityType_ReadAhead |
| 261 | AbilityType_Enlist |
| 262 | AbilityType_StunCounter |
| 263 | AbilityType_Prototype |
| 264 | AbilityType_Toxic |
| 265 | AbilityType_Meld |
| 266 | AbilityType_ProtectionFromEverything |
| 267 | AbilityType_ForMirrodin |
| 268 | AbilityType_AffinityForEquipment |
| 269 | AbilityType_Surveil1 |
| 270 | AbilityType_Surveil2 |
| 271 | AbilityType_Surveil3 |
| 272 | AbilityType_Surveil4 |
| 273 | AbilityType_Surveil5 |
| 274 | AbilityType_Dash |
| 275 | AbilityType_DashDelayedTrigger |
| 276 | AbilityType_BattleDefenseReplacement |
| 277 | AbilityType_BattleProtectorReplacement |
| 278 | AbilityType_SiegeDefeated |
| 279 | AbilityType_TransformIncubator |
| 280 | AbilityType_IncubateX |
| 281 | AbilityType_Incubate1 |
| 282 | AbilityType_Incubate2 |
| 283 | AbilityType_Incubate3 |
| 284 | AbilityType_Incubate4 |
| 285 | AbilityType_Incubate5 |
| 286 | AbilityType_Bolster1 |
| 287 | AbilityType_Backup |
| 288 | AbilityType_ProtectionFromColorless |
| 289 | AbilityType_ProtectionFromColorlessOrColorOfChoice |
| 290 | AbilityType_TheRingTempts |
| 291 | AbilityType_AmassSubType_Zombie1 |
| 292 | AbilityType_AmassSubType_Zombie2 |
| 293 | AbilityType_AmassSubType_Zombie3 |
| 294 | AbilityType_AmassSubType_Zombie4 |
| 295 | AbilityType_AmassSubType_ZombieX |
| 296 | AbilityType_AmassSubType_Orc1 |
| 297 | AbilityType_AmassSubType_Orc2 |
| 298 | AbilityType_AmassSubType_Orc3 |
| 299 | AbilityType_AmassSubType_Orc4 |
| 300 | AbilityType_AmassSubType_OrcX |
| 301 | AbilityType_ProtectionFromChosenType |
| 302 | AbilityType_Goad |
| 303 | AbilityType_Bargain |
| 304 | AbilityType_SacrificeMap |
| 305 | AbilityType_CraftWith |
| 306 | AbilityType_FinalityCounter |
| 307 | AbilityType_Disguise |
| 308 | AbilityType_Discover |
| 309 | AbilityType_Discover3 |
| 310 | AbilityType_Discover4 |
| 311 | AbilityType_Discover5 |
| 312 | AbilityType_Discover10 |
| 313 | AbilityType_DiscoverX |
| 314 | AbilityType_Cloak |
| 315 | AbilityType_ExileForDelve |
| 316 | AbilityType_CastSuspended |
| 317 | AbilityType_Suspect |
| 319 | AbilityType_CollectEvidence |
| 320 | AbilityType_CollectEvidence2 |
| 321 | AbilityType_CollectEvidence3 |
| 322 | AbilityType_CollectEvidence4 |
| 323 | AbilityType_CollectEvidence6 |
| 324 | AbilityType_CollectEvidence8 |
| 325 | AbilityType_CollectEvidence10 |
| 326 | AbilityType_CollectEvidenceX |
| 327 | AbilityType_CollectEvidence5 |
| 328 | AbilityType_Plot |
| 329 | AbilityType_Saddle |
| 330 | AbilityType_Spree |
| 331 | AbilityType_Incorporate |
| 332 | AbilityType_CommitACrime |
| 333 | AbilityType_AffinityForOutlaws |
| 334 | AbilityType_Investigate |
| 335 | AbilityType_Bestow |
| 336 | AbilityType_Populate |
| 337 | AbilityType_AffinityForAuras |
| 338 | AbilityType_Heist |
| 339 | AbilityType_ProtectionFromInstants |
| 340 | AbilityType_ProtectionFromSorceries |
| 341 | AbilityType_Offspring |
| 342 | AbilityType_Gift |
| 343 | AbilityType_HexproofFromActivatedAndTriggeredAbilities |
| 344 | AbilityType_Expend |
| 345 | AbilityType_Forage |
| 346 | AbilityType_Partner |
| 347 | AbilityType_ApplyLeftRoomLocked |
| 348 | AbilityType_ApplyRightRoomLocked |
| 349 | AbilityType_UnlockLeftRoom |
| 350 | AbilityType_UnlockRightRoom |
| 351 | AbilityType_ManifestDread |
| 352 | AbilityType_Impending |
| 353 | AbilityType_Exhaust |
| 354 | AbilityType_StartYourEngines |
| 355 | AbilityType_SpeedImplicitTrigger |
| 356 | AbilityType_Surge |
| 357 | AbilityType_Detain |
| 358 | AbilityType_Renown |
| 359 | AbilityType_Awaken |
| 360 | AbilityType_Behold |
| 361 | AbilityType_Endure |
| 362 | AbilityType_Harmonize |
| 363 | AbilityType_Mobilize |
| 364 | AbilityType_JobSelect |
| 365 | AbilityType_Tiered |
| 366 | AbilityType_Omen |
| 367 | AbilityType_ProtectionFromEachColor |
| 368 | AbilityType_ProtectionFromEachOpponent |
| 369 | AbilityType_SacrificeLander |
| 370 | AbilityType_CommanderNinjutsu |
| 371 | AbilityType_Warp |
| 372 | AbilityType_WarpDelayedTrigger |
| 373 | AbilityType_Station |
| 374 | AbilityType_StationLevel |
| 375 | AbilityType_Melee |
| 376 | AbilityType_MaxSpeed |
| 377 | AbilityType_Airbend |
| 378 | AbilityType_Earthbend |
| 379 | AbilityType_Firebending |
| 380 | AbilityType_Waterbend |
| 381 | AbilityType_ProtectionFromSpellsThatAreOneOrMoreColors |
| 382 | AbilityType_Webslinging |
| 383 | AbilityType_Mayhem |
| 384 | AbilityType_TapForWaterbend |
| 385 | AbilityType_EarthbendTrigger |
| 386 | AbilityType_PayWaterbendInsteadOfManaCost |
| 387 | AbilityType_Placeholder387 |
| 388 | AbilityType_Harness |
| 389 | AbilityType_ChooseABackground |
| 390 | AbilityType_RollDice |
| 391 | AbilityType_Blight |
| 392 | AbilityType_SacrificeMutagen |
| 393 | AbilityType_CharacterSelect |
| 394 | AbilityType_Sneak |
| 395 | AbilityType_PartnerWith |
| 396 | AbilityType_AffinityForGoats |
| 397 | AbilityType_AmassSubType_Goblin1 |
| 398 | AbilityType_AmassSubType_Goblin2 |
| 399 | AbilityType_AmassSubType_Goblin3 |
| 400 | AbilityType_AmassSubType_Goblin4 |
| 401 | AbilityType_AmassSubType_GoblinX |
| 402 | AbilityType_AmassSubType_Orc5 |
| 403 | AbilityType_HexproofFromEachOfItsColors |
| 404 | AbilityType_Increment |
| 405 | AbilityType_Paradigm |
| 406 | AbilityType_Myriad |
| 407 | AbilityType_Recruit |
| 408 | AbilityType_Storied |
| 409 | AbilityType_Discover6 |
| 410 | AbilityType_PowerUp |
| 411 | AbilityType_Prepare |
| 412 | AbilityType_Teamwork |
| 413 | AbilityType_Intensity |
| 414 | AbilityType_Placeholder414 |
| 415 | AbilityType_Placeholder415 |
| 416 | AbilityType_Placeholder416 |
| 417 | AbilityType_Placeholder417 |
| 418 | AbilityType_Placeholder418 |
| 419 | AbilityType_EmpowerJace |
| 420 | AbilityType_Placeholder420 |
| 421 | AbilityType_Placeholder421 |
| 422 | AbilityType_Placeholder422 |
| 423 | AbilityType_Placeholder423 |
| 424 | AbilityType_Placeholder424 |
| 425 | AbilityType_Placeholder425 |
| 427 | AbilityType_Placeholder427 |
| 428 | AbilityType_Placeholder428 |
| 429 | AbilityType_Placeholder429 |
| 430 | AbilityType_Placeholder430 |
| 431 | AbilityType_Placeholder431 |
| 432 | AbilityType_Placeholder432 |
| 999 | AbilityType_AddWUBRG_Test |
| 998 | AbilityType_GainHaste_Test |
| 997 | AbilityType_PutLibrary_Test |
| 996 | AbilityType_Wish_Test |
| 995 | AbilityType_EndTurn_Test |
| 994 | AbilityType_DeployBears_Test |
| 993 | AbilityType_AddCost_Test |
| 992 | AbilityType_DrawCard_Test |
| 991 | AbilityType_Bounce_Test |
| 990 | AbilityType_Donate_Test |
| 989 | AbilityType_Twiddle_Test |
| 987 | AbilityType_AdornCards_Test |
| 986 | AbilityType_AddDieRoll_Test |
| 985 | AbilityType_AddRandomCard_Test |
| 1001 | AbilityType_IntrinsicMana_W |
| 1002 | AbilityType_IntrinsicMana_U |
| 1003 | AbilityType_IntrinsicMana_B |
| 1004 | AbilityType_IntrinsicMana_R |
| 1005 | AbilityType_IntrinsicMana_G |
| 1006 | AbilityType_AutoGen |
| 149387 | AbilityType_Casualty2 |
| 141939 | AbilityType_Ward2 |

### wotc.mtgo.gre.external.messaging.AbilityWord

| Value | Name |
|---:|---|
| 0 | AbilityWord_None |
| 1 | AbilityWord_Battalion |
| 2 | AbilityWord_Bloodrush |
| 3 | AbilityWord_Channel |
| 4 | AbilityWord_Chroma |
| 5 | AbilityWord_Cohort |
| 6 | AbilityWord_Constellation |
| 7 | AbilityWord_Converge |
| 8 | AbilityWord_CouncilsDilemma |
| 9 | AbilityWord_Delirium |
| 10 | AbilityWord_Domain |
| 11 | AbilityWord_FatefulHour |
| 12 | AbilityWord_Ferocious |
| 13 | AbilityWord_Formidable |
| 14 | AbilityWord_Grandeur |
| 15 | AbilityWord_Hellbent |
| 16 | AbilityWord_Heroic |
| 17 | AbilityWord_Imprint |
| 18 | AbilityWord_Inspired |
| 19 | AbilityWord_JoinForces |
| 20 | AbilityWord_Kinship |
| 21 | AbilityWord_Landfall |
| 22 | AbilityWord_Lieutenant |
| 23 | AbilityWord_Metalcraft |
| 24 | AbilityWord_Morbid |
| 25 | AbilityWord_Parley |
| 26 | AbilityWord_Radiance |
| 27 | AbilityWord_Raid |
| 28 | AbilityWord_Rally |
| 29 | AbilityWord_SpellMastery |
| 30 | AbilityWord_Strive |
| 31 | AbilityWord_Sweep |
| 32 | AbilityWord_TemptingOffer |
| 33 | AbilityWord_Threshold |
| 34 | AbilityWord_WillOfTheCouncil |
| 35 | AbilityWord_Revolt |
| 36 | AbilityWord_Enrage |
| 37 | AbilityWord_Historic |
| 38 | AbilityWord_Undergrowth |
| 39 | AbilityWord_Addendum |
| 40 | AbilityWord_Adamant |
| 41 | AbilityWord_Magecraft |
| 42 | AbilityWord_Coven |
| 43 | AbilityWord_Alliance |
| 44 | AbilityWord_Corrupted |
| 45 | AbilityWord_Celebration |
| 46 | AbilityWord_Descend |
| 47 | AbilityWord_Valiant |
| 48 | AbilityWord_Eerie |
| 49 | AbilityWord_Survival |
| 50 | AbilityWord_Flurry |
| 51 | AbilityWord_Renew |
| 52 | AbilityWord_Void |
| 53 | AbilityWord_Disappear |
| 54 | AbilityWord_Vivid |
| 55 | AbilityWord_Repartee |
| 56 | AbilityWord_Opus |
| 57 | AbilityWord_Infusion |
| 58 | AbilityWord_Placeholder58 |

### wotc.mtgo.gre.external.messaging.AchievementParameterType

| Value | Name |
|---:|---|
| 0 | AchievementParameterType_None |
| 1 | AchievementParameterType_CardName |
| 2 | AchievementParameterType_AbilityGrpId |
| 3 | AchievementParameterType_Color |
| 4 | AchievementParameterType_CardType |
| 5 | AchievementParameterType_Subtype |
| 6 | AchievementParameterType_Supertype |
| 7 | AchievementParameterType_AbilityType |

### wotc.mtgo.gre.external.messaging.ActionDisqualificationReason

| Value | Name |
|---:|---|
| 0 | ActionDisqualificationReason_None |
| 1 | ActionDisqualificationReason_Timing |
| 2 | ActionDisqualificationReason_Limit |
| 3 | ActionDisqualificationReason_Ability |
| 4 | ActionDisqualificationReason_LegendarySorcery |
| 5 | ActionDisqualificationReason_InsufficientTargets |
| 6 | ActionDisqualificationReason_UnpayableNonmanaCost |
| 7 | ActionDisqualificationReason_UnpayableManaCost |

### wotc.mtgo.gre.external.messaging.ActionType

| Value | Name |
|---:|---|
| 0 | ActionType_None |
| 1 | ActionType_Cast |
| 2 | ActionType_Activate |
| 3 | ActionType_Play |
| 4 | ActionType_Activate_Mana |
| 5 | ActionType_Pass |
| 6 | ActionType_Activate_Test |
| 7 | ActionType_Special |
| 8 | ActionType_Special_TurnFaceUp |
| 9 | ActionType_ResolutionCost |
| 10 | ActionType_CastLeft |
| 11 | ActionType_CastRight |
| 12 | ActionType_Make_Payment |
| 14 | ActionType_CombatCost |
| 15 | ActionType_OpeningHandAction |
| 16 | ActionType_CastAdventure |
| 17 | ActionType_FloatMana |
| 18 | ActionType_CastMDFC |
| 19 | ActionType_PlayMDFC |
| 20 | ActionType_Special_Payment |
| 21 | ActionType_CastPrototype |
| 22 | ActionType_CastLeftRoom |
| 23 | ActionType_CastRightRoom |
| 24 | ActionType_CastOmen |
| 25 | ActionType_Placeholder25 |

### wotc.mtgo.gre.external.messaging.AllowFailToFind

| Value | Name |
|---:|---|
| 0 | AllowFailToFind_None |
| 1 | AllowFailToFind_Any |
| 2 | AllowFailToFind_Zero |

### wotc.mtgo.gre.external.messaging.Answer

| Value | Name |
|---:|---|
| 0 | Answer_None |
| 1 | Answer_Yes |
| 2 | Answer_No |

### wotc.mtgo.gre.external.messaging.AttackState

| Value | Name |
|---:|---|
| 0 | AttackState_None |
| 1 | AttackState_Declared |
| 2 | AttackState_Attacking |

### wotc.mtgo.gre.external.messaging.AttackWarningType

| Value | Name |
|---:|---|
| 0 | AttackWarningType_None |
| 1 | AttackWarningType_MustAttackWithAtLeastOne |
| 2 | AttackWarningType_CannotAttackAlone |
| 3 | AttackWarningType_MustAttack |
| 4 | AttackWarningType_UnpayableEffectCost |
| 5 | AttackWarningType_CannotBeAttackedByMoreThanOne |

### wotc.mtgo.gre.external.messaging.AutoPassOption

| Value | Name |
|---:|---|
| 0 | AutoPassOption_None |
| 1 | AutoPassOption_Turn |
| 2 | AutoPassOption_UnlessAction |
| 3 | AutoPassOption_EndStep |
| 4 | AutoPassOption_Clear |
| 5 | AutoPassOption_UnlessOpponentAction |
| 6 | AutoPassOption_ResolveMyStackEffects |
| 7 | AutoPassOption_FullControl |
| 9 | AutoPassOption_ResolveAll |

### wotc.mtgo.gre.external.messaging.AutoPassPriority

| Value | Name |
|---:|---|
| 0 | AutoPassPriority_None |
| 1 | AutoPassPriority_No |
| 2 | AutoPassPriority_Yes |

### wotc.mtgo.gre.external.messaging.AutoTapStopsSetting

| Value | Name |
|---:|---|
| 0 | AutoTapStopsSetting_None |
| 1 | AutoTapStopsSetting_Enable |
| 2 | AutoTapStopsSetting_Disable |

### wotc.mtgo.gre.external.messaging.BasicLandType

| Value | Name |
|---:|---|
| 0 | BasicLandType_None |
| 1 | BasicLandType_Plains |
| 2 | BasicLandType_Island |
| 3 | BasicLandType_Swamp |
| 4 | BasicLandType_Mountain |
| 5 | BasicLandType_Forest |

### wotc.mtgo.gre.external.messaging.BlockState

| Value | Name |
|---:|---|
| 0 | BlockState_None |
| 1 | BlockState_Declared |
| 2 | BlockState_Blocking |
| 3 | BlockState_Blocked |
| 4 | BlockState_Unblocked |

### wotc.mtgo.gre.external.messaging.BlockWarningType

| Value | Name |
|---:|---|
| 0 | BlockWarningType_None |
| 1 | BlockWarningType_InsufficientBlockers |
| 2 | BlockWarningType_CannotBlockAlone |
| 3 | BlockWarningType_MustBeBlocked |
| 4 | BlockWarningType_MustBlock |
| 5 | BlockWarningType_MustBeBlockedByAll |

### wotc.mtgo.gre.external.messaging.CardColor

| Value | Name |
|---:|---|
| 0 | CardColor_Colorless |
| 1 | CardColor_White |
| 2 | CardColor_Blue |
| 3 | CardColor_Black |
| 4 | CardColor_Red |
| 5 | CardColor_Green |
| 6 | CardColor_Land |
| 7 | CardColor_Artifact |

### wotc.mtgo.gre.external.messaging.CardMechanicType

| Value | Name |
|---:|---|
| 0 | CardMechanicType_None |
| 2 | CardMechanicType_AddAbility |
| 3 | CardMechanicType_AddAttachment |
| 4 | CardMechanicType_DealDamage |
| 5 | CardMechanicType_DestroyPermanent |
| 6 | CardMechanicType_Discard |
| 7 | CardMechanicType_DrawCard |
| 8 | CardMechanicType_LossOfGame |
| 9 | CardMechanicType_SetColor |
| 10 | CardMechanicType_PayLife |
| 11 | CardMechanicType_ModifyPower |
| 12 | CardMechanicType_ModifyToughness |
| 13 | CardMechanicType_PhaseOrStepTransition |
| 14 | CardMechanicType_PutCounterOnObject |
| 15 | CardMechanicType_CreateToken |
| 16 | CardMechanicType_RemoveCounterFromObject |
| 17 | CardMechanicType_ResolveAbility |
| 18 | CardMechanicType_Reveal |
| 19 | CardMechanicType_TapPermanent |
| 21 | CardMechanicType_UntapPermanent |
| 22 | CardMechanicType_ZoneTransfer |
| 23 | CardMechanicType_RegeneratePermanent |
| 24 | CardMechanicType_SacrificePermanent |
| 25 | CardMechanicType_SetController |
| 26 | CardMechanicType_DeclaredAttacker |
| 27 | CardMechanicType_DeclaredBlocker |
| 28 | CardMechanicType_BecomeTarget |
| 29 | CardMechanicType_AddToManaPool |
| 30 | CardMechanicType_EnterZone |
| 31 | CardMechanicType_ShuffleLibrary |
| 32 | CardMechanicType_CounterObjectOnStack |
| 33 | CardMechanicType_ExileGameObject |
| 34 | CardMechanicType_DamageDealt |
| 35 | CardMechanicType_PhaseInPermanent |
| 36 | CardMechanicType_PhaseOutPermanent |
| 37 | CardMechanicType_AttackerBlocked |
| 38 | CardMechanicType_AttackersSubmitted |
| 39 | CardMechanicType_BlockersDetermined |
| 40 | CardMechanicType_SetPower |
| 41 | CardMechanicType_SetToughness |
| 42 | CardMechanicType_BeginTurn |
| 43 | CardMechanicType_TurnFaceUp |
| 44 | CardMechanicType_Transform |
| 45 | CardMechanicType_Dredge |
| 46 | CardMechanicType_Explore |
| 47 | CardMechanicType_Provoke |
| 48 | CardMechanicType_Riot |
| 49 | CardMechanicType_Mutate |
| 50 | CardMechanicType_GraveyardExile |
| 51 | CardMechanicType_Exert |
| 52 | CardMechanicType_PutTopOrBottom |
| 53 | CardMechanicType_Forage |
| 54 | CardMechanicType_Webslinging |
| 55 | CardMechanicType_Sneak |
| 56 | CardMechanicType_Placeholder56 |
| 57 | CardMechanicType_PhaseOrStepTransition_SkipNext |

### wotc.mtgo.gre.external.messaging.CardType

| Value | Name |
|---:|---|
| 0 | CardType_None |
| 1 | CardType_Artifact |
| 2 | CardType_Creature |
| 3 | CardType_Enchantment |
| 4 | CardType_Instant |
| 5 | CardType_Land |
| 6 | CardType_Phenomenon |
| 7 | CardType_Plane |
| 8 | CardType_Planeswalker |
| 9 | CardType_Scheme |
| 10 | CardType_Sorcery |
| 11 | CardType_Kindred |
| 12 | CardType_Vanguard |
| 13 | CardType_Dungeon |
| 14 | CardType_Battle |
| 15 | CardType_Conspiracy |

### wotc.mtgo.gre.external.messaging.CastingTimeOptionType

| Value | Name |
|---:|---|
| 0 | CastingTimeOptionType_None |
| 1 | CastingTimeOptionType_Done |
| 2 | CastingTimeOptionType_ChooseX |
| 3 | CastingTimeOptionType_Kicker |
| 4 | CastingTimeOptionType_Multikicker |
| 5 | CastingTimeOptionType_AdditionalCost |
| 6 | CastingTimeOptionType_OptionalCost |
| 7 | CastingTimeOptionType_Replicate |
| 8 | CastingTimeOptionType_Conspire |
| 9 | CastingTimeOptionType_ManaType |
| 10 | CastingTimeOptionType_Modal |
| 11 | CastingTimeOptionType_ChooseOrCost |
| 12 | CastingTimeOptionType_Selection |
| 13 | CastingTimeOptionType_CastThroughAbility |
| 14 | CastingTimeOptionType_TimingPermission |
| 15 | CastingTimeOptionType_Casualty |
| 16 | CastingTimeOptionType_Specialize |
| 17 | CastingTimeOptionType_Bargain |
| 18 | CastingTimeOptionType_OptionalCostPaid |

### wotc.mtgo.gre.external.messaging.ChoicePersistence

| Value | Name |
|---:|---|
| 0 | ChoicePersistence_None |
| 1 | ChoicePersistence_ChooseOnce |
| 2 | ChoicePersistence_ChooseAlways |

### wotc.mtgo.gre.external.messaging.ClientMessageType

| Value | Name |
|---:|---|
| 0 | ClientMessageType_None |
| 1 | ClientMessageType_ConnectReq |
| 5 | ClientMessageType_CancelActionReq |
| 7 | ClientMessageType_ConcedeReq |
| 8 | ClientMessageType_EnterSideboardingReq |
| 9 | ClientMessageType_ForceDrawReq |
| 10 | ClientMessageType_CheckpointReq |
| 11 | ClientMessageType_GetSettingsReq |
| 12 | ClientMessageType_GroupResp |
| 13 | ClientMessageType_MulliganResp |
| 14 | ClientMessageType_OrderResp |
| 15 | ClientMessageType_PerformActionResp |
| 17 | ClientMessageType_ControlReq |
| 18 | ClientMessageType_SelectNResp |
| 20 | ClientMessageType_SetSettingsReq |
| 22 | ClientMessageType_UndoReq |
| 24 | ClientMessageType_ChooseStartingPlayerResp |
| 25 | ClientMessageType_OptionalActionResp |
| 26 | ClientMessageType_AllowForceDrawResp |
| 28 | ClientMessageType_RevealHandResp |
| 30 | ClientMessageType_DeclareAttackersResp |
| 31 | ClientMessageType_SubmitAttackersReq |
| 32 | ClientMessageType_DeclareBlockersResp |
| 33 | ClientMessageType_SubmitBlockersReq |
| 34 | ClientMessageType_OrderCombatDamageResp |
| 35 | ClientMessageType_AssignDamageResp |
| 36 | ClientMessageType_SelectTargetsResp |
| 37 | ClientMessageType_SubmitTargetsReq |
| 38 | ClientMessageType_DrawCardResp |
| 39 | ClientMessageType_SelectReplacementResp |
| 40 | ClientMessageType_SelectNGroupResp |
| 42 | ClientMessageType_DistributionResp |
| 43 | ClientMessageType_NumericInputResp |
| 44 | ClientMessageType_SearchResp |
| 45 | ClientMessageType_EffectCostResp |
| 46 | ClientMessageType_CastingTimeOptionsResp |
| 48 | ClientMessageType_SelectFromGroupsResp |
| 49 | ClientMessageType_SearchFromGroupsResp |
| 50 | ClientMessageType_GatherResp |
| 52 | ClientMessageType_AutoResp |
| 53 | ClientMessageType_UIMessage |
| 54 | ClientMessageType_SubmitDeckResp |
| 55 | ClientMessageType_TakeTimeoutReq |
| 56 | ClientMessageType_PerformAutoTapActionsResp |
| 57 | ClientMessageType_StringInputResp |
| 58 | ClientMessageType_SelectCountersResp |
| 59 | ClientMessageType_PredictionReq |

### wotc.mtgo.gre.external.messaging.ClientToMatchServiceMessageType

| Value | Name |
|---:|---|
| 0 | ClientToMatchServiceMessageType_None |
| 1 | ClientToMatchServiceMessageType_ClientToMatchDoorConnectRequest |
| 2 | ClientToMatchServiceMessageType_ClientToGREMessage |
| 3 | ClientToMatchServiceMessageType_ClientToGREUIMessage |
| 4 | ClientToMatchServiceMessageType_AuthenticateRequest |
| 5 | ClientToMatchServiceMessageType_CreateMatchGameRoomRequest |
| 8 | ClientToMatchServiceMessageType_EchoRequest |

### wotc.mtgo.gre.external.messaging.ClientType

| Value | Name |
|---:|---|
| 0 | ClientType_Invalid |
| 1 | ClientType_User |
| 2 | ClientType_Service |
| 3 | ClientType_Familiar |

### wotc.mtgo.gre.external.messaging.CoinFace

| Value | Name |
|---:|---|
| 0 | CoinFace_None |
| 1 | CoinFace_Heads |
| 2 | CoinFace_Tails |

### wotc.mtgo.gre.external.messaging.Color

| Value | Name |
|---:|---|
| 0 | Color_None |
| 1 | Color_White |
| 2 | Color_Blue |
| 3 | Color_Black |
| 4 | Color_Red |
| 5 | Color_Green |

### wotc.mtgo.gre.external.messaging.ConjurationType

| Value | Name |
|---:|---|
| 0 | ConjurationType_None |
| 1 | ConjurationType_Spellbook |
| 2 | ConjurationType_NamedCard |

### wotc.mtgo.gre.external.messaging.ConnectionState

| Value | Name |
|---:|---|
| 0 | ConnectionState_Invalid |
| 1 | ConnectionState_Open |
| 2 | ConnectionState_Closed |
| 3 | ConnectionState_Connecting |
| 4 | ConnectionState_Closing |

### wotc.mtgo.gre.external.messaging.ConnectionStatus

| Value | Name |
|---:|---|
| 0 | ConnectionStatus_None |
| 1 | ConnectionStatus_Success |
| 2 | ConnectionStatus_GRPVersionIncompat |
| 3 | ConnectionStatus_ProtoVersionIncompat |
| 4 | ConnectionStatus_InvalidSeatId |

### wotc.mtgo.gre.external.messaging.ControllerType

| Value | Name |
|---:|---|
| 0 | ControllerType_None |
| 1 | ControllerType_Player |
| 2 | ControllerType_AI |
| 3 | ControllerType_AI_Goldfish |
| 4 | ControllerType_AI_PetRock |
| 5 | ControllerType_AI_MultiplayerConcession |

### wotc.mtgo.gre.external.messaging.CostCategory

| Value | Name |
|---:|---|
| 0 | CostCategory_None |
| 1 | CostCategory_Automatic |
| 2 | CostCategory_Confirmation |
| 3 | CostCategory_Manual |

### wotc.mtgo.gre.external.messaging.CostType

| Value | Name |
|---:|---|
| 0 | CostType_None |
| 1 | CostType_Mana |
| 2 | CostType_TapSelf |
| 3 | CostType_SacSelf |
| 4 | CostType_Effect |
| 5 | CostType_Loyalty |
| 6 | CostType_DiscardSelf |
| 7 | CostType_Life |
| 8 | CostType_ExileSelf |
| 9 | CostType_UntapSelf |
| 10 | CostType_Or |
| 11 | CostType_And |

### wotc.mtgo.gre.external.messaging.CounterType

| Value | Name |
|---:|---|
| 0 | CounterType_None |
| 1 | CounterType_P1P1 |
| 2 | CounterType_M1M1 |
| 3 | CounterType_Poison |
| 4 | CounterType_Wind |
| 5 | CounterType_Time |
| 6 | CounterType_Fade |
| 7 | CounterType_Loyalty |
| 8 | CounterType_Wish |
| 9 | CounterType_Age |
| 10 | CounterType_Aim |
| 11 | CounterType_Arrow |
| 12 | CounterType_Arrowhead |
| 13 | CounterType_Awakening |
| 14 | CounterType_Blaze |
| 15 | CounterType_Blood |
| 16 | CounterType_Bounty |
| 17 | CounterType_Bribery |
| 18 | CounterType_Carrion |
| 19 | CounterType_Charge |
| 20 | CounterType_Control |
| 21 | CounterType_Corpse |
| 22 | CounterType_Credit |
| 23 | CounterType_Cube |
| 24 | CounterType_Currency |
| 25 | CounterType_Death |
| 26 | CounterType_Delay |
| 27 | CounterType_Depletion |
| 28 | CounterType_Despair |
| 29 | CounterType_Devotion |
| 30 | CounterType_Divinity |
| 31 | CounterType_Doom |
| 32 | CounterType_Dream |
| 33 | CounterType_Echo |
| 34 | CounterType_Elixir |
| 35 | CounterType_Energy |
| 36 | CounterType_Eon |
| 37 | CounterType_Eyeball |
| 38 | CounterType_Fate |
| 39 | CounterType_Feather |
| 40 | CounterType_Filibuster |
| 41 | CounterType_Flame |
| 42 | CounterType_Flood |
| 43 | CounterType_Fungus |
| 44 | CounterType_Fuse |
| 45 | CounterType_Glyph |
| 46 | CounterType_Gold |
| 47 | CounterType_Growth |
| 48 | CounterType_Hatchling |
| 49 | CounterType_Healing |
| 50 | CounterType_Hoofprint |
| 51 | CounterType_Hourglass |
| 52 | CounterType_Hunger |
| 53 | CounterType_Ice |
| 54 | CounterType_Infection |
| 55 | CounterType_Intervention |
| 56 | CounterType_Javelin |
| 57 | CounterType_Ki |
| 58 | CounterType_Level |
| 59 | CounterType_Luck |
| 60 | CounterType_Magnet |
| 61 | CounterType_Mannequin |
| 62 | CounterType_Matrix |
| 63 | CounterType_May |
| 64 | CounterType_Mine |
| 65 | CounterType_Mining |
| 66 | CounterType_Mire |
| 67 | CounterType_Muster |
| 68 | CounterType_Net |
| 69 | CounterType_Omen |
| 70 | CounterType_Ore |
| 71 | CounterType_Page |
| 72 | CounterType_Pain |
| 73 | CounterType_Paralyzation |
| 74 | CounterType_Petal |
| 75 | CounterType_Petrification |
| 76 | CounterType_Phylactery |
| 77 | CounterType_Pin |
| 78 | CounterType_Plague |
| 79 | CounterType_Polyp |
| 80 | CounterType_Pressure |
| 81 | CounterType_Pupa |
| 82 | CounterType_Quest |
| 83 | CounterType_Scream |
| 84 | CounterType_Scroll |
| 85 | CounterType_Shell |
| 86 | CounterType_Shield |
| 87 | CounterType_Shred |
| 88 | CounterType_Sleep |
| 89 | CounterType_Sleight |
| 90 | CounterType_Slime |
| 91 | CounterType_Soot |
| 92 | CounterType_Spell |
| 93 | CounterType_Spore |
| 94 | CounterType_Storage |
| 95 | CounterType_Strife |
| 96 | CounterType_Study |
| 97 | CounterType_Theft |
| 98 | CounterType_Tide |
| 100 | CounterType_Tower |
| 101 | CounterType_Training |
| 102 | CounterType_Trap |
| 103 | CounterType_Treasure |
| 104 | CounterType_Verse |
| 105 | CounterType_Vitality |
| 106 | CounterType_Wage |
| 107 | CounterType_Winch |
| 108 | CounterType_Lore |
| 109 | CounterType_P1P2 |
| 110 | CounterType_P0P1 |
| 111 | CounterType_P0P2 |
| 112 | CounterType_P1P0 |
| 113 | CounterType_P2P2 |
| 114 | CounterType_M0M1 |
| 115 | CounterType_M0M2 |
| 116 | CounterType_M1M0 |
| 117 | CounterType_M2M1 |
| 118 | CounterType_M2M2 |
| 119 | CounterType_Manifestation |
| 120 | CounterType_Gem |
| 121 | CounterType_Crystal |
| 122 | CounterType_Isolation |
| 123 | CounterType_Hour |
| 124 | CounterType_Unity |
| 125 | CounterType_Velocity |
| 126 | CounterType_Brick |
| 127 | CounterType_Landmark |
| 128 | CounterType_Prey |
| 129 | CounterType_Silver |
| 130 | CounterType_Egg |
| 131 | CounterType_Hit |
| 132 | CounterType_Knowledge |
| 133 | CounterType_Task |
| 134 | CounterType_Coin |
| 135 | CounterType_Deathtouch |
| 136 | CounterType_FirstStrike |
| 137 | CounterType_Flying |
| 138 | CounterType_Hexproof |
| 139 | CounterType_Lifelink |
| 140 | CounterType_Menace |
| 141 | CounterType_Reach |
| 142 | CounterType_Trample |
| 143 | CounterType_Vigilance |
| 144 | CounterType_Foreshadow |
| 145 | CounterType_Incarnation |
| 146 | CounterType_Soul |
| 147 | CounterType_Voyage |
| 148 | CounterType_Ghostform |
| 149 | CounterType_Night |
| 150 | CounterType_Indestructible |
| 151 | CounterType_Hone |
| 152 | CounterType_Book |
| 153 | CounterType_Point |
| 154 | CounterType_Enlightened |
| 155 | CounterType_Harmony |
| 156 | CounterType_Void |
| 157 | CounterType_Ember |
| 158 | CounterType_Ritual |
| 159 | CounterType_Valor |
| 160 | CounterType_Judgment |
| 161 | CounterType_Invitation |
| 162 | CounterType_Croak |
| 163 | CounterType_Bloodline |
| 164 | CounterType_Suspect |
| 165 | CounterType_Acorn |
| 166 | CounterType_Hatching |
| 167 | CounterType_Stash |
| 168 | CounterType_Collection |
| 169 | CounterType_Rope |
| 170 | CounterType_Ingenuity |
| 171 | CounterType_Phyresis |
| 172 | CounterType_Stun |
| 173 | CounterType_Oil |
| 174 | CounterType_Defense |
| 175 | CounterType_Experience |
| 176 | CounterType_Slumber |
| 177 | CounterType_Rejection |
| 178 | CounterType_Burden |
| 179 | CounterType_Hope |
| 180 | CounterType_Influence |
| 181 | CounterType_Skewer |
| 182 | CounterType_Bore |
| 183 | CounterType_Chorus |
| 184 | CounterType_Dread |
| 185 | CounterType_Finality |
| 186 | CounterType_Bloodstain |
| 187 | CounterType_Impostor |
| 188 | CounterType_Unlock |
| 189 | CounterType_Fetch |
| 190 | CounterType_Loot |
| 191 | CounterType_Exalted |
| 192 | CounterType_Story |
| 193 | CounterType_Everything |
| 194 | CounterType_Blight |
| 195 | CounterType_Supply |
| 196 | CounterType_Feeding |
| 197 | CounterType_Nest |
| 198 | CounterType_Possession |
| 199 | CounterType_Rev |
| 200 | CounterType_Incubation |
| 201 | CounterType_Revival |
| 202 | CounterType_Fellowship |
| 203 | CounterType_Bait |
| 204 | CounterType_Wreck |
| 205 | CounterType_Decayed |
| 206 | CounterType_Conqueror |
| 207 | CounterType_Fire |
| 208 | CounterType_Film |
| 209 | CounterType_DoubleStrike |
| 210 | CounterType_Invasion |
| 211 | CounterType_Plan |
| 212 | CounterType_Saurian |
| 213 | CounterType_PHCT213 |
| 214 | CounterType_PHCT214 |
| 215 | CounterType_Haste |
| 216 | CounterType_Shadow |
| 217 | CounterType_Refine |
| 218 | CounterType_PHCT218 |
| 219 | CounterType_PHCT219 |
| 220 | CounterType_PHCT220 |

### wotc.mtgo.gre.external.messaging.DamageRecType

| Value | Name |
|---:|---|
| 0 | DamageRecType_None |
| 1 | DamageRecType_Team |
| 2 | DamageRecType_Player |
| 3 | DamageRecType_PlanesWalker |

### wotc.mtgo.gre.external.messaging.DamageType

| Value | Name |
|---:|---|
| 0 | DamageType_None |
| 1 | DamageType_Combat |
| 2 | DamageType_Direct |
| 3 | DamageType_Fight |

### wotc.mtgo.gre.external.messaging.Designation

| Value | Name |
|---:|---|
| 0 | Designation_None |
| 1 | Designation_Commander |
| 2 | Designation_Monarch |
| 3 | Designation_Monstrous |
| 4 | Designation_PlanarController |
| 5 | Designation_Renowned |
| 6 | Designation_CitysBlessing |
| 7 | Designation_Companion |
| 8 | Designation_ClassLevel |
| 9 | Designation_PerpetualLayeredEffect |
| 10 | Designation_Day |
| 11 | Designation_Night |
| 12 | Designation_Intensity |
| 13 | Designation_Protector |
| 14 | Designation_Ringbearer |
| 15 | Designation_Solved |
| 16 | Designation_Suspected |
| 17 | Designation_Saddled |
| 18 | Designation_Plotted |
| 19 | Designation_LeftUnlocked |
| 20 | Designation_RightUnlocked |
| 21 | Designation_PlayerSpeed |
| 22 | Designation_Harnessed |
| 23 | Designation_Earthbent |
| 24 | Designation_Prepared |
| 25 | Designation_EnduringStory |
| 26 | Designation_Conjured |
| 27 | Designation_Goaded |
| 28 | Designation_Placeholder28 |

### wotc.mtgo.gre.external.messaging.DetermineOptionsState

| Value | Name |
|---:|---|
| 0 | DetermineOptionsState_None |
| 1 | DetermineOptionsState_PreModal |
| 2 | DetermineOptionsState_Modal |
| 3 | DetermineOptionsState_TargetLimiting |
| 4 | DetermineOptionsState_NonMana |
| 5 | DetermineOptionsState_Mana |

### wotc.mtgo.gre.external.messaging.EffectCostType

| Value | Name |
|---:|---|
| 0 | EffectCostType_None |
| 1 | EffectCostType_Select |
| 2 | EffectCostType_SelectCounter |
| 3 | EffectCostType_Autoselect |
| 4 | EffectCostType_SelectFromGroups |
| 5 | EffectCostType_GatherCounters |

### wotc.mtgo.gre.external.messaging.FailureReason

| Value | Name |
|---:|---|
| 0 | FailureReason_None |
| 1 | FailureReason_Expired |
| 2 | FailureReason_OutOfTurn |
| 3 | FailureReason_ReqRespMismatch |
| 4 | FailureReason_ActionNotBatchable |
| 5 | FailureReason_ActionInvalid |
| 6 | FailureReason_IncompleteMessage |
| 7 | FailureReason_IllegalOption |
| 8 | FailureReason_UnrecognizedValue |
| 9 | FailureReason_TargetsUnavailable |
| 10 | FailureReason_TargetIndexOutOfRange |
| 11 | FailureReason_TargetIllegal |
| 12 | FailureReason_UnrecognizedManaId |
| 13 | FailureReason_InvalidOptionSelection |
| 14 | FailureReason_UnexpectedMessage |
| 15 | FailureReason_LimitViolation |
| 16 | FailureReason_CombatRestrictionViolated |
| 17 | FailureReason_CombatRequirementViolated |
| 18 | FailureReason_UnpayableCost |
| 19 | FailureReason_InvalidSeatId |
| 20 | FailureReason_InvalidTeamId |
| 21 | FailureReason_InvalidMatchState |
| 22 | FailureReason_InvalidMatchScope |
| 23 | FailureReason_InvalidDeck |
| 24 | FailureReason_InvalidCancelState |
| 25 | FailureReason_InvalidUndoState |
| 26 | FailureReason_PermissionDenied |
| 27 | FailureReason_NoTimeout |
| 28 | FailureReason_InvalidColor |
| 29 | FailureReason_InvalidManaColor |
| 30 | FailureReason_InvalidCoinFace |
| 31 | FailureReason_InvalidCard |
| 32 | FailureReason_InvalidCardName |
| 33 | FailureReason_InvalidCardType |
| 34 | FailureReason_InvalidSubType |
| 35 | FailureReason_InvalidSuperType |
| 36 | FailureReason_InvalidCounterType |
| 37 | FailureReason_InvalidAbilityType |
| 38 | FailureReason_InvalidBasicLandType |
| 39 | FailureReason_InvalidOptionIndex |
| 40 | FailureReason_InvalidOptionContext |
| 41 | FailureReason_InvalidSideboard |
| 42 | FailureReason_InvalidCommander |
| 43 | FailureReason_DuplicateAttacker |
| 44 | FailureReason_UnexpectedDamageRecipient |
| 45 | FailureReason_InvalidAttacker |
| 46 | FailureReason_RedundantAttacker |
| 47 | FailureReason_InvalidBatchAttack |
| 48 | FailureReason_MissingDamageRecipient |
| 49 | FailureReason_InvalidDamageRecipient |
| 50 | FailureReason_InvalidObjective |
| 51 | FailureReason_InvalidAlternativeAttack |
| 52 | FailureReason_MissingAttackers |
| 53 | FailureReason_UnexpectedAttackers |
| 54 | FailureReason_InvalidParity |
| 55 | FailureReason_InvalidAlternativeAttackModification |
| 56 | FailureReason_DisallowedValue |
| 57 | FailureReason_InvalidTypeKind |
| 58 | FailureReason_InvalidWishCard |
| 59 | FailureReason_InvalidController |
| 60 | FailureReason_InvalidCardColor |
| 61 | FailureReason_InvalidDecisionPlayerForTarget |
| 62 | FailureReason_InvalidDamageSource |
| 63 | FailureReason_NonlethalDamageAssignment |
| 64 | FailureReason_InvalidDamageAssignment |
| 65 | FailureReason_TooManyTargets |
| 66 | FailureReason_NotEnoughTargets |
| 67 | FailureReason_IllegalTarget |
| 68 | FailureReason_TargetRestrictionViolated |
| 69 | FailureReason_DuplicateBlocker |
| 70 | FailureReason_InvalidCastingTimeOption |
| 71 | FailureReason_InvalidCastingTimeOptionType |
| 72 | FailureReason_InvalidCastingTimeOptionCount |
| 73 | FailureReason_InvalidModeCount |
| 74 | FailureReason_InvalidMode |
| 75 | FailureReason_DuplicateMode |
| 76 | FailureReason_InvalidActionCount |
| 77 | FailureReason_InvalidBlockerCount |
| 78 | FailureReason_InvalidBlocker |
| 79 | FailureReason_InvalidAutoTapSolution |
| 80 | FailureReason_InvalidAutoPassPriority |
| 81 | FailureReason_InvalidReplacement |
| 82 | FailureReason_InvalidTarget |
| 83 | FailureReason_InvalidTargetCount |
| 84 | FailureReason_InvalidGathering |
| 85 | FailureReason_UnexpectedMessageId |

### wotc.mtgo.gre.external.messaging.GameFormat

| Value | Name |
|---:|---|
| 0 | GameFormat_None |
| 1 | GameFormat_Block |
| 2 | GameFormat_Standard |
| 3 | GameFormat_Modern |
| 4 | GameFormat_Legacy |
| 5 | GameFormat_Vintage |
| 7 | GameFormat_Draft |
| 8 | GameFormat_Sealed |

### wotc.mtgo.gre.external.messaging.GameObjectType

| Value | Name |
|---:|---|
| 0 | GameObjectType_None |
| 1 | GameObjectType_Card |
| 2 | GameObjectType_Token |
| 3 | GameObjectType_Ability |
| 4 | GameObjectType_Emblem |
| 5 | GameObjectType_SplitCard |
| 6 | GameObjectType_SplitLeft |
| 7 | GameObjectType_SplitRight |
| 8 | GameObjectType_RevealedCard |
| 9 | GameObjectType_TriggerHolder |
| 10 | GameObjectType_Adventure |
| 11 | GameObjectType_MDFCBack |
| 12 | GameObjectType_DisturbBack |
| 13 | GameObjectType_Boon |
| 14 | GameObjectType_PrototypeFacet |
| 15 | GameObjectType_RoomLeft |
| 16 | GameObjectType_RoomRight |
| 17 | GameObjectType_Omen |

### wotc.mtgo.gre.external.messaging.GameRoomType

| Value | Name |
|---:|---|
| 0 | GameRoomType_Invalid |
| 1 | GameRoomType_Match |
| 2 | GameRoomType_Draft |

### wotc.mtgo.gre.external.messaging.GameStage

| Value | Name |
|---:|---|
| 0 | GameStage_None |
| 1 | GameStage_Start |
| 2 | GameStage_Play |
| 3 | GameStage_GameOver |

### wotc.mtgo.gre.external.messaging.GameStateType

| Value | Name |
|---:|---|
| 0 | GameStateType_None |
| 1 | GameStateType_Full |
| 2 | GameStateType_Diff |
| 3 | GameStateType_Binary |

### wotc.mtgo.gre.external.messaging.GameStateUpdate

| Value | Name |
|---:|---|
| 0 | GameStateUpdate_None |
| 1 | GameStateUpdate_Send |
| 2 | GameStateUpdate_SendAndRecord |
| 3 | GameStateUpdate_SendHiFi |
| 4 | GameStateUpdate_Undo |
| 5 | GameStateUpdate_Restore |

### wotc.mtgo.gre.external.messaging.GameType

| Value | Name |
|---:|---|
| 0 | GameType_None |
| 1 | GameType_Duel |
| 2 | GameType_MultiPlayer |
| 3 | GameType_Solitaire |

### wotc.mtgo.gre.external.messaging.GameVariant

| Value | Name |
|---:|---|
| 0 | GameVariant_None |
| 1 | GameVariant_Normal |
| 2 | GameVariant_Planechase |
| 3 | GameVariant_Vanguard |
| 4 | GameVariant_Commander |
| 5 | GameVariant_Archenemy |
| 6 | GameVariant_TeamVsTeam |
| 7 | GameVariant_TwoHeadedGiant |
| 8 | GameVariant_Brawl |
| 9 | GameVariant_Placeholder9 |
| 10 | GameVariant_Placeholder10 |
| 11 | GameVariant_Placeholder11 |
| 12 | GameVariant_Placeholder12 |
| 13 | GameVariant_Placeholder13 |

### wotc.mtgo.gre.external.messaging.GroupType

| Value | Name |
|---:|---|
| 0 | GroupType_None |
| 1 | GroupType_Ordered |
| 2 | GroupType_Arbitrary |

### wotc.mtgo.gre.external.messaging.GroupingStyle

| Value | Name |
|---:|---|
| 0 | GroupingStyle_None |
| 1 | GroupingStyle_SingleGroup |
| 2 | GroupingStyle_AllGroups |
| 3 | GroupingStyle_Mixed |

### wotc.mtgo.gre.external.messaging.HighlightType

| Value | Name |
|---:|---|
| 0 | HighlightType_None |
| 1 | HighlightType_Cold |
| 2 | HighlightType_Tepid |
| 3 | HighlightType_Hot |
| 4 | HighlightType_Counterspell |
| 5 | HighlightType_Random |
| 6 | HighlightType_CopySpell |
| 7 | HighlightType_ReplaceRole |

### wotc.mtgo.gre.external.messaging.IRFormat

| Value | Name |
|---:|---|
| 0 | IRFormat_None |
| 1 | IRFormat_SuperTypes |
| 2 | IRFormat_SideboardCards |
| 3 | IRFormat_VersionNumbers |
| 4 | IRFormat_SingleVersionNumber |
| 5 | IRFormat_InstancesSection |
| 6 | IRFormat_CannotNameSection |
| 7 | IRFormat_CardNamesSection |
| 8 | IRFormat_Changelist |
| 9 | IRFormat_Cards |
| 10 | IRFormat_CardSets |

### wotc.mtgo.gre.external.messaging.IdType

| Value | Name |
|---:|---|
| 0 | IdType_None |
| 1 | IdType_InstanceId |
| 2 | IdType_PromptParameterIndex |
| 3 | IdType_ZoneInstanceId |
| 4 | IdType_CardGrpId |
| 5 | IdType_AbilityGrpId |
| 6 | IdType_CardGrpIdIndex |
| 7 | IdType_ManaId |

### wotc.mtgo.gre.external.messaging.KeyValuePairValueType

| Value | Name |
|---:|---|
| 0 | KeyValuePairValueType_None |
| 1 | KeyValuePairValueType_uint32 |
| 2 | KeyValuePairValueType_int32 |
| 3 | KeyValuePairValueType_uint64 |
| 4 | KeyValuePairValueType_int64 |
| 5 | KeyValuePairValueType_bool |
| 6 | KeyValuePairValueType_string |
| 7 | KeyValuePairValueType_float |
| 8 | KeyValuePairValueType_double |

### wotc.mtgo.gre.external.messaging.LinkType

| Value | Name |
|---:|---|
| 0 | LinkType_None |
| 1 | LinkType_Exile |
| 2 | LinkType_Sacrifice |
| 3 | LinkType_Choose |
| 4 | LinkType_Haunt |
| 5 | LinkType_Reveal |
| 6 | LinkType_Kicker |
| 7 | LinkType_Target |
| 8 | LinkType_Tap |
| 9 | LinkType_Note |
| 10 | LinkType_PaidLife |
| 11 | LinkType_Backup |
| 12 | LinkType_Discard |
| 13 | LinkType_Bargain |
| 14 | LinkType_Return |
| 15 | LinkType_SecretlyChoose |
| 16 | LinkType_LoseAbility |
| 17 | LinkType_Gift |
| 18 | LinkType_Behold |
| 19 | LinkType_Teamwork |
| 20 | LinkType_AdditionalCost |

### wotc.mtgo.gre.external.messaging.LinkedFace

| Value | Name |
|---:|---|
| 0 | LinkedFace_None |
| 1 | LinkedFace_DFC_Front |
| 2 | LinkedFace_DFC_Back |
| 3 | LinkedFace_MeldCard |
| 4 | LinkedFace_MeldedPermanent |
| 5 | LinkedFace_SplitCard |
| 6 | LinkedFace_SplitHalf |
| 7 | LinkedFace_AdventureParent |
| 8 | LinkedFace_AdventureChild |
| 9 | LinkedFace_MDFC_Front |
| 10 | LinkedFace_MDFC_Back |
| 11 | LinkedFace_SpecializeParent |
| 12 | LinkedFace_SpecializeChild |
| 13 | LinkedFace_PrototypeParent |
| 14 | LinkedFace_PrototypeChild |
| 15 | LinkedFace_RoomCard |
| 16 | LinkedFace_RoomHalf |
| 17 | LinkedFace_OmenParent |
| 18 | LinkedFace_OmenChild |
| 19 | LinkedFace_PreparedSpell |
| 20 | LinkedFace_PreparedSpellParent |
| 21 | LinkedFace_Placeholder21 |
| 22 | LinkedFace_Placeholder22 |

### wotc.mtgo.gre.external.messaging.ManaColor

| Value | Name |
|---:|---|
| 0 | ManaColor_None |
| 1 | ManaColor_White |
| 2 | ManaColor_Blue |
| 3 | ManaColor_Black |
| 4 | ManaColor_Red |
| 5 | ManaColor_Green |
| 6 | ManaColor_Phyrexian |
| 7 | ManaColor_Generic |
| 8 | ManaColor_X |
| 9 | ManaColor_Y |
| 10 | ManaColor_TwoGeneric |
| 11 | ManaColor_AnyColor |
| 12 | ManaColor_Colorless |
| 13 | ManaColor_Snow |

### wotc.mtgo.gre.external.messaging.ManaCostSpecType

| Value | Name |
|---:|---|
| 0 | ManaCostSpecType_None |
| 1 | ManaCostSpecType_IsColorWashed |
| 2 | ManaCostSpecType_CannotDiscountGeneric |
| 3 | ManaCostSpecType_Waterbend |

### wotc.mtgo.gre.external.messaging.ManaPaymentConditionType

| Value | Name |
|---:|---|
| 0 | ManaPaymentConditionType_None |
| 1 | ManaPaymentConditionType_Threshold |
| 2 | ManaPaymentConditionType_Maximum |
| 3 | ManaPaymentConditionType_Diversity |
| 4 | ManaPaymentConditionType_Uniformity |

### wotc.mtgo.gre.external.messaging.ManaPaymentStrategyType

| Value | Name |
|---:|---|
| 0 | ManaPaymentStrategyType_None |
| 1 | ManaPaymentStrategyType_Auto |
| 2 | ManaPaymentStrategyType_Manual |
| 3 | ManaPaymentStrategyType_Legacy |

### wotc.mtgo.gre.external.messaging.ManaSelectionType

| Value | Name |
|---:|---|
| 0 | ManaSelectionType_None |
| 1 | ManaSelectionType_Auto |
| 2 | ManaSelectionType_Manual |

### wotc.mtgo.gre.external.messaging.ManaSpecType

| Value | Name |
|---:|---|
| 0 | ManaSpecType_None |
| 1 | ManaSpecType_FromBasic |
| 2 | ManaSpecType_Predictive |
| 3 | ManaSpecType_Restricted |
| 4 | ManaSpecType_Trigger |
| 5 | ManaSpecType_FromCreature |
| 6 | ManaSpecType_FromSnow |
| 7 | ManaSpecType_DoesNotEmpty |
| 8 | ManaSpecType_AdditionalEffect |
| 9 | ManaSpecType_FromTreasure |
| 10 | ManaSpecType_FromCave |
| 11 | ManaSpecType_CantBeCountered |
| 12 | ManaSpecType_ManaSubstitution |
| 13 | ManaSpecType_GainsHaste |

### wotc.mtgo.gre.external.messaging.MatchCompletedReasonType

| Value | Name |
|---:|---|
| 0 | MatchCompletedReasonType_Invalid |
| 1 | MatchCompletedReasonType_Success |
| 2 | MatchCompletedReasonType_ForceResultRequest |
| 3 | MatchCompletedReasonType_Canceled |
| 10 | MatchCompletedReasonType_PlayerJoinTimeout |
| 11 | MatchCompletedReasonType_PlayerInactivityTimeout |
| 12 | MatchCompletedReasonType_PlayerDisconnectTimeout |
| 30 | MatchCompletedReasonType_SystemShutdown |
| 31 | MatchCompletedReasonType_GreError |
| 33 | MatchCompletedReasonType_ServiceFabricError |
| 34 | MatchCompletedReasonType_CodeContractError |
| 40 | MatchCompletedReasonType_UnknownServerError |

### wotc.mtgo.gre.external.messaging.MatchGameRoomStateType

| Value | Name |
|---:|---|
| 0 | MatchGameRoomStateType_Invalid |
| 5 | MatchGameRoomStateType_Playing |
| 15 | MatchGameRoomStateType_MatchCompleted |

### wotc.mtgo.gre.external.messaging.MatchScope

| Value | Name |
|---:|---|
| 0 | MatchScope_None |
| 1 | MatchScope_Game |
| 2 | MatchScope_Match |

### wotc.mtgo.gre.external.messaging.MatchServiceErrorCode

| Value | Name |
|---:|---|
| 0 | MatchServiceErrorCode_Invalid |
| 103 | MatchServiceErrorCode_InvalidState |
| 108 | MatchServiceErrorCode_BadRequest |
| 111 | MatchServiceErrorCode_NotAuthorized |
| 115 | MatchServiceErrorCode_RequestQueueOverflow |
| 500 | MatchServiceErrorCode_UnknownServerError |

### wotc.mtgo.gre.external.messaging.MatchState

| Value | Name |
|---:|---|
| 0 | MatchState_None |
| 1 | MatchState_GameInProgress |
| 2 | MatchState_GameComplete |
| 3 | MatchState_MatchComplete |
| 4 | MatchState_Sideboarding |

### wotc.mtgo.gre.external.messaging.MatchWinCondition

| Value | Name |
|---:|---|
| 0 | MatchWinCondition_None |
| 1 | MatchWinCondition_SingleElimination |
| 2 | MatchWinCondition_Best2of3 |
| 3 | MatchWinCondition_Best3of5 |

### wotc.mtgo.gre.external.messaging.MessageIdType

| Value | Name |
|---:|---|
| 0 | MessageIdType_None |
| 1 | MessageIdType_Prompt |
| 2 | MessageIdType_Choice |
| 3 | MessageIdType_LocalizationId |

### wotc.mtgo.gre.external.messaging.MiscellaneousTerm

| Value | Name |
|---:|---|
| 0 | MiscellaneousTerm_None |
| 1 | MiscellaneousTerm_ScryDeprecated |
| 2 | MiscellaneousTerm_Historic |
| 3 | MiscellaneousTerm_Surveil |
| 4 | MiscellaneousTerm_DevotionToWhite |
| 5 | MiscellaneousTerm_DevotionToBlue |
| 6 | MiscellaneousTerm_DevotionToBlack |
| 7 | MiscellaneousTerm_DevotionToRed |
| 8 | MiscellaneousTerm_DevotionToGreen |
| 9 | MiscellaneousTerm_DevotionToWhiteAndBlue |
| 10 | MiscellaneousTerm_DevotionToWhiteAndBlack |
| 11 | MiscellaneousTerm_DevotionToBlueAndBlack |
| 12 | MiscellaneousTerm_DevotionToBlueAndRed |
| 13 | MiscellaneousTerm_DevotionToBlackAndRed |
| 14 | MiscellaneousTerm_DevotionToBlackAndGreen |
| 15 | MiscellaneousTerm_DevotionToRedAndWhite |
| 16 | MiscellaneousTerm_DevotionToRedAndGreen |
| 17 | MiscellaneousTerm_DevotionToGreenAndWhite |
| 18 | MiscellaneousTerm_DevotionToGreenAndBlue |
| 19 | MiscellaneousTerm_DevotionToAColor |
| 20 | MiscellaneousTerm_Party |
| 21 | MiscellaneousTerm_Investigate |
| 22 | MiscellaneousTerm_Modified |
| 23 | MiscellaneousTerm_ShieldCounters |
| 24 | MiscellaneousTerm_PoisonCounter |
| 25 | MiscellaneousTerm_StunCounter |
| 26 | MiscellaneousTerm_FinalityCounter |
| 27 | MiscellaneousTerm_Descended |
| 28 | MiscellaneousTerm_Outlaw |
| 29 | MiscellaneousTerm_HoneCounter |
| 30 | MiscellaneousTerm_Worthy |
| 31 | MiscellaneousTerm_TheMonarch |
| 32 | MiscellaneousTerm_ConditionalTheMonarch |
| 33 | MiscellaneousTerm_Placeholder33 |
| 34 | MiscellaneousTerm_Placeholder34 |

### wotc.mtgo.gre.external.messaging.ModalChoiceAdvisability

| Value | Name |
|---:|---|
| 0 | ModalChoiceAdvisability_None |
| 1 | ModalChoiceAdvisability_Discourage |

### wotc.mtgo.gre.external.messaging.MulliganOption

| Value | Name |
|---:|---|
| 0 | MulliganOption_None |
| 1 | MulliganOption_Mulligan |
| 2 | MulliganOption_AcceptHand |

### wotc.mtgo.gre.external.messaging.MulliganType

| Value | Name |
|---:|---|
| 0 | MulliganType_None |
| 1 | MulliganType_Paris |
| 2 | MulliganType_Vancouver |
| 3 | MulliganType_London |

### wotc.mtgo.gre.external.messaging.NumericInputType

| Value | Name |
|---:|---|
| 0 | NumericInputType_None |
| 1 | NumericInputType_ChooseX |
| 2 | NumericInputType_ChooseAnyAmount |
| 3 | NumericInputType_DebugCode |
| 4 | NumericInputType_ChooseDieRoll |

### wotc.mtgo.gre.external.messaging.OptionResponse

| Value | Name |
|---:|---|
| 0 | OptionResponse_None |
| 1 | OptionResponse_Allow_Yes |
| 2 | OptionResponse_Cancel_No |
| -1 | OptionResponse_Cancel |

### wotc.mtgo.gre.external.messaging.OptionType

| Value | Name |
|---:|---|
| 0 | OptionType_None |
| 1 | OptionType_Modal |
| 4 | OptionType_Numeric |
| 5 | OptionType_String |
| 6 | OptionType_ManaType |
| 7 | OptionType_Order |
| 8 | OptionType_Search |
| 9 | OptionType_Group |
| 10 | OptionType_Select |
| 11 | OptionType_SelectGroup |
| 12 | OptionType_Distribution |
| 13 | OptionType_OptionalAction |
| 14 | OptionType_ActionsAvailable |
| 15 | OptionType_SelectFromGroups |
| 16 | OptionType_SearchFromGroups |
| 17 | OptionType_Gathering |
| 18 | OptionType_SelectCounters |
| 19 | OptionType_ResolutionTimePayLife |
| 20 | OptionType_SelectWithWeightRange |

### wotc.mtgo.gre.external.messaging.OrderingType

| Value | Name |
|---:|---|
| 0 | OrderingType_None |
| 1 | OrderingType_OrderAsIndicated |
| 2 | OrderingType_OrderArbitraryOnce |
| 3 | OrderingType_OrderArbitraryAlways |
| 4 | OrderingType_PromptAlways |

### wotc.mtgo.gre.external.messaging.ParameterType

| Value | Name |
|---:|---|
| 0 | ParameterType_None |
| 1 | ParameterType_NonLocalizedString |
| 2 | ParameterType_Number |
| 3 | ParameterType_Reference |
| 5 | ParameterType_PromptId |

### wotc.mtgo.gre.external.messaging.Parity

| Value | Name |
|---:|---|
| 0 | Parity_Even |
| 1 | Parity_Odd |

### wotc.mtgo.gre.external.messaging.PerformActionFor

| Value | Name |
|---:|---|
| 0 | PerformActionFor_Attackers |
| 1 | PerformActionFor_Blockers |

### wotc.mtgo.gre.external.messaging.Phase

| Value | Name |
|---:|---|
| 0 | Phase_None |
| 1 | Phase_Beginning |
| 2 | Phase_Main1 |
| 3 | Phase_Combat |
| 4 | Phase_Main2 |
| 5 | Phase_Ending |

### wotc.mtgo.gre.external.messaging.PlayerCmdType

| Value | Name |
|---:|---|
| 0 | PlayerCmdType_Invalid |
| 405 | PlayerCmdType_ReportMatchCreate |
| 406 | PlayerCmdType_ReportMatchResult |
| 3000 | PlayerCmdType_ReportMatchAchievements |

### wotc.mtgo.gre.external.messaging.PlayerStatus

| Value | Name |
|---:|---|
| 0 | PlayerStatus_None |
| 1 | PlayerStatus_InGame |
| 2 | PlayerStatus_Removed |
| 3 | PlayerStatus_PendingLoss |

### wotc.mtgo.gre.external.messaging.PromptMessage

| Value | Name |
|---:|---|
| 0 | PromptMessage_None |
| 1 | PromptMessage_GameStateMessage |
| 2 | PromptMessage_ActionsAvailableReq |
| 3 | PromptMessage_IllegalRequest |
| 4 | PromptMessage_MatchState |
| 5 | PromptMessage_OptionReq |
| 6 | PromptMessage_DeclareAttackersReq |
| 7 | PromptMessage_DeclareBlockersReq |
| 8 | PromptMessage_AssignDamageReq |
| 9 | PromptMessage_OrderCombatDamageReq |
| 10 | PromptMessage_SelectTargetsReq |
| 11 | PromptMessage_PayCostReq |
| 12 | PromptMessage_InvalidSelection |
| 13 | PromptMessage_InvalidOrdering |
| 14 | PromptMessage_Discard |
| 23 | PromptMessage_SelectOptionsReq |
| 25 | PromptMessage_GameResultWinLoss |
| 26 | PromptMessage_GameResultDraw |
| 27 | PromptMessage_MatchResultWinLoss |
| 28 | PromptMessage_MatchResultDraw |
| 29 | PromptMessage_IntermissionRevealHand |
| 30 | PromptMessage_IntermissionDrawCard |
| 31 | PromptMessage_IntermissionBeginSideboarding |
| 32 | PromptMessage_ChooseStarting |
| 34 | PromptMessage_MulliganOrAcceptHand |
| 36 | PromptMessage_Mulliganed |
| 37 | PromptMessage_AcceptedHand |
| 38 | PromptMessage_ChoiceMulligan |
| 39 | PromptMessage_ChoiceKeep |
| 40 | PromptMessage_GroupReq |
| 42 | PromptMessage_OrderReq |
| 44 | PromptMessage_SelectNGroupReq |
| 46 | PromptMessage_CoinFlip |
| 47 | PromptMessage_ChoiceHeads |
| 48 | PromptMessage_ChoiceTails |
| 51 | PromptMessage_XCostReq |
| 53 | PromptMessage_MinimumBlockersNotMet |
| 54 | PromptMessage_DoModularAbility |
| 56 | PromptMessage_MoveCountersMessage |
| 58 | PromptMessage_DoRecover |
| 60 | PromptMessage_DoEchoPayment |
| 62 | PromptMessage_DoProvokeAbility |
| 64 | PromptMessage_DoCascadeAbility |
| 66 | PromptMessage_DoSoulshift |
| 68 | PromptMessage_ApplyPlaneswalkerRedirection |
| 70 | PromptMessage_PlaneswalkerUniqueness |
| 72 | PromptMessage_LegendRule |
| 74 | PromptMessage_SelectReplacement |
| 76 | PromptMessage_ApplyUnleash |
| 78 | PromptMessage_ChooseObjectToCopy |
| 80 | PromptMessage_SelectManaTypeReq |
| 83 | PromptMessage_ChooseModalReq |
| 85 | PromptMessage_OrderObjectEnteringGraveyard |
| 86 | PromptMessage_OrderObjectEnteringLibrary |
| 88 | PromptMessage_ChooseCreaturesForConspire |
| 89 | PromptMessage_ApplyDredge |
| 91 | PromptMessage_OrderObjectEnteringBattlefield |
| 92 | PromptMessage_Scry |
| 94 | PromptMessage_UnpayableCost |
| 95 | PromptMessage_CastForMadnessCost |
| 96 | PromptMessage_SacrificeClue |
| 97 | PromptMessage_SelectFromGroupsReq |
| 99 | PromptMessage_SelectPermanentsToUntap |
| 101 | PromptMessage_UnboundedLoopDetectedActive |
| 102 | PromptMessage_UnboundedLoopDetectedNotActive |
| 103 | PromptMessage_InfiniteLoopDetected |
| 104 | PromptMessage_ClickOKToContinue |
| 106 | PromptMessage_GatherReq |
| 108 | PromptMessage_SelectZoneForMutuallyExclusiveZoneTransfers |
| 110 | PromptMessage_PutOrRemoveCounters |
| 111 | PromptMessage_SelectZonesToSearch |
| 112 | PromptMessage_ChooseAction |
| 113 | PromptMessage_ChooseTargetForNextCopy |
| 114 | PromptMessage_PerformOpeningHandActions |
| 115 | PromptMessage_DoExplorePutCardInGraveyard |
| 117 | PromptMessage_Warning_InsufficientBlockers |
| 118 | PromptMessage_ChooseAColor |
| 119 | PromptMessage_Warning_BlockerCannotBlockAlone |
| 120 | PromptMessage_Warning_AttackerCannotAttackAlone |
| 121 | PromptMessage_Warning_AttackerMustBeBlocked |
| 122 | PromptMessage_Warning_MustAttackWithAtLeastOne |
| 123 | PromptMessage_AttackPlayerOrPlaneswalker |
| 124 | PromptMessage_Warning_MustAttack |
| 125 | PromptMessage_Warning_MustBlock |
| 126 | PromptMessage_CitysBlessingDesignation |
| 127 | PromptMessage_AssignDamageReqCanIgnoreBlockers |
| 128 | PromptMessage_SelectAnAttachee |
| 129 | PromptMessage_Surveil |
| 130 | PromptMessage_Warning_AttackerMustBeBlockedByAll |
| 131 | PromptMessage_ApplyRiot |
| 133 | PromptMessage_CommanderGraveyardReplacement |
| 134 | PromptMessage_CommanderExileReplacement |
| 135 | PromptMessage_CommanderLibraryReplacement |
| 136 | PromptMessage_CommanderHandReplacement |
| 137 | PromptMessage_UntapOptional |
| 138 | PromptMessage_OrderObjectEnteringStack |
| 139 | PromptMessage_MandatoryChooseNewTarget |
| 140 | PromptMessage_AutotapSolutionChanged |
| 141 | PromptMessage_TargetMutate |
| 142 | PromptMessage_TargetUnownedMutate |
| 143 | PromptMessage_ApplyMutate |
| 144 | PromptMessage_ResurrectCommander |
| 145 | PromptMessage_SelectPT |
| 146 | PromptMessage_SelectPTModification |
| 147 | PromptMessage_LearnDefault |
| 148 | PromptMessage_LearnOnlyLessons |
| 149 | PromptMessage_LearnOnlyDiscards |
| 150 | PromptMessage_Decline |
| 151 | PromptMessage_ModularTarget |
| 152 | PromptMessage_TargetCreatureYouControl |
| 153 | PromptMessage_ConniveDiscard |
| 154 | PromptMessage_Casualty |
| 155 | PromptMessage_ChooseTokenType |
| 156 | PromptMessage_CasualtyX |
| 157 | PromptMessage_Specialize |
| 158 | PromptMessage_SpecializeDiscardWhite |
| 159 | PromptMessage_SpecializeDiscardBlue |
| 160 | PromptMessage_SpecializeDiscardBlack |
| 161 | PromptMessage_SpecializeDiscardRed |
| 162 | PromptMessage_SpecializeDiscardGreen |
| 163 | PromptMessage_RemoveCommanderPerpetualEffects |
| 164 | PromptMessage_RemoveCommanderPerpetualEffectsAndUnspecialize |
| 165 | PromptMessage_ChoosePile |
| 166 | PromptMessage_ApplyReadAhead |
| 167 | PromptMessage_UnpayableEnlistCost |
| 168 | PromptMessage_DraftACard |
| 169 | PromptMessage_ProliferateChoiceConfirmation |
| 170 | PromptMessage_MayCastCard |
| 171 | PromptMessage_PreselectCreatureForEmerge |
| 172 | PromptMessage_AttackPlayerOrBattle |
| 173 | PromptMessage_AttackPlayerPlaneswalkerOrBattle |
| 174 | PromptMessage_TheRingTempts |
| 175 | PromptMessage_BargainSacrificeCost |
| 176 | PromptMessage_AutoTapSelfAnimateWarning |
| 177 | PromptMessage_ReplicateCount |
| 178 | PromptMessage_HeistSelection |
| 179 | PromptMessage_AutoTapPrismaticEndingInsufficientColorsWarning |
| 180 | PromptMessage_AnnihilatorSacrifice |
| 181 | PromptMessage_PreselectArtifactForEmerge |
| 182 | PromptMessage_Awaken |
| 183 | PromptMessage_PreselectCreatureForHarmonize |
| 184 | PromptMessage_SelectCreatureToDamage |
| 185 | PromptMessage_UnsatisfiedManaPaymentBonus |
| 186 | PromptMessage_EnduringStory |
| 187 | PromptMessage_SylvanLibrary_Pretty_SelectPrompt_02 |
| 188 | PromptMessage_SylvanLibrary_Pretty_SelectPrompt_12 |
| 189 | PromptMessage_SylvanLibrary_Pretty_SelectPrompt_22 |
| 190 | PromptMessage_SylvanLibrary_Pretty_SelectPrompt_01 |
| 191 | PromptMessage_SylvanLibrary_Pretty_SelectPrompt_11 |
| 192 | PromptMessage_SylvanLibrary_Pretty_OrderPrompt |
| 193 | PromptMessage_SylvanLibrary_Precise_FirstSelectPrompt |
| 194 | PromptMessage_SylvanLibrary_Precise_SecondSelectPrompt |
| 195 | PromptMessage_SylvanLibrary_Precise_OptionalActionPrompt |
| 196 | PromptMessage_RevealMiracle |
| 197 | PromptMessage_CastForMiracleCost |
| 198 | PromptMessage_SylvanLibrary_Precise_SecondSelectPrompt_CantPayLife |
| 199 | PromptMessage_Placeholder199 |
| 200 | PromptMessage_EmpowerJace |

### wotc.mtgo.gre.external.messaging.ProtoVersion

| Value | Name |
|---:|---|
| 0 | ProtoVersion_None |
| 1 | ProtoVersion_HiFiGameStates |
| 2 | ProtoVersion_PreviousGameStateId |
| 3 | ProtoVersion_Timers |
| 4 | ProtoVersion_ManaRequirement |
| 5 | ProtoVersion_AutoTapActions |
| 6 | ProtoVersion_UIMessages |
| 7 | ProtoVersion_AutoTapStopsSetting |
| 8 | ProtoVersion_Sideboarding |
| 9 | ProtoVersion_AutoOptionalPaymentCancellationSetting |
| 10 | ProtoVersion_CostCategory |
| 11 | ProtoVersion_TimersV2 |
| 12 | ProtoVersion_Control |
| 13 | ProtoVersion_TimersV3 |
| 14 | ProtoVersion_AutoAcceptHand |
| 15 | ProtoVersion_TimersV4 |
| 16 | ProtoVersion_TimersV5 |
| 17 | ProtoVersion_ManaPaymentStrategyType |
| 18 | ProtoVersion_TimersV6 |
| 19 | ProtoVersion_TimersV7 |
| 20 | ProtoVersion_TimersV8 |
| 21 | ProtoVersion_TransientStops |
| 22 | ProtoVersion_Cosmetics |
| 23 | ProtoVersion_ResultSpec |
| 24 | ProtoVersion_ResultReason |
| 25 | ProtoVersion_SuperFormat |
| 26 | ProtoVersion_AutoSelectReplacementSetting |
| 27 | ProtoVersion_MulliganType |
| 28 | ProtoVersion_PendingMessageType |
| 29 | ProtoVersion_AutoDeclareAttackersThatMustAttackSetting |
| 30 | ProtoVersion_DeceptiveActions |
| 31 | ProtoVersion_PerformAutoTapActions |
| 32 | ProtoVersion_MulliganReq |
| 33 | ProtoVersion_AutoTapSolution |
| 34 | ProtoVersion_ManaPaymentDeprecated |
| 35 | ProtoVersion_SelectFromGroupRespGroups |
| 36 | ProtoVersion_PersistentAnnotations |

### wotc.mtgo.gre.external.messaging.QualificationSubtype

| Value | Name |
|---:|---|
| 0 | QualificationSubtype_None |
| 1 | QualificationSubtype_CantActivateAny |
| 2 | QualificationSubtype_CantActivateLoyalty |
| 3 | QualificationSubtype_CantActivateNonMana |
| 4 | QualificationSubtype_CantPreventAny |
| 5 | QualificationSubtype_CantPreventCombatDamage |
| 6 | QualificationSubtype_MayPlayTopItem |

### wotc.mtgo.gre.external.messaging.QualificationType

| Value | Name |
|---:|---|
| 0 | QualificationType_None |
| 1 | QualificationType_CostToCast |
| 10 | QualificationType_CantBeCountered |
| 11 | QualificationType_CantBeActivated |
| 12 | QualificationType_CantBeRegenerated |
| 13 | QualificationType_CantBeEnchanted |
| 14 | QualificationType_CantBeEquipped |
| 15 | QualificationType_CantBeDestroyed |
| 16 | QualificationType_CantBePrevented |
| 17 | QualificationType_CantBeTheTargetOf |
| 19 | QualificationType_CantBeSacrificed |
| 20 | QualificationType_CantGenericEffect |
| 21 | QualificationType_CantUntap |
| 22 | QualificationType_CantDraw |
| 23 | QualificationType_CantPlay |
| 24 | QualificationType_CantCycle |
| 25 | QualificationType_CantCast |
| 26 | QualificationType_CantSearch |
| 27 | QualificationType_CantHave |
| 28 | QualificationType_CantEnter |
| 30 | QualificationType_CantAttack |
| 31 | QualificationType_CantBlock |
| 32 | QualificationType_CantBeBlocked |
| 33 | QualificationType_MustBlockAttacker |
| 34 | QualificationType_MustAttack |
| 39 | QualificationType_BlockedByMaxCount |
| 40 | QualificationType_BlockedByMinCount |
| 41 | QualificationType_MustAttackSpecific |
| 42 | QualificationType_BlockIfAble |
| 43 | QualificationType_AbleToAttack |
| 44 | QualificationType_AttackThatCombatIfAble |
| 47 | QualificationType_MayPlay |
| 48 | QualificationType_CantTransform |
| 49 | QualificationType_CanAttack |
| 50 | QualificationType_CantCrew |
| 52 | QualificationType_CantBlockUnlessAlso |
| 53 | QualificationType_CantActivate |
| 54 | QualificationType_MaxAttackers |
| 55 | QualificationType_MaxBlockers |
| 56 | QualificationType_CantAttackUnlessAlso |
| 57 | QualificationType_CanOnlyAttackAlone |
| 58 | QualificationType_IfAttacks |
| 59 | QualificationType_AttackCost |
| 60 | QualificationType_BlockCost |
| 61 | QualificationType_CantCauseAbilitiesToTrigger |
| 62 | QualificationType_AssignCombatDamageWithToughness |
| 63 | QualificationType_PlayerMustAttackElementsOfList |
| 64 | QualificationType_CantLoseTheGame |
| 65 | QualificationType_CantLoseMana |
| 66 | QualificationType_MayPlayAdditionalLands |
| 67 | QualificationType_AssignCombatDamageAsThoughWerentBlocked |
| 68 | QualificationType_MayPlayValidItems |
| 69 | QualificationType_CanBeTheTargetOf |
| 70 | QualificationType_AdaptAsThoughNoCounters |
| 71 | QualificationType_MayPlayAtInstantSpeed |
| 72 | QualificationType_LethalDeterminedByPower |
| 73 | QualificationType_CantPhaseIn |
| 74 | QualificationType_CantGainLife |
| 75 | QualificationType_CantPayEffectCost |
| 76 | QualificationType_PaysCrewlikeCostWithModifiedWeight |
| 77 | QualificationType_CanForetell |
| 78 | QualificationType_IgnoreBoastActivationLimit |
| 79 | QualificationType_SpecialActionPermission |
| 80 | QualificationType_UntapDuringOpponentsTurn |
| 81 | QualificationType_DrawDuringOpponentsTurn |
| 82 | QualificationType_DontApplyLegendRule |
| 83 | QualificationType_MayActivateLoyaltyAbilitiesNTimes |
| 84 | QualificationType_CantBeCopied |
| 85 | QualificationType_DealDamageAsThoughHadAbility |
| 86 | QualificationType_MayLookAtFacedownCards |
| 87 | QualificationType_CantTurnFaceUp |
| 88 | QualificationType_NextSpellCastGainsAbility |
| 89 | QualificationType_CantGetCountersOnObject |
| 90 | QualificationType_Detained |
| 91 | QualificationType_MayActivateLoyaltyAbilitiesAsThoughNotActivated |
| 92 | QualificationType_MayActivateAbilitiesAsThoughNotActivated |
| 93 | QualificationType_MayActivateAbilityNMoreTimesThisTurn |
| 94 | QualificationType_CantBeAttacked |
| 95 | QualificationType_AssignCombatDamageAsThoughPositive |
| 96 | QualificationType_PlayWithZoneRevealed |

### wotc.mtgo.gre.external.messaging.QueueType

| Value | Name |
|---:|---|
| 0 | QueueType_Invalid |
| 1 | QueueType_MatchQueue |
| 2 | QueueType_DraftQueue |

### wotc.mtgo.gre.external.messaging.ReasonFaceDown

| Value | Name |
|---:|---|
| 0 | ReasonFaceDown_None |
| 1 | ReasonFaceDown_MovedFaceDown |
| 2 | ReasonFaceDown_Morph |
| 3 | ReasonFaceDown_Manifest |
| 4 | ReasonFaceDown_TurnedFaceDown |
| 5 | ReasonFaceDown_Foretell |
| 6 | ReasonFaceDown_Disguise |
| 7 | ReasonFaceDown_Cloak |
| 8 | ReasonFaceDown_ManifestDread |

### wotc.mtgo.gre.external.messaging.ReferenceType

| Value | Name |
|---:|---|
| 0 | ReferenceType_None |
| 1 | ReferenceType_InstanceId |
| 2 | ReferenceType_CatalogId |
| 3 | ReferenceType_LocalizationId |
| 4 | ReferenceType_PlayerSeatId |

### wotc.mtgo.gre.external.messaging.ReplacementEffectSpawnerType

| Value | Name |
|---:|---|
| 0 | ReplacementEffectSpawnerType_None |
| 1 | ReplacementEffectSpawnerType_PreventDamage |
| 2 | ReplacementEffectSpawnerType_ExileOnDeath |
| 3 | ReplacementEffectSpawnerType_DamageRedirection |

### wotc.mtgo.gre.external.messaging.RequiresConfirmation

| Value | Name |
|---:|---|
| 0 | RequiresConfirmation_None |
| 1 | RequiresConfirmation_Sacrifice |
| 2 | RequiresConfirmation_PayLife |
| 3 | RequiresConfirmation_Discard |
| 4 | RequiresConfirmation_Exile |
| 5 | RequiresConfirmation_Target |
| 6 | RequiresConfirmation_Selection |
| 7 | RequiresConfirmation_ForOpponent |
| 8 | RequiresConfirmation_Misclick |

### wotc.mtgo.gre.external.messaging.ResultReason

| Value | Name |
|---:|---|
| 0 | ResultReason_None |
| 1 | ResultReason_Game |
| 2 | ResultReason_Concede |
| 3 | ResultReason_Timeout |
| 4 | ResultReason_Loop |
| 5 | ResultReason_Force |

### wotc.mtgo.gre.external.messaging.ResultType

| Value | Name |
|---:|---|
| 0 | ResultType_None |
| 1 | ResultType_Suspended |
| 2 | ResultType_Draw |
| 3 | ResultType_WinLoss |

### wotc.mtgo.gre.external.messaging.SelectAction

| Value | Name |
|---:|---|
| 0 | SelectAction_None |
| 1 | SelectAction_Select |
| 2 | SelectAction_Unselect |

### wotc.mtgo.gre.external.messaging.SelectReplacementsType

| Value | Name |
|---:|---|
| 0 | SelectReplacementsType_None |
| 1 | SelectReplacementsType_AllDredge |
| 2 | SelectReplacementsType_ApplyLast |

### wotc.mtgo.gre.external.messaging.SelectionListType

| Value | Name |
|---:|---|
| 0 | SelectionListType_None |
| 1 | SelectionListType_Static |
| 2 | SelectionListType_Dynamic |
| 3 | SelectionListType_StaticSubset |

### wotc.mtgo.gre.external.messaging.Setting

| Value | Name |
|---:|---|
| 0 | Setting_None |
| 1 | Setting_Enable |
| 2 | Setting_Disable |

### wotc.mtgo.gre.external.messaging.SettingKey

| Value | Name |
|---:|---|
| 0 | SettingKey_None |
| 1 | SettingKey_ByAbility |
| 2 | SettingKey_ByCardTitle |

### wotc.mtgo.gre.external.messaging.SettingScope

| Value | Name |
|---:|---|
| 0 | SettingScope_None |
| 1 | SettingScope_AnyPlayer |
| 2 | SettingScope_Opponents |
| 3 | SettingScope_Team |

### wotc.mtgo.gre.external.messaging.SettingStatus

| Value | Name |
|---:|---|
| 0 | SettingStatus_None |
| 1 | SettingStatus_Set |
| 2 | SettingStatus_Clear |

### wotc.mtgo.gre.external.messaging.ShuffleRestriction

| Value | Name |
|---:|---|
| 0 | ShuffleRestriction_None |
| 1 | ShuffleRestriction_OpeningHand |
| 2 | ShuffleRestriction_All |

### wotc.mtgo.gre.external.messaging.SmartStopsSetting

| Value | Name |
|---:|---|
| 0 | SmartStopsSetting_None |
| 1 | SmartStopsSetting_Enable |
| 2 | SmartStopsSetting_Disable |

### wotc.mtgo.gre.external.messaging.StaticList

| Value | Name |
|---:|---|
| 0 | StaticList_None |
| 1 | StaticList_CardColors |
| 2 | StaticList_ManaColors |
| 3 | StaticList_SuperTypes |
| 4 | StaticList_CardTypes |
| 5 | StaticList_SubTypes |
| 6 | StaticList_Colors |
| 7 | StaticList_CoinFaces |
| 8 | StaticList_WishCards |
| 9 | StaticList_BasicLandTypes |
| 10 | StaticList_CreatureTypes |
| 11 | StaticList_CounterTypes |
| 12 | StaticList_Keywords |
| 13 | StaticList_CardNames |
| 14 | StaticList_Parities |
| 15 | StaticList_TypeKinds |

### wotc.mtgo.gre.external.messaging.Step

| Value | Name |
|---:|---|
| 0 | Step_None |
| 1 | Step_Untap |
| 2 | Step_Upkeep |
| 3 | Step_Draw |
| 4 | Step_BeginCombat |
| 5 | Step_DeclareAttack |
| 6 | Step_DeclareBlock |
| 7 | Step_CombatDamage |
| 8 | Step_EndCombat |
| 9 | Step_End |
| 10 | Step_Cleanup |
| 11 | Step_FirstStrikeDamage |

### wotc.mtgo.gre.external.messaging.StopType

| Value | Name |
|---:|---|
| 0 | StopType_None |
| 1 | StopType_UpkeepStep |
| 2 | StopType_DrawStep |
| 3 | StopType_PrecombatMainPhase |
| 4 | StopType_BeginCombatStep |
| 5 | StopType_DeclareAttackersStep |
| 6 | StopType_DeclareBlockersStep |
| 7 | StopType_CombatDamageStep |
| 8 | StopType_EndCombatStep |
| 9 | StopType_PostcombatMainPhase |
| 10 | StopType_EndStep |
| 11 | StopType_FirstStrikeDamageStep |

### wotc.mtgo.gre.external.messaging.SubType

| Value | Name |
|---:|---|
| 0 | SubType_None |
| 1 | SubType_Angel |
| 2 | SubType_Archer |
| 3 | SubType_Archon |
| 4 | SubType_Artificer |
| 5 | SubType_Assassin |
| 6 | SubType_Aura |
| 7 | SubType_Basilisk |
| 8 | SubType_Bat |
| 9 | SubType_Bear |
| 10 | SubType_Beast |
| 11 | SubType_Berserker |
| 12 | SubType_Bird |
| 13 | SubType_Boar |
| 14 | SubType_Cat |
| 15 | SubType_Chandra |
| 16 | SubType_Cleric |
| 17 | SubType_Construct |
| 18 | SubType_Crocodile |
| 19 | SubType_Demon |
| 20 | SubType_Djinn |
| 21 | SubType_Dragon |
| 22 | SubType_Drake |
| 23 | SubType_Druid |
| 24 | SubType_Fish |
| 25 | SubType_Elemental |
| 26 | SubType_Elephant |
| 27 | SubType_Elf |
| 28 | SubType_Equipment |
| 29 | SubType_Forest |
| 30 | SubType_Garruk |
| 31 | SubType_Gate |
| 32 | SubType_Giant |
| 33 | SubType_Gideon |
| 34 | SubType_Goblin |
| 35 | SubType_Golem |
| 36 | SubType_Griffin |
| 37 | SubType_Horse |
| 39 | SubType_Human |
| 40 | SubType_Hydra |
| 41 | SubType_Illusion |
| 42 | SubType_Insect |
| 43 | SubType_Island |
| 44 | SubType_Jace |
| 45 | SubType_Knight |
| 46 | SubType_Merfolk |
| 47 | SubType_Minotaur |
| 48 | SubType_Monk |
| 49 | SubType_Mountain |
| 50 | SubType_Ogre |
| 51 | SubType_Ooze |
| 52 | SubType_Pegasus |
| 53 | SubType_Phoenix |
| 54 | SubType_Plains |
| 55 | SubType_Rhino |
| 56 | SubType_Rogue |
| 57 | SubType_Salamander |
| 58 | SubType_Scout |
| 59 | SubType_Serpent |
| 60 | SubType_Shade |
| 61 | SubType_Shaman |
| 62 | SubType_Siren |
| 63 | SubType_Skeleton |
| 64 | SubType_Soldier |
| 65 | SubType_Sorin |
| 66 | SubType_Sphinx |
| 67 | SubType_Spider |
| 68 | SubType_Spirit |
| 69 | SubType_Swamp |
| 70 | SubType_Tower |
| 71 | SubType_Treefolk |
| 72 | SubType_Troll |
| 73 | SubType_Urzas |
| 74 | SubType_Vampire |
| 75 | SubType_Vedalken |
| 76 | SubType_Wall |
| 77 | SubType_Warrior |
| 78 | SubType_Wizard |
| 79 | SubType_Wolf |
| 80 | SubType_Wurm |
| 81 | SubType_Zombie |
| 82 | SubType_Mine |
| 83 | SubType_Power_Plant |
| 84 | SubType_Saproling |
| 85 | SubType_Avatar |
| 86 | SubType_Sliver |
| 87 | SubType_Samurai |
| 88 | SubType_Pest |
| 89 | SubType_Thalakos |
| 90 | SubType_Dauthi |
| 91 | SubType_Minion |
| 92 | SubType_Advisor |
| 93 | SubType_Ajani |
| 94 | SubType_Alara |
| 95 | SubType_Ally |
| 97 | SubType_Antelope |
| 98 | SubType_Ape |
| 99 | SubType_Arcane |
| 100 | SubType_Arkhos |
| 101 | SubType_Ashiok |
| 102 | SubType_AssemblyWorker |
| 103 | SubType_Atog |
| 104 | SubType_Aurochs |
| 105 | SubType_Azgol |
| 106 | SubType_Badger |
| 107 | SubType_Barbarian |
| 108 | SubType_Beeble |
| 109 | SubType_Belenon |
| 110 | SubType_Bolas |
| 111 | SubType_Bolass |
| 112 | SubType_Bringer |
| 113 | SubType_Brushwagg |
| 114 | SubType_Camel |
| 115 | SubType_Carrier |
| 116 | SubType_Centaur |
| 118 | SubType_Chimera |
| 119 | SubType_Cockatrice |
| 120 | SubType_Crab |
| 121 | SubType_Curse |
| 122 | SubType_Cyclops |
| 123 | SubType_Desert |
| 124 | SubType_Devil |
| 125 | SubType_Dominaria |
| 126 | SubType_Domri |
| 127 | SubType_Dreadnought |
| 128 | SubType_Drone |
| 129 | SubType_Dryad |
| 130 | SubType_Dwarf |
| 131 | SubType_Efreet |
| 132 | SubType_Elder |
| 133 | SubType_Eldrazi |
| 134 | SubType_Elk |
| 135 | SubType_Elspeth |
| 136 | SubType_Equilor |
| 137 | SubType_Ergamon |
| 138 | SubType_Eye |
| 139 | SubType_Fabacin |
| 140 | SubType_Faerie |
| 141 | SubType_Ferret |
| 142 | SubType_Flagbearer |
| 143 | SubType_Fortification |
| 144 | SubType_Fox |
| 145 | SubType_Frog |
| 146 | SubType_Fungus |
| 147 | SubType_Gargoyle |
| 148 | SubType_Gnome |
| 149 | SubType_Goat |
| 150 | SubType_God |
| 151 | SubType_Gorgon |
| 152 | SubType_Gremlin |
| 153 | SubType_Hag |
| 154 | SubType_Harpy |
| 155 | SubType_Hellion |
| 156 | SubType_Hippo |
| 157 | SubType_Hippogriff |
| 158 | SubType_Homarid |
| 159 | SubType_Homunculus |
| 160 | SubType_Horror |
| 161 | SubType_Hyena |
| 162 | SubType_Imp |
| 163 | SubType_Incarnation |
| 164 | SubType_Innistrad |
| 165 | SubType_Iquatana |
| 166 | SubType_Ir |
| 167 | SubType_Jellyfish |
| 168 | SubType_Juggernaut |
| 169 | SubType_Kaldheim |
| 170 | SubType_Kamigawa |
| 171 | SubType_Karn |
| 172 | SubType_Karsus |
| 173 | SubType_Kavu |
| 174 | SubType_Kephalai |
| 175 | SubType_Kirin |
| 176 | SubType_Kithkin |
| 177 | SubType_Kobold |
| 178 | SubType_Kolbahan |
| 179 | SubType_Kor |
| 180 | SubType_Koth |
| 181 | SubType_Kraken |
| 182 | SubType_Kyneth |
| 183 | SubType_Lair |
| 184 | SubType_Lammasu |
| 185 | SubType_Leech |
| 186 | SubType_Leviathan |
| 187 | SubType_Lhurgoyf |
| 188 | SubType_Licid |
| 189 | SubType_Liliana |
| 190 | SubType_Lizard |
| 191 | SubType_Locus |
| 192 | SubType_Lorwyn |
| 193 | SubType_Luvion |
| 194 | SubType_Manticore |
| 195 | SubType_Masticore |
| 196 | SubType_Meditation |
| 197 | SubType_Mercadia |
| 198 | SubType_Mercenary |
| 199 | SubType_Metathran |
| 200 | SubType_Mirrodin |
| 201 | SubType_Moag |
| 202 | SubType_Monger |
| 203 | SubType_Mongoose |
| 204 | SubType_Mongseng |
| 205 | SubType_Moonfolk |
| 206 | SubType_Muraganda |
| 207 | SubType_Mutant |
| 208 | SubType_Myr |
| 209 | SubType_Mystic |
| 210 | SubType_Nautilus |
| 211 | SubType_Nephilim |
| 212 | SubType_New |
| 213 | SubType_Nightmare |
| 214 | SubType_Nightstalker |
| 215 | SubType_Ninja |
| 216 | SubType_Nissa |
| 217 | SubType_Noggle |
| 218 | SubType_Nomad |
| 219 | SubType_Nymph |
| 220 | SubType_Octopus |
| 221 | SubType_Orc |
| 222 | SubType_Orgg |
| 223 | SubType_Ouphe |
| 224 | SubType_Ox |
| 225 | SubType_Oyster |
| 226 | SubType_Phelddagrif |
| 227 | SubType_Phyrexia |
| 228 | SubType_Pirate |
| 229 | SubType_Plant |
| 230 | SubType_Praetor |
| 231 | SubType_Pyrulea |
| 232 | SubType_Rabbit |
| 233 | SubType_Rabiah |
| 234 | SubType_Ral |
| 235 | SubType_Rat |
| 236 | SubType_Rath |
| 237 | SubType_Ravnica |
| 238 | SubType_Realm |
| 239 | SubType_Rebel |
| 240 | SubType_Regatha |
| 241 | SubType_Rigger |
| 242 | SubType_Sable |
| 243 | SubType_Sarkhan |
| 244 | SubType_Satyr |
| 245 | SubType_Scarecrow |
| 246 | SubType_Scorpion |
| 247 | SubType_Segovia |
| 248 | SubType_Serras |
| 249 | SubType_Shadowmoor |
| 250 | SubType_Shandalar |
| 251 | SubType_Shapeshifter |
| 252 | SubType_Sheep |
| 253 | SubType_Shrine |
| 254 | SubType_Slith |
| 255 | SubType_Slug |
| 256 | SubType_Snake |
| 257 | SubType_Soltari |
| 258 | SubType_Spawn |
| 259 | SubType_Specter |
| 260 | SubType_Spellshaper |
| 261 | SubType_Spike |
| 262 | SubType_Sponge |
| 263 | SubType_Squid |
| 264 | SubType_Squirrel |
| 265 | SubType_Starfish |
| 266 | SubType_Surrakar |
| 267 | SubType_Tamiyo |
| 268 | SubType_Tezzeret |
| 269 | SubType_Thopter |
| 270 | SubType_Thrull |
| 271 | SubType_Tibalt |
| 272 | SubType_Trap |
| 273 | SubType_Turtle |
| 274 | SubType_Ulgrotha |
| 275 | SubType_Unicorn |
| 276 | SubType_Valla |
| 277 | SubType_Venser |
| 279 | SubType_Volver |
| 280 | SubType_Vraska |
| 281 | SubType_Vryn |
| 282 | SubType_Weird |
| 283 | SubType_Werewolf |
| 284 | SubType_Whale |
| 285 | SubType_Wildfire |
| 286 | SubType_Wolverine |
| 287 | SubType_Wombat |
| 288 | SubType_Worm |
| 289 | SubType_Wraith |
| 290 | SubType_Xenagos |
| 291 | SubType_Xerex |
| 292 | SubType_Yeti |
| 293 | SubType_Zendikar |
| 294 | SubType_Zubera |
| 295 | SubType_Germ |
| 296 | SubType_Contraption |
| 297 | SubType_Citizen |
| 298 | SubType_Coward |
| 299 | SubType_Deserter |
| 300 | SubType_Prism |
| 301 | SubType_Reflection |
| 302 | SubType_Sand |
| 303 | SubType_Serf |
| 304 | SubType_Dack |
| 305 | SubType_Kiora |
| 306 | SubType_AllCreatureTypes |
| 307 | SubType_Blinkmoth |
| 308 | SubType_Camarid |
| 309 | SubType_Caribou |
| 310 | SubType_Graveborn |
| 311 | SubType_Lamia |
| 312 | SubType_Orb |
| 313 | SubType_Pentavite |
| 314 | SubType_Pincher |
| 315 | SubType_Splinter |
| 316 | SubType_Survivor |
| 317 | SubType_Tetravite |
| 318 | SubType_Triskelavite |
| 319 | SubType_Scion |
| 320 | SubType_Processor |
| 321 | SubType_Arlinn |
| 322 | SubType_Mole |
| 323 | SubType_Nahiri |
| 324 | SubType_Clue |
| 325 | SubType_Teferi |
| 326 | SubType_Daretti |
| 327 | SubType_Freyalise |
| 328 | SubType_Nixilis |
| 329 | SubType_Narset |
| 330 | SubType_Ugin |
| 331 | SubType_Vehicle |
| 332 | SubType_Servo |
| 333 | SubType_Dovin |
| 334 | SubType_Saheeli |
| 335 | SubType_Monkey |
| 336 | SubType_Aetherborn |
| 337 | SubType_Pilot |
| 338 | SubType_Jackal |
| 340 | SubType_Cartouche |
| 341 | SubType_Samut |
| 342 | SubType_Dinosaur |
| 343 | SubType_Treasure |
| 344 | SubType_Huatli |
| 345 | SubType_Angrath |
| 346 | SubType_Trilobite |
| 347 | SubType_Saga |
| 348 | SubType_Jaya |
| 349 | SubType_Vivien |
| 350 | SubType_Egg |
| 351 | SubType_Kaya |
| 352 | SubType_Aminatou |
| 353 | SubType_Estrid |
| 354 | SubType_Rowan |
| 355 | SubType_Will |
| 356 | SubType_Windgrace |
| 357 | SubType_Yanggu |
| 358 | SubType_Yanling |
| 359 | SubType_Army |
| 360 | SubType_Teyo |
| 361 | SubType_Kasmina |
| 362 | SubType_Davriel |
| 363 | SubType_Food |
| 364 | SubType_Mouse |
| 365 | SubType_Noble |
| 366 | SubType_Peasant |
| 367 | SubType_Oko |
| 368 | SubType_Warlock |
| 369 | SubType_Adventure |
| 370 | SubType_Demigod |
| 371 | SubType_Gold |
| 372 | SubType_Tentacle |
| 373 | SubType_Azra |
| 374 | SubType_Pangolin |
| 375 | SubType_Calix |
| 376 | SubType_Lukka |
| 377 | SubType_Otter |
| 378 | SubType_Shark |
| 379 | SubType_Dog |
| 380 | SubType_Basri |
| 381 | SubType_Rune |
| 382 | SubType_Tyvar |
| 383 | SubType_Phyrexian |
| 384 | SubType_Niko |
| 385 | SubType_Shard |
| 386 | SubType_Inkling |
| 387 | SubType_Fractal |
| 388 | SubType_Lesson |
| 389 | SubType_Bard |
| 390 | SubType_Beholder |
| 391 | SubType_Gnoll |
| 392 | SubType_Hamster |
| 393 | SubType_Halfling |
| 394 | SubType_Tiefling |
| 395 | SubType_Bahamut |
| 396 | SubType_Ellywick |
| 397 | SubType_Lolth |
| 398 | SubType_Mordenkainen |
| 399 | SubType_Zariel |
| 400 | SubType_Class |
| 401 | SubType_Ranger |
| 402 | SubType_Wrenn |
| 403 | SubType_Blood |
| 404 | SubType_Serra |
| 405 | SubType_Kaito |
| 406 | SubType_Raccoon |
| 407 | SubType_Gith |
| 408 | SubType_Minsc |
| 409 | SubType_Tasha |
| 410 | SubType_Walrus |
| 411 | SubType_Powerstone |
| 412 | SubType_Urza |
| 413 | SubType_Mite |
| 414 | SubType_Sphere |
| 415 | SubType_Incubator |
| 416 | SubType_Siege |
| 417 | SubType_Role |
| 418 | SubType_Capybara |
| 419 | SubType_Cave |
| 420 | SubType_Llama |
| 421 | SubType_Map |
| 422 | SubType_Quintorius |
| 423 | SubType_Snail |
| 424 | SubType_Case |
| 425 | SubType_Detective |
| 426 | SubType_Grist |
| 427 | SubType_Chorus |
| 428 | SubType_Armadillo |
| 429 | SubType_Beaver |
| 430 | SubType_Coyote |
| 431 | SubType_Mount |
| 432 | SubType_Porcupine |
| 433 | SubType_Possum |
| 434 | SubType_Varmint |
| 435 | SubType_Sloth |
| 436 | SubType_Skunk |
| 437 | SubType_Weasel |
| 438 | SubType_Room |
| 439 | SubType_Balloon |
| 440 | SubType_Clown |
| 441 | SubType_Doctor |
| 442 | SubType_Glimmer |
| 443 | SubType_Toy |
| 444 | SubType_Robot |
| 445 | SubType_Seal |
| 446 | SubType_Omen |
| 447 | SubType_Alien |
| 448 | SubType_Hero |
| 449 | SubType_Moogle |
| 450 | SubType_Qu |
| 451 | SubType_Town |
| 452 | SubType_Comet |
| 453 | SubType_Drix |
| 454 | SubType_Lander |
| 455 | SubType_Lobster |
| 456 | SubType_Planet |
| 457 | SubType_Scientist |
| 458 | SubType_Spacecraft |
| 459 | SubType_Bison |
| 460 | SubType_Lemur |
| 461 | SubType_Kangaroo |
| 462 | SubType_Performer |
| 463 | SubType_Platypus |
| 464 | SubType_Symbiote |
| 465 | SubType_Villain |
| 466 | SubType_Sorcerer |
| 468 | SubType_Stone |
| 469 | SubType_Mutagen |
| 470 | SubType_Utrom |
| 471 | SubType_Book |
| 472 | SubType_Dellian |
| 473 | SubType_Giraffe |
| 478 | SubType_Plan |
| 479 | SubType_Spy |
| 481 | SubType_Infinity |
| 482 | SubType_Eternal |
| 483 | SubType_Gamma |
| 484 | SubType_Inhuman |
| 485 | SubType_Kree |
| 486 | SubType_Skrull |
| 487 | SubType_Vibranium |
| 488 | SubType_Shiar |
| 489 | SubType_Heartwood |
| 490 | SubType_Sculpture |
| 491 | SubType_PlaceholderSubType491 |
| 492 | SubType_PlaceholderSubType492 |
| 493 | SubType_PlaceholderSubType493 |
| 494 | SubType_PlaceholderSubType494 |
| 495 | SubType_PlaceholderSubType495 |
| 496 | SubType_PlaceholderSubType496 |
| 497 | SubType_PlaceholderSubType497 |
| 498 | SubType_PlaceholderSubType498 |
| 499 | SubType_PlaceholderSubType499 |
| 500 | SubType_PlaceholderSubType500 |
| 501 | SubType_PlaceholderSubType501 |
| 502 | SubType_PlaceholderSubType502 |
| 503 | SubType_PlaceholderSubType503 |
| 504 | SubType_PlaceholderSubType504 |
| 505 | SubType_PlaceholderSubType505 |
| 506 | SubType_PlaceholderSubType506 |
| 507 | SubType_PlaceholderSubType507 |
| 508 | SubType_PlaceholderSubType508 |
| 509 | SubType_PlaceholderSubType509 |
| 510 | SubType_PlaceholderSubType510 |
| 511 | SubType_PlaceholderSubType511 |
| 512 | SubType_PlaceholderSubType512 |
| 513 | SubType_PlaceholderSubType513 |
| 514 | SubType_PlaceholderSubType514 |
| 515 | SubType_PlaceholderSubType515 |
| 516 | SubType_PlaceholderSubType516 |
| 517 | SubType_PlaceholderSubType517 |
| 518 | SubType_PlaceholderSubType518 |
| 519 | SubType_PlaceholderSubType519 |
| 520 | SubType_PlaceholderSubType520 |
| 521 | SubType_PlaceholderSubType521 |
| 522 | SubType_PlaceholderSubType522 |
| 523 | SubType_PlaceholderSubType523 |
| 524 | SubType_PlaceholderSubType524 |
| 525 | SubType_PlaceholderSubType525 |
| 526 | SubType_PlaceholderSubType526 |
| 527 | SubType_PlaceholderSubType527 |
| 528 | SubType_PlaceholderSubType528 |
| 529 | SubType_PlaceholderSubType529 |
| 530 | SubType_PlaceholderSubType530 |
| 531 | SubType_PlaceholderSubType531 |
| 532 | SubType_PlaceholderSubType532 |
| 533 | SubType_PlaceholderSubType533 |
| 534 | SubType_PlaceholderSubType534 |
| 535 | SubType_PlaceholderSubType535 |
| 536 | SubType_PlaceholderSubType536 |
| 537 | SubType_PlaceholderSubType537 |
| 538 | SubType_PlaceholderSubType538 |
| 539 | SubType_PlaceholderSubType539 |
| 540 | SubType_PlaceholderSubType540 |

### wotc.mtgo.gre.external.messaging.SubZoneType

| Value | Name |
|---:|---|
| 0 | SubZoneType_None |
| 1 | SubZoneType_Top |
| 2 | SubZoneType_Bottom |

### wotc.mtgo.gre.external.messaging.SuperFormat

| Value | Name |
|---:|---|
| 0 | SuperFormat_None |
| 1 | SuperFormat_Limited |
| 2 | SuperFormat_Constructed |

### wotc.mtgo.gre.external.messaging.SuperType

| Value | Name |
|---:|---|
| 0 | SuperType_None |
| 1 | SuperType_Basic |
| 2 | SuperType_Legendary |
| 3 | SuperType_Ongoing |
| 4 | SuperType_Snow |
| 5 | SuperType_World |

### wotc.mtgo.gre.external.messaging.SyntheticEventType

| Value | Name |
|---:|---|
| 0 | SyntheticEventType_None |
| 1 | SyntheticEventType_PlayerLostLifeFirstTimeThisTurn |
| 2 | SyntheticEventType_PlaneswalkerControlConditionMet |
| 3 | SyntheticEventType_Escaped |

### wotc.mtgo.gre.external.messaging.TargetType

| Value | Name |
|---:|---|
| 0 | TargetType_None |
| 1 | TargetType_Player |
| 2 | TargetType_GameObject |

### wotc.mtgo.gre.external.messaging.TeamStatus

| Value | Name |
|---:|---|
| 0 | TeamStatus_None |
| 1 | TeamStatus_InGame |
| 2 | TeamStatus_Removed |
| 3 | TeamStatus_PendingLoss |

### wotc.mtgo.gre.external.messaging.TeamType

| Value | Name |
|---:|---|
| 0 | TeamType_None |
| 1 | TeamType_Individual |
| 2 | TeamType_SharedTeam |

### wotc.mtgo.gre.external.messaging.TimeoutType

| Value | Name |
|---:|---|
| 0 | TimeoutType_None |
| 1 | TimeoutType_ChessClock |
| 2 | TimeoutType_Inactivity |

### wotc.mtgo.gre.external.messaging.TimerBehavior

| Value | Name |
|---:|---|
| 0 | TimerBehavior_None |
| 1 | TimerBehavior_Timeout |
| 2 | TimerBehavior_AutoRespond |
| 3 | TimerBehavior_TakeControl |
| 4 | TimerBehavior_ReleaseControl |
| 5 | TimerBehavior_Start |
| 6 | TimerBehavior_Restart |
| 7 | TimerBehavior_Stop |
| 8 | TimerBehavior_Reset |
| 9 | TimerBehavior_Activate |
| 10 | TimerBehavior_Deactivate |
| 11 | TimerBehavior_StartDelayedTimer |

### wotc.mtgo.gre.external.messaging.TimerEvent

| Value | Name |
|---:|---|
| 0 | TimerEvent_None |
| 1 | TimerEvent_OnRequestSent |
| 2 | TimerEvent_OnResponseReceived |
| 3 | TimerEvent_OnExpiration |
| 4 | TimerEvent_OnMatchStart |
| 5 | TimerEvent_OnGameStart |
| 6 | TimerEvent_OnPlayStart |
| 7 | TimerEvent_OnTurnChanged |
| 8 | TimerEvent_OnActivePlayer |
| 9 | TimerEvent_OnNonActivePlayer |
| 10 | TimerEvent_Beginning |
| 11 | TimerEvent_Main1 |
| 12 | TimerEvent_Combat |
| 13 | TimerEvent_Main2 |
| 14 | TimerEvent_Ending |
| 15 | TimerEvent_OnWin |
| 16 | TimerEvent_OnLoss |
| 17 | TimerEvent_OnDraw |
| 18 | TimerEvent_OnGameOver |
| 19 | TimerEvent_OnMatchOver |
| 20 | TimerEvent_OnTurnOver |
| 21 | TimerEvent_Upkeep |
| 22 | TimerEvent_Draw |
| 23 | TimerEvent_BeginCombat |
| 24 | TimerEvent_DeclareAttack |
| 25 | TimerEvent_DeclareBlock |
| 26 | TimerEvent_CombatDamage |
| 27 | TimerEvent_EndCombat |
| 28 | TimerEvent_End |
| 29 | TimerEvent_Cleanup |
| 30 | TimerEvent_FirstStrikeDamage |

### wotc.mtgo.gre.external.messaging.TimerPackage

| Value | Name |
|---:|---|
| 0 | TimerPackage_None |
| 1 | TimerPackage_V1 |
| 2 | TimerPackage_V2 |
| 3 | TimerPackage_V3 |
| 4 | TimerPackage_V4 |
| 5 | TimerPackage_V5 |
| 6 | TimerPackage_DirectChallenge |
| 7 | TimerPackage_Default |
| 8 | TimerPackage_BestOfThree |
| 9 | TimerPackage_BestOfOneMatchClock |
| 10 | TimerPackage_BestOfThreeAdditionalTimeouts |
| 11 | TimerPackage_BestOfOnePoweredCube |
| 12 | TimerPackage_BestOfThreePoweredCube |

### wotc.mtgo.gre.external.messaging.TimerType

| Value | Name |
|---:|---|
| 0 | TimerType_None |
| 1 | TimerType_Decision |
| 2 | TimerType_Inactivity |
| 3 | TimerType_ActivePlayer |
| 4 | TimerType_NonActivePlayer |
| 5 | TimerType_Prologue |
| 6 | TimerType_Epilogue |
| 7 | TimerType_Delay |
| 8 | TimerType_MatchClock |

### wotc.mtgo.gre.external.messaging.TypeCategory

| Value | Name |
|---:|---|
| 0 | TypeCategory_None |
| 3 | TypeCategory_Color |
| 5 | TypeCategory_CardType |
| 6 | TypeCategory_SubType |
| 7 | TypeCategory_SuperType |

### wotc.mtgo.gre.external.messaging.TypeKind

| Value | Name |
|---:|---|
| 0 | TypeKind_None |
| 1 | TypeKind_Land |
| 2 | TypeKind_Nonland |
| 3 | TypeKind_NonlandPermanent |

### wotc.mtgo.gre.external.messaging.Visibility

| Value | Name |
|---:|---|
| 0 | Visibility_None |
| 1 | Visibility_Public |
| 2 | Visibility_Private |
| 3 | Visibility_Hidden |
| 4 | Visibility_Deceptive |

### wotc.mtgo.gre.external.messaging.WellKnownCatalogId

| Value | Name |
|---:|---|
| 0 | WellKnownCatalogId_None |
| 1 | WellKnownCatalogId_Token |
| 2 | WellKnownCatalogId_Emblem |
| 3 | WellKnownCatalogId_StandardCardBack |
| 4 | WellKnownCatalogId_Obscured |
| 5 | WellKnownCatalogId_DelayedTriggerAbilityHolder |
| 6 | WellKnownCatalogId_Boon |

### wotc.mtgo.gre.external.messaging.ZoneType

| Value | Name |
|---:|---|
| 0 | ZoneType_None |
| 1 | ZoneType_Library |
| 2 | ZoneType_Hand |
| 3 | ZoneType_Battlefield |
| 4 | ZoneType_Stack |
| 5 | ZoneType_Graveyard |
| 6 | ZoneType_Exile |
| 7 | ZoneType_Command |
| 8 | ZoneType_Revealed |
| 9 | ZoneType_Limbo |
| 10 | ZoneType_Sideboard |
| 11 | ZoneType_Pending |
| 12 | ZoneType_PhasedOut |
| 13 | ZoneType_Suppressed |

## Source hashes

- `Wizards.MDN.GreProtobuf.dll`: `0170f35ec3af708871d41d6a6e939f8faf5972a18691f8c7ac168f3458bfc928`
- `Wizards.Arena.Models.Protobuf.dll`: `add9ae06a7acc246c73d6b1dedbe99cc36c20463a6e618d5e106145af2a0239f`
