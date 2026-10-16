# Arena request-to-menu matching guide

Source: the installed client DLLs, fingerprint checked against the extracted protocol. This guide distinguishes declared protocol values, registered handlers, and recovered menu-routing logic. No new matches or UI tests were run. Asset lookup trees can further alter layout and text; source-card and current-state data matter.

## Start here

A request name is not a menu name. Match the incoming type, payload, context/list/ID types, recipient seat, source card and resolution state. Follow routing conditions in order: the first matching branch wins. Prompt IDs supply localized content and sometimes select a special workflow; changing only the prompt does not generally choose a menu.

## Practical menu recipes

| Intended interaction | Request | Selecting condition | Native workflow | Notes |
|---|---|---|---|---|
| Scry: top versus bottom | GroupReq | context=Scry (1) | ScryWorkflow | Returns GroupResp with Library/Top and Library/Bottom groups. This is the drag-across-the-library interaction. |
| Surveil: top versus graveyard | GroupReq | context=Surveil (2) | SurveilWorkflow | Returns GroupResp; graveyard is a different destination from library bottom. |
| London mulligan bottoming | GroupReq | context=LondonMulligan (3) | LondonWorkflow | The mulligan grouping workflow uses the request group constraints. |
| Generic divide-into-piles | GroupReq | context=None (0), with group specs | GroupWorkflow | Liliana ultimate has a separate battlefield-pile path selected from the resolving card. |
| Reorder top of library | OrderReq | orderingContext=OrderingForTop (2) | OrderWorkflow | Only changes relative order among the supplied cards. Cannot move a card to the other end of the library. |
| Reorder bottom of library | OrderReq | orderingContext=OrderingForBottom (1) | OrderWorkflow | May auto-submit when full control is disabled and the local library has more than 10 cards. |
| Ordinary ordering | OrderReq | orderingContext=None (0) | OrderWorkflow | No library placeholder; arrange the supplied items. |
| Triggered abilities | SelectNReq | optionContext=Stacking AND context=TriggeredAbility | TriggeredAbilityWorkflow | Other stacking selections take SelectNWorkflow_Stacking instead. |
| Mana in the pool | SelectNReq | context=ManaPool | SelectNWorkflow_ManaPool | Uses mana entries rather than card instance IDs. |
| Choose a card color | SelectNReq | listType=Static or StaticSubset; staticList=Colors or CardColors | SelectColorWorkflow | Earlier SelectN routing branches still take precedence. |
| Choose a mana color | SelectNReq | context=ManaFromAbility OR staticList=ManaColors | SelectColorWorkflow | Provide the corresponding offered colors. |
| Choose a basic land type | SelectNReq | context=Resolution; optionContext=Resolution; listType=Static; staticList=BasicLandTypes | SelectBasicLandWorkflow | Static subtype variant uses exactly the five basic-land subtype IDs; see SelectNRequest predicates. |
| Text choice: prompt buttons | SelectNReq | Choice selection; minSel=maxSel=1; two choices with no cancellation, or one choice with AllowCancel.Continue | ChoicePromptButtonsWorkflow | This branch is checked before the other choice layouts. |
| Text choice: small button menu | SelectNReq | Choice selection; prior prompt-button rule did not match; at most 6 choices; minSel=maxSel=1 | ChoiceButtonsWorkflow | Dynamic text choices use PromptParameterIndex IDs and matching prompt parameters. |
| Text choice: list | SelectNReq | Choice selection; prior button rules did not match; more than 3 sorted choices | ChoiceSelectionWorkflow | Not every selection with many card IDs is a text-choice list. |
| Text choice: modal cards | SelectNReq | Choice selection; all earlier choice-layout rules did not match | ChoiceModalWorkflow | At most 3 sorted choices at this point. Preserve legal selection counts and option descriptions. |
| Choose exactly one counter | SelectCountersReq | minSelect=1; maxSelect=1 | SelectCounterWorkflow | Different from choosing a counter TYPE through a static SelectN list. |
| Choose multiple counters | SelectCountersReq | Other minSelect/maxSelect combinations | SelectCountersWorkflow | Uses counter distribution and selection helpers. |
| Distribute a fresh amount | DistributionReq | No existingDistributions | DistributionWorkflow | Uses the supplied amount, recipients, and minimum-per-target constraints. |
| Edit an existing distribution | DistributionReq | existingDistributions is nonempty | DistributionWithExistingValuesWorkflow | Starts with existing allocated amounts. |
| Gather an exact amount | GatherReq | amountToGather != 0 | GatherWorkflow_RemoveExact | Uses sources and destinationId; response contains Gatherings. |
| Gather any number | GatherReq | amountToGather=0 | GatherWorkflow_AnyNumber | Separate from distributing an amount outward. |
| Choose a number | NumericInputReq | Effective prompt ID is not 166 | NumericInputWorkflow | Use numeric bounds/options supplied by the request. |
| Read ahead | NumericInputReq | Effective prompt.promptId=166 | ReadAheadWorkflow | Uses the specific read-ahead workflow; not an arbitrary generic number picker. |
| Select a pile | SelectNGroupReq | Default path | SelectNGroupWorkflow | Liliana ultimate and Hostile Negotiations have special paths based on source/state. |
| Choose starting player | ChooseStartingPlayerReq | Dedicated request | ChooseStartingPlayerWorkflow | Player choice workflow. |
| Choose replacement effect | SelectReplacementReq | Default path | SelectReplacementWorkflow | The client has an additional TurtleHurtle special-case route. |
| Choose casting options | CastingTimeOptionsReq | Nested child request type/options | CastingTimeOptionWorkflow | Child branches include X, kicker, timing permission, SelectN, modes, either/or costs, cost keywords, specialize, additional costs, and replicate. |
| Search a zone | SearchReq | Dedicated search payload | SearchWorkflow | Keep search semantics, legal IDs and zone information; do not substitute a generic choice just because it displays cards. |
| Search grouped cards | SearchFromGroupsReq | Dedicated grouped search payload | SearchFromGroupsWorkflow | Distinct request from SelectFromGroupsReq and from grouping cards into piles. |
| Select from grouped cards | SelectFromGroupsReq | Dedicated grouped selection payload | SelectFromGroupsWorkflow | Additional browser variants are selected by zones and request/state. |
| Optional action | OptionalActionMessage | Action/request data plus source and asset lookup rules | OptionalActionMessageWorkflow | The client can instead choose Explore, scry-like, special browser, or card-button layouts. See routing code. |

## Every incoming message

Messages with no handler recovered are not advertised here as working menus. A registered translator alone also does not establish that a visible workflow exists. Workflow candidates below include helper/sub-workflows; they are not independent wire-message types.

| ID | Incoming message | Registered handler | Routing entry |
|---:|---|---|---|
| 1 | GameStateMessage | GreClient.Rules.GreInterface.ProcessMessage | State/notification or no menu route recovered |
| 2 | ActionsAvailableReq | ActionsAvailableRequestTranslator | ActionsAvailableTranslation |
| 6 | ChooseStartingPlayerReq | ChooseStartingPlayerRequestTranslator | ChooseStartingPlayerTranslation |
| 7 | ConnectResp | ConnectResponseNotificationParser | State/notification or no menu route recovered |
| 9 | GetSettingsResp | GetSettingsResponseTranslator | State/notification or no menu route recovered |
| 10 | SetSettingsResp | SetSettingsResponseTranslator | State/notification or no menu route recovered |
| 11 | GroupReq | GroupRequestTranslator | GroupTranslation |
| 12 | IllegalRequest | IllegalRequestNotificationParser | State/notification or no menu route recovered |
| 15 | MulliganReq | MulliganRequestTranslator | MulliganTranslation |
| 17 | OrderReq | OrderRequestTranslator | OrderTranslation |
| 18 | PromptReq | PromptRequestTranslator | State/notification or no menu route recovered |
| 21 | RevealHandReq | Not recovered | State/notification or no menu route recovered |
| 22 | SelectNReq | SelectNRequestTranslator | SelectNWorkflowTranslation |
| 24 | AllowForceDraw | Not recovered | State/notification or no menu route recovered |
| 25 | BinaryGameState | BinaryGameStateTranslator | State/notification or no menu route recovered |
| 26 | DeclareAttackersReq | DeclareAttackersRequestTranslator | DeclareAttackersTranslation |
| 27 | SubmitAttackersResp | Not recovered | State/notification or no menu route recovered |
| 28 | DeclareBlockersReq | DeclareBlockersRequestTranslator | DeclareBlockersTranslation |
| 29 | SubmitBlockersResp | Not recovered | State/notification or no menu route recovered |
| 30 | AssignDamageReq | AssignDamageRequestTranslator | AssignDamageTranslation |
| 31 | AssignDamageConfirmation | Not recovered | State/notification or no menu route recovered |
| 32 | OrderCombatDamageReq | Not recovered | State/notification or no menu route recovered |
| 33 | OrderDamageConfirmation | Not recovered | State/notification or no menu route recovered |
| 34 | SelectTargetsReq | SelectTargetsRequestTranslator | SelectTargetsTranslation |
| 35 | SubmitTargetsResp | Not recovered | State/notification or no menu route recovered |
| 36 | PayCostsReq | PayCostsRequestTranslator | PayCostsTranslation |
| 37 | IntermissionReq | IntermissionRequestTranslator | IntermissionTranslation |
| 38 | DieRollResultsResp | Not recovered | State/notification or no menu route recovered |
| 39 | SelectReplacementReq | SelectReplacementRequestTranslator | SelectReplacementTranslation |
| 40 | SelectNGroupReq | SelectNGroupRequestTranslator | SelectNGroupTranslation |
| 42 | DistributionReq | DistributionRequestTranslator | DistributionTranslation |
| 43 | NumericInputReq | NumericInputRequestTranslator | NumericInputTranslation |
| 44 | SearchReq | SearchRequestTranslator | SearchTranslation |
| 45 | OptionalActionMessage | OptionalActionRequestTranslator | OptionalActionTranslation |
| 46 | CastingTimeOptionsReq | CastingTimeOptionsRequestTranslator | CastingTimeOptionTranslation |
| 48 | SelectFromGroupsReq | SelectFromGroupsRequestTranslator | SelectFromGroupsWorkflowTranslation |
| 49 | SearchFromGroupsReq | SearchFromGroupsRequestTranslator | SearchFromGroupsTranslation |
| 50 | GatherReq | GatherRequestTranslator | GatherTranslation |
| 51 | QueuedGameStateMessage | GreClient.Rules.GreInterface.ProcessMessage | State/notification or no menu route recovered |
| 52 | UIMessage | UiMessageTranslator | State/notification or no menu route recovered |
| 53 | SubmitDeckReq | SubmitDeckRequestTranslator | SubmitDeckTranslation |
| 54 | EdictalMessage | EdictalMessageNotificationParser | State/notification or no menu route recovered |
| 55 | TimeoutMessage | TimeoutMessageNotificationParser | State/notification or no menu route recovered |
| 56 | TimerStateMessage | TimerStateMessageNotificationParser | State/notification or no menu route recovered |
| 57 | SubmitDeckConfirmation | Not recovered | State/notification or no menu route recovered |
| 58 | StringInputReq | StringInputRequestTranslator | State/notification or no menu route recovered |
| 59 | SelectCountersReq | SelectCountersRequestTranslator | SelectCountersTranslation |
| 60 | PredictionResp | Not recovered | State/notification or no menu route recovered |

### GameStateMessage (1)

