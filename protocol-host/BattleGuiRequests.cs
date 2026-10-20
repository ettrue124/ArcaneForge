using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

/// <summary>Native requests for Forge's blocking GUI decisions.</summary>
static class BattleGuiRequests
{
    static string ChoiceLabel(JsonElement input,JsonElement option)
    {
        string label=option.GetProperty("label").GetString()??"";
        if(input.TryGetProperty("nativeKind",out var menuKind) && menuKind.GetString()=="replacement") {
            int explanation=label.IndexOf(" - ",StringComparison.Ordinal);
            if(explanation>0)return label.Substring(0,explanation);
        }
        // Siege entry replacements are actions, not full reminder paragraphs.
        // Keep their original option IDs while fitting the native buttons.
        if(label.Contains("As a Siege enters",StringComparison.OrdinalIgnoreCase)
            && label.Contains("protect it",StringComparison.OrdinalIgnoreCase))return "Choose a protector";
        if(label.Contains("enters with six defense counters",StringComparison.OrdinalIgnoreCase))return "Place six defense counters";
        if(!input.TryGetProperty("title",out var titleValue))return label;
        string title=titleValue.GetString()?.Trim()??"";
        if(title.Length==0)return label;
        var options=input.GetProperty("options");
        bool yesNo=options.GetArrayLength()==2
            && options[0].GetProperty("label").GetString()?.Equals("Yes",StringComparison.OrdinalIgnoreCase)==true
            && options[1].GetProperty("label").GetString()?.Equals("No",StringComparison.OrdinalIgnoreCase)==true;
        if(!yesNo)return label;
        bool yes=option.GetProperty("id").GetUInt32()==options[0].GetProperty("id").GetUInt32();
        // Search confirmations include Forge's shuffle and alternate-zone
        // explanation. Native buttons cannot fit that full paragraph.
        if(title.Contains("search your library",StringComparison.OrdinalIgnoreCase)
            && title.Contains("library will be shuffled",StringComparison.OrdinalIgnoreCase))
            return yes?"Search and shuffle":"Skip searching";
        if(title.StartsWith("Apply replacement effect of Copy Enchantment?",StringComparison.OrdinalIgnoreCase)
            && title.Contains("as a copy",StringComparison.OrdinalIgnoreCase))
            return yes?"Choose an enchantment to copy":"Enter without copying";
        if(title.StartsWith("Apply replacement effect of Time Vault?",StringComparison.OrdinalIgnoreCase)
            && title.Contains("skip that turn instead",StringComparison.OrdinalIgnoreCase)
            && title.Contains("untap this artifact",StringComparison.OrdinalIgnoreCase))
            return yes?"Skip this turn and untap Time Vault":"Take this turn; keep Time Vault tapped";
        // The resolving ability already shows the target, stats and duration.
        // Forge's optional animation question repeats that paragraph; native
        // choice buttons have room for the decision, not the rules text.
        if(title.Contains("has base power and toughness",StringComparison.OrdinalIgnoreCase)
            && title.Contains("becomes a creature",StringComparison.OrdinalIgnoreCase)
            && title.Contains("Elemental",StringComparison.OrdinalIgnoreCase)
            && title.Contains("haste and menace",StringComparison.OrdinalIgnoreCase))
            return yes?"Yes — animate the land":"No — leave it as a land";
        // Native SelectN has a fixed heading. Keep the actual Forge question
        // in its affirmative button so a generic Yes/No never loses context.
        return label;
    }
    public static GREToClientMessage? Choice(JsonElement input,uint msgId,uint stateId)
    {
        uint max=input.GetProperty("max").GetUInt32();
        if(max==0 && !(input.TryGetProperty("privateLook",out var privateLook) && privateLook.GetBoolean()))return null;
        bool bottomChoice=input.TryGetProperty("title",out var choiceTitle) && (choiceTitle.GetString()??"").Contains("put on the bottom",StringComparison.OrdinalIgnoreCase);
        bool battlefield=input.TryGetProperty("nativeKind",out var choiceKind) && choiceKind.GetString()=="battlefield";
        var prompt=new Prompt {PromptId=input.TryGetProperty("promptId",out var explicitPrompt)?explicitPrompt.GetUInt32():battlefield?2920000u:bottomChoice?41u:23u};
        if(battlefield)prompt.Parameters.Add(new PromptParameter {ParameterName="question",Type=ParameterType.NonLocalizedString,
            StringValue=input.GetProperty("title").GetString()??"Select cards"});
        bool entities=input.TryGetProperty("entityChoices",out var e) && e.GetBoolean();
        bool stackMenu=input.TryGetProperty("readableStackTargets",out var readableStack) && readableStack.GetBoolean();
        var req=new SelectNReq {MinSel=input.GetProperty("min").GetInt32(),MaxSel=max,
            SourceId=input.GetProperty("sourceId").GetUInt32(),ListType=SelectionListType.Dynamic,
            IdType=entities?IdType.InstanceId:IdType.PromptParameterIndex,Context=SelectionContext.Resolution,
            OptionContext=OptionContext.Resolution,ValidationType=SelectionValidationType.NonRepeatable,Prompt=prompt};
        uint position=0;
        foreach(var option in input.GetProperty("options").EnumerateArray()) {
            uint id=stackMenu?++position:option.GetProperty("id").GetUInt32();req.Ids.Add(id);req.UnfilteredIds.Add(id);req.HotIds.Add(id);
        }
        // Arena's proliferate workflow preselects HotIds. Offer every legal
        // entity without deciding which counters the human wants to increase.
        if(input.TryGetProperty("proliferateSelection",out var proliferate) && proliferate.GetBoolean())req.HotIds.Clear();
        if(!entities) {
            position=0;
            foreach(var option in input.GetProperty("options").EnumerateArray()) {
                uint id=stackMenu?++position:option.GetProperty("id").GetUInt32();
                prompt.Parameters.Add(new PromptParameter {
                    ParameterName = id.ToString(),
                    Type = ParameterType.NonLocalizedString,
                    StringValue = ManaMenuSymbols.Format(ChoiceLabel(input,option))
                });
            }
        }
        if(entities && req.MinSel==0 && req.MaxSel==0) {req.UnfilteredIds.Clear();req.HotIds.Clear();}
        if(!entities && input.TryGetProperty("title",out var question) && !string.IsNullOrWhiteSpace(question.GetString())
            && !input.TryGetProperty("promptId",out _) && prompt.Parameters.Count<=64) {
            prompt.PromptId=2920000u+(uint)prompt.Parameters.Count;
            prompt.Parameters.Add(new PromptParameter {ParameterName="question",Type=ParameterType.NonLocalizedString,
                StringValue=question.GetString()!});
        }
        return new GREToClientMessage {Type=(GREMessageType)22,MsgId=msgId,GameStateId=stateId,SystemSeatIds={1},Prompt=prompt,SelectNReq=req,
            AllowCancel=req.MinSel==0?AllowCancel.Continue:AllowCancel.No};
    }
    public static GREToClientMessage? Create(JsonElement input,uint msgId,uint stateId)
    {
        var message=new GREToClientMessage {MsgId=msgId,GameStateId=stateId,SystemSeatIds={1}};
        switch(input.GetProperty("type").GetString()) {
            case "InputGuiChoice": return Choice(input,msgId,stateId);
            case "InputGuiOrder":
                var title = input.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "" : "";
                var topStr = input.TryGetProperty("top", out var topProp) ? topProp.GetString() ?? "" : "";
                var isBottom = title.Contains("bottom", StringComparison.OrdinalIgnoreCase) || topStr.Contains("bottom", StringComparison.OrdinalIgnoreCase);
                message.Type=GREMessageType.OrderReq;message.Prompt=new Prompt {PromptId = (uint)(isBottom ? 41 : 42)};
                bool libraryOrder=title.Contains("library",StringComparison.OrdinalIgnoreCase) || topStr.Contains("library",StringComparison.OrdinalIgnoreCase);
                message.OrderReq=new OrderReq {OrderingContext = libraryOrder
                    ? (isBottom ? OrderingContext.OrderingForBottom : OrderingContext.OrderingForTop) : OrderingContext.None};
                message.OrderReq.Ids.AddRange(input.GetProperty("options").EnumerateArray().Select(o=>o.GetProperty("id").GetUInt32()));
                break;
            case "InputGuiText":
                message.Type=GREMessageType.StringInputReq;message.StringInputReq=new StringInputReq {SourceId=input.GetProperty("sourceId").GetUInt32()};
                if(input.TryGetProperty("title",out var textQuestion))message.Prompt=new Prompt {PromptId=2920000,
                    Parameters={new PromptParameter {ParameterName="question",Type=ParameterType.NonLocalizedString,
                        StringValue=textQuestion.GetString()??"Choose a card name"}}};
                break;
            case "InputGuiGroups":
                message.Type=GREMessageType.GroupReq;message.Prompt=new Prompt {PromptId=40};
                bool surveil=input.TryGetProperty("surveil",out var sv) && sv.GetBoolean();
                message.GroupReq=new GroupReq {GroupType=GroupType.Ordered,Context=surveil?GroupingContext.Surveil:GroupingContext.Scry};
                var ids=input.GetProperty("choices").EnumerateArray().Select(v=>v.GetUInt32()).ToArray();
                message.GroupReq.InstanceIds.AddRange(ids);
                foreach(var (key,sub) in new[]{("toTop",SubZoneType.Top),("toBottom",SubZoneType.Bottom)})
                    if(input.GetProperty(key).GetBoolean())message.GroupReq.GroupSpecs.Add(new GroupSpecification {
                        LowerBound=0,UpperBound=(uint)ids.Length,ZoneType=ZoneType.Library,SubZoneType=sub});
                if(surveil)message.GroupReq.GroupSpecs.Add(new GroupSpecification {
                    LowerBound=0,UpperBound=(uint)ids.Length,ZoneType=ZoneType.Graveyard,SubZoneType=SubZoneType.None});
                break;
            case "InputGuiCombatDamage":
                message.Type=GREMessageType.AssignDamageReq;message.AssignDamageReq=new AssignDamageReq();
                uint source=input.GetProperty("sourceId").GetUInt32(),total=input.GetProperty("amount").GetUInt32();
                var prompt=new Prompt {PromptId=8,Parameters={new PromptParameter {ParameterName="1",Type=ParameterType.Reference,Reference=new Reference {Type=ReferenceType.InstanceId,Id=source}}}};
                var assigner=new DamageAssigner {InstanceId=source,TotalDamage=total,CanIgnoreBlockers=input.GetProperty("ignoreOrder").GetBoolean(),DecisionPrompt=prompt};
                uint remaining=total;
                foreach(var target in input.GetProperty("targets").EnumerateArray()) {
                    uint min=assigner.CanIgnoreBlockers?0:Math.Min(remaining,target.GetProperty("lethal").GetUInt32());
                    assigner.Assignments.Add(new DamageAssignment {InstanceId=target.GetProperty("id").GetUInt32(),MinDamage=min,MaxDamage=total,AssignedDamage=min});
                    remaining-=min;
                }
                if(assigner.Assignments.Count>0)assigner.Assignments[^1].AssignedDamage+=remaining;
                message.AssignDamageReq.DamageAssigners.Add(assigner);message.Prompt=prompt;
                break;
            case "InputGuiDistribution":
                message.Type=GREMessageType.DistributionReq;
                uint amount=input.GetProperty("amount").GetUInt32();
                message.DistributionReq=new DistributionReq {MinAmount=amount,MaxAmount=amount,MinPerTarget=input.GetProperty("minPerTarget").GetUInt32(),SourceId=input.GetProperty("sourceId").GetUInt32()};
                foreach(var target in input.GetProperty("targets").EnumerateArray()) {
                    message.DistributionReq.TargetIds.Add(target.GetProperty("id").GetUInt32());
                    message.DistributionReq.ValidSelectedTargetIds.Add(target.GetProperty("id").GetUInt32());
                }
                break;
            case "InputGuiGather":
                message.Type=GREMessageType.GatherReq;
                message.Prompt=new Prompt {PromptId=106};
                message.GatherReq=new GatherReq {
                    DestinationId=input.TryGetProperty("destinationId",out var gDid)?gDid.GetUInt32():0,
                    AmountToGather=input.TryGetProperty("amount",out var gAm)?gAm.GetUInt32():0
                };
                if(input.TryGetProperty("sources",out var gSources)) {
                    foreach(var s in gSources.EnumerateArray()) {
                        message.GatherReq.Sources.Add(new GatherSource {
                            SourceId=s.GetProperty("sourceId").GetUInt32(),
                            MinAmount=s.TryGetProperty("min",out var sMin)?sMin.GetUInt32():0,
                            MaxAmount=s.TryGetProperty("max",out var sMax)?sMax.GetUInt32():0
                        });
                    }
                }
                break;
            default: throw new InvalidDataException("Forge prompt has no native adapter: "+input.GetProperty("type").GetString());
        }
        return message;
    }
}
