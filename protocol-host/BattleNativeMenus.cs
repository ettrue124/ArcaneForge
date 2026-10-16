using System.Text.Json;
using System.Text.RegularExpressions;
using Wotc.Mtgo.Gre.External.Messaging;

/// <summary>Semantic Forge decisions mapped to native workflows, with inverse ID mapping.</summary>
static class BattleNativeMenus
{
    static string Key(string value) => Regex.Replace(value.Replace("+","P").Replace("-","M"),"[^a-zA-Z0-9]","");
    static bool EnumId<T>(string name,out uint id) where T:struct,Enum {
        foreach(var value in Enum.GetValues<T>())if(Key(value.ToString()).Equals(Key(name),StringComparison.OrdinalIgnoreCase)) {
            id=Convert.ToUInt32(value);return id!=0;
        }
        id=0;return false;
    }
    static bool StaticChoices(JsonElement input,out StaticList list,out Dictionary<uint,uint> ids) {
        list=StaticList.None;ids=new();
        string kind=input.TryGetProperty("nativeKind",out var k)?k.GetString()??"":"";
        if(kind is not ("color" or "type" or "counterType"))return false;
        string typeKind=input.TryGetProperty("typeKind",out var t)?t.GetString()??"":"";
        foreach(var option in input.GetProperty("options").EnumerateArray()) {
            string name=option.TryGetProperty("nativeValue",out var n)?n.GetString()!:option.GetProperty("label").GetString()!;
            uint wire;bool valid;
            if(kind=="color") {list=StaticList.ManaColors;valid=EnumId<ManaColor>(name,out wire);}
            else if(kind=="counterType") {list=StaticList.CounterTypes;valid=EnumId<CounterType>(name,out wire);}
            else if(typeKind.Equals("Card",StringComparison.OrdinalIgnoreCase)) {list=StaticList.CardTypes;valid=EnumId<CardType>(name,out wire);}
            else if(typeKind.Equals("Super",StringComparison.OrdinalIgnoreCase)) {list=StaticList.SuperTypes;valid=EnumId<SuperType>(name,out wire);}
            else {list=StaticList.SubTypes;valid=EnumId<SubType>(name,out wire);}
            // Unknown Forge-only names retain the labeled workflow; never silently omit an option.
            if(!valid || !ids.TryAdd(wire,option.GetProperty("id").GetUInt32()))return false;
        }
        return ids.Count>0;
    }
    static Dictionary<uint,uint>? AbilityChoices(JsonElement snapshot,JsonElement input,bool modes) {
        var result=new Dictionary<uint,uint>();
        foreach(var option in input.GetProperty(modes?"modeDefinitions":"options").EnumerateArray()) {
            uint source=(modes?input:option).GetProperty("sourceId").GetUInt32();
            var card=snapshot.GetProperty("cards").EnumerateArray().FirstOrDefault(c=>c.GetProperty("instanceId").GetUInt32()==source);
            if(card.ValueKind!=JsonValueKind.Object)return null;
            uint ability=BattlePresentation.AbilityId(card.GetProperty("grpId").GetUInt32(),option.GetProperty("description").GetString()!,card.GetProperty("name").GetString()!);
            if(ability==0 || !result.TryAdd(ability,option.GetProperty(modes?"index":"id").GetUInt32()))return null;
        }
        return result.Count>0?result:null;
    }
    public static GREToClientMessage? Create(JsonElement snapshot,JsonElement input,uint msgId,uint stateId) {
        if(input.GetProperty("type").GetString()!="InputGuiChoice")return null;
        var message=new GREToClientMessage {MsgId=msgId,GameStateId=stateId,SystemSeatIds={1},Prompt=new Prompt {PromptId=23}};
        string nativeKind=input.TryGetProperty("nativeKind",out var native)?native.GetString()??"":"";
        if(nativeKind=="modes" && AbilityChoices(snapshot,input,true) is {} modes) {
            uint source=input.GetProperty("sourceId").GetUInt32();
            var card=snapshot.GetProperty("cards").EnumerateArray().Single(c=>c.GetProperty("instanceId").GetUInt32()==source);
            var modal=new ModalReq {MinSel=input.GetProperty("modeMin").GetUInt32(),MaxSel=input.GetProperty("modeMax").GetUInt32()};
            modal.ModalOptions.AddRange(modes.Keys.Select(id=>new ModalOption {GrpId=id}));
            message.Type=GREMessageType.CastingTimeOptionsReq;
            // This is a blocking Forge choice, not a cancellable Input proxy.
            message.AllowCancel=AllowCancel.No;
            message.CastingTimeOptionsReq=new CastingTimeOptionsReq {CastingTimeOptionReq={new CastingTimeOptionReq {
                CtoId=checked((uint)snapshot.GetProperty("buttonEventId").GetInt64()),CastingTimeOptionType=CastingTimeOptionType.Modal,
                AffectorId=source,AffectedId=source,GrpId=card.GetProperty("grpId").GetUInt32(),PlayerIdToPrompt=1,IsRequired=true,ModalReq=modal}}};
            return message;
        }
        if(nativeKind=="manaType") {
            uint source=input.GetProperty("sourceId").GetUInt32();
            var card=snapshot.GetProperty("cards").EnumerateArray().FirstOrDefault(c=>c.GetProperty("instanceId").GetUInt32()==source);
            if(card.ValueKind!=JsonValueKind.Object)return null;
            var manaReq=new SelectManaTypeReq {SourceId=source};
            if(input.TryGetProperty("manaColors",out var mcArr)) {
                foreach(var c in mcArr.EnumerateArray()) {
                    if(Enum.TryParse<ManaColor>(c.GetString(),true,out var mc))
                        manaReq.ManaColors.Add(mc);
                }
            }
            message.Type=GREMessageType.CastingTimeOptionsReq;
            message.AllowCancel=AllowCancel.No;
            var cto=new CastingTimeOptionReq {
                CtoId=checked((uint)snapshot.GetProperty("buttonEventId").GetInt64()),
                CastingTimeOptionType=CastingTimeOptionType.ManaType,
                AffectorId=source,AffectedId=source,
                GrpId=card.GetProperty("grpId").GetUInt32(),
                PlayerIdToPrompt=1,IsRequired=true,
                SelectManaTypeReq=manaReq
            };
            if(input.TryGetProperty("manaCost",out var costProp))
                MatchDoorHost.TryParseManaCost(costProp.GetString()!, cto.ManaCost);
            message.CastingTimeOptionsReq=new CastingTimeOptionsReq {CastingTimeOptionReq={cto}};
            return message;
        }
        if(nativeKind=="replacement" && AbilityChoices(snapshot,input,false) is {} replacements) {
            message.Type=GREMessageType.SelectReplacementReq;message.SelectReplacementReq=new SelectReplacementReq();
            foreach(var pair in replacements) {
                var option=input.GetProperty("options").EnumerateArray().Single(o=>o.GetProperty("id").GetUInt32()==pair.Value);
                message.SelectReplacementReq.Replacements.Add(new ReplacementEffect {ObjectInstance=option.GetProperty("sourceId").GetUInt32(),
                    AbilityGrpId=pair.Key,ReplacementEffectId=pair.Value});
            }
            return message;
        }
        if(StaticChoices(input,out var list,out var map)) {
            message.Type=(GREMessageType)22;
            message.SelectNReq=new SelectNReq {SourceId=input.GetProperty("sourceId").GetUInt32(),
                MinSel=input.GetProperty("min").GetInt32(),MaxSel=input.GetProperty("max").GetUInt32(),
                Context=SelectionContext.Resolution,OptionContext=OptionContext.Resolution,
                ListType=SelectionListType.StaticSubset,StaticList=list,ValidationType=SelectionValidationType.NonRepeatable,Prompt=message.Prompt};
            message.SelectNReq.Ids.AddRange(map.Keys);
            message.AllowCancel=message.SelectNReq.MinSel==0?AllowCancel.Continue:AllowCancel.No;
            return message;
        }
        if(input.TryGetProperty("nativeKind",out var kind) && kind.GetString()=="piles") {
            message.Type=(GREMessageType)40;
            message.SelectNGroupReq=new SelectNGroupReq {MinSel=1,MaxSel=1,SourceId=input.GetProperty("sourceId").GetUInt32()};
            foreach(var g in input.GetProperty("groups").EnumerateArray()) {
                var group=new Wotc.Mtgo.Gre.External.Messaging.Group {GroupId=g.GetProperty("id").GetInt32(),IsFacedown=g.GetProperty("faceDown").GetBoolean()};
                group.Ids.AddRange(g.GetProperty("ids").EnumerateArray().Select(i=>i.GetUInt32()));
                message.SelectNGroupReq.Groups.Add(group);
            }
            return message;
        }
        if(input.TryGetProperty("nativeKind",out kind) && kind.GetString()=="search") {
            message.Type=GREMessageType.SearchReq;
            message.SearchReq=new SearchReq {SourceId=input.GetProperty("sourceId").GetUInt32(),
                MinFind=input.GetProperty("min").GetUInt32(),MaxFind=input.GetProperty("max").GetUInt32(),
                AllowFailToFind=input.GetProperty("min").GetInt32()==0?AllowFailToFind.Any:AllowFailToFind.None};
            var ids=input.GetProperty("choices").EnumerateArray().Select(i=>i.GetUInt32()).ToHashSet();
            message.SearchReq.ItemsSought.AddRange(ids);message.SearchReq.ItemsToSearch.AddRange(ids);
            foreach(var card in snapshot.GetProperty("cards").EnumerateArray().Where(c=>ids.Contains(c.GetProperty("instanceId").GetUInt32()))) {
                uint owner=card.GetProperty("ownerSeat").GetUInt32();
                uint zone=card.GetProperty("zone").GetString() switch {"Library"=>owner*100+1,"Hand"=>owner*100+2,"Graveyard"=>owner*100+5,"Exile"=>60,_=>0};
                if(zone!=0 && !message.SearchReq.ZonesToSearch.Contains(zone))message.SearchReq.ZonesToSearch.Add(zone);
            }
            return message;
        }
        if(input.TryGetProperty("nativeKind",out kind) && kind.GetString()=="searchFromGroups") {
            message.Type=GREMessageType.SearchFromGroupsReq;
            message.SearchFromGroupsReq=new SearchFromGroupsReq {
                SourceId=input.GetProperty("sourceId").GetUInt32(),
                MinFind=input.GetProperty("min").GetUInt32(),
                MaxFind=input.GetProperty("max").GetUInt32(),
                AllowFailToFind=input.GetProperty("min").GetInt32()==0?AllowFailToFind.Any:AllowFailToFind.None};
            if(input.TryGetProperty("groups",out var gArr)) {
                foreach(var g in gArr.EnumerateArray()) {
                    var group=new Wotc.Mtgo.Gre.External.Messaging.Group {
                        GroupId=g.GetProperty("id").GetInt32(),
                        IsFacedown=g.TryGetProperty("faceDown",out var fd)&&fd.GetBoolean()};
                    if(g.TryGetProperty("ids",out var ids))
                        group.Ids.AddRange(ids.EnumerateArray().Select(i=>i.GetUInt32()));
                    message.SearchFromGroupsReq.Groups.Add(group);
                }
            }
            if(input.TryGetProperty("zones",out var zones)) {
                message.SearchFromGroupsReq.ZonesToSearch.AddRange(zones.EnumerateArray().Select(z=>z.GetUInt32()));
            } else {
                var allIds=message.SearchFromGroupsReq.Groups.SelectMany(g=>g.Ids).ToHashSet();
                foreach(var card in snapshot.GetProperty("cards").EnumerateArray().Where(c=>allIds.Contains(c.GetProperty("instanceId").GetUInt32()))) {
                    uint owner=card.GetProperty("ownerSeat").GetUInt32();
                    uint zone=card.GetProperty("zone").GetString() switch {"Library"=>owner*100+1,"Hand"=>owner*100+2,"Graveyard"=>owner*100+5,"Exile"=>60,_=>0};
                    if(zone!=0 && !message.SearchFromGroupsReq.ZonesToSearch.Contains(zone))message.SearchFromGroupsReq.ZonesToSearch.Add(zone);
                }
            }
            return message;
        }
        if(input.TryGetProperty("nativeKind",out kind) && kind.GetString()=="selectFromGroups") {
            message.Type=GREMessageType.SelectFromGroupsReq;
            message.SelectFromGroupsReq=new SelectFromGroupsReq {
                SourceId=input.GetProperty("sourceId").GetUInt32(),
                MinTotalSel=input.GetProperty("min").GetUInt32(),
                MaxTotalSel=input.GetProperty("max").GetUInt32()};
            if(input.TryGetProperty("groups",out var gArr)) {
                foreach(var g in gArr.EnumerateArray()) {
                    var group=new Wotc.Mtgo.Gre.External.Messaging.Group {
                        GroupId=g.GetProperty("id").GetInt32(),
                        IsFacedown=g.TryGetProperty("faceDown",out var fd)&&fd.GetBoolean()};
                    if(g.TryGetProperty("ids",out var ids))
                        group.Ids.AddRange(ids.EnumerateArray().Select(i=>i.GetUInt32()));
                    message.SelectFromGroupsReq.Groups.Add(group);
                }
            }
            if(input.TryGetProperty("zones",out var zones))
                message.SelectFromGroupsReq.ZoneIds.AddRange(zones.EnumerateArray().Select(z=>z.GetUInt32()));
            return message;
        }
        if(input.TryGetProperty("nativeKind",out kind) && kind.GetString()=="selectCounters") {
            message.Type=GREMessageType.SelectCountersReq;
            message.SelectCountersReq=new SelectCountersReq {
                SourceId=input.GetProperty("sourceId").GetUInt32(),
                MinSel=input.GetProperty("min").GetUInt32(),
                MaxSel=input.GetProperty("max").GetUInt32(),
                Context=SelectionContext.Resolution,
                OptionContext=OptionContext.Resolution};
            if(input.TryGetProperty("counterPairs",out var pairs)) {
                foreach(var p in pairs.EnumerateArray()) {
                    // Preserve the generic labeled choice when Arena has no
                    // corresponding counter; enum zero is not a usable counter.
                    if(!EnumId<CounterType>(p.GetProperty("counterType").GetString()!,out uint ctWire))return null;
                    message.SelectCountersReq.CounterPairs.Add(new CounterPair {
                        InstanceId=p.GetProperty("instanceId").GetUInt32(),
                        CounterType=(CounterType)ctWire,
                        Count=p.GetProperty("count").GetUInt32()});
                }
            }
            return message;
        }
        if(input.TryGetProperty("nativeKind",out kind) && kind.GetString()=="gather") {
            message.Type=GREMessageType.GatherReq;
            message.Prompt=new Prompt {PromptId=106};
            message.GatherReq=new GatherReq {
                DestinationId=input.TryGetProperty("destinationId",out var did)?did.GetUInt32():0,
                AmountToGather=input.TryGetProperty("amount",out var am)?am.GetUInt32():0};
            if(input.TryGetProperty("sources",out var gSources)) {
                foreach(var s in gSources.EnumerateArray()) {
                    message.GatherReq.Sources.Add(new GatherSource {
                        SourceId=s.GetProperty("sourceId").GetUInt32(),
                        MinAmount=s.TryGetProperty("min",out var sMin)?sMin.GetUInt32():0,
                        MaxAmount=s.TryGetProperty("max",out var sMax)?sMax.GetUInt32():0});
                }
            }
            return message;
        }
        if(input.TryGetProperty("nativeKind",out kind) && kind.GetString()=="submitDeck") {
            message.Type=GREMessageType.SubmitDeckReq;
            var submitReq=new SubmitDeckReq {Deck=new DeckMessage()};
            if(input.TryGetProperty("deckCards",out var dc)) {
                submitReq.Deck.DeckCards.AddRange(dc.EnumerateArray().Select(c=>c.GetUInt32()));
            }
            if(input.TryGetProperty("sideboardCards",out var sc)) {
                submitReq.Deck.SideboardCards.AddRange(sc.EnumerateArray().Select(c=>c.GetUInt32()));
            }
            message.SubmitDeckReq=submitReq;
            return message;
        }
        return null;
    }
    public static bool TryAnswer(ClientToGREMessage message,JsonElement snapshot,out object? answer) {
        answer=null;
        if(!snapshot.TryGetProperty("input",out var input))return false;
        if(input.GetProperty("type").GetString()!="InputGuiChoice")return false;
        if(message.SelectNResp is {} selection && StaticChoices(input,out _,out var map)) {
            answer=selection.Ids.Select(id=>map.TryGetValue(id,out var original)?original:throw new InvalidDataException("Unadvertised static selection")).ToArray();
            return true;
        }
        string kind=input.TryGetProperty("nativeKind",out var k)?k.GetString()??"":"";
        if(kind=="modes" && message.CastingTimeOptionsResp?.CastingTimeOptionResp is {} modal && AbilityChoices(snapshot,input,true) is {} modes) {
            if(modal.CtoId!=snapshot.GetProperty("buttonEventId").GetUInt32() || modal.CastingTimeOptionType!=CastingTimeOptionType.Modal || modal.ChooseModalResp==null)
                throw new InvalidDataException("Modal response does not match the pending request");
            var selected=modal.ChooseModalResp.GrpIds.Select(id=>modes.TryGetValue(id,out var index)?index:throw new InvalidDataException("Unknown mode")).Order().ToArray();
            var combo=input.GetProperty("modeCombinations").EnumerateArray().FirstOrDefault(c=>c.GetProperty("modes").EnumerateArray().Select(i=>i.GetUInt32()).Order().SequenceEqual(selected));
            if(combo.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Illegal combination of modes");
            answer=new[]{combo.GetProperty("id").GetUInt32()};return true;
        }
        if(kind=="manaType" && message.CastingTimeOptionsResp?.CastingTimeOptionResp is {} manaTypeResp) {
            if(manaTypeResp.CtoId!=snapshot.GetProperty("buttonEventId").GetUInt32() || manaTypeResp.CastingTimeOptionType!=CastingTimeOptionType.ManaType || manaTypeResp.SelectManaTypeResp==null)
                throw new InvalidDataException("ManaType response does not match the pending request");
            if(manaTypeResp.SelectManaTypeResp.ManaColor==ManaColor.Phyrexian)
                answer=new uint[] { 2 };
            else
                answer=new uint[] { 1 };
            return true;
        }
        if(kind=="replacement" && message.SelectReplacementResp?.Replacement is {} replacement && AbilityChoices(snapshot,input,false) is {} replacements) {
            if(!replacements.TryGetValue(replacement.AbilityGrpId,out var id) || id!=replacement.ReplacementEffectId)
                throw new InvalidDataException("Unknown replacement effect");
            answer=new[]{id};return true;
        }
        if(kind=="piles" && message.SelectNGroupResp is {} piles) {
            var legal=input.GetProperty("groups").EnumerateArray().Select(g=>g.GetProperty("id").GetUInt32()).ToHashSet();
            if(piles.Selectedgroupids.Count!=1 || !legal.Contains(piles.Selectedgroupids[0]))throw new InvalidDataException("Select exactly one advertised pile");
            answer=piles.Selectedgroupids.ToArray();return true;
        }
        if(kind=="search" && message.SearchResp is {} search) {
            if(search.AddZoneToSearchScope!=0)throw new InvalidDataException("Search scope is fixed by the rules engine");
            answer=search.ItemsFound.ToArray();return true;
        }
        if(kind=="searchFromGroups" && message.SearchFromGroupsResp is {} sfg) {
            if(sfg.AddZoneToSearchScope!=0)throw new InvalidDataException("Search scope is fixed by the rules engine");
            answer=sfg.Groups.SelectMany(g=>g.Ids).ToArray();
            return true;
        }
        if(kind=="selectFromGroups" && message.SelectFromGroupsResp is {} sfgResp) {
            answer=sfgResp.Groups.SelectMany(g=>g.Ids).ToArray();
            return true;
        }
        if(kind=="selectCounters" && message.SelectCountersResp is {} countersResp) {
            answer=countersResp.Selections.Select(s=>new {instanceId=s.InstanceId,counterType=(int)s.CounterType,count=s.Count}).ToArray();
            return true;
        }
        if(kind=="gather" && message.GatherResp is {} gather) {
            answer=gather.Gatherings.Select(g=>new {id=g.InstanceId,amount=g.Amount}).ToArray();
            return true;
        }
        if(kind=="submitDeck" && message.SubmitDeckResp is {} sdResp) {
            answer=new {
                deckCards=sdResp.Deck?.DeckCards.ToArray()??Array.Empty<uint>(),
                sideboardCards=sdResp.Deck?.SideboardCards.ToArray()??Array.Empty<uint>()
            };
            return true;
        }
        return false;
    }
}