Handler: [GreClient.Rules.GreInterface.ProcessMessage](handler-sources/GreClient.Rules.GreInterface.cs).
- Internal request, predicates and response construction: [GreClient.Rules.ActionsAvailableRequestTranslator.cs](handler-sources/GreClient.Rules.ActionsAvailableRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.AssignDamageRequestTranslator.cs](handler-sources/GreClient.Rules.AssignDamageRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.CastingTimeOptionsRequestTranslator.cs](handler-sources/GreClient.Rules.CastingTimeOptionsRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.ChooseStartingPlayerRequestTranslator.cs](handler-sources/GreClient.Rules.ChooseStartingPlayerRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.DeclareAttackersRequestTranslator.cs](handler-sources/GreClient.Rules.DeclareAttackersRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.DeclareBlockersRequestTranslator.cs](handler-sources/GreClient.Rules.DeclareBlockersRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.DistributionRequestTranslator.cs](handler-sources/GreClient.Rules.DistributionRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.GatherRequestTranslator.cs](handler-sources/GreClient.Rules.GatherRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.GroupRequestTranslator.cs](handler-sources/GreClient.Rules.GroupRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.Notifications.IllegalRequestNotificationParser.cs](handler-sources/GreClient.Rules.Notifications.IllegalRequestNotificationParser.cs)
- Internal request, predicates and response construction: [GreClient.Rules.IntermissionRequestTranslator.cs](handler-sources/GreClient.Rules.IntermissionRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.MulliganRequestTranslator.cs](handler-sources/GreClient.Rules.MulliganRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.NumericInputRequestTranslator.cs](handler-sources/GreClient.Rules.NumericInputRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.OptionalActionRequestTranslator.cs](handler-sources/GreClient.Rules.OptionalActionRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.OrderRequestTranslator.cs](handler-sources/GreClient.Rules.OrderRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.PayCostsRequestTranslator.cs](handler-sources/GreClient.Rules.PayCostsRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.PromptRequestTranslator.cs](handler-sources/GreClient.Rules.PromptRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.RequestAnnotationProcessor.cs](handler-sources/GreClient.Rules.RequestAnnotationProcessor.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SearchFromGroupsRequestTranslator.cs](handler-sources/GreClient.Rules.SearchFromGroupsRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SearchRequestTranslator.cs](handler-sources/GreClient.Rules.SearchRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SelectCountersRequestTranslator.cs](handler-sources/GreClient.Rules.SelectCountersRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SelectFromGroupsRequestTranslator.cs](handler-sources/GreClient.Rules.SelectFromGroupsRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SelectNGroupRequestTranslator.cs](handler-sources/GreClient.Rules.SelectNGroupRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SelectNRequestTranslator.cs](handler-sources/GreClient.Rules.SelectNRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SelectReplacementRequestTranslator.cs](handler-sources/GreClient.Rules.SelectReplacementRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SelectTargetsRequestTranslator.cs](handler-sources/GreClient.Rules.SelectTargetsRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.StringInputRequestTranslator.cs](handler-sources/GreClient.Rules.StringInputRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SubmitDeckRequestTranslator.cs](handler-sources/GreClient.Rules.SubmitDeckRequestTranslator.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | type | GameStateType |  |
| 2 | gameStateId | UINT32 |  |
| 3 | gameInfo | GameInfo |  |
| 4 | teams | TeamInfo | yes |
| 5 | players | PlayerInfo | yes |
| 6 | turnInfo | TurnInfo |  |
| 7 | zones | ZoneInfo | yes |
| 8 | gameObjects | GameObjectInfo | yes |
| 9 | annotations | AnnotationInfo | yes |
| 10 | diffDeletedInstanceIds | UINT32 | yes |
| 11 | pendingMessageCount | UINT32 |  |
| 12 | prevGameStateId | UINT32 |  |
| 13 | timers | TimerInfo | yes |
| 14 | update | GameStateUpdate |  |
| 15 | actions | ActionInfo | yes |
| 16 | persistentAnnotations | AnnotationInfo | yes |
| 17 | diffDeletedPersistentAnnotationIds | UINT32 | yes |

### ActionsAvailableReq (2)

Additional injected/alternate route: [Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.DefaultTranslation](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.DefaultTranslation.cs) → [ActionsAvailableWorkflow](ui-sources/ActionsAvailableWorkflow.cs).

Handler: [ActionsAvailableRequestTranslator](handler-sources/GreClient.Rules.ActionsAvailableRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.ActionsAvailableRequest.cs](handler-sources/GreClient.Rules.ActionsAvailableRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.ActionsAvailableTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.ActionsAvailableTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.ResolutionCastTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.ResolutionCastTranslation.cs)

Workflow implementations reachable through construction references:

- [ActionsAvailableWorkflow_Browser](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.ActionsAvailableWorkflow_Browser.cs)
- [ActionsAvailableWorkflow_MultiZoneBrowser](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.ActionsAvailableWorkflow_MultiZoneBrowser.cs)
- [CannotTakeActionWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.CannotTakeActionWorkflow.cs)
- [OpeningHandActionWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.OpeningHandActionWorkflow.cs)
- [ResolutionCastWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.ResolutionCastWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | actions | Action | yes |
| 2 | inactiveActions | Action | yes |

### ChooseStartingPlayerReq (6)

Handler: [ChooseStartingPlayerRequestTranslator](handler-sources/GreClient.Rules.ChooseStartingPlayerRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.ChooseStartingPlayerRequest.cs](handler-sources/GreClient.Rules.ChooseStartingPlayerRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.ChooseStartingPlayerTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.ChooseStartingPlayerTranslation.cs)

Workflow implementations reachable through construction references:

- [ChooseStartingPlayerWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.ChooseStartingPlayerWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | teamType | TeamType |  |
| 2 | systemSeatIds | UINT32 | yes |
| 3 | teamIds | UINT32 | yes |
| 4 | prompt | Prompt |  |

### ConnectResp (7)

Handler: [ConnectResponseNotificationParser](handler-sources/GreClient.Rules.Notifications.ConnectResponseNotificationParser.cs).

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | status | ConnectionStatus |  |
| 6 | protoVer | ProtoVersion |  |
| 8 | settings | SettingsMessage |  |
| 9 | deckMessage | DeckMessage |  |
| 10 | grpVersion | Version |  |
| 11 | greVersion | Version |  |
| 12 | skins | CardSkinTuple | yes |
| 13 | grpChangelist | UINT32 |  |
| 14 | greChangelist | UINT32 |  |

### GetSettingsResp (9)

Handler: [GetSettingsResponseTranslator](handler-sources/GreClient.Rules.GetSettingsResponseTranslator.cs).

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | settings | SettingsMessage |  |

### SetSettingsResp (10)

Handler: [SetSettingsResponseTranslator](handler-sources/GreClient.Rules.SetSettingsResponseTranslator.cs).

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | settings | SettingsMessage |  |

### GroupReq (11)

Handler: [GroupRequestTranslator](handler-sources/GreClient.Rules.GroupRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.GroupRequest.cs](handler-sources/GreClient.Rules.GroupRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.Grouping.GroupTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.Grouping.GroupTranslation.cs)

Workflow implementations reachable through construction references:

- [LondonWorkflow](ui-sources/LondonWorkflow.cs)
- [ScryWorkflow](ui-sources/ScryWorkflow.cs)
- [SurveilWorkflow](ui-sources/SurveilWorkflow.cs)
- [GroupWorkflow_BattlefieldPermanents](ui-sources/Wotc.Mtga.DuelScene.Interactions.GroupWorkflow_BattlefieldPermanents.cs)
- [GroupWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.Grouping.GroupWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | instanceIds | UINT32 | yes |
| 2 | groupSpecs | GroupSpecification | yes |
| 5 | groupType | GroupType |  |
| 6 | context | GroupingContext |  |
| 7 | sourceId | UINT32 |  |

### IllegalRequest (12)

Handler: [IllegalRequestNotificationParser](handler-sources/GreClient.Rules.Notifications.IllegalRequestNotificationParser.cs).
- Internal request, predicates and response construction: [GreClient.Rules.IllegalRequestNotification.cs](handler-sources/GreClient.Rules.IllegalRequestNotification.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | invalidMessage | ClientToGREMessage |  |
| 2 | reason | FailureReason |  |

### MulliganReq (15)

Handler: [MulliganRequestTranslator](handler-sources/GreClient.Rules.MulliganRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.MulliganRequest.cs](handler-sources/GreClient.Rules.MulliganRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.Mulligan.MulliganTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.Mulligan.MulliganTranslation.cs)

Workflow implementations reachable through construction references:

- [MulliganWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.Mulligan.MulliganWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | mulliganType | MulliganType |  |
| 2 | freeMulliganCount | UINT32 |  |
| 3 | mulliganCount | UINT32 |  |

### OrderReq (17)

Handler: [OrderRequestTranslator](handler-sources/GreClient.Rules.OrderRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.OrderRequest.cs](handler-sources/GreClient.Rules.OrderRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.OrderTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.OrderTranslation.cs)

Workflow implementations reachable through construction references:

- [OrderWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.OrderWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | ids | UINT32 | yes |
| 3 | orderingContext | OrderingContext |  |

### PromptReq (18)

Handler: [PromptRequestTranslator](handler-sources/GreClient.Rules.PromptRequestTranslator.cs).
This uses the GRE envelope prompt, not a separate PromptReq payload. Inspect the translator before treating it as a menu request.

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | promptId | UINT32 |  |
| 2 | parameters | PromptParameter | yes |

### RevealHandReq (21)

No dedicated registered handler recovered. Do not assume a working UI from the enum declaration.

### SelectNReq (22)

Handler: [SelectNRequestTranslator](handler-sources/GreClient.Rules.SelectNRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.SelectNRequest.cs](handler-sources/GreClient.Rules.SelectNRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceOptionsTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceOptionsTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceSelectionTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceSelectionTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceWithContextTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceWithContextTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.DynamicListTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.DynamicListTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflowTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflowTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.SelectZonesTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectZonesTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.SelectionWorkflowTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectionWorkflowTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.StaticCardNameTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticCardNameTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.StaticCardTypeTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticCardTypeTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.StaticCounterTypeTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticCounterTypeTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.StaticKeywordTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticKeywordTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.StaticListTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticListTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.StaticSubtypeTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticSubtypeTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.SelectN.StaticSuperTypeTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticSuperTypeTranslation.cs)

Workflow implementations reachable through construction references:

- [ChoiceButtonsWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceButtonsWorkflow.cs)
- [ChoiceModalWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceModalWorkflow.cs)
- [ChoicePromptButtonsWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.ChoicePromptButtonsWorkflow.cs)
- [ChoiceSelectionWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceSelectionWorkflow.cs)
- [ChoiceWithContextWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceWithContextWorkflow.cs)
- [DungeonRoomSelectWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.DungeonRoomSelectWorkflow.cs)
- [DungeonSelectWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.DungeonSelectWorkflow.cs)
- [LimboSelectionWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.LimboSelectionWorkflow.cs)
- [ParityWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.ParityWorkflow.cs)
- [SelectBasicLandSubtypeWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectBasicLandSubtypeWorkflow.cs)
- [SelectBasicLandWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectBasicLandWorkflow.cs)
- [SelectColorWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectColorWorkflow.cs)
- [SelectNWorkflow_Ability](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Ability.cs)
- [SelectNWorkflow_AnchorWord](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_AnchorWord.cs)
- [SelectNWorkflow_Counters](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Counters.cs)
- [SelectNWorkflow_Look](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Look.cs)
- [SelectNWorkflow_ManaPool](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_ManaPool.cs)
- [SelectNWorkflow_Printing](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Printing.cs)
- [SelectNWorkflow_Selection_BattlefieldHandBrowser](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Selection_BattlefieldHandBrowser.cs)
- [SelectNWorkflow_Selection_Browser](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Selection_Browser.cs)
- [SelectNWorkflow_Selection_DifferentCardTypes](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Selection_DifferentCardTypes.cs)
- [SelectNWorkflow_Selection_DiscardHandBrowser](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Selection_DiscardHandBrowser.cs)
- [SelectNWorkflow_Selection_SubRooms](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Selection_SubRooms.cs)
- [SelectNWorkflow_Selection_SurveilStyleBrowser](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Selection_SurveilStyleBrowser.cs)
- [SelectNWorkflow_Selection_Weighted](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Selection_Weighted.cs)
- [SelectNWorkflow_Selection_Weighted_Browser](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Selection_Weighted_Browser.cs)
- [SelectNWorkflow_Stacking](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Stacking.cs)
- [SelectNWorkflow_Wish_DEBUG](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflow_Wish_DEBUG.cs)
- [SelectPrintingIndexWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectPrintingIndexWorkflow.cs)
- [SelectZoneWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectZoneWorkflow.cs)
- [SelectZonesWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectZonesWorkflow.cs)
- [SelectionWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectionWorkflow.cs)
- [TriggeredAbilityWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.TriggeredAbilityWorkflow.cs)
- [TypeKindsWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.TypeKindsWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | minSel | INT32 |  |
| 2 | maxSel | UINT32 |  |
| 3 | context | SelectionContext |  |
| 5 | optionContext | OptionContext |  |
| 6 | listType | SelectionListType |  |
| 7 | ids | UINT32 | yes |
| 8 | weights | INT32 | yes |
| 9 | staticList | StaticList |  |
| 11 | prompt | Prompt |  |
| 12 | idType | IdType |  |
| 14 | unfiltered_ids | UINT32 | yes |
| 15 | sourceId | UINT32 |  |
| 16 | hot_ids | UINT32 | yes |
| 17 | validationType | SelectionValidationType |  |
| 18 | zone_ids | UINT32 | yes |
| 19 | shouldCancel | BOOL |  |
| 20 | minWeight | INT32 |  |
| 21 | maxWeight | INT32 |  |

### AllowForceDraw (24)

No dedicated registered handler recovered. Do not assume a working UI from the enum declaration.

### BinaryGameState (25)

Handler: [BinaryGameStateTranslator](handler-sources/GreClient.Rules.BinaryGameStateTranslator.cs).

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | gameStateMsg | GameStateMessage |  |
| 2 | instanceData | BYTES |  |
| 4 | internalGREData | BYTES |  |

### DeclareAttackersReq (26)

Handler: [DeclareAttackersRequestTranslator](handler-sources/GreClient.Rules.DeclareAttackersRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.DeclareAttackerRequest.cs](handler-sources/GreClient.Rules.DeclareAttackerRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.DeclareAttackersTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.DeclareAttackersTranslation.cs)

Workflow implementations reachable through construction references:

- [DeclareAttackersWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.DeclareAttackersWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | attackers | Attacker | yes |
| 2 | hasRequirements | BOOL |  |
| 3 | hasRestrictions | BOOL |  |
| 4 | AttackWarnings | AttackWarning | yes |
| 5 | manaCost | ManaRequirement | yes |
| 7 | qualifiedAttackers | Attacker | yes |
| 8 | canSubmitAttackers | BOOL |  |

### SubmitAttackersResp (27)

No dedicated registered handler recovered. Do not assume a working UI from the enum declaration.

### DeclareBlockersReq (28)

Handler: [DeclareBlockersRequestTranslator](handler-sources/GreClient.Rules.DeclareBlockersRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.DeclareBlockersRequest.cs](handler-sources/GreClient.Rules.DeclareBlockersRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.DeclareBlockersTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.DeclareBlockersTranslation.cs)

Workflow implementations reachable through construction references:

- [DeclareBlockersWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.DeclareBlockersWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | blockers | Blocker | yes |
| 2 | hasRequirements | BOOL |  |
| 3 | hasRestrictions | BOOL |  |
| 4 | blockWarnings | BlockWarning | yes |
| 5 | manaCost | ManaRequirement | yes |

### SubmitBlockersResp (29)

No dedicated registered handler recovered. Do not assume a working UI from the enum declaration.

### AssignDamageReq (30)

Additional injected/alternate route: [Wotc.Mtga.DuelScene.Interactions.AssignDamage.AutoAssignDamageTranslation](ui-sources/Wotc.Mtga.DuelScene.Interactions.AssignDamage.AutoAssignDamageTranslation.cs) → [Wotc.Mtga.DuelScene.Interactions.AssignDamage.AutoAssignDamageWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.AssignDamage.AutoAssignDamageWorkflow.cs).

Handler: [AssignDamageRequestTranslator](handler-sources/GreClient.Rules.AssignDamageRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.AssignDamageRequest.cs](handler-sources/GreClient.Rules.AssignDamageRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.AssignDamage.AssignDamageTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.AssignDamage.AssignDamageTranslation.cs)

Workflow implementations reachable through construction references:

- [AssignDamageWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.AssignDamage.AssignDamageWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | damageAssigners | DamageAssigner | yes |

### AssignDamageConfirmation (31)

No dedicated registered handler recovered. Do not assume a working UI from the enum declaration.

### OrderCombatDamageReq (32)

No dedicated registered handler recovered. Do not assume a working UI from the enum declaration.

### OrderDamageConfirmation (33)

No dedicated registered handler recovered. Do not assume a working UI from the enum declaration.

### SelectTargetsReq (34)

Handler: [SelectTargetsRequestTranslator](handler-sources/GreClient.Rules.SelectTargetsRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.SelectTargetsRequest.cs](handler-sources/GreClient.Rules.SelectTargetsRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.SelectTargetsTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectTargetsTranslation.cs)

Workflow implementations reachable through construction references:

- [SelectTargetsWorkflow](ui-sources/SelectTargetsWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | targets | TargetSelection | yes |
| 2 | sourceId | UINT32 |  |
| 3 | abilityGrpId | UINT32 |  |

### SubmitTargetsResp (35)

No dedicated registered handler recovered. Do not assume a working UI from the enum declaration.

### PayCostsReq (36)

Handler: [PayCostsRequestTranslator](handler-sources/GreClient.Rules.PayCostsRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.PayCostsRequest.cs](handler-sources/GreClient.Rules.PayCostsRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.PayCostsTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.PayCostsTranslation.cs)

Workflow implementations reachable through construction references:

- [PayCostWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.PayCostWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | manaCost | ManaRequirement | yes |
| 2 | paymentActions | ActionsAvailableReq |  |
| 3 | paymentSelection | SelectNReq |  |
| 4 | effectCostReq | EffectCostReq |  |
| 6 | autoTapActionsReq | AutoTapActionsAvailableReq |  |
| 7 | shouldCancel | BOOL |  |

### IntermissionReq (37)

Handler: [IntermissionRequestTranslator](handler-sources/GreClient.Rules.IntermissionRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.IntermissionRequest.cs](handler-sources/GreClient.Rules.IntermissionRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.IntermissionTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.IntermissionTranslation.cs)

Workflow implementations reachable through construction references:

- [IntermissionWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.IntermissionWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | options | UserOption | yes |
| 2 | intermissionPrompt | Prompt |  |
| 5 | result | ResultSpec |  |

### DieRollResultsResp (38)

No dedicated registered handler recovered. Do not assume a working UI from the enum declaration.

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | playerDieRolls | PlayerDieRoll | yes |

### SelectReplacementReq (39)

Handler: [SelectReplacementRequestTranslator](handler-sources/GreClient.Rules.SelectReplacementRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.SelectReplacementRequest.cs](handler-sources/GreClient.Rules.SelectReplacementRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.SelectReplacements.SelectReplacementTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectReplacements.SelectReplacementTranslation.cs)

Workflow implementations reachable through construction references:

- [SelectReplacementWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectReplacementWorkflow.cs)
- [TurtleHurtleWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectReplacements.TurtleHurtleWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | replacements | ReplacementEffect | yes |
| 2 | isOptional | BOOL |  |
| 3 | replacementsType | SelectReplacementsType |  |
| 4 | gameObjectSelections | ReplacementSelectionByZcid | yes |

### SelectNGroupReq (40)

Handler: [SelectNGroupRequestTranslator](handler-sources/GreClient.Rules.SelectNGroupRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.SelectNGroupRequest.cs](handler-sources/GreClient.Rules.SelectNGroupRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.SelectNGroup.SelectNGroupTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectNGroup.SelectNGroupTranslation.cs)

Workflow implementations reachable through construction references:

- [SelectNFaceUpPileGroupWorkflow](ui-sources/SelectNFaceUpPileGroupWorkflow.cs)
- [SelectNGroupWorkflow](ui-sources/SelectNGroupWorkflow.cs)
- [SelectNPileGroupWorkflow](ui-sources/SelectNPileGroupWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | minSel | UINT32 |  |
| 2 | maxSel | UINT32 |  |
| 3 | groups | Group | yes |
| 5 | sourceId | UINT32 |  |

### DistributionReq (42)

Handler: [DistributionRequestTranslator](handler-sources/GreClient.Rules.DistributionRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.DistributionRequest.cs](handler-sources/GreClient.Rules.DistributionRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.Distribution.DistributionTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.Distribution.DistributionTranslation.cs)

Workflow implementations reachable through construction references:

- [DistributionWithExistingValuesWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.DistributionWithExistingValuesWorkflow.cs)
- [DistributionWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.DistributionWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 2 | minAmount | UINT32 |  |
| 3 | maxAmount | UINT32 |  |
| 4 | minPerTarget | UINT32 |  |
| 5 | targetIds | UINT32 | yes |
| 6 | existingDistributionValues | UINT32 | yes |
| 7 | requiredDistributionValues | UINT32 | yes |
| 8 | validSelectedTargetIds | UINT32 | yes |
| 9 | sourceId | UINT32 |  |

### NumericInputReq (43)

Handler: [NumericInputRequestTranslator](handler-sources/GreClient.Rules.NumericInputRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.NumericInputRequest.cs](handler-sources/GreClient.Rules.NumericInputRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.NumericInput.NumericInputTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.NumericInput.NumericInputTranslation.cs)

Workflow implementations reachable through construction references:

- [NumericInputWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.NumericInputWorkflow.cs)
- [ReadAheadWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.ReadAheadWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 2 | minValue | UINT32 |  |
| 3 | maxValue | UINT32 |  |
| 4 | stepSize | UINT32 |  |
| 5 | sourceId | UINT32 |  |
| 6 | numericInputType | NumericInputType |  |
| 7 | disallowedValues | UINT32 | yes |
| 8 | suggestedValues | UINT32 | yes |
| 9 | disallowEven | BOOL |  |
| 10 | disallowOdd | BOOL |  |

### SearchReq (44)

Handler: [SearchRequestTranslator](handler-sources/GreClient.Rules.SearchRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.SearchRequest.cs](handler-sources/GreClient.Rules.SearchRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.Search.SearchTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.Search.SearchTranslation.cs)

Workflow implementations reachable through construction references:

- [SearchWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.Search.SearchWorkflow.cs)
- [SearchWorkflow_MultiZone](ui-sources/Wotc.Mtga.DuelScene.Interactions.Search.SearchWorkflow_MultiZone.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 2 | minFind | UINT32 |  |
| 3 | maxFind | UINT32 |  |
| 4 | zonesToSearch | UINT32 | yes |
| 5 | itemsToSearch | UINT32 | yes |
| 6 | itemsSought | UINT32 | yes |
| 7 | sourceId | UINT32 |  |
| 8 | additionalZones | UINT32 | yes |
| 9 | allowFailToFind | AllowFailToFind |  |

### OptionalActionMessage (45)

Handler: [OptionalActionRequestTranslator](handler-sources/GreClient.Rules.OptionalActionRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.OptionalActionMessageRequest.cs](handler-sources/GreClient.Rules.OptionalActionMessageRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.OptionalAction.DefaultTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.OptionalAction.DefaultTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.OptionalAction.OptionalActionTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.OptionalAction.OptionalActionTranslation.cs)

Workflow implementations reachable through construction references:

- [OptionalActionMessageWorkflow_Commander](ui-sources/OptionalActionMessageWorkflow_Commander.cs)
- [OptionalActionWorkflow_Mutate](ui-sources/OptionalActionWorkflow_Mutate.cs)
- [OptionalActionWorkflow_NonMechanic_BrowserButtons](ui-sources/OptionalActionWorkflow_NonMechanic_BrowserButtons.cs)
- [ExploreWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.OptionalAction.ExploreWorkflow.cs)
- [NonMechanicCardButtonWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.OptionalAction.NonMechanicCardButtonWorkflow.cs)
- [OptionalActionMessageWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.OptionalAction.OptionalActionMessageWorkflow.cs)
- [OptionalActionMessageWorkflow_SurveilStyleBrowser](ui-sources/Wotc.Mtga.DuelScene.Interactions.OptionalAction.OptionalActionMessageWorkflow_SurveilStyleBrowser.cs)
- [OptionalActionMessage_ScryishWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.OptionalAction.OptionalActionMessage_ScryishWorkflow.cs)
- [OptionalActionWorkflow_Riot](ui-sources/Wotc.Mtga.DuelScene.Interactions.OptionalAction.OptionalActionWorkflow_Riot.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | prompt | Prompt |  |
| 3 | sourceId | UINT32 |  |
| 4 | optionalActionTypes | CardMechanicType | yes |
| 5 | recipientIds | UINT32 | yes |
| 6 | highlight | HighlightType |  |
| 7 | highlightIds | UINT32 | yes |

### CastingTimeOptionsReq (46)

Handler: [CastingTimeOptionsRequestTranslator](handler-sources/GreClient.Rules.CastingTimeOptionsRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.CastingTimeOptionRequest.cs](handler-sources/GreClient.Rules.CastingTimeOptionRequest.cs)
- Internal request, predicates and response construction: [GreClient.Rules.CastingTimeOption_ManaTypeRequest.cs](handler-sources/GreClient.Rules.CastingTimeOption_ManaTypeRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.CastingTimeOptionTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.CastingTimeOptionTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.ChooseXTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.ChooseXTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.CostKeywordTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.CostKeywordTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.ModalTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.ModalTranslation.cs)
- [Wotc.Mtga.DuelScene.Interactions.ReplicateTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.ReplicateTranslation.cs)

Workflow implementations reachable through construction references:

- [CastingTimeOption_ChooseOrCostWorkflow](ui-sources/CastingTimeOption_ChooseOrCostWorkflow.cs)
- [CastingTimeOption_FlashWorkflow](ui-sources/CastingTimeOption_FlashWorkflow.cs)
- [CastingTimeOption_KickerWorkflow](ui-sources/CastingTimeOption_KickerWorkflow.cs)
- [CastingTimeOption_ModalRepeatWorkflow](ui-sources/CastingTimeOption_ModalRepeatWorkflow.cs)
- [AdditionalCostWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.AdditionalCostWorkflow.cs)
- [CastingTimeOptionWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.CastingTimeOptionWorkflow.cs)
- [CastingTimeOption_ModalSelectCostOptionWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.CastingTimeOption_ModalSelectCostOptionWorkflow.cs)
- [CastingTimeOption_SpecializeWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.CastingTimeOption_SpecializeWorkflow.cs)
- [ChooseXWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.ChooseXWorkflow.cs)
- [CostKeywordWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.CostKeywordWorkflow.cs)
- [ModalWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.ModalWorkflow.cs)
- [ReplicateWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.ReplicateWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | castingTimeOptionReq | CastingTimeOptionReq | yes |

### SelectFromGroupsReq (48)

Handler: [SelectFromGroupsRequestTranslator](handler-sources/GreClient.Rules.SelectFromGroupsRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.SelectFromGroupsRequest.cs](handler-sources/GreClient.Rules.SelectFromGroupsRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.SelectFromGroups.SelectFromGroupsWorkflowTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectFromGroups.SelectFromGroupsWorkflowTranslation.cs)

Workflow implementations reachable through construction references:

- [SelectFromGroupsWorkflow_Browser](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectFromGroups.SelectFromGroupsWorkflow_Browser.cs)
- [SelectFromGroupsWorkflow_MultiZone](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectFromGroups.SelectFromGroupsWorkflow_MultiZone.cs)
- [SelectFromGroupsWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectFromGroupsWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 3 | groups | Group | yes |
| 5 | minTotalSel | UINT32 |  |
| 6 | maxTotalSel | UINT32 |  |
| 7 | groupingStyle | GroupingStyle |  |
| 8 | unfiltered_ids | UINT32 | yes |
| 9 | sourceId | UINT32 |  |
| 10 | zone_ids | UINT32 | yes |

### SearchFromGroupsReq (49)

Handler: [SearchFromGroupsRequestTranslator](handler-sources/GreClient.Rules.SearchFromGroupsRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.SearchFromGroupsRequest.cs](handler-sources/GreClient.Rules.SearchFromGroupsRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.SearchFromGroupsTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SearchFromGroupsTranslation.cs)

Workflow implementations reachable through construction references:

- [SearchFromGroupsWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SearchFromGroupsWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 2 | minFind | UINT32 |  |
| 3 | maxFind | UINT32 |  |
| 4 | zonesToSearch | UINT32 | yes |
| 5 | groups | Group | yes |
| 6 | groupingStyle | GroupingStyle |  |
| 7 | sourceId | UINT32 |  |
| 8 | allowFailToFind | AllowFailToFind |  |
| 9 | additionalZones | UINT32 | yes |

### GatherReq (50)

Handler: [GatherRequestTranslator](handler-sources/GreClient.Rules.GatherRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.GatherRequest.cs](handler-sources/GreClient.Rules.GatherRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.Gather.GatherTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.Gather.GatherTranslation.cs)

Workflow implementations reachable through construction references:

- [GatherWorkflow_AnyNumber](ui-sources/Wotc.Mtga.DuelScene.Interactions.Gather.GatherWorkflow_AnyNumber.cs)
- [GatherWorkflow_RemoveExact](ui-sources/Wotc.Mtga.DuelScene.Interactions.Gather.GatherWorkflow_RemoveExact.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 2 | destinationId | UINT32 |  |
| 3 | sources | GatherSource | yes |
| 4 | amountToGather | UINT32 |  |

### QueuedGameStateMessage (51)

Handler: [GreClient.Rules.GreInterface.ProcessMessage](handler-sources/GreClient.Rules.GreInterface.cs).
- Internal request, predicates and response construction: [GreClient.Rules.ActionsAvailableRequestTranslator.cs](handler-sources/GreClient.Rules.ActionsAvailableRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.AssignDamageRequestTranslator.cs](handler-sources/GreClient.Rules.AssignDamageRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.CastingTimeOptionsRequestTranslator.cs](handler-sources/GreClient.Rules.CastingTimeOptionsRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.ChooseStartingPlayerRequestTranslator.cs](handler-sources/GreClient.Rules.ChooseStartingPlayerRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.DeclareAttackersRequestTranslator.cs](handler-sources/GreClient.Rules.DeclareAttackersRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.DeclareBlockersRequestTranslator.cs](handler-sources/GreClient.Rules.DeclareBlockersRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.DistributionRequestTranslator.cs](handler-sources/GreClient.Rules.DistributionRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.GatherRequestTranslator.cs](handler-sources/GreClient.Rules.GatherRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.GroupRequestTranslator.cs](handler-sources/GreClient.Rules.GroupRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.Notifications.IllegalRequestNotificationParser.cs](handler-sources/GreClient.Rules.Notifications.IllegalRequestNotificationParser.cs)
- Internal request, predicates and response construction: [GreClient.Rules.IntermissionRequestTranslator.cs](handler-sources/GreClient.Rules.IntermissionRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.MulliganRequestTranslator.cs](handler-sources/GreClient.Rules.MulliganRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.NumericInputRequestTranslator.cs](handler-sources/GreClient.Rules.NumericInputRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.OptionalActionRequestTranslator.cs](handler-sources/GreClient.Rules.OptionalActionRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.OrderRequestTranslator.cs](handler-sources/GreClient.Rules.OrderRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.PayCostsRequestTranslator.cs](handler-sources/GreClient.Rules.PayCostsRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.PromptRequestTranslator.cs](handler-sources/GreClient.Rules.PromptRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.RequestAnnotationProcessor.cs](handler-sources/GreClient.Rules.RequestAnnotationProcessor.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SearchFromGroupsRequestTranslator.cs](handler-sources/GreClient.Rules.SearchFromGroupsRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SearchRequestTranslator.cs](handler-sources/GreClient.Rules.SearchRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SelectCountersRequestTranslator.cs](handler-sources/GreClient.Rules.SelectCountersRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SelectFromGroupsRequestTranslator.cs](handler-sources/GreClient.Rules.SelectFromGroupsRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SelectNGroupRequestTranslator.cs](handler-sources/GreClient.Rules.SelectNGroupRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SelectNRequestTranslator.cs](handler-sources/GreClient.Rules.SelectNRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SelectReplacementRequestTranslator.cs](handler-sources/GreClient.Rules.SelectReplacementRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SelectTargetsRequestTranslator.cs](handler-sources/GreClient.Rules.SelectTargetsRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.StringInputRequestTranslator.cs](handler-sources/GreClient.Rules.StringInputRequestTranslator.cs)
- Internal request, predicates and response construction: [GreClient.Rules.SubmitDeckRequestTranslator.cs](handler-sources/GreClient.Rules.SubmitDeckRequestTranslator.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | type | GameStateType |  |
| 2 | gameStateId | UINT32 |  |
| 3 | gameInfo | GameInfo |  |
| 4 | teams | TeamInfo | yes |
| 5 | players | PlayerInfo | yes |
| 6 | turnInfo | TurnInfo |  |
| 7 | zones | ZoneInfo | yes |
| 8 | gameObjects | GameObjectInfo | yes |
| 9 | annotations | AnnotationInfo | yes |
| 10 | diffDeletedInstanceIds | UINT32 | yes |
| 11 | pendingMessageCount | UINT32 |  |
| 12 | prevGameStateId | UINT32 |  |
| 13 | timers | TimerInfo | yes |
| 14 | update | GameStateUpdate |  |
| 15 | actions | ActionInfo | yes |
| 16 | persistentAnnotations | AnnotationInfo | yes |
| 17 | diffDeletedPersistentAnnotationIds | UINT32 | yes |

### UIMessage (52)

Handler: [UiMessageTranslator](handler-sources/GreClient.Rules.UiMessageTranslator.cs).

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | seatIds | UINT32 | yes |
| 2 | onSelect | OnSelect |  |
| 3 | onHover | OnHover |  |
| 4 | onShuffle | OnShuffle |  |
| 5 | onChat | OnChat |  |
| 6 | onGenericEvent | OnGenericEvent |  |

### SubmitDeckReq (53)

Handler: [SubmitDeckRequestTranslator](handler-sources/GreClient.Rules.SubmitDeckRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.SubmitDeckRequest.cs](handler-sources/GreClient.Rules.SubmitDeckRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.SubmitDeckTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SubmitDeckTranslation.cs)

Workflow implementations reachable through construction references:

- [SubmitDeckWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SubmitDeckWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | deck | DeckMessage |  |

### EdictalMessage (54)

Handler: [EdictalMessageNotificationParser](handler-sources/GreClient.Rules.Notifications.EdictalMessageNotificationParser.cs).

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | edictMessage | ClientToGREMessage |  |

### TimeoutMessage (55)

Handler: [TimeoutMessageNotificationParser](handler-sources/GreClient.Rules.Notifications.TimeoutMessageNotificationParser.cs).

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | seatId | UINT32 |  |
| 2 | timeoutCount | UINT32 |  |
| 3 | timer | TimerInfo |  |

### TimerStateMessage (56)

Handler: [TimerStateMessageNotificationParser](handler-sources/GreClient.Rules.Notifications.TimerStateMessageNotificationParser.cs).

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | seatId | UINT32 |  |
| 2 | timers | TimerInfo | yes |

### SubmitDeckConfirmation (57)

No dedicated registered handler recovered. Do not assume a working UI from the enum declaration.

### StringInputReq (58)

Handler: [StringInputRequestTranslator](handler-sources/GreClient.Rules.StringInputRequestTranslator.cs).
The translator constructs StringInputRequest, but the inspected main WorkflowTranslator has no StringInputRequest branch. A normal text-entry menu is therefore not confirmed.
- Internal request, predicates and response construction: [GreClient.Rules.StringInputRequest.cs](handler-sources/GreClient.Rules.StringInputRequest.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 2 | sourceId | UINT32 |  |

### SelectCountersReq (59)

Handler: [SelectCountersRequestTranslator](handler-sources/GreClient.Rules.SelectCountersRequestTranslator.cs).
- Internal request, predicates and response construction: [GreClient.Rules.SelectCountersRequest.cs](handler-sources/GreClient.Rules.SelectCountersRequest.cs)

Routing sources (exact conditions, including special cases):

- [Wotc.Mtga.DuelScene.Interactions.SelectCounters.SelectCountersTranslation.cs](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectCounters.SelectCountersTranslation.cs)

Workflow implementations reachable through construction references:

- [SelectCounterWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectCounters.SelectCounterWorkflow.cs)
- [SelectCountersWorkflow](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectCounters.SelectCountersWorkflow.cs)

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | minSel | UINT32 |  |
| 2 | maxSel | UINT32 |  |
| 3 | context | SelectionContext |  |
| 5 | optionContext | OptionContext |  |
| 6 | listType | SelectionListType |  |
| 7 | counterPairs | CounterPair | yes |
| 9 | sourceId | UINT32 |  |

### PredictionResp (60)

No dedicated registered handler recovered. Do not assume a working UI from the enum declaration.

Payload fields:

| Wire # | Field | Type | Repeated |
|---:|---|---|---|
| 1 | gameStateMessages | GameStateMessage | yes |
| 2 | suppressedTriggeredAbilities | TriggeredAbility | yes |
| 3 | greRequests | GREToClientMessage | yes |

## SelectN predicates

The complete predicates that turn one SelectNReq into different menus are in [SelectNRequest](handler-sources/GreClient.Rules.SelectNRequest.cs). The checked order and zone-dependent branches are in [SelectNWorkflowTranslation](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflowTranslation.cs). Static card-name, creature-subtype, card-type, supertype, keyword and counter-type choices have separate translation helpers listed above.

## Animation/state annotation matching

These belong in GameStateMessage annotations, not standalone menu requests. Parser registrations establish a handler association; they do not alone prove that a particular animation is visible.

| Value | Annotation | Registered parser(s) |
|---:|---|---|
| 0 | None | Not recovered in inspected factories |
| 1 | ZoneTransfer | ZoneTransferAnnotationParser |
| 2 | LossOfGame | LossOfGameStateAnnotationParser |
| 3 | DamageDealt | DamageDealtAnnotationParser |
| 4 | TappedUntappedPermanent | TappedUntappedPermanentAnnotationParser |
| 5 | ModifiedPower | ModifiedPowerAnnotationParser |
| 6 | ModifiedToughness | ModifiedToughnessAnnotationParser |
| 7 | ModifiedColor | ModifiedColorAnnotationParser |
| 8 | PhaseOrStepModified | PhaseModifiedEventParser |
| 9 | AddAbility | AddAbilityStateAnnotationParser |
| 10 | ModifiedLife | ModifiedLifeAnnotationParser |
| 11 | CreateAttachment | Not recovered in inspected factories |
| 12 | RemoveAttachment | RemoveAttachmentEventParser |
| 13 | ObjectIdChanged | ObjectIDChangedAnnotationParser |
| 14 | Counter | CounterStateAnnotationParser |
| 15 | ControllerChanged | ControllerChangedAnnotationParser |
| 16 | CounterAdded | CounterAddedAnnotationParser |
| 17 | CounterRemoved | CounterRemovedAnnotationParser |
| 18 | LayeredEffectCreated | LayeredEffectCreatedEventParser |
| 19 | LayeredEffectDestroyed | Not recovered in inspected factories |
| 20 | Attachment | AttachmentStateAnnotationParser |
| 21 | Haunt | Not recovered in inspected factories |
| 22 | CopiedObject | CopiedObjectAnnotationParser |
| 23 | RemoveAbility | RemoveAbilityAnnotationParser |
| 24 | WinTheGame | Not recovered in inspected factories |
| 25 | ModifiedType | ModifiedTypeAnnotationParser |
| 26 | TargetSpec | TargetSpecStateAnnotationParser |
| 27 | TextChange | Not recovered in inspected factories |
| 28 | FaceDown | FaceDownStateAnnotationParser |
| 29 | TurnPermanent | TurnedPermanentAnnotationParser |
| 30 | DynamicAbility | Not recovered in inspected factories |
| 31 | ObjectsSelected | ObjectsSelectedStateAnnotationParser |
| 32 | TriggeringObject | TriggeringObjectAnnotationParser |
| 33 | DamageSource | Not recovered in inspected factories |
| 34 | ManaPaid | ManaPaidAnnotationParser |
| 35 | TokenCreated | TokenCreatedEventParser |
| 36 | AbilityInstanceCreated | AbilityCreatedEventParser |
| 37 | AbilityInstanceDeleted | AbilityDeletedEventParser |
| 38 | DisplayCardUnderCard | DisplayCardUnderCardStateAnnotationParser |
| 39 | AbilityWordActive | AbilityWordActiveStateAnnotationParser |
| 40 | LinkInfo | LinkInfoEventAnnotationParser, LinkInfoStateAnnotationParser |
| 41 | TokenDeleted | TokenDeletedEventParser |
| 42 | Qualification | QualificationAnnotationParser |
| 43 | ResolutionStart | ResolutionStartEventParser, ResolutionStartAnnotationParser |
| 44 | ResolutionComplete | ResolutionCompleteEventParser, ResolutionCompleteAnnotationParser |
| 45 | Designation | DesignationStateAnnotationParser |
| 46 | GainDesignation | GainDesignationAnnotationParser |
| 47 | CardRevealed | CardRevealedAnnotationParaser |
| 48 | NewTurnStarted | NewTurnStartedEventParser |
| 49 | ManaDetails | ManaDetailsAnnotationParser |
| 50 | DisqualifiedEffect | DisqualifiedEffectAnnotationParser |
| 51 | LayeredEffect | LayeredEffectStateAnnotationParser |
| 52 | MiscContinuousEffect | MiscContinuousEffectStateAnnotationParser |
| 53 | ShouldntPlay | ShouldntPlayStateAnnotationParser |
| 54 | UseOrCostsManaCost | UseOrCostsManaCostAnnotationParser |
| 55 | RemainingSelections | RemainingSelectionsStateAnnotationParser |
| 56 | Shuffle | ShuffleAnnotationParser |
| 57 | CoinFlip | CoinFlipEventParser |
| 58 | ChoiceResult | ChoiceResultEventParser |
| 59 | RevealedCardCreated | RevealedCardCreatedEventParser |
| 60 | RevealedCardDeleted | RevealedCardDeletedEventParser |
| 61 | SuspendLike | SuspendLikeStateAnnotationParser |
| 62 | ReplacementEffect | ReplacementEffectStateAnnotationParser |
| 63 | EnteredZoneThisTurn | EnteredZoneThisTurnAnnotationParser |
| 64 | CastingTimeOption | CastingTimeOptionAnnotationParser |
| 65 | Scry | ScryEventAnnotationParser |
| 66 | PredictedDirectDamage | PredictedDirectDamageStateAnnotationParser |
| 67 | SwitchPowerToughness | SwitchPowerToughnessStateAnnotationParser |
| 68 | SupplementalText | AbilityWordActiveStateAnnotationParser |
| 69 | PendingEffect | PendingEffectEventParser |
| 70 | AttachmentCreated | AttachmentCreatedEventParser |
| 71 | PowerToughnessModCreated | PowerToughnessModCreatedEventParser |
| 72 | SyntheticEvent | SyntheticEventAnnotationParser |
| 73 | UserActionTaken | UserActionTakenAnnotationParser |
| 74 | DelayedTriggerAffectees | DelayedTriggerAffecteesStateAnnotationParser |
| 75 | InstanceRevealedToOpponent | InstanceIdRevealedToOpponentParser |
| 76 | ModifiedName | Not recovered in inspected factories |
| 77 | ReplacementEffectApplied | ReplacementEffectAppliedEventParser, ReplacementEffectAppliedStateAnnotationParser |
| 78 | ReferencedObjects | ReferencedObjectsStateAnnotationParser |
| 79 | ChoosingAttachments | ChoosingAttachmentsAnnotationParser |
| 80 | TemporaryPermanent | TemporaryPermanentStateAnnotationParser |
| 81 | GamewideHistoryCount | GamewideHistoryCountStateAnnotationParser |
| 82 | AbilityExhausted | ExhaustedAbilityStateAnnotationParser |
| 83 | MultistepEffectStarted | MultistepEffectStartedAnnotationParser |
| 84 | MultistepEffectComplete | MultistepEffectCompleteAnnotationParser |
| 85 | DieRoll | DieRollResultEventAnnotationParser, DieRollResultStateAnnotationParser |
| 86 | DungeonStatus | DungeonStateAnnotationParser |
| 87 | LinkedDamage | LinkedDamageAnnotationParser |
| 88 | ClassLevel | ClassLevelStateAnnotationParser |
| 89 | TokenImmediatelyDied | TokenImmediatelyDiedAnnotationParser |
| 90 | DamagedThisTurn | DamagedThisTurnStateAnnotationParser |
| 91 | ReferencedCardNames | ReferencedCardNamesAnnotationParser |
| 92 | PlayerSelectingTargets | PlayerSelectingTargetsAnnotationParser |
| 93 | PlayerSubmittedTargets | PlayerSubmittedTargetsAnnotationParser |
| 94 | CrewedThisTurn | CrewedSaddledAnnotationParser |
| 95 | PhasedOut | PhasedOutAnnotationParser |
| 96 | PhasedIn | PhasedInAnnotationParser |
| 97 | LoyaltyActivationsRemaining | LoyaltyActivationsRemainingAnnotationParser |
| 98 | GroupedIds | GroupedIdsStateAnnotationParser |
| 99 | RegeneratePending | RegeneratePendingStateAnnotationParser |
| 100 | PermanentRegenerated | PermanentRegeneratedEventAnnotationParser |
| 101 | ObjectsToDistinguish | ObjectsToDistinguishAnnotationParser |
| 102 | BoonInfo | BoonInfoAnnotationParser |
| 103 | LoseDesignation | LoseDesignationAnnotationParser |
| 104 | SaddledThisTurn | CrewedSaddledAnnotationParser |
| 105 | HiddenInformation | HiddenInformationStateAnnotationParser |
| 106 | LoopCount | LoopCountAnnotationParser |
| 107 | Vote | Not recovered in inspected factories |
| 108 | SuppressedPowerAndToughness | SuppressedPowerAndToughnessStateParser |
| 109 | GenericRelationship | GenericRelationshipStateAnnotationParser |
| 110 | ColorProduction | ColorProductionStateAnnotationParser |
| 111 | CopyException | CopyExceptionAnnotationParser |
| 112 | SelectNDecoration | Not recovered in inspected factories |
| 113 | ModifiedCost | Not recovered in inspected factories |
| 114 | MultiplayerNumericAid | MultiplayerNumericAidStateParser |
| 115 | HighlightReason | HighlightReasonStateParser |
| 116 | PersistentChoiceResult | Not recovered in inspected factories |
| 117 | EnteredZoneAtSameTime | Not recovered in inspected factories |

## Complete routing branch index

These are excerpts, in original line order. Open the linked source for full boolean conditions, helper predicates and default branches.

### ActionsAvailableTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.ActionsAvailableTranslation.cs)

```csharp
// L58
if (TranslatesToCannotTakeActionWorkflow(request, gameState))
// L60
return new CannotTakeActionWorkflow(request, _cardDatabase, _gameStateProvider, _cardViewManager, _fakeCardController, _browserManager, _logger);
// L62
if (DoesHaveOpeningHandActions(request))
// L64
return new OpeningHandActionWorkflow(request, _cardDatabase.ClientLocProvider, _cardViewManager, _browserManager);
// L66
if (ResolutionCastTranslation.IsResolutionCast(gameState, request.Actions, request.InactiveActions))
// L68
return _resolutionCastTranslation.Translate(request);
// L75
if (instanceId != 0)
// L78
if (action.SourceId != 0)
// L87
if (AreAllActionsInHandWithAResolvingSource(mtgGameState, hashSet, zonesFromCardIds) || ShouldOpenSingleZoneBrowser(mtgGameState, zonesFromCardIds, request.Actions, request.InactiveActions))
// L90
return new ActionsAvailableWorkflow_Browser(request, actionSubmission, new ActionProcessor(actionSubmission.SubmitAction, _clientSideInteraction), _cardDatabase.ClientLocProvider, _gameStateProvider, _cardViewManager, _browserManager, _browserHeaderTextProvider, _assetLookupSystem, _logger);
// L92
if (ShouldOpenMultiZoneBrowser(mtgGameState, zonesFromCardIds))
// L95
return new ActionsAvailableWorkflow_MultiZoneBrowser(request, actionSubmission2, new ActionProcessor(actionSubmission2.SubmitAction, _clientSideInteraction), _cardDatabase, _cardDatabase.ClientLocProvider, _gameStateProvider, _cardViewManager, _browserManager, _browserHeaderTextProvider, _assetLookupSystem, _logger);
// L97
return _defaultTranslation.Translate(request);
// L105
if (!gameState.TryGetCard(id, out var card))
// L111
if (type == ZoneType.Limbo)
// L115
if (addNHCastables && (type == ZoneType.Exile || type == ZoneType.Graveyard || type == ZoneType.Sideboard))
// L119
if (list.Count > 0 && list.Exists((ActionInfo ai) => ai.SeatId == localPlayerId))
// L131
if (involvedZones.Count != 1)
// L136
if (mtgZone.Type != ZoneType.Hand)
// L140
if (mtgZone.OwnerNum != GREPlayerNum.LocalPlayer)
// L144
if (actionSources.Count != 1)
// L148
if (currentState.ResolvingCardInstance == null)
// L157
if (involvedZones.Count != 1)
// L167
if (involvedZones.Count <= 1)
// L174
if (involvedZone.OwnerNum != GREPlayerNum.Invalid)
// L179
if (hashSet.Count <= 1)
// L186
if ((uint)(type - 1) > 1u && type != ZoneType.Graveyard)
// L211
if ((action.IsPlayAction() || action.IsCastAction()) && gs.TryGetCard(action.InstanceId, out var card) && card.Zone.Type == ZoneType.Library)
// L213
if (num == 0)
// L217
else if (action.InstanceId != num)
// L225
if ((inactiveAction.IsPlayAction() || inactiveAction.IsCastAction()) && gs.TryGetCard(inactiveAction.InstanceId, out var card2) && card2.Zone.Type == ZoneType.Library)
// L227
if (num == 0)
// L231
else if (inactiveAction.InstanceId != num)
// L245
if (actions[i].ActionType == ActionType.OpeningHandAction)
// L255
if (sources.Count == 1)
// L262
if (stack.CardIds.Contains(item))
// L272
if (request.InactiveActions.Count == 0)
// L276
if (request.CancellationType != AllowCancel.No)
// L280
if (request.Actions.Count != 1)
// L284
if (request.Actions[0].ActionType != ActionType.Pass)
// L288
if (gameState.ResolvingCardInstance == null)
// L291
if (prompt != null)
```

### AdornCardsTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.AdornCardsTranslation.cs)

```csharp
// L30
return new AdornCardWorkflow(req, _objectPool, _browserController, _databaseUtilities);
```

### AssignDamageTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.AssignDamage.AssignDamageTranslation.cs)

```csharp
// L16
return new AssignDamageWorkflow(req, _context);
```

### AutoAssignDamageTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.AssignDamage.AutoAssignDamageTranslation.cs)

```csharp
// L9
return new AutoAssignDamageWorkflow(req);
```

### AutoTapActionsTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.AutoTapActionsTranslation.cs)

```csharp
// L46
return new AutoTapActionsWorkflow(req, _objectPool, _gameStateProvider, _promptTextProvider, _browserController, _cardHolderProvider, _playWarningManager, _buttonTextFetcher, _assetLookupSystem);
```

### CastingTimeOptionTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.CastingTimeOptionTranslation.cs)

```csharp
// L22
return new CastingTimeOptionWorkflow(req, _abilityProvider, _childTranslation);
```

### CastingTimeOption_ManaTypeTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption_ManaTypeTranslation.cs)

```csharp
// L25
return new CastingTimeOption_ManaTypeWorkflow(req, _browserController, _locProvider, _assetLookupSystem);
```

### ChoiceOptionsTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceOptionsTranslation.cs)

```csharp
// L22
if (listType == SelectionListType.Static || listType == SelectionListType.StaticSubset)
// L24
return _staticTranslation.Translate(request);
// L26
if (request.ListType == SelectionListType.Dynamic && request.IdType == IdType.PromptParameterIndex)
// L28
return _dynamicTranslation.Translate(request);
```

### ChoiceSelectionTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceSelectionTranslation.cs)

```csharp
// L55
if (UsePromptButtonWorkflow(choiceOptions, req))
// L57
return new ChoicePromptButtonsWorkflow(req, choiceOptions, _cardDatabase, _resolutionEffectProvider, _assetLookupSystem);
// L59
if (choiceOptions.ChoiceToIdMap.Count <= 6 && req.MinSel == 1 && req.MaxSel == 1)
// L61
return new ChoiceButtonsWorkflow(req, choiceOptions, _assetLookupSystem, _cardDatabase, _cardHolderManager, _gameStateProvider, _gameplaySettings, _browserManager);
// L63
if (choiceOptions.SortedChoices.Count > 3)
// L65
return new ChoiceSelectionWorkflow(req, _objectPool, _browserManager, choiceOptions, _promptTextProvider);
// L67
return new ChoiceModalWorkflow(req, choiceOptions, _cardDatabase, _gameStateProvider, _cardBuilder, _browserManager, _headerTextProvider);
// L72
if (request.MinSel == 1 && request.MaxSel == 1)
// L74
if (choiceOptions.ChoiceToIdMap.Count != 2 || request.CanCancel)
// L76
if (choiceOptions.ChoiceToIdMap.Count == 1)
```

### ChoiceWithContextTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.ChoiceWithContextTranslation.cs)

```csharp
// L34
return new ChoiceWithContextWorkflow(selectNRequest, _gameStateProvider, _promptTextProvider, _browserController, _choiceOptionsTranslation);
```

### ChooseStartingPlayerTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.ChooseStartingPlayerTranslation.cs)

```csharp
// L29
return new ChooseStartingPlayerWorkflow(req, _locProvider, _gameStateProvider, _browserController);
```

### ChooseXTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.ChooseXTranslation.cs)

```csharp
// L42
if (payload != null)
// L65
return new ChooseXWorkflow(cto_chooseX, _interfaceBuilder, _confirmZeroLogger, _buttonTextProvider.Text(cto_chooseX));
```

### ConfirmBrowserTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.ConfirmBrowserTranslation.cs)

```csharp
// L19
if (MDNPlayerPrefs.GameplayWarningsEnabled && _playWarningProvider.ShouldDisplayPlayWarning(cardView, action))
// L21
return new ConfirmChoice_Browser(_context, cardView, action);
```

### ConfirmWidgetTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.ConfirmWidgetTranslation.cs)

```csharp
// L38
if (currentCardHolder == null)
// L43
if (widgetData.HasValue && MDNPlayerPrefs.GameplayWarningsEnabled)
// L45
return new ConfirmChoice_Widget(_cardHolderProvider, _widgetUI, cardView, (action, widgetData.Value));
// L52
if (cardHolder != CardHolderType.Battlefield)
// L56
if (action.ActionType == ActionType.MakePayment)
// L60
if (action.ActionType == ActionType.SpecialTurnFaceUp)
// L62
return new ConfirmWidget.Option
// L69
if (abilityGrpId == 0)
// L73
if (action.ActionType == ActionType.Special)
// L76
if (abilityGrpId2 == 349 || abilityGrpId2 == 350)
// L78
return new ConfirmWidget.Option
// L86
if (abilityPrintingById == null)
// L90
if (abilityPrintingById.RequiresConfirmation != RequiresConfirmation.Misclick)
// L94
if (abilityPrintingById.Tags.Contains(MetaDataTag.SacrificeFoodAbility))
// L96
return new ConfirmWidget.Option
// L102
return new ConfirmWidget.Option
```

### CostKeywordTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.CostKeywordTranslation.cs)

```csharp
// L49
return new CostKeywordWorkflow(req, childRequest.GrpId, GetComparer(childRequest.OptionType), _cardDatabase, _gameStateProvider, _cardViewProvider, _fakeCardController, _browserController, _headerTextProvider);
// L54
if (!_requestComparers.TryGetValue(ctoType, out var value))
```

### DeclareAttackersTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.DeclareAttackersTranslation.cs)

```csharp
// L41
return new DeclareAttackersWorkflow(req, _objPool, _promptEngine, _gameStateProvider, _cardViewProvider, GetBattlefield(), _uiManager, _gameManager.InteractionSystem, _gameManager.UIMessageHandler);
// L47
if (mtgGameState == null)
// L51
if (mtgGameState.Battlefield == null)
```

### DeclareBlockersTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.DeclareBlockersTranslation.cs)

```csharp
// L42
return new DeclareBlockersWorkflow(req, _objPool, _clientLocProvider, _promptEngine, _gameStateProvider, _cardViewProvider, GetBattlefield(), _uiManager);
// L48
if (mtgGameState == null)
// L52
if (mtgGameState.Battlefield == null)
```

### DefaultTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.OptionalAction.DefaultTranslation.cs)

```csharp
// L41
return new OptionalActionMessageWorkflow(req, _pool, _greLocProvider, _clientLocProvider, _gameStateProvider, _browserProvider, _assetLookupSystem);
```

### DistributionTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.Distribution.DistributionTranslation.cs)

```csharp
// L23
if (req.ExistingDistributions.Count > 0)
// L25
return new DistributionWithExistingValuesWorkflow(req, _objPool, _cardHolderProvider, _spinnerController);
// L27
return new DistributionWorkflow(req, _cardHolderProvider, _spinnerController);
```

### DynamicListTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.DynamicListTranslation.cs)

```csharp
// L46
switch (promptParameter.Type)
// L48
case ParameterType.PromptId:
// L54
if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<PromptPayload> loadedTree))
// L57
if (payload != null)
// L63
if (parameterProvider.TryGetValue(request, _context, out var paramValue))
// L74
if (string.IsNullOrEmpty(text))
// L80
case ParameterType.Number:
// L83
case ParameterType.NonLocalizedString:
// L86
case ParameterType.Reference:
// L89
switch (reference.Type)
// L91
case ReferenceType.CatalogId:
// L94
case ReferenceType.InstanceId:
// L100
case ReferenceType.LocalizationId:
// L103
case ReferenceType.PlayerSeatId:
// L114
if (list.Count == 0)
// L119
return new ChoiceOptions(dictionary, list, Array.Empty<string>());
```

### EffectCostTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.EffectCostTranslation.cs)

```csharp
// L16
return new EffectCostWorkflow(req, _workflowTranslator);
```

### GatherTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.Gather.GatherTranslation.cs)

```csharp
// L27
if (req.AmountToGather != 0)
// L29
return new GatherWorkflow_RemoveExact(req, _cardHolderProvider, _gameManager.SpinnerController);
// L31
return new GatherWorkflow_AnyNumber(req, _cardHolderProvider, _entityViewProvider, _gameManager.SpinnerController);
```

### GroupTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.Grouping.GroupTranslation.cs)

```csharp
// L54
if (req.Context == GroupingContext.Surveil)
// L56
return new SurveilWorkflow(req, _gameStateProvider, _cardDatabase, _browserManager, _cardViewProvider);
// L58
if (req.Context == GroupingContext.Scry)
// L60
return new ScryWorkflow(req, _cardDatabase.ClientLocProvider, _cardViewProvider, _browserManager);
// L62
if (req.Context == GroupingContext.LondonMulligan)
// L64
return new LondonWorkflow(req, _cardViewProvider, _cardDatabase, _browserManager, _gameManager.Logger);
// L66
if (IsLilianaOfTheVeilUltimate(_gameStateProvider.LatestGameState))
// L68
return new GroupWorkflow_BattlefieldPermanents(req, _cardHolderProvider, _cardDatabase.ClientLocProvider, _browserManager, _genericPool, _cardViewProvider, _cardDatabase.CardTitleProvider, _highlightController, _cardHoverController, _workflowProvider, _gameStateProvider, _gameManager.SpinnerController);
// L70
return new GroupWorkflow(req, _cardViewProvider, _browserManager);
// L75
if (state.ResolvingCardInstance != null && state.ResolvingCardInstance.GrpId != 0)
```

### IntermissionTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.IntermissionTranslation.cs)

```csharp
// L42
return new IntermissionWorkflow(req, _entityViewProvider, _gameStateProvider, _duelSceneStateController, _cardHolderProvider, _browserController, _vfxProvider, _assetLookupSystem);
```

### ModalTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.CastingTimeOption.ModalTranslation.cs)

```csharp
// L27
if (ContainsRepeatSelections(req))
// L29
return new CastingTimeOption_ModalRepeatWorkflow(req, _context.Get<ICardDatabaseAdapter>(), _context.Get<ICardBuilder<DuelScene_CDC>>(), _context.Get<IEntityViewProvider>(), _context.Get<IBrowserController>(), _context.Get<IGameplaySettingsProvider>(), _context.Get<IBrowserHeaderTextProvider>());
// L31
if (IsModalSelectCostOptionWorkflow(req, _abilityDataProvider))
// L33
return new CastingTimeOption_ModalSelectCostOptionWorkflow(req, _context.Get<ICardDatabaseAdapter>(), _context.Get<ICardBuilder<DuelScene_CDC>>(), _context.Get<IObjectPool>(), _context.Get<ICardViewProvider>(), _context.Get<IBrowserController>(), _context.Get<IBrowserHeaderTextProvider>());
// L35
return new ModalWorkflow(req, _context.Get<ICardDatabaseAdapter>(), _context.Get<ICardBuilder<DuelScene_CDC>>(), _context.Get<IEntityViewManager>(), _context.Get<IAbilityDataProvider>(), _context.Get<IBrowserController>(), _context.Get<IClientLocProvider>(), _context.Get<IPromptEngine>(), _context.Get<IBrowserHeaderTextProvider>(), _context.Get<IGameplaySettingsProvider>(), _assetLookupSystem);
// L40
if (req != null && req.RepeatSelectionAllowed)
// L49
if (req != null && req.AbilityGrpId != 0 && abilityProvider.TryGetAbilityPrintingById(req.AbilityGrpId, out var ability))
// L51
if (ability.BaseId != 330)
```

### MulliganTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.Mulligan.MulliganTranslation.cs)

```csharp
// L52
return new MulliganWorkflow(req, _cardViewManager, _cardDatabase, _gameStateProvider, _browserController, _canvasRootProvider, _assetLookupSystem, new CardPreviewAnimation(_splineMovementSystem, _cardViewManager, _cardDatabase.CardDataProvider, _gameStateProvider, _cardHolderProvider, _stateProvider), _gameManager);
```

### NPEActionsAvailableWorkflowTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.NPEActionsAvailableWorkflowTranslation.cs)

```csharp
// L25
return new ActionsAvailableWorkflow_NPE(req, _gameManager, _clientSideInteraction, _context, _director);
```

### NPESelectionWorkflowTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.NPESelectionWorkflowTranslation.cs)

```csharp
// L49
return new SelectionWorkflow_NPE(req, _cardDatabase, _gameStateProvider, _resolutionEffectProvider, _gameplaySettingsProvider, _cardViewProvider, _cardHolderProvider, _browserManager, _selectionConfirmation, _playWarningManager, _assetLookupSystem, _director);
```

### NumericInputTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.NumericInput.NumericInputTranslation.cs)

```csharp
// L30
if (UseReadAheadWorkflow(req))
// L32
return new ReadAheadWorkflow(req, _context.Get<IGameStateProvider>(), _context.Get<ICardDatabaseAdapter>(), _context.Get<IEntityViewManager>(), _context.Get<IPromptEngine>(), _context.Get<IClientLocProvider>(), _context.Get<IGreLocProvider>(), _context.Get<IBrowserController>());
// L34
return new NumericInputWorkflow(req, _chooseXInterfaceBuilder, _context.Get<IEntityViewProvider>(), _assetLookupSystem, _context.Get<IGameStateProvider>());
// L39
if (req == null)
// L44
if (prompt == null)
```

### OptionalActionTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.OptionalAction.OptionalActionTranslation.cs)

```csharp
// L57
if (req.Mechanics.Contains(CardMechanicType.Mutate))
// L59
return new OptionalActionWorkflow_Mutate(req, _gameStateProvider, _cardDatabase, _browserManager);
// L61
if (req.Mechanics.Contains(CardMechanicType.Riot))
// L63
return new OptionalActionWorkflow_Riot(req, _cardDatabase, _cardBuilder, _gameStateProvider, _cardViewProvider, _browserManager);
// L65
if (req.Mechanics.Contains(CardMechanicType.Explore))
// L67
return new ExploreWorkflow(req, _cardDatabase.ClientLocProvider, _gameStateProvider, _cardDatabase, _browserManager, _cardViewProvider);
// L69
if (req.Mechanics.Contains(CardMechanicType.Transform))
// L71
return _defaultTranslation.Translate(req);
// L73
if (OptionalActionMessage_ScryishWorkflow.UseScryStyleBrowser(req, _assetLookupSystem, mtgGameState))
// L75
return new OptionalActionMessage_ScryishWorkflow(req, _cardDatabase, _gameStateProvider, _resolutionEffectProvider, _cardViewProvider, _browserManager, _assetLookupSystem);
// L77
if (SurveilStyleBrowserWorkflow<OptionalActionMessageRequest>.UseSurveilStyleBrowser(req, _assetLookupSystem, mtgGameState))
// L79
return new OptionalActionMessageWorkflow_SurveilStyleBrowser(req, _cardDatabase.ClientLocProvider, _browserManager, _gameStateProvider, _cardDatabase, _cardViewProvider);
// L81
if (TryGetSpecialBrowserLayout(req, mtgGameState, _cardDatabase, _assetLookupSystem, out var layoutInfo))
// L83
return new OptionalActionWorkflow_NonMechanic_BrowserButtons(req, layoutInfo, _context.Get<ICardDataProvider>(), _context.Get<ICardTitleProvider>(), _context.Get<ICardViewProvider>(), _context.Get<IGameStateProvider>(), _context.Get<IFakeCardViewController>(), _context.Get<IPromptTextProvider>(), _context.Get<IGreLocProvider>(), _context.Get<IClientLocProvider>(), _context.Get<IBrowserManager>());
// L85
if (TryGetNonMechanicCardButtonBrowserText(cardById, req, _cardDatabase, _assetLookupSystem, mtgGameState, out var browserText))
// L87
return new NonMechanicCardButtonWorkflow(req, cardById, browserText, _cardBuilder, _cardDatabase.ClientLocProvider, _cardDatabase, _browserManager, _context.Get<IPromptTextProvider>());
// L89
if (UseCommanderWorkflow(cardById, req.SourceId, mtgGameState, req))
// L91
return new OptionalActionMessageWorkflow_Commander(req);
// L93
return _defaultTranslation.Translate(req);
// L98
if (sourceCard == null)
// L102
if (!req.Mechanics.Exists((CardMechanicType x) => x == CardMechanicType.ZoneTransfer))
// L107
if (localPlayer == null)
// L111
if (!localPlayer.CommanderIds.Contains(sourceId))
// L116
if (prompt == null)
// L121
if (parameters == null)
// L125
if (parameters.Count < 2)
// L130
if (promptParameter.ParameterName != "CardId")
// L134
if (promptParameter.Type != ParameterType.Number)
// L138
if (promptParameter.NumberValue != sourceCard.InstanceId)
// L143
if (promptParameter2.ParameterName != "CardId")
// L147
if (promptParameter2.Type != ParameterType.Number)
// L151
if (Enum.IsDefined(typeof(ZoneType), promptParameter2.NumberValue))
// L161
if (gameState.ReplacementEffects.TryGetValue(sourceId, out var value) && TryGetSpecialBrowserLayout(value, cardDatabase.AbilityDataProvider, assetLookupSystem, out layoutInfo))
// L165
if (gameState.TryGetCard(sourceId, out var card) && TryGetSpecialBrowserLayout(CardDataExtensions.CreateWithDatabase(card, cardDatabase), assetLookupSystem, out layoutInfo))
// L177
if (TryGetSpecialBrowserLayout(abilityDataProvider.GetAbilityPrintingById(replacementEffect.AbilityId), assetLookupSystem, out layoutInfo))
// L191
if (assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SpecialBrowserLayout> loadedTree))
// L203
if (assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<SpecialBrowserLayout> loadedTree))
// L213
if (nonMechanicCardButtonPayload != null)
// L224
if (cardInstance == null)
// L228
if (als.TreeLoader.TryLoadTree(out AssetLookupTree<NonMechanic_CardButtons> loadedTree))
```

### OrderTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.OrderTranslation.cs)

```csharp
// L45
return new OrderWorkflow(req, _locProvider, _promptTextProvider, _gameStateProvider, _cardViewProvider, _settingsProvider, _resolutionEffectProvider, _browserController, _assetLookupSystem);
```

### PayCostsTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.PayCostsTranslation.cs)

```csharp
// L32
if (ListenForLogging(req))
// L36
return new PayCostWorkflow(req, _gameStateProvider, _cardViewProvider, _gameManager.InteractionSystem, _gameManager.UIMessageHandler, _gameManager.UIManager, _workflowTranslator, _logger);
// L41
if (outMsg.EffectCostResp != null && outMsg.EffectCostResp.CostSelection != null)
// L51
if (IsNonManaPayment(childRequest))
// L61
if (!(request is EffectCostRequest effectCostRequest))
// L65
if (effectCostRequest.EffectCostType != EffectCostType.Select)
// L70
if (costSelection == null)
// L74
if (costSelection.MinSel == 1 && costSelection.MaxSel == 1 && costSelection.Context == SelectionContext.NonManaPayment)
```

### ReplicateTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.ReplicateTranslation.cs)

```csharp
// L20
return new ReplicateWorkflow(req, _interfaceBuilder, _locProvider);
```

### ResolutionCastTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.ResolutionCastTranslation.cs)

```csharp
// L46
return new ResolutionCastWorkflow(req, _cardDatabase, _gameStateProvider, _cardViewProvider, _browserManager, actionSubmission, new ActionProcessor(actionSubmission.SubmitAction, _clientSideInteraction), _headerTextProvider, _assetLookupSystem);
// L51
if (gameState == null || activeActions == null || inactiveActions == null)
// L60
if (AllActionsAreResolutionCasts(resolvingCardId, activeActions))
// L69
if (resolvingCardId == 0)
// L75
if (action.ActionType != ActionType.Pass && !IsResolutionCastAction(action, resolvingCardId))
// L85
if (action.IsCastAction())
```

### SearchFromGroupsTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SearchFromGroupsTranslation.cs)

```csharp
// L39
return new SearchFromGroupsWorkflow(req, _cardDatabase, _gameStateProvider, _entityViewProvider, _promptTextProvider, _browserController, _eventQueue);
```

### SearchTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.Search.SearchTranslation.cs)

```csharp
// L38
if (req.IsMultiZoneSearch)
// L40
return new SearchWorkflow_MultiZone(req, _cardDatabase, _gameStateProvider, _gameplaySettings, _cardViewProvider, _browserManager);
// L42
return new SearchWorkflow(req, _cardDatabase.CardTitleProvider, _gameStateProvider, _gameplaySettings, _cardViewProvider, _browserManager, _headerTextProvider);
```

### SelectCountersTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectCounters.SelectCountersTranslation.cs)

```csharp
// L50
if (req.MinSelect == 1 && req.MaxSelect == 1)
// L52
return new SelectCounterWorkflow(req, _unityPool, _cardDatabase, _gameStateProvider, _cardHolderProvider, _browserController, _headerTextProvider, _assetLookupSystem);
// L54
return new SelectCountersWorkflow(req, _pool, _counterDistribution, _counterSelection);
```

### SelectFromGroupsWorkflowTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectFromGroups.SelectFromGroupsWorkflowTranslation.cs)

```csharp
// L53
if (zoneList.Count == 1)
// L56
if (mtgZone.Type == ZoneType.Hand && mtgZone.OwnerNum == GREPlayerNum.Opponent)
// L61
if (zoneIds.Count == 1)
// L64
if (zoneById.Type == ZoneType.Hand && zoneById.OwnerNum == GREPlayerNum.Opponent)
// L76
if (currentState.Zones.TryGetValue(zoneId, out var value))
// L96
if (gameState.TryGetCard(id, out var card))
// L109
if (!list.Contains(zonesFromSelectFromGroup))
// L114
if (InvolvesOpponentHand(list, req.ZoneIds, _gameStateProvider.LatestGameState) || (list.Count == 1 && InvolvedZonesOverlap(list, _defaultSingleBrowserZoneTypes)))
// L116
return new SelectFromGroupsWorkflow_Browser(req, _cardViewProvider, _browserManager, _headerTextProvider);
// L118
if (req.ZoneIds.Count > 1 && !list.Exists((MtgZone x) => x.Type == ZoneType.Battlefield))
// L120
return new SelectFromGroupsWorkflow_MultiZone(req, _cardDatabase.ClientLocProvider, _gameStateProvider, _cardViewProvider, _browserManager, _headerTextProvider);
// L122
if (req.ZoneIds.Count == 0 && list.Count > 1)
// L128
return new SelectFromGroupsWorkflow(req, _genericPool, _gameStateProvider, _cardHolderProvider, _browserManager);
// L135
if (involvedZones.Exists((MtgZone x) => x.Type == zoneType))
```

### SelectNGroupTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectNGroup.SelectNGroupTranslation.cs)

```csharp
// L37
if (IsLilianaOfTheVeilUltimate(_gameManager.LatestGameState))
// L39
return new SelectNPileGroupWorkflow(req, _cardViewProvider, _browserController, _browserHeaderTextProvider);
// L41
if (IsHostileNegotiationsChoosePileFaceUp(_gameManager.LatestGameState, req))
// L43
return new SelectNFaceUpPileGroupWorkflow(req, _cardViewProvider, _gameManager.SplineMovementSystem, _browserController, _cardHolderProvider, _browserHeaderTextProvider);
// L45
return new SelectNGroupWorkflow(req, _cardViewProvider, _browserController, _browserHeaderTextProvider);
// L50
if (state.ResolvingCardInstance != null && state.ResolvingCardInstance.GrpId != 0)
// L59
if (state.TryGetCard(request.SourceId, out var card) && card.TitleId == 618299)
// L63
if (!group.IsFacedown)
```

### SelectNWorkflowTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectNWorkflowTranslation.cs)

```csharp
// L115
if (selectNRequest.IsTriggeredAbilitySelection)
// L117
return new TriggeredAbilityWorkflow(selectNRequest, _entityViewManager, _cardDatabase.ClientLocProvider, _browserManager, _unityObjectPool, FaceInfoGeneratorFactory.DuelScene.LeftBattlefieldGenerator(_cardDatabase, GetLatestGameState, _comparer), _assetLookupSystem, _context.Get<CardViewBuilder>(), _gameManager.transform);
// L119
if (selectNRequest.IsStackingDecision)
// L121
return new SelectNWorkflow_Stacking(selectNRequest);
// L123
if (selectNRequest.IsWishSelection)
// L125
return new SelectNWorkflow_Wish_DEBUG(selectNRequest, _cardDatabase);
// L127
if (selectNRequest.IsManaPoolSelection)
// L129
return new SelectNWorkflow_ManaPool(selectNRequest, _entityViewManager, _gameStateProvider);
// L131
if (selectNRequest.IsCardColorSelection || selectNRequest.IsManaColorSelection)
// L133
return new SelectColorWorkflow(selectNRequest, _cardDatabase, _gameStateProvider, _gameplaySettingsProvider, _promptTextProvider, _entityViewManager, _cardHolderProvider, _browserManager, _headerTextProvider, _playWarningManager, _gameManager.UIManager.ManaColorSelector, _assetLookupSystem);
// L135
if (selectNRequest.IsBasicLandSelection)
// L137
return new SelectBasicLandWorkflow(selectNRequest, _gameManager.UIManager.ManaColorSelector, _entityViewManager, _cardHolderProvider, _cardDatabase.ClientLocProvider, _browserManager, _gameplaySettingsProvider, _cardDatabase.PromptEngine, _cardDatabase, _gameStateProvider, _headerTextProvider, _assetLookupSystem);
// L139
if (selectNRequest.IsBasicLandSubtypeSelection)
// L141
return new SelectBasicLandSubtypeWorkflow(selectNRequest, _gameStateProvider, _cardHolderProvider, _entityViewManager, _cardDatabase.ClientLocProvider, _browserManager, _gameManager.UIManager.ManaColorSelector);
// L143
if (selectNRequest.IsCounterSelection)
// L145
return new SelectNWorkflow_Counters(selectNRequest, _objectPool, _unityObjectPool, _cardDatabase, _gameStateProvider, _gameplaySettingsProvider, _promptTextProvider, _browserManager, _headerTextProvider, _fakeCardViewController, _assetLookupSystem, _gameManager.SpinnerController);
// L147
if (selectNRequest.IsZoneSelection)
// L149
return _zoneSelectionTranslation.Translate(selectNRequest);
// L151
if (selectNRequest.IsTypeKindSelection)
// L153
return new TypeKindsWorkflow(selectNRequest);
// L155
if (selectNRequest.IsAnchorWordSelection(_gameStateProvider.LatestGameState))
// L157
return new SelectNWorkflow_AnchorWord(selectNRequest, _context);
// L159
if (selectNRequest.IsChoiceSelectionWithContext)
// L161
return _choiceWithContextTranslation.Translate(selectNRequest);
// L163
if (selectNRequest.IsChoiceSelection)
// L165
return _choiceSelectionTranslation.Translate(selectNRequest);
// L167
if (selectNRequest.IsLookSelection)
// L169
return new SelectNWorkflow_Look(selectNRequest, _objectPool, _cardDatabase, _entityViewManager, _browserManager, _fakeCardViewController);
// L171
if (selectNRequest.IsParitySelection)
// L173
return new ParityWorkflow(selectNRequest);
// L175
if (selectNRequest.IsDungeonSelection(_cardDatabase.CardDataProvider))
// L177
return new DungeonSelectWorkflow(selectNRequest, _entityViewManager, _cardDatabase.CardDataProvider, _cardHolderProvider, _browserManager, _splineMovementSystem);
// L179
if (selectNRequest.IsDungeonRoomSelection())
// L181
return new DungeonRoomSelectWorkflow(selectNRequest, DungeonRoomSelectWorkflow.DecidingPlayerDungeon(mtgGameState), _cardDatabase.ClientLocProvider, _browserManager);
// L183
if (selectNRequest.IsAbilitySelection)
// L185
return new SelectNWorkflow_Ability(selectNRequest, _cardDatabase, _cardDatabase.AbilityDataProvider, _fakeCardViewController, _gameStateProvider, _browserManager, _headerTextProvider);
// L187
if (selectNRequest.IsPrintingSelection)
// L189
return new SelectNWorkflow_Printing(selectNRequest, _cardDatabase, _gameStateProvider, _gameplaySettingsProvider, _fakeCardViewController, _browserManager, _headerTextProvider);
// L191
if (selectNRequest.IsRevealedCardSelection(mtgGameState))
// L193
return new RevealedCardSelection(selectNRequest, _cardDatabase, _gameStateProvider, _gameplaySettingsProvider, _fakeCardViewController, _browserManager, _headerTextProvider);
// L195
if (selectNRequest.IsPrintingIndexSelection)
// L197
return new SelectPrintingIndexWorkflow(selectNRequest, _cardDatabase, _gameStateProvider, _fakeCardViewController, _browserManager, _headerTextProvider);
// L199
if (SurveilStyleBrowserWorkflow<SelectNRequest>.UseSurveilStyleBrowser(selectNRequest, _assetLookupSystem, mtgGameState))
// L201
return new SelectNWorkflow_Selection_SurveilStyleBrowser(selectNRequest, _browserManager, _gameStateProvider, _cardDatabase, _cardDatabase.ClientLocProvider, _entityViewManager);
// L203
if (selectNRequest.IsRoomUnlockSubCardSelection(mtgGameState))
// L205
return new SelectNWorkflow_Selection_SubRooms(selectNRequest, _gameStateProvider, _cardDatabase, _fakeCardViewController, _cardHolderProvider, _browserManager, _headerTextProvider, _cardDatabase.ClientLocProvider);
// L207
if (selectNRequest.IsLimboSelection(mtgGameState, _cardDatabase))
// L209
return new LimboSelectionWorkflow(selectNRequest, _cardDatabase, _gameStateProvider, _gameplaySettingsProvider, _fakeCardViewController, _browserManager, _headerTextProvider, _entityViewManager);
// L214
if (!list.Contains(unfilteredId))
// L221
if (selectNRequest.IsWeightedSelection)
// L223
if (flag && !SelectNWorkflow_Selection_Weighted.IsForage(selectNRequest.Prompt))
// L225
return new SelectNWorkflow_Selection_Weighted_Browser(selectNRequest, _gameplaySettingsProvider, _entityViewManager, _browserManager, _headerTextProvider, _cardDatabase.AbilityDataProvider, _gameStateProvider, _assetLookupSystem);
// L227
if (useCardTypeSelection(mtgGameState, selectNRequest.Prompt?.Parameters))
// L229
return new SelectNWorkflow_Selection_DifferentCardTypes(selectNRequest, _gameStateProvider);
// L231
return new SelectNWorkflow_Selection_Weighted(selectNRequest, _cardDatabase.ClientLocProvider, _cardDatabase.AbilityDataProvider, _gameStateProvider, _gameplaySettingsProvider, _browserManager, _entityViewManager, _cardHolderProvider, _assetLookupSystem);
// L233
if (flag)
// L235
return new SelectNWorkflow_Selection_Browser(selectNRequest, _objectPool, _cardDatabase, _gameStateProvider, _resolutionEffectProvider, _gameplaySettingsProvider, _entityViewManager, _browserManager, _headerTextProvider, _playWarningManager);
// L237
if (OpenInBattlefieldHandBrowser(mtgGameState, selectNRequest, _entityViewManager))
// L239
return new SelectNWorkflow_Selection_BattlefieldHandBrowser(selectNRequest, _entityViewManager, _cardDatabase.ClientLocProvider, _browserManager, _headerTextProvider, _gameStateProvider, _cardDatabase);
// L241
if (IsDiscardHandActionWithBrowser(zonesFromSelectN, selectNRequest))
// L243
return new SelectNWorkflow_Selection_DiscardHandBrowser(selectNRequest, _entityViewManager, _cardDatabase.ClientLocProvider, _browserManager);
// L245
return _defaultTranslation.Translate(selectNRequest);
// L250
if (item.Type == ParameterType.Number && item.ParameterName == "CardId" && gameState.TryGetCard((uint)item.NumberValue, out var card) && card.GrpId == 151474)
// L272
if (currentState.TryGetCard(cardId, out var card))
// L276
if (type != ZoneType.Limbo)
// L281
else if (cardViewProvider.TryGetCardView(cardId, out cardView))
// L285
if (type2 != ZoneType.Limbo)
// L313
if (!involvedZones.Exists((MtgZone x) => x.Type == ZoneType.Hand))
// L317
if (request.Context != SelectionContext.Discard)
// L321
if (request.Ids.Count < 15)
// L325
if (request.MinSel <= 7)
// L334
if (involvedZones.Count <= 1)
// L341
if ((uint)(type - 1) > 1u && (uint)(type - 5) > 1u && type != ZoneType.Sideboard)
// L351
if (involvedZones.Count != 1)
// L355
if (involvedZones.Exists((MtgZone x) => x.Type == ZoneType.Hand && x.OwnerNum == GREPlayerNum.Opponent))
// L368
switch (zonesFromCardId.Type)
// L370
case ZoneType.Hand:
// L373
case ZoneType.Battlefield:
// L380
if (request.Prompt.PromptId == 16461 && num != 0)
```

### SelectReplacementTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectReplacements.SelectReplacementTranslation.cs)

```csharp
// L37
if (!PickWhichTurtleToHurtle(req))
// L39
return new SelectReplacementWorkflow(req, _cardDatabase, _gameStateProvider, _fakeCardViewController, _browserController, _headerTextProvider);
// L41
return new TurtleHurtleWorkflow(req, _objectPool, _cardViewProvider);
```

### SelectTargetsTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectTargetsTranslation.cs)

```csharp
// L63
return new SelectTargetsWorkflow(req, _objPool, _cardDatabase, _gameStateProvider, _gameplaySettingsProvider, _cardHolderProvider, _browserManager, _headerTextProvider, _cardViewProvider, _promptTextProvider, _playWarningManager, _autoTargetingSolution, _assetLookupSystem);
```

### SelectZonesTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectZonesTranslation.cs)

```csharp
// L39
if (IsSingleZoneSelection(req))
// L41
return new SelectZoneWorkflow(req, _gameStateProvider, _cardHolderProvider, _entityViewProvider, _assetLookupSystem);
// L43
return new SelectZonesWorkflow(req, _gameStateProvider, _clientLocProvider, _browserController);
// L48
if (req.MinSel == 1 && req.MaxSel == 1 && req.Ids.Count == 2)
```

### SelectionWorkflowTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.SelectionWorkflowTranslation.cs)

```csharp
// L46
return new SelectionWorkflow(req, _cardDatabase, _gameStateProvider, _resolutionEffectProvider, _gameplaySettingsProvider, _cardViewProvider, _cardHolderProvider, _browserManager, _selectionConfirmation, _playWarningManager, _assetLookupSystem);
```

### SingleActionTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.SingleActionTranslation.cs)

```csharp
// L10
if (interactions.Count != 1)
// L15
if (greInteraction == null)
// L20
if (greAction == null)
```

### StaticCardNameTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticCardNameTranslation.cs)

```csharp
// L45
if ((ids.Count != 0 && !ids.Contains(value.TitleId)) || value.IsToken || value.IsRebalanced || value.LinkedFaceType == LinkedFace.SplitHalf || value.LinkedFaceType == LinkedFace.RoomHalf || value.ExpansionCode.Equals("ArenaSUP"))
// L50
if (cardTitle != null && !dictionary.ContainsKey(cardTitle))
// L54
if (localizedTitle != null && !dictionary.ContainsKey(localizedTitle))
// L69
if (promptParameter != null)
// L72
if (cardById != null)
// L76
if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<NameACardPrepopulationLogic> loadedTree))
// L79
if (payload != null)
// L94
if (gameState.ResolvingCardInstance != null)
// L98
if (_assetLookupSystem.TreeLoader.TryLoadTree(out AssetLookupTree<NameACardPrepopulationLogic> loadedTree2))
// L101
if (payload2 != null)
// L103
if (payload2.SetSourceGrpIdsToDeckGrpIds)
// L121
if (!ExcludeCard(cardPrinting))
// L123
if (includeControlledCreatures && (gameState.Battlefield.VisibleCards.Exists((MtgCardInstance ci) => ci.GrpId == cardPrinting.GrpId && ci.Zone.Type == ZoneType.Battlefield && ci.Owner.IsLocalPlayer) || (IsDualFaceType(cardPrinting.LinkedFaceType) && gameState.Battlefield.VisibleCards.Exists((MtgCardInstance ci) => cardPrinting.LinkedFaceGrpIds.Contains(ci.GrpId) && ci.Zone.Type == ZoneType.Battlefield && ci.Owner.IsLocalPlayer))))
// L127
if ((!ids.Any() && !flag) || ids.Contains(cardPrinting.TitleId))
// L133
if ((list2.Count == 0) & flag)
// L138
if (!ExcludeCard(cardPrintingById))
// L147
return new ChoiceOptions(dictionary, list, list2);
// L150
if (cardPrintingData == null)
// L154
if (cardPrintingData.LinkedFaceType == LinkedFace.SplitHalf)
// L158
if (!includeBackOfDoubleFaceCards && cardPrintingData.LinkedFaceType == LinkedFace.DfcFront)
// L162
if (!includeSplitChildren && cardPrintingData.LinkedFaceType == LinkedFace.SplitCard)
// L166
if (!includeAdventureChildren && cardPrintingData.LinkedFaceType == LinkedFace.AdventureParent)
// L170
if (!includeLands && cardPrintingData.Types.Contains(CardType.Land))
// L174
if (includeControlledCreatures && !cardPrintingData.Types.Contains(CardType.Creature))
// L184
if (!includeNonOpponentCards && !enumerable2.Contains(cardPrintingData.GrpId) && !enumerable3.Contains(cardPrintingData.GrpId) && enumerable2.Intersect(cardPrintingData.LinkedFaceGrpIds).Count() == 0 && enumerable3.Intersect(cardPrintingData.LinkedFaceGrpIds).Count() == 0)
// L188
if (!includeNonVisibleSpecializeChildren && (cardPrintingData.LinkedFaceType == LinkedFace.SpecializeChild || cardPrintingData.LinkedFaceType == LinkedFace.SpecializeParent) && !gameState.VisibleCards.Select((KeyValuePair<uint, MtgCardInstance> c) => c.Value.GrpId).Contains(cardPrintingData.GrpId) && !gameState.RevealedCards.Select((KeyValuePair<uint, MtgCardInstance> c) => c.Value.GrpId).Contains(cardPrintingData.GrpId))
// L196
if (linkedFace != LinkedFace.DfcFront && linkedFace != LinkedFace.DfcBack && linkedFace != LinkedFace.MdfcBack)
// L207
if ((choiceToIdMap == null || choiceToIdMap.ContainsKey(originalCardTitle)) && !hintingOptions.Contains(originalCardTitle))
// L211
if (localizedTitle != null && !hintingOptions.Contains(localizedTitle))
```

### StaticCardTypeTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticCardTypeTranslation.cs)

```csharp
// L55
if (ShouldUseDeckBasedHinting(((promptParameter != null) ? mtgGameState.GetCardById((uint)promptParameter.NumberValue) : null)?.TitleId ?? 0))
// L60
if (cardPrintingById == null || CardUtilities.IsOmenOrAdventureParentFace(cardPrintingById.LinkedFaceType) || CardUtilities.IsFrontFaceOfDoubleSided(cardPrintingById.LinkedFaceType) || cardPrintingById.LinkedFaceType == LinkedFace.SplitHalf)
// L67
if (dictionary.ContainsKey(localizedTextForEnumValue2) && !list2.Contains(localizedTextForEnumValue2))
// L74
else if (HintAllCastableTypes(_cardDatabase, mtgGameState))
// L79
if (dictionary.ContainsKey(localizedTextForEnumValue3))
// L94
if (dictionary.ContainsKey(localizedTextForEnumValue4) && !list2.Contains(localizedTextForEnumValue4))
// L103
return new ChoiceOptions(dictionary, list, list2);
// L108
if (cardTitleId == 428606 || cardTitleId == 477101)
// L118
if (topCardOnStack == null)
```

### StaticCounterTypeTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticCounterTypeTranslation.cs)

```csharp
// L43
if (dictionary.ContainsKey(localizedTextForEnumValue2) && !list2.Contains(localizedTextForEnumValue2))
// L52
return new ChoiceOptions(dictionary, list, list2);
```

### StaticKeywordTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticKeywordTranslation.cs)

```csharp
// L28
return new ChoiceOptions(dictionary, list, Array.Empty<string>());
```

### StaticListTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticListTranslation.cs)

```csharp
// L19
if (_typeToTranslationMap.TryGetValue(request.StaticList, out var value))
// L21
return value.Translate(request);
```

### StaticSubtypeTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticSubtypeTranslation.cs)

```csharp
// L30
if (item != num)
// L39
if (dictionary.ContainsKey(localizedTextForEnumValue2) && !list2.Contains(localizedTextForEnumValue2))
// L47
return new ChoiceOptions(dictionary, list, list2);
```

### StaticSuperTypeTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SelectN.StaticSuperTypeTranslation.cs)

```csharp
// L43
if (dictionary.ContainsKey(localizedTextForEnumValue2) && !list2.Contains(localizedTextForEnumValue2))
// L52
return new ChoiceOptions(dictionary, list, list2);
```

### SubmitDeckTranslation

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.SubmitDeckTranslation.cs)

```csharp
// L45
return new SubmitDeckWorkflow(req, _camera, _cardDatabase, _accountClient, _gameStateProvider, _duelSceneStateController, _matchManager, _assetLookupSystem, GetSideboardTimer(_gameStateProvider.LatestGameState));
```

### WorkflowTranslator

[Full source](ui-sources/Wotc.Mtga.DuelScene.Interactions.WorkflowTranslator.cs)

```csharp
// L112
if (!(req is ActionsAvailableRequest req2))
// L114
if (!(req is MulliganRequest req3))
// L116
if (!(req is ChooseStartingPlayerRequest req4))
// L118
if (!(req is StringInputRequest req5))
// L120
if (!(req is NumericInputRequest req6))
// L122
if (!(req is GatherRequest req7))
// L124
if (!(req is OrderRequest req8))
// L126
if (!(req is GroupRequest req9))
// L128
if (!(req is SearchRequest req10))
// L130
if (!(req is AssignDamageRequest req11))
// L132
if (!(req is SelectNGroupRequest req12))
// L134
if (!(req is OptionalActionMessageRequest req13))
// L136
if (!(req is CastingTimeOptionRequest req14))
// L138
if (!(req is CastingTimeOption_ManaTypeRequest req15))
// L140
if (!(req is DistributionRequest req16))
// L142
if (!(req is SelectFromGroupsRequest req17))
// L144
if (!(req is SelectReplacementRequest req18))
// L146
if (!(req is SelectNRequest req19))
// L148
if (!(req is PayCostsRequest req20))
// L150
if (!(req is SearchFromGroupsRequest req21))
// L152
if (!(req is EffectCostRequest req22))
// L154
if (!(req is AutoTapActionsRequest req23))
// L156
if (!(req is IntermissionRequest req24))
// L158
if (!(req is SubmitDeckRequest req25))
// L160
if (!(req is SelectCountersRequest req26))
// L162
if (!(req is SelectTargetsRequest req27))
// L164
if (!(req is DeclareAttackerRequest req28))
// L166
if (req is DeclareBlockersRequest req29)
// L168
return _declareBlockersTranslation.Translate(req29);
// L172
return _declareAttackersTranslation.Translate(req28);
// L174
return _selectTargetsTranslation.Translate(req27);
// L176
return _selectCountersTranslation.Translate(req26);
// L178
return _submitDeckTranslation.Translate(req25);
// L180
return _intermissionTranslation.Translate(req24);
// L182
return _autoTapActionsTranslation.Translate(req23);
// L184
return _effectCostTranslation.Translate(req22);
// L186
return _searchFromGroupsTranslation.Translate(req21);
// L188
return _payCostsTranslation.Translate(req20);
// L190
return _selectNTranslation.Translate(req19);
// L192
return _selectReplacementTranslation.Translate(req18);
// L194
return _selectFromGroupsTranslation.Translate(req17);
// L196
return _distributionTranslation.Translate(req16);
// L198
return _ctoManaTypeTranslation.Translate(req15);
// L200
return _castingTimeOptionTranslation.Translate(req14);
// L202
return _optionalActionTranslation.Translate(req13);
// L204
return _selectNGroupTranslation.Translate(req12);
// L206
return _assignDamageTranslation.Translate(req11);
// L208
return _searchTranslation.Translate(req10);
// L210
return _groupTranslation.Translate(req9);
// L212
return _orderTranslation.Translate(req8);
// L214
return _gatherTranslation.Translate(req7);
// L216
return _numericInputTranslation.Translate(req6);
// L218
return _adornCardTranslation.Translate(req5);
// L220
return _chooseStartingPlayerTranslation.Translate(req4);
// L222
return _mulliganTranslation.Translate(req3);
// L224
return _actionsAvailableTranslation.Translate(req2);
```
