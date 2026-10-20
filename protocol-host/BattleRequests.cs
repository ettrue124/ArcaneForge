using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

static class BattleRequests
{
    static uint TargetPrompt(JsonElement snapshot, JsonElement input)
    {
        uint min = input.GetProperty("min").GetUInt32(), max = input.GetProperty("max").GetUInt32();
        uint generic = min == 1 && max == 1 ? 1072u : 10u;
        var targetIds = input.GetProperty("targets").EnumerateArray().Select(t => t.GetUInt32()).ToHashSet();
        if (input.TryGetProperty("selected", out var selected))
            targetIds.UnionWith(selected.EnumerateArray().Select(t => t.GetUInt32()));
        bool playerTargets = targetIds.Any(id => id >= 1 && id <= 4);
        bool cardTargets = targetIds.Any(id => id > 4);
        // Forge's aura restriction includes Player.CanBeEnchantedBy even for
        // artifact-only auras. Actual legal candidates determine whether the
        // player-specific prompt applies. Mixed targets keep a general prompt.
        if (playerTargets) return cardTargets ? generic : 1019u;
        bool allArtifacts = false;
        if (cardTargets && snapshot.TryGetProperty("cards", out var cards)) {
            var candidates = cards.EnumerateArray().Where(c => targetIds.Contains(c.GetProperty("instanceId").GetUInt32())).ToArray();
            allArtifacts = candidates.Length == targetIds.Count && candidates.All(c =>
                c.TryGetProperty("cardTypes", out var kinds) && kinds.EnumerateArray().Any(k => k.GetString() == "Artifact"));
        }
        string desc = input.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        if (desc.Contains("+1/+1 counter", StringComparison.OrdinalIgnoreCase)) return 1463u; // "Target a creature on which to put a +1/+1 counter."
        // A fight spell's subability shares the root description. Use this
        // target group's restriction before scanning that combined text.
        if(input.TryGetProperty("validTargetTypes",out var restrictions)) {
            var valid=restrictions.EnumerateArray().Select(t=>t.GetString()??"").ToArray();
            if(valid.Any(t=>t.Contains("Creature.YouDontCtrl",StringComparison.OrdinalIgnoreCase)))return 1014u;
            if(valid.Any(t=>t.Contains("Creature.YouCtrl",StringComparison.OrdinalIgnoreCase)))return 152u;
        }
        if (desc.Contains("creature you control", StringComparison.OrdinalIgnoreCase)) return 152u; // "Target a creature you control."
        if (desc.Contains("creature an opponent controls", StringComparison.OrdinalIgnoreCase)
            || desc.Contains("creature you don't control", StringComparison.OrdinalIgnoreCase)) return 1014u; // "Target a creature an opponent controls."
        if (desc.Contains("artifact or enchantment", StringComparison.OrdinalIgnoreCase)
            || desc.Contains("artifact and/or enchantment", StringComparison.OrdinalIgnoreCase)) return 1064u; // "Target an artifact or enchantment."
        if (!cardTargets && (desc.Contains("target player", StringComparison.OrdinalIgnoreCase)
            || desc.Contains("target opponent", StringComparison.OrdinalIgnoreCase))) return 1019u; // "Target a player."
        if (input.TryGetProperty("validTargetTypes", out var types)) {
            var valid = types.EnumerateArray().Select(t => t.GetString() ?? "").ToArray();
            if (valid.Any(t => t.Contains("Creature", StringComparison.OrdinalIgnoreCase)) && max == 1)
                return 1010u; // "Target a creature."
            if (allArtifacts) return 1023u; // Installed native prompt: "Target an artifact."
            if (valid.Any(t => t.Contains("Artifact", StringComparison.OrdinalIgnoreCase) || t.Contains("Enchantment", StringComparison.OrdinalIgnoreCase)))
                return 1064u; // "Target an artifact or enchantment."
            if (valid.Any(t => t.Contains("Permanent", StringComparison.OrdinalIgnoreCase)))
                return 1006u; // "Target a permanent."
            if (!cardTargets && valid.All(t => t.StartsWith("Player", StringComparison.OrdinalIgnoreCase) || t.StartsWith("Opponent", StringComparison.OrdinalIgnoreCase)))
                return 1019u; // "Target a player."
        }
        return generic; // 1072 = "Choose a target.", 10 = "Choose targets."
    }
    static DamageRecipient Recipient(uint id) => id <= 4
        ? new DamageRecipient { Type = (DamageRecType)2, PlayerSystemSeatId = id }
        : new DamageRecipient { Type = (DamageRecType)3, PlaneswalkerInstanceId = id };
    public static uint RecipientId(DamageRecipient value) => value.Type == (DamageRecType)2
        ? value.PlayerSystemSeatId : value.Type == (DamageRecType)3 ? value.PlaneswalkerInstanceId
        : throw new InvalidDataException("Unsupported combat recipient");
    public static GREToClientMessage? Create(JsonElement snapshot, uint msgId, uint stateId)
    {
        if (!snapshot.TryGetProperty("input", out var input)) return null;
        var result = new GREToClientMessage { MsgId = msgId, GameStateId = stateId, SystemSeatIds = { 1 } };
        var nativeMenu=BattleNativeMenus.Create(snapshot,input,msgId,stateId);
        if(nativeMenu!=null)return nativeMenu;
        if(input.GetProperty("type").GetString()!="InputGuiStackTargets" && (input.GetProperty("type").GetString()??"").StartsWith("InputGui",StringComparison.Ordinal))return BattleGuiRequests.Create(input,msgId,stateId);
        switch (input.GetProperty("type").GetString()) {
            case "InputPayMana":
            case "InputPayManaOfCostPayment": {
                result.Type = GREMessageType.PayCostsReq;
                var payCosts = new PayCostsReq();
                if (input.TryGetProperty("manaCost", out var costElem)) {
                    if(!MatchDoorHost.TryParseManaCost(costElem.GetString() ?? "", payCosts.ManaCost)) {
                        // Keep manual mana actions available without displaying
                        // a partial cost as though it were the complete payment.
                        result.Type=GREMessageType.ActionsAvailableReq;
                        result.ActionsAvailableReq=new ActionsAvailableReq();
                        result.ActionsAvailableReq.Actions.AddRange(BattlePresentation.Actions(snapshot));
                        result.ActionsAvailableReq.Actions.Add(new Wotc.Mtgo.Gre.External.Messaging.Action {ActionType=ActionType.Pass});
                        result.AllowCancel=input.TryGetProperty("mandatory",out var mandatory)&&mandatory.GetBoolean()?AllowCancel.No:AllowCancel.Abort;
                        return result;
                    }
                }
                uint costSourceId = input.TryGetProperty("sourceId", out var src) ? src.GetUInt32() : 0;
                if(snapshot.TryGetProperty("stack",out var paymentStack))
                    foreach(var item in paymentStack.EnumerateArray())
                        if(item.GetProperty("parentId").GetUInt32()==costSourceId) {
                            costSourceId=item.GetProperty("instanceId").GetUInt32();break;
                        }
                foreach (var req in payCosts.ManaCost) {
                    req.ObjectId = costSourceId;
                }
                payCosts.PaymentActions = new ActionsAvailableReq();
                foreach(var offered in BattlePresentation.Actions(snapshot)) {
                    if(offered.Highlight==HighlightType.Cold)payCosts.PaymentActions.InactiveActions.Add(offered);
                    else payCosts.PaymentActions.Actions.Add(offered);
                }
                var autoTapReq = new AutoTapActionsAvailableReq();
                var solution = new AutoTapSolution();
                if (input.TryGetProperty("autoTapSources", out var autoTapSources)) {
                    foreach (var source in autoTapSources.EnumerateArray()) {
                        solution.AutoTapActions.Add(new AutoTapAction { InstanceId = source.GetUInt32() });
                    }
                }
                if(input.TryGetProperty("autoTapPool",out var paymentPool))foreach(var mana in paymentPool.EnumerateArray()) {
                    var info=new ManaInfo {ManaId=mana.GetProperty("manaId").GetUInt32(),Color=(ManaColor)mana.GetProperty("color").GetInt32(),
                        SrcInstanceId=mana.GetProperty("srcInstanceId").GetUInt32(),Count=mana.GetProperty("count").GetUInt32()};
                    if(mana.TryGetProperty("restricted",out var restricted) && restricted.GetBoolean())
                        info.Specs.Add(new ManaInfo.Types.Spec {Type=ManaSpecType.Restricted});
                    solution.AutoTapActions.Add(new AutoTapAction {ManaId=info.ManaId,ManaPaymentOption=new ManaPaymentOption {Mana={info}}});
                }
                if (solution.AutoTapActions.Count > 0)autoTapReq.AutoTapSolutions.Add(solution);
                // Keep the native payment workflow present even when Forge
                // cannot propose a complete automatic solution. No empty
                // solution is advertised as though the cost had been paid.
                payCosts.AutoTapActionsReq = autoTapReq;
                result.PayCostsReq = payCosts;
                result.Prompt = new Prompt { PromptId = 23 };
                result.AllowCancel = input.TryGetProperty("mandatory", out var payMand) && payMand.GetBoolean() ? AllowCancel.No : AllowCancel.Abort;
                result.AllowUndo = snapshot.TryGetProperty("canUndo", out var canUndo) && canUndo.GetBoolean();
                break;
            }
            case "InputChooseNumber":
                if(!input.GetProperty("isX").GetBoolean() && input.GetProperty("max").GetUInt32()<=10
                    && input.GetProperty("min").GetUInt32()<=input.GetProperty("max").GetUInt32()) {
                    var first=input.GetProperty("min").GetUInt32();
                    var last=input.GetProperty("max").GetUInt32();
                    var permitted=input.TryGetProperty("allowed",out var offeredNumbers) && offeredNumbers.GetArrayLength()>0
                        ?offeredNumbers.EnumerateArray().Select(v=>v.GetUInt32()).ToHashSet():null;
                    var options=Enumerable.Range((int)first,(int)(last-first+1))
                        .Where(v=>permitted==null || permitted.Contains((uint)v))
                        .Select(v=>new {id=v+1,label=v.ToString()}).ToArray();
                    using var choice=JsonDocument.Parse(JsonSerializer.Serialize(new {type="InputGuiChoice",min=1,max=1,
                        sourceId=input.GetProperty("sourceId").GetUInt32(),entityChoices=false,
                        title=input.TryGetProperty("title",out var nt)?nt.GetString():"Choose a number",options}));
                    return BattleGuiRequests.Choice(choice.RootElement,msgId,stateId);
                }
                var numeric=new NumericInputReq {MinValue=input.GetProperty("min").GetUInt32(),MaxValue=input.GetProperty("max").GetUInt32(),StepSize=1,
                    SourceId=input.GetProperty("sourceId").GetUInt32(),NumericInputType=input.GetProperty("isX").GetBoolean()?NumericInputType.ChooseX:NumericInputType.ChooseAnyAmount};
                if(input.TryGetProperty("allowed",out var allowed) && allowed.GetArrayLength()>0) {
                    var values=allowed.EnumerateArray().Select(v=>v.GetUInt32()).ToHashSet();
                    numeric.SuggestedValues.AddRange(values.Order());
                    for(uint v=numeric.MinValue;v<=numeric.MaxValue;v++)if(!values.Contains(v))numeric.DisallowedValues.Add(v);
                }
                if(!input.GetProperty("isX").GetBoolean()) {
                    result.Type=GREMessageType.NumericInputReq;
                    result.NumericInputReq=numeric;
                    result.AllowCancel=AllowCancel.No;
                    if(input.TryGetProperty("title",out var numberQuestion) && !string.IsNullOrWhiteSpace(numberQuestion.GetString()))
                        result.Prompt=new Prompt {PromptId=2920000,Parameters={new PromptParameter {
                            ParameterName="question",Type=ParameterType.NonLocalizedString,StringValue=numberQuestion.GetString()!}}};
                    if(input.TryGetProperty("promptId",out var pId)) {
                        result.Prompt=new Prompt {PromptId=pId.GetUInt32()};
                    } else if(input.TryGetProperty("readAhead",out var ra) && ra.GetBoolean()) {
                        result.Prompt=new Prompt {PromptId=166};
                    }
                    break;
                }
                result.Type=GREMessageType.CastingTimeOptionsReq;
                result.CastingTimeOptionsReq=new CastingTimeOptionsReq {CastingTimeOptionReq={new CastingTimeOptionReq {
                    CtoId=checked((uint)snapshot.GetProperty("buttonEventId").GetInt64()),
                    CastingTimeOptionType=CastingTimeOptionType.ChooseX,GrpId=input.GetProperty("grpId").GetUInt32(),
                    AffectorId=numeric.SourceId,AffectedId=numeric.SourceId,PlayerIdToPrompt=1,IsRequired=true,NumericInputReq=numeric}}};
                // NumberPending is a blocking decision, not an Input proxy
                // that can receive selectButtonCancel.
                result.AllowCancel=AllowCancel.No;
                break;
            case "InputSubmitDeck": {
                result.Type = GREMessageType.SubmitDeckReq;
                var submitReq = new SubmitDeckReq { Deck = new DeckMessage() };
                if (input.TryGetProperty("deckCards", out var dc)) {
                    submitReq.Deck.DeckCards.AddRange(dc.EnumerateArray().Select(c => c.GetUInt32()));
                }
                if (input.TryGetProperty("sideboardCards", out var sc)) {
                    submitReq.Deck.SideboardCards.AddRange(sc.EnumerateArray().Select(c => c.GetUInt32()));
                }
                result.SubmitDeckReq = submitReq;
                break;
            }
            case "InputConfirm":
                if (!input.GetProperty("startingPlayer").GetBoolean()) {
                    uint source = input.GetProperty("sourceId").GetUInt32();
                    string msg = input.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
                    if(input.TryGetProperty("optionalDraw",out var optionalDraw) && optionalDraw.GetBoolean()) {
                        uint drawSource=source;
                        if(snapshot.TryGetProperty("stack",out var drawStack))
                            foreach(var item in drawStack.EnumerateArray())
                                if(item.GetProperty("parentId").GetUInt32()==source)drawSource=item.GetProperty("instanceId").GetUInt32();
                        result.Type=GREMessageType.OptionalActionMessage;
                        result.Prompt=new Prompt {PromptId=1001};
                        result.OptionalActionMessage=new OptionalActionMessage {SourceId=drawSource,Prompt=result.Prompt,
                            Highlight=HighlightType.Hot,HighlightIds={source}};
                        return result;
                    }
                    if(input.TryGetProperty("optionalSacrificePrompt",out var sacrificePrompt)) {
                        uint resolvingSource=source;
                        if(snapshot.TryGetProperty("stack",out var sacrificeStack))
                            foreach(var item in sacrificeStack.EnumerateArray())
                                if(item.GetProperty("parentId").GetUInt32()==source)resolvingSource=item.GetProperty("instanceId").GetUInt32();
                        result.Type=GREMessageType.OptionalActionMessage;
                        result.Prompt=new Prompt {PromptId=sacrificePrompt.GetUInt32()};
                        result.OptionalActionMessage=new OptionalActionMessage {SourceId=resolvingSource,Prompt=result.Prompt,
                            OptionalActionTypes={CardMechanicType.SacrificePermanent},Highlight=HighlightType.Hot,HighlightIds={source}};
                        return result;
                    }
                    if(input.TryGetProperty("optionalTrigger",out var optionalTrigger) && optionalTrigger.GetBoolean()) {
                        uint triggerSource=source;
                        if(snapshot.TryGetProperty("stack",out var triggerStack))
                            foreach(var item in triggerStack.EnumerateArray())
                                if(item.GetProperty("parentId").GetUInt32()==source)triggerSource=item.GetProperty("instanceId").GetUInt32();
                        result.Type=GREMessageType.OptionalActionMessage;
                        result.Prompt=new Prompt {PromptId=input.TryGetProperty("optionalTriggerPrompt",out var triggerPrompt)?triggerPrompt.GetUInt32():0u};
                        result.OptionalActionMessage=new OptionalActionMessage {SourceId=triggerSource,Prompt=result.Prompt,
                            Highlight=HighlightType.Hot,HighlightIds={source}};
                        return result;
                    }
                    if(input.TryGetProperty("freeCastCardId",out var offeredCard)) {
                        uint offeredId=offeredCard.GetUInt32();
                        var offered=snapshot.GetProperty("cards").EnumerateArray().FirstOrDefault(c=>c.GetProperty("instanceId").GetUInt32()==offeredId);
                        uint resolvingId=source;
                        if(snapshot.TryGetProperty("stack",out var stack))
                            foreach(var item in stack.EnumerateArray())
                                if(item.GetProperty("parentId").GetUInt32()==source)resolvingId=item.GetProperty("instanceId").GetUInt32();
                        if(offered.ValueKind==JsonValueKind.Object) {
                            result.Type=GREMessageType.ActionsAvailableReq;
                            result.ActionsAvailableReq=new ActionsAvailableReq {Actions={
                                new Wotc.Mtgo.Gre.External.Messaging.Action {ActionType=offered.TryGetProperty("isLand",out var isLand) && isLand.GetBoolean()?ActionType.Play:ActionType.Cast,InstanceId=offeredId,
                                    GrpId=offered.GetProperty("grpId").GetUInt32(),SourceId=resolvingId,Highlight=HighlightType.Hot},
                                new Wotc.Mtgo.Gre.External.Messaging.Action {ActionType=ActionType.Pass}}};
                            result.Prompt=new Prompt {PromptId=1134};
                            return result;
                        }
                    }
                    if(input.TryGetProperty("exploreCardId",out var explored)) {
                        uint exploredId=explored.GetUInt32();
                        var explorePrompt=new Prompt {PromptId=115,Parameters={new PromptParameter {
                            ParameterName="1",Type=ParameterType.Reference,Reference=new Reference {Type=ReferenceType.InstanceId,Id=source}},new PromptParameter {
                            ParameterName="2",Type=ParameterType.Reference,Reference=new Reference {Type=ReferenceType.InstanceId,Id=exploredId}}}};
                        result.Type=GREMessageType.OptionalActionMessage;result.Prompt=explorePrompt;
                        result.OptionalActionMessage=new OptionalActionMessage {SourceId=source,Prompt=explorePrompt,
                            OptionalActionTypes={CardMechanicType.Explore},RecipientIds={exploredId}};
                        return result;
                    }
                    var lifePayment=System.Text.RegularExpressions.Regex.Match(msg.Trim(),
                        @"^(?:(?:Do you want to|Would you like to|You may)\s+)?pay\s+(\d+)\s+life[?.]?$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if(lifePayment.Success && int.TryParse(lifePayment.Groups[1].Value,out var lifeAmount)) {
                        // Arena's formatter uses list position, not ParameterName.
                        // These prompts reserve argument 1 for the source card.
                        var lifePrompt=new Prompt {PromptId=8167,Parameters={new PromptParameter {
                            ParameterName="1",Type=ParameterType.Reference,Reference=new Reference {Type=ReferenceType.InstanceId,Id=source}},new PromptParameter {
                            ParameterName="2",Type=ParameterType.Number,NumberValue=lifeAmount}}};
                        result.Type=GREMessageType.OptionalActionMessage;
                        result.Prompt=lifePrompt;
                        result.OptionalActionMessage=new OptionalActionMessage {SourceId=source,Prompt=lifePrompt,
                            OptionalActionTypes={CardMechanicType.PayLife}};
                        return result;
                    }
                    if(!msg.StartsWith("Sacrifice ",StringComparison.OrdinalIgnoreCase) && !msg.Contains("command zone",StringComparison.OrdinalIgnoreCase)
                        && input.TryGetProperty("options",out var confirmOptions)) {
                        using var choice=JsonDocument.Parse(JsonSerializer.Serialize(new {sourceId=source,title=msg,min=1,max=1,entityChoices=false,
                            options=confirmOptions.EnumerateArray().Select((o,i)=>new {id=i+1,label=o.GetString()})}));
                        return BattleGuiRequests.Choice(choice.RootElement,msgId,stateId);
                    }
                    if (source == 0) return null;
                    string cardName = input.TryGetProperty("cardName", out var cn) ? cn.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(cardName) && msg.StartsWith("Sacrifice ", StringComparison.OrdinalIgnoreCase) && msg.EndsWith("?")) {
                        cardName = msg.Substring(10, msg.Length - 11).Trim();
                    }
                    if (string.IsNullOrEmpty(cardName) && snapshot.TryGetProperty("cards", out var confirmCards)) {
                        foreach (var c in confirmCards.EnumerateArray()) {
                            if (c.GetProperty("instanceId").GetUInt32() == source && c.TryGetProperty("name", out var n)) {
                                cardName = n.GetString() ?? "";
                                break;
                            }
                        }
                    }
                    result.Type = (GREMessageType)45;
                    bool commandReturn=msg.Contains("command zone",StringComparison.OrdinalIgnoreCase);
                    var prompt = new Prompt { PromptId = commandReturn?144u:1739u };
                    if (!commandReturn && !string.IsNullOrEmpty(cardName)) {
                        prompt.Parameters.Add(new PromptParameter {
                            ParameterName = "1",
                            Type = (ParameterType)1,
                            StringValue = cardName
                        });
                    } else if(!commandReturn) {
                        prompt.Parameters.Add(new PromptParameter {
                            ParameterName = "1",
                            Type = (ParameterType)3,
                            Reference = new Reference { Type = (ReferenceType)1, Id = source }
                        });
                    }
                    result.Prompt = prompt;
                    result.OptionalActionMessage = new OptionalActionMessage { SourceId = source, Prompt = result.Prompt };
                    break;
                }
                result.Type = GREMessageType.ChooseStartingPlayerReq;
                result.ChooseStartingPlayerReq = new ChooseStartingPlayerReq { TeamType = (TeamType)1, SystemSeatIds = { 1, 2 } };
                break;
            case "InputSelectCardsFromList":
            case "InputSelectEntitiesFromList":
                result.Type = (GREMessageType)22;
                string purpose=input.TryGetProperty("selectionPurpose",out var selectionPurpose)?selectionPurpose.GetString()??"":"";
                bool fixedDiscard=purpose=="discard" && input.GetProperty("min").GetInt32()==input.GetProperty("max").GetInt32();
                result.Prompt = new Prompt { PromptId = fixedDiscard?14u:purpose=="sacrificeCreature"?1029u:2428u };
                if(fixedDiscard)result.Prompt.Parameters.Add(new PromptParameter {Type=ParameterType.Number,ParameterName="1",NumberValue=input.GetProperty("min").GetInt32()});
                result.SelectNReq = new SelectNReq { MinSel = input.GetProperty("min").GetInt32(), MaxSel = input.GetProperty("max").GetUInt32(),
                    SourceId=input.TryGetProperty("sourceId",out var selectionSource)?selectionSource.GetUInt32():0u,
                    IdType = IdType.InstanceId, ListType = SelectionListType.Dynamic, Context = purpose=="discard"?SelectionContext.Discard:SelectionContext.Resolution,
                    OptionContext = OptionContext.Resolution, Prompt = result.Prompt };
                result.SelectNReq.Ids.AddRange(input.GetProperty("choices").EnumerateArray().Select(id => id.GetUInt32()));
                result.SelectNReq.UnfilteredIds.AddRange(result.SelectNReq.Ids);
                result.SelectNReq.HotIds.AddRange(result.SelectNReq.Ids);
                // Sacrifices are chosen as one batch and confirmed by the player.
                // HotIds can be treated as an initial selection by the client.
                if(purpose.StartsWith("sacrifice",StringComparison.Ordinal))result.SelectNReq.HotIds.Clear();
                result.AllowCancel = input.GetProperty("min").GetInt32() == 0 ? AllowCancel.Continue : AllowCancel.No;
                break;
            case "InputSelectTargets":
            case "InputGuiStackTargets":
                result.Type = GREMessageType.SelectTargetsReq;
                var selection = new TargetSelection { TargetIdx = input.TryGetProperty("targetIndex",out var index)?index.GetUInt32():0, MinTargets = input.GetProperty("min").GetUInt32(),
                    MaxTargets = input.GetProperty("max").GetUInt32(), TargetingPlayer = input.TryGetProperty("targetingPlayer",out var player)?player.GetUInt32():1,
                    Prompt = new Prompt { PromptId = TargetPrompt(snapshot, input) } };
                if(input.GetProperty("type").GetString()=="InputGuiStackTargets") {
                    // A stack choice must carry real spell/ability instance
                    // IDs. Prompt-index options become blank preview cards in
                    // the native stack browser instead of readable buttons.
                    var stackTargets=input.GetProperty("targets").EnumerateArray().Select(t=>t.GetUInt32()).ToHashSet();
                    bool spellsOnly=snapshot.TryGetProperty("stack",out var stack)
                        && stack.EnumerateArray().Where(s=>stackTargets.Contains(s.GetProperty("instanceId").GetUInt32()))
                            .All(s=>!s.GetProperty("isAbility").GetBoolean());
                    selection.Prompt=new Prompt {PromptId=2920000u,Parameters={new PromptParameter {
                        ParameterName="question",Type=ParameterType.NonLocalizedString,
                        StringValue=spellsOnly?"Select target spell":"Select target spell or ability"}}};
                }
                var selectedTargetIds=input.GetProperty("selected").EnumerateArray().Select(id=>id.GetUInt32()).ToHashSet();
                foreach (uint target in input.GetProperty("targets").EnumerateArray().Select(id=>id.GetUInt32()).Concat(selectedTargetIds).Distinct())
                    selection.Targets.Add(new Target { TargetInstanceId = target,
                        LegalAction = selectedTargetIds.Contains(target) ? SelectAction.Unselect : SelectAction.Select, Highlight = HighlightType.Hot });
                selection.SelectedTargets=(uint)selectedTargetIds.Count;
                result.Prompt = selection.Prompt;
                result.SelectTargetsReq = new SelectTargetsReq { SourceId = input.GetProperty("sourceId").GetUInt32(), Targets = { selection } };
                if(input.GetProperty("type").GetString()=="InputGuiStackTargets")
                    result.AllowCancel=selection.MinTargets==0?AllowCancel.Continue:AllowCancel.No;
                else
                    result.AllowCancel=input.TryGetProperty("mandatory",out var mand)&&mand.GetBoolean()?AllowCancel.No:AllowCancel.Abort;
                if(snapshot.TryGetProperty("cards",out var sourceCards)) {
                    foreach(var source in sourceCards.EnumerateArray().Where(c=>c.GetProperty("instanceId").GetUInt32()==result.SelectTargetsReq.SourceId)) {
                        uint ability=input.TryGetProperty("description",out var desc)?BattlePresentation.AbilityId(source.GetProperty("grpId").GetUInt32(),desc.GetString()!,input.GetProperty("cardName").GetString()!):0;
                        if(input.TryGetProperty("abilityIdentity",out var targetIdentity) && targetIdentity.TryGetProperty("granted",out var granted) && granted.GetBoolean()) {
                            uint individual=BattlePresentation.GrantedAbilityId(targetIdentity);
                            if(individual!=0)ability=individual;
                        }
                        result.SelectTargetsReq.AbilityGrpId=ability; selection.TargetingAbilityGrpId=ability;
                    }
                    if(result.SelectTargetsReq.AbilityGrpId==0 && input.TryGetProperty("abilityIdentity",out var copyTargetIdentity)
                        && input.TryGetProperty("sourceIsSpellCopy",out var copyTarget) && copyTarget.GetBoolean()) {
                        uint ability=BattlePresentation.AbilityId(copyTargetIdentity.GetProperty("sourceGrpId").GetUInt32(),
                            input.GetProperty("description").GetString()!,input.GetProperty("cardName").GetString()!);
                        result.SelectTargetsReq.AbilityGrpId=ability;selection.TargetingAbilityGrpId=ability;
                    }
                    var targetList = input.GetProperty("targets").EnumerateArray().Select(id => id.GetUInt32()).ToList();
                    bool anyPlayerTarget = targetList.Any(id => id <= 4);
                    if (!anyPlayerTarget && targetList.Count > 0) {
                        var targetCards = sourceCards.EnumerateArray()
                            .Where(c => targetList.Contains(c.GetProperty("instanceId").GetUInt32()))
                            .ToArray();
                        if (targetCards.Length > 0) {
                            string firstZone = targetCards[0].GetProperty("zone").GetString() ?? "";
                            if (targetCards.All(c => c.GetProperty("zone").GetString() == firstZone)) {
                                uint targetOwner = targetCards[0].GetProperty("ownerSeat").GetUInt32();
                                selection.TargetSourceZoneId = firstZone switch {
                                    "Battlefield" => 30,
                                    "Stack" => 40,
                                    "Graveyard" => targetOwner * 100 + 5,
                                    "Exile" => 60,
                                    "Hand" => targetOwner * 100 + 2,
                                    _ => 0
                                };
                            }
                        } else if (input.TryGetProperty("validTargetTypes", out var vtt)) {
                            var types = vtt.EnumerateArray().Select(t => t.GetString() ?? "").ToArray();
                            if (types.Any(t => t.Contains("Creature", StringComparison.OrdinalIgnoreCase)
                                || t.Contains("Permanent", StringComparison.OrdinalIgnoreCase)
                                || t.Contains("Artifact", StringComparison.OrdinalIgnoreCase)
                                || t.Contains("Enchantment", StringComparison.OrdinalIgnoreCase)
                                || t.Contains("Land", StringComparison.OrdinalIgnoreCase)
                                || t.Contains("Planeswalker", StringComparison.OrdinalIgnoreCase))) {
                                selection.TargetSourceZoneId = 30;
                            }
                        }
                    }
                }
                break;
            case "InputAttack":
                result.Type = GREMessageType.DeclareAttackersReq;
                result.DeclareAttackersReq = new DeclareAttackersReq { CanSubmitAttackers = true };
                foreach (var card in input.GetProperty("attackers").EnumerateArray()) {
                    var attacker = new Attacker { AttackerInstanceId = card.GetProperty("instanceId").GetUInt32() };
                    foreach (var id in card.GetProperty("defenders").EnumerateArray()) attacker.LegalDamageRecipients.Add(Recipient(id.GetUInt32()));
                    uint selected = card.GetProperty("selected").GetUInt32();
                    if (selected != 0) attacker.SelectedDamageRecipient = Recipient(selected);
                    result.DeclareAttackersReq.Attackers.Add(attacker);
                }
                break;
            case "InputBlock":
                result.Type = GREMessageType.DeclareBlockersReq;
                result.DeclareBlockersReq = new DeclareBlockersReq();
                foreach (var card in input.GetProperty("blockers").EnumerateArray()) {
                    var blocker = new Blocker { BlockerInstanceId = card.GetProperty("instanceId").GetUInt32(),
                        MaxAttackers = card.TryGetProperty("maxAttackers",out var maxAttackers)?maxAttackers.GetUInt32():1 };
                    blocker.AttackerInstanceIds.AddRange(card.GetProperty("attackers").EnumerateArray().Select(id => id.GetUInt32()));
                    blocker.SelectedAttackerInstanceIds.AddRange(card.GetProperty("selected").EnumerateArray().Select(id => id.GetUInt32()));
                    result.DeclareBlockersReq.Blockers.Add(blocker);
                }
                break;
            default: return null;
        }
        return result;
    }
}
