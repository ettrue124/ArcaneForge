using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;
using Action = Wotc.Mtgo.Gre.External.Messaging.Action;

static class BattlePresentation
{
    public static void StateEntryPayment(JsonElement snapshot,GameStateMessage current) {
        if(!snapshot.TryGetProperty("input",out var input) || !input.TryGetProperty("entryReplacementDescription",out var description)
            || !input.TryGetProperty("sourceId",out var source))return;
        uint id=source.GetUInt32();
        var card=current.GameObjects.FirstOrDefault(c=>c.InstanceId==id && c.GrpId!=0);
        if(card==null)return;
        string name=input.TryGetProperty("cardName",out var cardName)?cardName.GetString()??"":"";
        uint ability=AbilityId(card.GrpId,description.GetString()??"",name);
        if(ability==0)return;
        // The installed OptionalAction layout selects shock/bolt-land buttons
        // from replacement-effect ability identity, not the PayLife mechanic.
        current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(3_600_000_000u+id),
            Type={AnnotationType.ReplacementEffect},AffectorId=id,AffectedIds={id},Details={
                IntDetail("grpid",checked((int)ability)),IntDetail("replacedEffectSource",checked((int)id)),
                IntDetail("ReplacementSourceZcid",checked((int)id))}});
    }
    static readonly Lazy<Dictionary<string,JsonElement>> AbilityText = new(() =>
        JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText("data/arena-activated-abilities.json"))!);
    static readonly Lazy<Dictionary<string,JsonElement>> AllAbilityText = new(() =>
        JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(File.ReadAllText("data/arena-ability-text.json"))!);
    // Exact shared keyword IDs only. Build once; never scan the full catalogue
    // on each board update and never select an ambiguous text match.
    static readonly Lazy<Dictionary<string,uint>> SharedKeywordIds=new(()=>AllAbilityText.Value.Values
        .SelectMany(v=>v.EnumerateArray()).GroupBy(a=>a.GetProperty("text").GetString()!.Trim(),StringComparer.OrdinalIgnoreCase)
        .Select(g=>(Text:g.Key,Ids:g.Select(a=>a.GetProperty("id").GetUInt32()).Distinct().ToArray()))
        .Where(g=>g.Ids.Length==1).ToDictionary(g=>g.Text,g=>g.Ids[0],StringComparer.OrdinalIgnoreCase));
    public static uint KeywordAbilityId(string keyword)=>SharedKeywordIds.Value.GetValueOrDefault(keyword.Trim());
    static readonly Lazy<Dictionary<uint,int>> SharedAbilityReferences=new(()=>AllAbilityText.Value.Values
        .SelectMany(v=>v.EnumerateArray().Select(a=>a.GetProperty("id").GetUInt32()).Distinct())
        .GroupBy(id=>id).ToDictionary(g=>g.Key,g=>g.Count()));
    public static uint AbilityId(uint cardGrp, string description, string cardName = "",bool activatedOnly=false, string loyaltyCost = "")
        => AbilityIdCache.GetOrAdd((cardGrp,description,cardName,activatedOnly,loyaltyCost),
            key=>FindAbilityId(key.Card,key.Text,key.Name,key.Activated,key.Loyalty));
    static readonly System.Collections.Concurrent.ConcurrentDictionary<(uint Card,string Text,string Name,bool Activated,string Loyalty),uint> AbilityIdCache=new();
    static uint FindAbilityId(uint cardGrp,string description,string cardName,bool activatedOnly,string loyaltyCost)
    {
        uint Match(Dictionary<string,JsonElement> catalogue) {
            if((string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(loyaltyCost)) || !catalogue.TryGetValue(cardGrp.ToString(),out var abilities))return 0;
            string Text(string value) {
                value=System.Text.RegularExpressions.Regex.Replace(value,"<[^>]*>","");
                value=value.Replace("CARDNAME","",StringComparison.OrdinalIgnoreCase)
                           .Replace("NICKNAME","",StringComparison.OrdinalIgnoreCase)
                           .Replace("this planeswalker","",StringComparison.OrdinalIgnoreCase)
                           .Replace("this creature","",StringComparison.OrdinalIgnoreCase)
                           .Replace("this permanent","",StringComparison.OrdinalIgnoreCase)
                           .Replace("this spell","",StringComparison.OrdinalIgnoreCase)
                           .Replace("this card","",StringComparison.OrdinalIgnoreCase)
                           .Replace("this land","",StringComparison.OrdinalIgnoreCase)
                           .Replace("this artifact","",StringComparison.OrdinalIgnoreCase)
                           .Replace("this enchantment","",StringComparison.OrdinalIgnoreCase)
                           .Replace("this equipment","",StringComparison.OrdinalIgnoreCase);
                if(cardName.Length>0) {
                    value=value.Replace(cardName,"",StringComparison.OrdinalIgnoreCase);
                    if(cardName.Contains(" // ")) {
                        foreach(var part in cardName.Split(" // "))
                            if(part.Trim().Length>0)
                                value=value.Replace(part.Trim(),"",StringComparison.OrdinalIgnoreCase);
                    }
                    if(cardName.Contains(','))
                        value=value.Replace(cardName.Split(',')[0].Trim(),"",StringComparison.OrdinalIgnoreCase);
                    if(cardName.Contains(':'))
                        value=value.Replace(cardName.Split(':')[0].Trim(),"",StringComparison.OrdinalIgnoreCase);
                    var firstWord=cardName.Split(' ')[0].Trim();
                    if(firstWord.Length>0 &&
                        !firstWord.Equals("the",StringComparison.OrdinalIgnoreCase) &&
                        !firstWord.Equals("a",StringComparison.OrdinalIgnoreCase) &&
                        !firstWord.Equals("an",StringComparison.OrdinalIgnoreCase))
                        value=System.Text.RegularExpressions.Regex.Replace(value,@"\b"+System.Text.RegularExpressions.Regex.Escape(firstWord)+@"\b","",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                return Normalize(value);
            }
            var targetDesc = Text(description);
            // 1. Exact normalized text match
            if(!string.IsNullOrEmpty(targetDesc)) {
                var matches=abilities.EnumerateArray().Where(a=>Text(a.GetProperty("text").GetString()!)==targetDesc).ToArray();
                if(matches.Length==1)return matches[0].GetProperty("id").GetUInt32();
            }

            // 2. Loyalty cost matching fallback
            if(!string.IsNullOrEmpty(loyaltyCost)) {
                string normLoyalty = loyaltyCost.Replace("[","").Replace("]","").Trim();
                if(normLoyalty is "+0" or "-0") normLoyalty = "0";
                var loyaltyMatches = abilities.EnumerateArray()
                    .Where(a => a.TryGetProperty("loyalty", out var l) && (l.GetString() == normLoyalty || normLoyalty == "0" && l.GetString() is "0" or "+0" or "-0"))
                    .ToArray();
                if(loyaltyMatches.Length == 1) return loyaltyMatches[0].GetProperty("id").GetUInt32();
                if(loyaltyMatches.Length > 1 && !string.IsNullOrEmpty(targetDesc)) {
                    var subset = loyaltyMatches.Where(a => {
                        var at = Text(a.GetProperty("text").GetString()!);
                        return at.Contains(targetDesc) || targetDesc.Contains(at) ||
                               (at.Length >= 10 && targetDesc.Length >= 10 && at[..10] == targetDesc[..10]);
                    }).ToArray();
                    if(subset.Length == 1) return subset[0].GetProperty("id").GetUInt32();
                }
            }

            // 3. Containment / substring matching fallback
            if(!string.IsNullOrEmpty(targetDesc) && targetDesc.Length >= 8) {
                var contains = abilities.EnumerateArray().Where(a => {
                    var at = Text(a.GetProperty("text").GetString()!);
                    return at.Contains(targetDesc) || targetDesc.Contains(at);
                }).ToArray();
                if(contains.Length == 1) return contains[0].GetProperty("id").GetUInt32();
                if(contains.Length > 1) {
                    var filtered = contains.Where(a => {
                        var raw = a.GetProperty("text").GetString() ?? "";
                        return !raw.Contains("•") && !raw.Contains("choose one", StringComparison.OrdinalIgnoreCase)
                            && !raw.Contains("choose any number", StringComparison.OrdinalIgnoreCase)
                            && !raw.Contains("choose two", StringComparison.OrdinalIgnoreCase);
                    }).ToArray();
                    if(filtered.Length == 1) return filtered[0].GetProperty("id").GetUInt32();
                }
            }

            // 4. Prefix match fallback (handles wording variations like "The artifact" vs "That artifact")
            if(!string.IsNullOrEmpty(targetDesc) && targetDesc.Length >= 15) {
                var prefixes = abilities.EnumerateArray().Where(a => {
                    var at = Text(a.GetProperty("text").GetString()!);
                    return at.Length >= 15 && at[..15] == targetDesc[..15];
                }).ToArray();
                if(prefixes.Length == 1) return prefixes[0].GetProperty("id").GetUInt32();
            }

            // 5. Significant word overlap fallback
            if(!string.IsNullOrEmpty(description)) {
                var targetWords = System.Text.RegularExpressions.Regex.Matches(description.ToLowerInvariant(), @"\b[a-z0-9]{3,}\b")
                    .Select(m => m.Value).ToHashSet();
                if(targetWords.Count >= 3) {
                    var scored = abilities.EnumerateArray().Select(a => {
                        var arenaWords = System.Text.RegularExpressions.Regex.Matches(a.GetProperty("text").GetString()!.ToLowerInvariant(), @"\b[a-z0-9]{3,}\b")
                            .Select(m => m.Value).ToHashSet();
                        int common = targetWords.Intersect(arenaWords).Count();
                        double score = (double)common / Math.Max(targetWords.Count, arenaWords.Count);
                        return (Id: a.GetProperty("id").GetUInt32(), Score: score);
                    }).Where(s => s.Score >= 0.5).OrderByDescending(s => s.Score).ToArray();
                    if(scored.Length == 1 || (scored.Length > 1 && scored[0].Score > scored[1].Score + 0.2)) {
                        return scored[0].Id;
                    }
                }
            }

            return 0;
        }
        uint id = Match(activatedOnly ? AbilityText.Value : AllAbilityText.Value);
        if (id == 0 && activatedOnly) id = Match(AllAbilityText.Value);
        return id;
    }
    static string Normalize(string text,bool keepCost=false)
    {
        text=System.Text.RegularExpressions.Regex.Replace(text,"<[^>]*>","");
        // Forge includes reminder text; Arena stores it separately. Strip
        // balanced parentheses on both sides, never guess by a shared prefix.
        string clean;
        do {clean=text;text=System.Text.RegularExpressions.Regex.Replace(text,@"\([^()]*\)","");}while(clean!=text);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\{[^}]*\}",m=>
            System.Text.RegularExpressions.Regex.Replace(m.Value,@"o(?=[WUBRGCXYZSTQ0-9E])","",System.Text.RegularExpressions.RegexOptions.IgnoreCase));
        int colon=text.IndexOf(':'); if(colon>=0 && !keepCost) text=text[(colon+1)..];
        text=text.Replace("shuffle your library", "shuffle", StringComparison.OrdinalIgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\bof (?:their|his|her|your) choice\b","",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\bcard named\s+","",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\s+card(?=\s+into\b)","",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\band you lose\b","and lose",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\bnumber of life\b","amount of life",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\b(?:zero)\b","0",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\b(?:one)\b","1",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\b(?:two)\b","2",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\b(?:three)\b","3",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\b(?:four)\b","4",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\b(?:five)\b","5",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\b(?:six)\b","6",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\b(?:seven)\b","7",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\b(?:eight)\b","8",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\b(?:nine)\b","9",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=System.Text.RegularExpressions.Regex.Replace(text,@"\b(?:ten)\b","10",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text=text.Replace("+", "plus").Replace("-", "minus");
        return System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]", "");
    }
    static readonly Lazy<JsonElement> GrantedDefinitions = new(() => JsonSerializer.Deserialize<JsonElement>(
        File.ReadAllText("data/arena-granted-ability-definitions.json")));
    static readonly Lazy<ILookup<string,JsonElement>> GrantedByLoyalty=new(() =>
        GrantedDefinitions.Value.EnumerateArray().ToLookup(a=>a.GetProperty("loyalty").GetString()??""));
    public static void WarmAbilityMetadata() {
        _=AbilityText.Value;
        _=AllAbilityText.Value;
        _=GrantedByLoyalty.Value;
    }
    public static object[] EmblemAbilities(JsonElement card) {
        string text=card.TryGetProperty("emblemGrantText",out var grant)?grant.GetString()??"":"";
        var quotes=System.Text.RegularExpressions.Regex.Matches(text,"[\"“]([^\"”]+)[\"”]").Select(m=>Normalize(m.Groups[1].Value)).ToHashSet();
        return GrantedDefinitions.Value.EnumerateArray().Where(a=>quotes.Contains(Normalize(a.GetProperty("text").GetString()!)))
            .GroupBy(a=>Normalize(a.GetProperty("text").GetString()!)).Select(g=>(object)new {grpId=g.Min(a=>a.GetProperty("id").GetUInt32())}).ToArray();
    }
    public static void StateOpponentKnowledge(JsonElement snapshot,GameStateMessage current,uint viewer) {
        var privateCards=current.GameObjects.Where(c=>c.OwnerSeatId==viewer && c.GrpId!=0
            && current.Zones.Any(z=>z.ZoneId==c.ZoneId && z.Type is ZoneType.Hand))
            .Select(c=>c.InstanceId).ToHashSet();
        foreach(var card in snapshot.GetProperty("cards").EnumerateArray()) {
            uint id=card.GetProperty("instanceId").GetUInt32();
            if(!privateCards.Contains(id) || !card.TryGetProperty("viewers",out var viewers)
                || !viewers.EnumerateArray().Any(v=>v.GetUInt32()!=viewer))continue;
            current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(2_500_000_000u+id),
                Type={AnnotationType.InstanceRevealedToOpponent},AffectedIds={id}});
        }
    }
    public static void StateChosenTypes(JsonElement snapshot,GameStateMessage current) {
        var visible=current.GameObjects.Where(c=>c.GrpId!=0).Select(c=>c.InstanceId).ToHashSet();
        foreach(var card in snapshot.GetProperty("cards").EnumerateArray()) {
            uint id=card.GetProperty("instanceId").GetUInt32();
            if(visible.Contains(id) && card.TryGetProperty("devotion",out var devotion))
                current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(2_400_000_000u+id),Type={AnnotationType.AbilityWordActive},AffectorId=id,AffectedIds={id},
                    Details={new KeyValuePairInfo {Key="AbilityWordName",Type=(KeyValuePairValueType)6,ValueString={"Devotion"}},
                        IntDetail("value",devotion.GetInt32()),IntDetail("isActive",1),
                        new KeyValuePairInfo {Key="ShowOnMiniCDC",Type=(KeyValuePairValueType)6,ValueString={"true"}}}});
            if(!visible.Contains(id))continue;
            if(card.TryGetProperty("chosenColors",out var colors)) {
                var chosenColors=colors.EnumerateArray().Select(c=>Enum.TryParse<Color>(c.GetString(),true,out var color) && color!=Color.None && Enum.IsDefined(color)?(uint)color:0u)
                    .Where(c=>c!=0).Distinct().Order().ToArray();
                if(chosenColors.Length>0) {
                    var annotation=new AnnotationInfo {Id=checked(2_200_000_000u+id*2),Type={AnnotationType.LinkInfo},AffectorId=id,
                        Details={IntDetail("LinkType",(int)LinkType.Choose),new KeyValuePairInfo {Key="ChooseLinkType",Type=KeyValuePairValueType.String,ValueString={"Color"}}}};
                    annotation.AffectedIds.Add(chosenColors);current.PersistentAnnotations.Add(annotation);
                }
            }
            if(card.TryGetProperty("chosenNumber",out var amount) && amount.ValueKind==JsonValueKind.Number && amount.TryGetInt32(out var number) && number>=0)
                current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(2_200_000_001u+id*2),Type={AnnotationType.LinkInfo},AffectorId=id,
                    AffectedIds={(uint)number},Details={IntDetail("LinkType",(int)LinkType.Choose),
                        new KeyValuePairInfo {Key="ChooseLinkType",Type=KeyValuePairValueType.String,ValueString={"Amount"}}}});
            if(!card.TryGetProperty("chosenType",out var chosen) || string.IsNullOrWhiteSpace(chosen.GetString()))continue;
            string key=System.Text.RegularExpressions.Regex.Replace(chosen.GetString()!,"[^a-zA-Z0-9]","");
            if(!Enum.TryParse<SubType>(key,true,out var subtype) || !Enum.IsDefined(subtype))continue;
            current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(2_300_000_000u+id),Type={AnnotationType.LinkInfo},AffectorId=id,
                AffectedIds={(uint)TypeCategory.SubType,(uint)subtype},Details={IntDetail("LinkType",(int)LinkType.Choose),
                new KeyValuePairInfo {Key="ChooseLinkType",Type=(KeyValuePairValueType)6,ValueString={"Type"}}}});
        }
    }
    public static void StateStatModifications(JsonElement snapshot,GameStateMessage current) {
        var visible=current.GameObjects.Where(c=>c.GrpId!=0 && c.GrpId!=3).Select(c=>c.InstanceId).ToHashSet();
        foreach(var card in snapshot.GetProperty("cards").EnumerateArray()) {
            uint target=card.GetProperty("instanceId").GetUInt32();
            if(!visible.Contains(target) || card.GetProperty("zone").GetString() is not ("Battlefield" or "PhasedOut"))continue;
            foreach(var stat in new[]{(Key:"powerModified",Type:AnnotationType.ModifiedPower,Slot:0u),(Key:"toughnessModified",Type:AnnotationType.ModifiedToughness,Slot:1u)}) {
                if(!card.TryGetProperty(stat.Key,out var modified) || modified.ValueKind!=JsonValueKind.True)continue;
                uint id=checked(3_500_000_000u+target*2+stat.Slot);
                // Persistent presentation metadata only: final stats already
                // include this change, so a numeric delta would apply it twice.
                current.PersistentAnnotations.Add(new AnnotationInfo {Id=id,AffectorId=target,AffectedIds={target},
                    Type={AnnotationType.LayeredEffect,stat.Type},Details={IntDetail("effect_id",unchecked((int)id)),
                        new KeyValuePairInfo {Key="LayeredEffectType",Type=KeyValuePairValueType.String,
                            ValueString={stat.Slot==0?"Effect_ModifiedPower":"Effect_ModifiedToughness"}}}});
            }
        }
    }
    public static void StateBlockingRestrictions(JsonElement snapshot,GameStateMessage current) {
        var visible=current.GameObjects.Where(c=>c.GrpId!=0 && c.ZoneId==30).Select(c=>c.InstanceId).ToHashSet();
        foreach(var card in snapshot.GetProperty("cards").EnumerateArray()) {
            uint target=card.GetProperty("instanceId").GetUInt32();
            if(!visible.Contains(target))continue;
            if(card.TryGetProperty("decayed",out var decayed) && decayed.GetBoolean()) {
                uint ability=KeywordAbilityId("Decayed");
                if(ability!=0)current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(3_700_000_000u+target),
                    AffectorId=target,AffectedIds={target},Type={AnnotationType.Qualification},Details={
                        IntDetail("QualificationType",(int)QualificationType.CantBlock),IntDetail("grpid",checked((int)ability)),
                        IntDetail("parent_grpid",checked((int)card.GetProperty("grpId").GetUInt32())),IntDetail("SourceParent",checked((int)target))}});
            }
            if(!card.TryGetProperty("blockingRestrictions",out var restrictions))continue;
            uint slot=0;
            foreach(var restriction in restrictions.EnumerateArray()) {
                uint sourceGrp=restriction.GetProperty("sourceGrpId").GetUInt32();
                uint ability=AbilityId(sourceGrp,restriction.GetProperty("description").GetString()!,restriction.GetProperty("cardName").GetString()!);
                if(ability==0)continue;
                current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(3_600_000_000u+target*16+slot++),AffectorId=target,AffectedIds={target},
                    Type={AnnotationType.Qualification},Details={IntDetail("QualificationType",(int)QualificationType.CantBeBlocked),
                        IntDetail("grpid",checked((int)ability)),IntDetail("parent_grpid",checked((int)sourceGrp)),IntDetail("SourceParent",checked((int)target))}});
            }
        }
    }
    public static void StateCostModifications(JsonElement snapshot, GameStateMessage current) {
        if (!snapshot.TryGetProperty("cards", out var cards) || !snapshot.TryGetProperty("legalActions", out var actions)) return;
        var printedCosts = new Dictionary<uint, string>();
        foreach (var card in cards.EnumerateArray()) {
            uint id = card.GetProperty("instanceId").GetUInt32();
            if (card.TryGetProperty("manaCost", out var mc) && !string.IsNullOrWhiteSpace(mc.GetString())) {
                printedCosts[id] = mc.GetString()!;
            }
        }
        foreach (var action in actions.EnumerateArray()) {
            if (!action.TryGetProperty("instanceId", out var idProp) ||
                !action.TryGetProperty("actionType", out var typeProp) ||
                typeProp.GetInt32() != 1) continue;
            uint id = idProp.GetUInt32();
            if (!printedCosts.TryGetValue(id, out var printedCost)) continue;
            if (!action.TryGetProperty("manaCost", out var actualCostProp)) continue;
            string actualCost = actualCostProp.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(actualCost) || actualCost == printedCost) continue;
            if (current.PersistentAnnotations.Any(a => a.Id == 2_600_000_000u + id)) continue;
            string cleanCost = actualCost.Replace("{", "").Replace("}", "");
            current.PersistentAnnotations.Add(new AnnotationInfo {
                Id = checked(2_600_000_000u + id),
                Type = { AnnotationType.UseOrCostsManaCost },
                AffectedIds = { id },
                Details = {
                    new KeyValuePairInfo {
                        Key = "cost",
                        Type = (KeyValuePairValueType)6,
                        ValueString = { cleanCost }
                    }
                }
            });
        }
    }
    static readonly System.Collections.Concurrent.ConcurrentDictionary<(uint Source,string Description,string Loyalty,string Host,string Card),uint> GrantedAbilityCache=new();
    public static uint GrantedAbilityId(JsonElement identity) {
        // Forge's built-in "play this land" action has no rules text. It is
        // not a granted ability and cannot match a native ability definition.
        if(string.IsNullOrWhiteSpace(identity.GetProperty("description").GetString()))return 0;
        var key=(identity.GetProperty("sourceGrpId").GetUInt32(),identity.GetProperty("description").GetString()??"",
            identity.TryGetProperty("loyaltyCost",out var loyalty)?loyalty.GetString()??"":"",
            identity.TryGetProperty("hostCardName",out var host)?host.GetString()??"":"",
            identity.TryGetProperty("cardName",out var card)?card.GetString()??"":"");
        // Definition matching is independent of the in-game ability ID. Cache
        // misses too: unmapped abilities must not rescan the catalogue per frame.
        return GrantedAbilityCache.GetOrAdd(key,_=>FindGrantedAbilityId(identity));
    }
    static uint FindGrantedAbilityId(JsonElement identity) {
        string description=identity.GetProperty("description").GetString()!;
        string loyalty=identity.TryGetProperty("loyaltyCost",out var cost)?cost.GetString()!:"";
        string Text(string value,bool keepCost=false) {
            value=System.Text.RegularExpressions.Regex.Replace(value,"<[^>]*>","");
            value=value.Replace("CARDNAME","",StringComparison.OrdinalIgnoreCase)
                       .Replace("NICKNAME","",StringComparison.OrdinalIgnoreCase)
                       .Replace("this planeswalker","",StringComparison.OrdinalIgnoreCase)
                       .Replace("this creature","",StringComparison.OrdinalIgnoreCase)
                       .Replace("this permanent","",StringComparison.OrdinalIgnoreCase)
                       .Replace("this spell","",StringComparison.OrdinalIgnoreCase)
                       .Replace("this card","",StringComparison.OrdinalIgnoreCase)
                       .Replace("this land","",StringComparison.OrdinalIgnoreCase)
                       .Replace("this artifact","",StringComparison.OrdinalIgnoreCase)
                       .Replace("this enchantment","",StringComparison.OrdinalIgnoreCase)
                       .Replace("this equipment","",StringComparison.OrdinalIgnoreCase);
            void Strip(string? name) {
                if(string.IsNullOrWhiteSpace(name))return;
                value=value.Replace(name,"",StringComparison.OrdinalIgnoreCase);
                if(name.Contains(" // ")) {
                    foreach(var part in name.Split(" // "))
                        if(part.Trim().Length>0)value=value.Replace(part.Trim(),"",StringComparison.OrdinalIgnoreCase);
                }
                if(name.Contains(','))value=value.Replace(name.Split(',')[0].Trim(),"",StringComparison.OrdinalIgnoreCase);
                if(name.Contains(':'))value=value.Replace(name.Split(':')[0].Trim(),"",StringComparison.OrdinalIgnoreCase);
                var firstWord=name.Split(' ')[0].Trim();
                if(firstWord.Length>0 &&
                    !firstWord.Equals("the",StringComparison.OrdinalIgnoreCase) &&
                    !firstWord.Equals("a",StringComparison.OrdinalIgnoreCase) &&
                    !firstWord.Equals("an",StringComparison.OrdinalIgnoreCase))
                    value=System.Text.RegularExpressions.Regex.Replace(value,@"\b"+System.Text.RegularExpressions.Regex.Escape(firstWord)+@"\b","",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            if(identity.TryGetProperty("hostCardName",out var host))Strip(host.GetString());
            if(identity.TryGetProperty("cardName",out var card))Strip(card.GetString());
            return Normalize(value,keepCost);
        }
        string[] Quotes(string value) => System.Text.RegularExpressions.Regex.Matches(value,"[\"“]([^\"”]+)[\"”]")
            .Select(m=>m.Groups[1].Value).ToArray();
        bool CostMatches(string value) {
            var prefix=System.Text.RegularExpressions.Regex.Match(value,@"^\s*\[?([+\-]?\d+|[+\-]?X)\]?\s*:");
            return loyalty.Length==0 || !prefix.Success || prefix.Groups[1].Value==loyalty;
        }
        // Use the quoted child text, even when Forge describes the granting effect.
        var sourceQuotes=AllAbilityText.Value.TryGetValue(identity.GetProperty("sourceGrpId").GetUInt32().ToString(),out var source)
            ? source.EnumerateArray().SelectMany(a=>Quotes(a.GetProperty("text").GetString()!)).Where(CostMatches).Distinct().ToArray()
            : Array.Empty<string>();
        string normalizedDescription=Text(description);
        var matchingQuotes=sourceQuotes.Where(q=>Text(q)==normalizedDescription).ToArray();
        if(matchingQuotes.Length==0 && Quotes(description).Length>0)
            matchingQuotes=Quotes(description).Where(CostMatches).ToArray();
        if(matchingQuotes.Length==1)description=matchingQuotes[0];
        normalizedDescription=Text(description);
        var grantingIds=source.ValueKind==JsonValueKind.Array
            ? source.EnumerateArray().Where(a=>Quotes(a.GetProperty("text").GetString()!).Length>0)
                .Select(a=>a.GetProperty("id").GetUInt32()).ToHashSet() : new HashSet<uint>();
        IEnumerable<JsonElement> candidates=loyalty.Length==0
            ? GrantedDefinitions.Value.EnumerateArray() : GrantedByLoyalty.Value[loyalty];
        var matches=candidates.Where(a=>
            // Never select the outer granting rule as an individual ability.
            !grantingIds.Contains(a.GetProperty("id").GetUInt32())
            && (loyalty.Length==0 || a.GetProperty("loyalty").GetString()==loyalty)
            && Text(a.GetProperty("text").GetString()!)==normalizedDescription)
            .Select(a=>a.GetProperty("id").GetUInt32()).Distinct().ToArray();
        if(matches.Length==1)return matches[0];
        // Preserve quoted activation costs before resolving effect-only text.
        // Equivalent installed definitions use a shared printing ID only when
        // one has a unique highest reference count across installed printings.
        if(matches.Length>1 && matchingQuotes.Length==1 && description.Contains(':')) {
            var exact=candidates.Where(a=>matches.Contains(a.GetProperty("id").GetUInt32())
                && Text(a.GetProperty("text").GetString()!,true)==Text(description,true))
                .Select(a=>a.GetProperty("id").GetUInt32()).Distinct().ToArray();
            if(exact.Length==1)return exact[0];
            if(exact.Length>1) {
                var ranked=exact.Select(id=>(Id:id,Count:SharedAbilityReferences.Value.GetValueOrDefault(id)))
                    .OrderByDescending(a=>a.Count).ToArray();
                if(ranked[0].Count>ranked[1].Count)return ranked[0].Id;
            }
        }
        // A loyalty activation must use its own definition, never its granting static text.
        if(loyalty.Length>0)return 0;
        return 0;
    }
    public static List<Action> Actions(JsonElement snapshot)
    {
        var result = new List<Action>();
        if (!snapshot.TryGetProperty("legalActions", out var choices)) return result;
        foreach (var c in choices.EnumerateArray()) {
            bool land = c.GetProperty("isLand").GetBoolean();
            var action = new Action {
                ActionType = c.TryGetProperty("actionType", out var type) ? (ActionType)type.GetInt32() : land ? ActionType.Play : ActionType.Cast,
                InstanceId = c.GetProperty("instanceId").GetUInt32(), GrpId = c.GetProperty("grpId").GetUInt32(),
                Selection = c.TryGetProperty("abilityId", out var ability) ? ability.GetUInt32() : 0,
                Highlight = c.TryGetProperty("highlight", out var hl) ? (HighlightType)hl.GetInt32() : HighlightType.Hot
            };
            if(c.TryGetProperty("faceDownCast",out var faceDownCast) && faceDownCast.GetBoolean()) {
                action.AlternativeGrpId=c.GetProperty("faceDownKind").GetString()=="Disguise"?307u:37u;
                action.SourceId=action.InstanceId;
            }
            if(action.ActionType==ActionType.SpecialTurnFaceUp && c.TryGetProperty("turnFaceUpKind",out var upKind)) {
                action.SourceId=action.InstanceId;
                string kind=upKind.GetString()!;
                action.AbilityGrpId=kind switch {"Manifest"=>145u,"Cloak"=>314u,"Disguise"=>307u,"Morph"=>37u,_=>0u};
            }
            if (action.ActionType is ActionType.Activate or ActionType.ActivateMana && c.TryGetProperty("description", out var description)) {
                string loyaltyCost = "";
                if (c.TryGetProperty("abilityIdentity", out var identity) && identity.TryGetProperty("loyaltyCost", out var lcProp))
                    loyaltyCost = lcProp.GetString() ?? "";
                action.AbilityGrpId=AbilityId(action.GrpId,description.GetString()!,c.TryGetProperty("cardName",out var name)?name.GetString()!:"",true,loyaltyCost);
                if(c.TryGetProperty("cycling",out var cycling) && cycling.GetBoolean()
                    && AbilityText.Value.TryGetValue(action.GrpId.ToString(),out var cardAbilities)) {
                    var cycleIds=cardAbilities.EnumerateArray().Where(a=>System.Text.RegularExpressions.Regex.IsMatch(
                        a.GetProperty("text").GetString()!,@"^\s*(?:<[^>]*>)*Cycling\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        .Select(a=>a.GetProperty("id").GetUInt32()).Distinct().ToArray();
                    if(cycleIds.Length==1)action.AbilityGrpId=cycleIds[0];
                }
                if(identity.ValueKind == JsonValueKind.Object && (action.AbilityGrpId==0
                    || identity.TryGetProperty("granted",out var granted) && granted.GetBoolean())) {
                    uint grantedId = GrantedAbilityId(identity);
                    if (grantedId != 0) action.AbilityGrpId = grantedId;
                }
                if(action.AbilityGrpId!=0)action.SourceId=action.InstanceId;
            }
            if (action.ActionType is ActionType.CastMdfc or ActionType.PlayMdfc
                || action.ActionType is ActionType.CastLeft or ActionType.CastRight
                || action.ActionType is ActionType.CastAdventure or ActionType.CastOmen or ActionType.CastPrototype
                || action.ActionType is ActionType.CastLeftRoom or ActionType.CastRightRoom) {
                action.SourceId = action.InstanceId;
            }
            if (!land && !MatchDoorHost.TryManaCost(c.GetProperty("manaCost").GetString() ?? "", action)) continue;
            var solution = new AutoTapSolution();
            if (c.TryGetProperty("autoTapSources", out var sources))
                foreach (var source in sources.EnumerateArray()) solution.AutoTapActions.Add(new AutoTapAction { InstanceId = source.GetUInt32() });
            if (c.TryGetProperty("autoTapPool", out var pool))
                foreach (var mana in pool.EnumerateArray()) {
                    var info = new ManaInfo { ManaId = mana.GetProperty("manaId").GetUInt32(), Color = (ManaColor)mana.GetProperty("color").GetInt32(),
                        SrcInstanceId = mana.GetProperty("srcInstanceId").GetUInt32(), Count = mana.GetProperty("count").GetUInt32() };
                    solution.AutoTapActions.Add(new AutoTapAction { ManaId = info.ManaId, ManaPaymentOption = new ManaPaymentOption { Mana = { info } } });
                }
            if (solution.AutoTapActions.Count > 0) action.AutoTapSolution = solution;
            result.Add(action);
        }
        return result;
    }
    public sealed record PlaybackResult(long Cursor, HashSet<uint> Taps, HashSet<uint> Zones);
    public static void StateCastingChoices(JsonElement snapshot,GameStateMessage current)
    {
        if(!snapshot.TryGetProperty("stack",out var stack))return;
        foreach(var entry in stack.EnumerateArray()) {
            if(entry.GetProperty("isAbility").GetBoolean() || !entry.TryGetProperty("xValue",out var x))continue;
            uint source=entry.GetProperty("instanceId").GetUInt32();
            current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(2_000_000_000u+source),
                Type={AnnotationType.CastingTimeOption},AffectedIds={source},
                Details={IntDetail("type",(int)CastingTimeOptionType.ChooseX),IntDetail("value",x.GetInt32())}});
        }
    }
    public static void StateAttachments(JsonElement snapshot,GameStateMessage current)
    {
        var present=current.GameObjects.Where(c=>c.GrpId!=0).Select(c=>c.InstanceId)
            .Concat(current.Players.Select(p=>p.SystemSeatNumber)).ToHashSet();
        foreach(var card in snapshot.GetProperty("cards").EnumerateArray()) {
            if(!card.TryGetProperty("attachedTo",out var target))continue;
            uint source=card.GetProperty("instanceId").GetUInt32(), attachedTo=target.GetUInt32();
            if(source==attachedTo || !present.Contains(source) || !present.Contains(attachedTo))continue;
            current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(2_100_000_000u+source),
                Type={AnnotationType.Attachment},AffectorId=source,AffectedIds={attachedTo}});
        }
    }
    static KeyValuePairInfo IntDetail(string key, params int[] values)
    {
        var detail = new KeyValuePairInfo { Key = key, Type = (KeyValuePairValueType)2 };
        detail.ValueInt32.AddRange(values); return detail;
    }
    static readonly Dictionary<string,CounterType> CounterTypes=Enum.GetValues<CounterType>()
        .Where(t=>t!=CounterType.None && !t.ToString().StartsWith("PHCT",StringComparison.OrdinalIgnoreCase))
        .ToDictionary(t=>CounterKey(t.ToString()),StringComparer.OrdinalIgnoreCase);
    static string CounterKey(string name)=>System.Text.RegularExpressions.Regex.Replace(name,"[^a-zA-Z0-9]","");
    public static void StateCounters(JsonElement snapshot, GameStateMessage current)
    {
        var visible=current.GameObjects.Where(c=>c.GrpId!=0).ToDictionary(c=>c.InstanceId);
        var players=current.Players.Select(p=>p.SystemSeatNumber).ToHashSet();
        void AddCounters(uint target,JsonElement entity) {
            if(!entity.TryGetProperty("counters",out var counters))return;
            foreach(var counter in counters.EnumerateArray()) {
                if(!CounterTypes.TryGetValue(CounterKey(counter.GetProperty("type").GetString()!),out var type))continue;
                int count=counter.GetProperty("count").GetInt32();if(count<=0)continue;
                // Full states replace this collection. Stable IDs distinguish
                // counter types without replaying addition effects on reconnect.
                current.PersistentAnnotations.Add(new AnnotationInfo {
                    Id=checked(1_000_000_000u+target*256+(uint)type),Type={AnnotationType.Counter},AffectedIds={target},
                    Details={IntDetail("counter_type",(int)type),IntDetail("count",count)}
                });
            }
        }
        foreach(var card in snapshot.GetProperty("cards").EnumerateArray()) {
            uint target=card.GetProperty("instanceId").GetUInt32();
            if(visible.TryGetValue(target,out var native)) {
                AddCounters(target,card);
                foreach(var (key,designation,slot) in new[]{("monstrous",Designation.Monstrous,0u),("renowned",Designation.Renowned,1u)})
                    if(card.TryGetProperty(key,out var designated) && designated.GetBoolean())
                        current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(3_300_000_000u+target*2+slot),
                            Type={AnnotationType.Designation},AffectorId=target,AffectedIds={target},
                            Details={IntDetail("DesignationType",(int)designation),IntDetail("CostIncrease",0),IntDetail("grpid",checked((int)native.GrpId))}});
                if(card.TryGetProperty("enteredZoneThisTurn",out var entered) && entered.GetBoolean() && native.ZoneId!=40)
                    current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(3_000_000_000u+target),
                        Type={AnnotationType.EnteredZoneThisTurn},AffectorId=native.ZoneId,AffectedIds={target}});
                if(card.TryGetProperty("classLevel",out var level) && level.GetInt32()>0)
                    current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(3_100_000_000u+target),
                        Type={AnnotationType.ClassLevel},AffectorId=target,AffectedIds={target},Details={IntDetail("Level",level.GetInt32())}});
                if(card.TryGetProperty("isCopy",out var copied) && copied.GetBoolean()
                    && card.TryGetProperty("copyFromGrpId",out var copyGrp))
                    current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(2_900_000_000u+target),
                        Type={AnnotationType.CopiedObject},AffectedIds={target},Details={IntDetail("copyFromGrpid",checked((int)copyGrp.GetUInt32()))}});
                if(card.TryGetProperty("regenerationShields",out var shields) && shields.GetInt32()>0) {
                    var pending=new AnnotationInfo {Id=checked(2_700_000_000u+target),Type={AnnotationType.RegeneratePending}};
                    pending.AffectedIds.AddRange(Enumerable.Repeat(target,shields.GetInt32()));
                    current.PersistentAnnotations.Add(pending);
                }
            }
            if(card.TryGetProperty("faceDown",out var faceDown) && faceDown.GetBoolean()
                && card.TryGetProperty("faceDownReason",out var reason) && Enum.TryParse<ReasonFaceDown>(reason.GetString(),out var faceReason))
                current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(2_800_000_000u+target),Type={AnnotationType.FaceDown},
                    AffectedIds={target},Details={IntDetail("REASON",(int)faceReason)}});
        }
        foreach(var player in snapshot.GetProperty("players").EnumerateArray()) {
            uint target=player.GetProperty("seat").GetUInt32();
            if(players.Contains(target)) {
                AddCounters(target,player);
                foreach(var (key,designation,slot) in new[]{("monarch",Designation.Monarch,0u),("citysBlessing",Designation.CitysBlessing,1u)})
                    if(player.TryGetProperty(key,out var designated) && designated.GetBoolean())
                        current.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(3_400_000_000u+target*2+slot),
                            Type={AnnotationType.Designation},AffectorId=target,AffectedIds={target},
                            Details={IntDetail("DesignationType",(int)designation),IntDetail("CostIncrease",0),IntDetail("grpid",0)}});
            }
        }
    }
    public static void StateCommanders(JsonElement snapshot, GameStateMessage current)
    {
        var visible=current.GameObjects.Where(c=>c.GrpId!=0).ToDictionary(c=>c.InstanceId);
        foreach(var card in snapshot.GetProperty("cards").EnumerateArray()) {
            uint id=card.GetProperty("instanceId").GetUInt32();
            if(!visible.TryGetValue(id,out var native) || !card.TryGetProperty("commander",out var commander) || !commander.GetBoolean())continue;
            int casts=card.TryGetProperty("commanderCastCount",out var count)?count.GetInt32():0;
            current.PersistentAnnotations.Add(new AnnotationInfo {
                Id=checked(2_000_000_000u+id),Type={AnnotationType.Designation},AffectedIds={id},
                Details={IntDetail("DesignationType",(int)Designation.Commander),IntDetail("CostIncrease",checked(casts*2)),
                    IntDetail("grpid",(int)native.GrpId),IntDetail("ControllerId",(int)native.ControllerSeatId)}
            });
        }
    }
    public static void StateLoyaltyActivations(JsonElement snapshot, GameStateMessage current)
    {
        var visible = current.GameObjects.Where(c => c.GrpId != 0).Select(c => c.InstanceId).ToHashSet();
        if (!snapshot.TryGetProperty("cards", out var cards)) return;
        foreach (var card in cards.EnumerateArray()) {
            uint target = card.GetProperty("instanceId").GetUInt32();
            if (!visible.Contains(target)) continue;
            if (!card.TryGetProperty("loyaltyActivationsRemaining", out var rem)) continue;
            int remaining = rem.GetInt32();
            current.PersistentAnnotations.Add(new AnnotationInfo {
                Id = checked(2_200_000_000u + target),
                Type = { AnnotationType.LoyaltyActivationsRemaining },
                AffectedIds = { target },
                Details = { IntDetail("ActivationsRemaining", remaining) }
            });
        }
    }
    static Dictionary<(uint target,int type),int> Counters(GameStateMessage state) => state.PersistentAnnotations
        .Where(a=>a.Type.Contains(AnnotationType.Counter)).ToDictionary(
            a=>(a.AffectedIds.Single(),a.Details.Single(d=>d.Key=="counter_type").ValueInt32.Single()),
            a=>a.Details.Single(d=>d.Key=="count").ValueInt32.Single());
    static void CounterTransitions(GameStateMessage previous,GameStateMessage current,ref uint nextId)
    {
        var old=Counters(previous);var next=Counters(current);
        var visible=previous.GameObjects.Where(c=>c.GrpId!=0).Select(c=>c.InstanceId).Intersect(
            current.GameObjects.Where(c=>c.GrpId!=0 && c.ZoneId==30).Select(c=>c.InstanceId)).ToHashSet();
        visible.UnionWith(previous.Players.Select(p=>p.SystemSeatNumber).Intersect(current.Players.Select(p=>p.SystemSeatNumber)));
        foreach(var key in old.Keys.Union(next.Keys)) {
            if(!visible.Contains(key.target))continue;
            int delta=next.GetValueOrDefault(key)-old.GetValueOrDefault(key);if(delta==0)continue;
            current.Annotations.Add(new AnnotationInfo {Id=nextId++,AffectorId=current.Annotations.LastOrDefault(a=>a.Type.Contains(AnnotationType.ResolutionStart))?.AffectorId ?? key.target,
                Type={delta>0?AnnotationType.CounterAdded:AnnotationType.CounterRemoved},AffectedIds={key.target},
                Details={IntDetail("counter_type",key.type),IntDetail("transaction_amount",Math.Abs(delta))}});
        }
    }
    public static void StateTargets(JsonElement snapshot, GameStateMessage current, ref uint nextId)
    {
        var records=new Dictionary<uint,JsonElement>();
        foreach(string key in new[]{"playbackObjects","stack"})
            if(snapshot.TryGetProperty(key,out var entries))foreach(var entry in entries.EnumerateArray())records[entry.GetProperty("instanceId").GetUInt32()]=entry;
        var ids=current.GameObjects.Select(c=>c.InstanceId).Concat(current.Players.Select(p=>p.SystemSeatNumber)).ToHashSet();
        foreach(var entry in records.Values) {
            uint source=entry.GetProperty("instanceId").GetUInt32();
            if(!ids.Contains(source))continue;
            uint ability=AbilityId(entry.GetProperty("sourceGrpId").GetUInt32(),entry.GetProperty("description").GetString()!,entry.GetProperty("cardName").GetString()!);
            if(entry.TryGetProperty("abilityIdentity",out var targetIdentity) && targetIdentity.TryGetProperty("granted",out var granted) && granted.GetBoolean()) {
                uint individual=GrantedAbilityId(targetIdentity);
                if(individual!=0)ability=individual;
            }
            foreach(var targets in entry.GetProperty("targets").EnumerateArray()) {
                var selected=targets.GetProperty("ids").EnumerateArray().Select(t=>t.GetUInt32()).ToArray();
                if(selected.Length==0 || selected.Any(t=>!ids.Contains(t)))continue;
                current.Annotations.Add(new AnnotationInfo { Id=nextId++,Type={AnnotationType.TargetSpec},AffectorId=source,AffectedIds={selected},Details={
                    IntDetail("index",targets.GetProperty("index").GetInt32()),IntDetail("abilityGrpId",checked((int)ability)),
                    IntDetail("distributions",targets.GetProperty("distributions").EnumerateArray().Select(d=>d.GetInt32()).ToArray())
                }});
            }
        }
    }
    public static PlaybackResult Playback(JsonElement snapshot, GameStateMessage? previous, GameStateMessage current, long after, ref uint nextId)
    {
        long cursor = snapshot.TryGetProperty("playbackId", out var last) ? last.GetInt64() : after;
        if(cursor < after)throw new InvalidDataException("Playback cursor moved backwards");
        var result = new PlaybackResult(cursor, new(), new());
        // Initial/reconnected viewers receive the current board, not historical effects.
        if(previous == null || !snapshot.TryGetProperty("playbackEvents", out var events))return result;
        if(snapshot.TryGetProperty("playbackGap",out var gap) && gap.GetBoolean()) {
            Console.WriteLine("Playback journal exceeded retention; refreshing board without historical effects.");
            return result;
        }
        var cards = previous.GameObjects.Concat(current.GameObjects).GroupBy(c=>c.InstanceId).ToDictionary(g=>g.Key,g=>g.Last());
        var entities = cards.Keys.Concat(current.Players.Select(p=>p.SystemSeatNumber)).ToHashSet();
        var zones = current.Zones.Select(z=>z.ZoneId).ToHashSet();
        long seen = after;
        foreach(var e in events.EnumerateArray()) {
            long eventId=e.GetProperty("eventId").GetInt64();
            if(eventId<=after)continue;
            if(eventId<=seen || eventId>cursor)throw new InvalidDataException("Unordered playback journal");
            seen=eventId;
            string type=e.GetProperty("type").GetString()!;
            uint source=e.TryGetProperty("sourceId",out var s)?s.GetUInt32():0;
            uint target=e.TryGetProperty("targetId",out var t)?t.GetUInt32():0;
            var annotation = new AnnotationInfo();
            switch(type) {
                case "multistepEffect":
                    if(!cards.TryGetValue(source,out var effectCard) || effectCard.GrpId==0 || !current.Players.Any(p=>p.SystemSeatNumber==target))continue;
                    if(!Enum.TryParse<AbilitySubCategory>(e.GetProperty("category").GetString(),out var category)
                        || category is not (AbilitySubCategory.Scry or AbilitySubCategory.Surveil or AbilitySubCategory.Proliferate or AbilitySubCategory.Explore or AbilitySubCategory.Connive))
                        throw new InvalidDataException("Unsupported multistep effect category");
                    annotation.Type.Add(e.GetProperty("starting").GetBoolean()?AnnotationType.MultistepEffectStarted:AnnotationType.MultistepEffectComplete);
                    annotation.AffectorId=source;annotation.AffectedIds.Add(target);
                    annotation.Details.Add(IntDetail("SubCategory",(int)category));
                    break;
                case "replacementApplied":
                    if(!cards.ContainsKey(source) || !entities.Contains(target))continue;
                    uint replacementAbility=AbilityId(e.GetProperty("sourceGrpId").GetUInt32(),
                        e.GetProperty("description").GetString()!,e.GetProperty("cardName").GetString()!,true);
                    if(replacementAbility==0)continue;
                    annotation.Type.Add(AnnotationType.ReplacementEffectApplied);
                    annotation.AffectorId=source;annotation.AffectedIds.Add(target);
                    annotation.Details.Add(IntDetail("grpid",checked((int)replacementAbility)));
                    annotation.Details.Add(IntDetail("IsDamageReplacement",e.GetProperty("damage").GetBoolean()?1:0));
                    break;
                case "illegalAttachment":
                    if(!cards.ContainsKey(target))continue;
                    string invalidatingText=e.GetProperty("description").GetString()!;
                    uint invalidatingAbility=AbilityId(e.GetProperty("sourceGrpId").GetUInt32(),invalidatingText,e.GetProperty("cardName").GetString()!);
                    if(invalidatingAbility==0)invalidatingAbility=KeywordAbilityId(invalidatingText.Split(" (")[0]);
                    if(invalidatingAbility==0)continue;
                    annotation.Type.Add(AnnotationType.RemoveAttachment);annotation.AffectorId=source;annotation.AffectedIds.Add(target);
                    annotation.Details.Add(IntDetail("invalidating_grpid",checked((int)invalidatingAbility)));
                    break;
                case "randomOutcome":
                    if(!cards.ContainsKey(source) || !current.Players.Any(p=>p.SystemSeatNumber==target))continue;
                    var outcomes=e.GetProperty("results").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                    if(outcomes.Length==0)continue;
                    annotation.AffectorId=source;annotation.AffectedIds.Add(target);
                    if(e.GetProperty("kind").GetString()=="coin") {
                        if(outcomes.Any(v=>v is not (0 or 1)))throw new InvalidDataException("Invalid coin outcome");
                        annotation.Type.Add(AnnotationType.CoinFlip);
                        annotation.Details.Add(IntDetail("CoinFlipResult",outcomes));
                    } else if(e.GetProperty("kind").GetString()=="die") {
                        var natural=e.GetProperty("natural").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                        int sides=e.GetProperty("sides").GetInt32();
                        if(outcomes.Length!=1 || natural.Length!=1 || sides<1 || natural[0]<1 || natural[0]>sides)
                            throw new InvalidDataException("Invalid die outcome");
                        annotation.Type.Add(AnnotationType.DieRoll);
                        annotation.Details.Add(IntDetail("Result",outcomes[0]));
                        annotation.Details.Add(IntDetail("NaturalResult",natural[0]));
                        annotation.Details.Add(IntDetail("Faces",sides));
                        annotation.Details.Add(new KeyValuePairInfo {Key="Ignored",Type=(KeyValuePairValueType)6,
                            ValueString={e.GetProperty("ignored").GetBoolean()?"true":"false"}});
                    } else continue;
                    break;
                case "regenerated":
                    if(!cards.ContainsKey(target))continue;
                    annotation.Type.Add(AnnotationType.PermanentRegenerated);
                    annotation.AffectedIds.Add(target);
                    break;
                case "scryResult":
                    var topIds=e.GetProperty("topIds").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                    var bottomIds=e.GetProperty("bottomIds").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                    var scryIds=topIds.Concat(bottomIds).Distinct().ToArray();
                    if(scryIds.Length==0 || scryIds.Any(id=>!cards.ContainsKey((uint)id)))continue;
                    annotation.Type.Add(AnnotationType.Scry);
                    annotation.AffectedIds.AddRange(scryIds.Select(id=>(uint)id));
                    annotation.Details.Add(IntDetail("topIds",topIds));
                    annotation.Details.Add(IntDetail("bottomIds",bottomIds));
                    if(e.TryGetProperty("sourceGrpId",out var scrySource) && scrySource.GetInt32()>0)
                        annotation.Details.Add(IntDetail("effectGrpid",scrySource.GetInt32()));
                    break;
                case "userAction":
                    uint playerId=e.GetProperty("playerId").GetUInt32();
                    if(!cards.TryGetValue(source,out var actedCard) || actedCard.GrpId==0 || !current.Players.Any(p=>p.SystemSeatNumber==playerId))continue;
                    int actionType=e.GetProperty("actionType").GetInt32();uint abilityGrp=0;
                    if(actionType is 2 or 4 && e.TryGetProperty("object",out var actionObject)) {
                        abilityGrp=AbilityId(actionObject.GetProperty("sourceGrpId").GetUInt32(),actionObject.GetProperty("description").GetString()!,actionObject.GetProperty("cardName").GetString()!,true);
                        if(actionObject.TryGetProperty("abilityIdentity",out var identity) && (abilityGrp==0
                            || identity.TryGetProperty("granted",out var granted) && granted.GetBoolean()))abilityGrp=GrantedAbilityId(identity);
                    }
                    annotation.Type.Add(AnnotationType.UserActionTaken);annotation.AffectorId=playerId;annotation.AffectedIds.Add(source);
                    annotation.Details.Add(IntDetail("actionType",actionType));annotation.Details.Add(IntDetail("abilityGrpId",checked((int)abilityGrp)));
                    break;
                case "targetsSelecting": case "targetsSubmitted":
                    uint actingPlayer=e.GetProperty("playerId").GetUInt32();
                    if(!cards.TryGetValue(source,out var targetedSource) || targetedSource.GrpId==0 || !current.Players.Any(p=>p.SystemSeatNumber==actingPlayer))continue;
                    annotation.Type.Add(type=="targetsSelecting"?AnnotationType.PlayerSelectingTargets:AnnotationType.PlayerSubmittedTargets);
                    annotation.AffectorId=actingPlayer; annotation.AffectedIds.Add(source);
                    break;
                case "abilityCreated":
                    var created=current.GameObjects.FirstOrDefault(c=>c.InstanceId==source && c.Type==GameObjectType.Ability);
                    if(created==null)continue;
                    annotation.Type.Add(AnnotationType.AbilityInstanceCreated); annotation.AffectedIds.Add(source);
                    annotation.AffectorId=created.ParentId;
                    if(cards.TryGetValue(created.ParentId,out var parent))annotation.Details.Add(IntDetail("source_zone",checked((int)parent.ZoneId)));
                    break;
                case "lifeGain":
                    if(!entities.Contains(target))continue;
                    annotation.Type.Add(AnnotationType.ModifiedLife);annotation.AffectorId=entities.Contains(source)?source:target;
                    annotation.AffectedIds.Add(target);annotation.Details.Add(IntDetail("life",e.GetProperty("amount").GetInt32()));
                    break;
                case "damage":
                    if(!cards.TryGetValue(source,out var damager) || damager.GrpId==0 || !entities.Contains(target))continue;
                    int amount=e.GetProperty("amount").GetInt32(); if(amount<=0)continue;
                    annotation.Type.Add(AnnotationType.DamageDealt);
                    annotation.AffectorId=source; annotation.AffectedIds.Add(target);
                    annotation.Details.Add(IntDetail("type", e.GetProperty("damageType").GetInt32()));
                    annotation.Details.Add(IntDetail("damage",amount));
                    // The full authoritative snapshot already includes marked damage.
                    annotation.Details.Add(IntDetail("markDamage",0));
                    break;
                case "resolutionStart": case "resolutionComplete":
                    if(!cards.TryGetValue(source,out var resolving) || resolving.GrpId==0)continue;
                    annotation.Type.Add(type=="resolutionStart"?AnnotationType.ResolutionStart:AnnotationType.ResolutionComplete);
                    annotation.AffectorId=source; annotation.Details.Add(IntDetail("grpid",checked((int)resolving.GrpId)));
                    annotation.AffectedIds.Add(source);
                    break;
                case "shuffle":
                    uint player=e.GetProperty("playerId").GetUInt32();
                    if(!current.Players.Any(p=>p.SystemSeatNumber==player))continue;
                    annotation.Type.Add(AnnotationType.Shuffle); annotation.AffectedIds.Add(player);
                    // Forge preserves instance IDs across a shuffle. No view-ID remap is needed.
                    annotation.Details.Add(IntDetail("OldIds")); annotation.Details.Add(IntDetail("NewIds"));
                    break;
                case "reveal":
                    uint revealedId=checked(100_000_000u+target);
                    if(!cards.ContainsKey(revealedId))continue;
                    annotation.Type.Add(AnnotationType.CardRevealed);annotation.AffectedIds.Add(revealedId);
                    annotation.Details.Add(IntDetail("source_zone",e.GetProperty("sourceZone").GetInt32()));
                    break;
                case "manaPaid":
                    if(!cards.TryGetValue(source,out var producer) || !cards.ContainsKey(target))continue;
                    annotation.Type.Add(AnnotationType.ManaPaid);annotation.AffectorId=source;annotation.AffectedIds.Add(target);
                    annotation.Details.Add(IntDetail("id",e.GetProperty("manaId").GetInt32()));
                    annotation.Details.Add(IntDetail("color",e.GetProperty("color").GetInt32()));
                    annotation.Details.Add(IntDetail("substitution_grpid",checked((int)producer.GrpId)));
                    break;
                case "phasing":
                    if(!cards.ContainsKey(target))continue;
                    annotation.Type.Add(e.GetProperty("phasedOut").GetBoolean()?AnnotationType.PhasedOut:AnnotationType.PhasedIn);
                    annotation.AffectedIds.Add(target);result.Zones.Add(target);
                    break;
                case "tap":
                    if(!previous.GameObjects.Any(c=>c.InstanceId==target && c.ZoneId==30)
                        || !current.GameObjects.Any(c=>c.InstanceId==target && c.ZoneId==30) || result.Taps.Contains(target))continue;
                    annotation.Type.Add(AnnotationType.TappedUntappedPermanent); annotation.AffectedIds.Add(target);
                    annotation.Details.Add(IntDetail("tapped",current.GameObjects.Single(c=>c.InstanceId==target).IsTapped?1:0)); result.Taps.Add(target);
                    break;
                case "objectIdChanged":
                    if(!cards.ContainsKey(target))continue;
                    annotation.Type.Add(AnnotationType.ObjectIdChanged);
                    annotation.AffectedIds.Add(target);
                    annotation.Details.Add(IntDetail("orig_id",checked((int)source)));
                    annotation.Details.Add(IntDetail("new_id",checked((int)target)));
                    break;
                case "zoneTransfer":
                    uint from=e.GetProperty("fromZone").GetUInt32(), to=e.GetProperty("toZone").GetUInt32();
                    if(!cards.ContainsKey(target) || !zones.Contains(from) || !zones.Contains(to) || from==to)continue;
                    annotation.Type.Add(AnnotationType.ZoneTransfer); annotation.AffectedIds.Add(target);
                    annotation.Details.Add(IntDetail("zone_src",checked((int)from))); annotation.Details.Add(IntDetail("zone_dest",checked((int)to)));
                    annotation.Details.Add(new KeyValuePairInfo { Key="category",Type=(KeyValuePairValueType)6,ValueString={e.GetProperty("category").GetString()!} });
                    result.Zones.Add(target);
                    break;
                default: continue;
            }
            annotation.Id=nextId++; current.Annotations.Add(annotation);
        }
        // Arena reconciles combat hits with source-attributed life changes. A
        // bare life-total diff has no attacker ID and reduces impact amounts to zero.
        var playerIds=current.Players.Select(p=>p.SystemSeatNumber).ToHashSet();
        var hits=events.EnumerateArray().Where(e=>e.GetProperty("eventId").GetInt64()>after
            && e.GetProperty("type").GetString()=="damage" && e.GetProperty("damageType").GetInt32()==1
            && playerIds.Contains(e.GetProperty("targetId").GetUInt32())
            && (!e.TryGetProperty("infect",out var infect) || !infect.GetBoolean()));
        foreach(var group in hits.GroupBy(e=>e.GetProperty("targetId").GetUInt32())) {
            uint source=group.First().GetProperty("sourceId").GetUInt32();
            if(!cards.ContainsKey(source))continue;
            int damage=group.Sum(e=>e.GetProperty("amount").GetInt32());
            current.Annotations.Add(new AnnotationInfo {Id=nextId++,Type={AnnotationType.ModifiedLife},AffectorId=source,
                AffectedIds={group.Key},Details={IntDetail("life",-damage)}});
        }
        return result;
    }
    public static void Transitions(GameStateMessage? previous, GameStateMessage current, ref uint nextId, PlaybackResult? playback = null)
    {
        if (previous == null) return;
        if(previous.TurnInfo is {} oldTurn && current.TurnInfo is {} turn) {
            bool newTurn=oldTurn.TurnNumber!=turn.TurnNumber || oldTurn.ActivePlayer!=turn.ActivePlayer;
            // TurnInfo supplies state; these events drive Arena's TurnController
            // and phase ladder. Its ordinary diff generator does not emit them.
            if(newTurn)current.Annotations.Add(new AnnotationInfo {Id=nextId++,Type={AnnotationType.NewTurnStarted},AffectorId=turn.ActivePlayer,AffectedIds={turn.ActivePlayer}});
            if(newTurn || oldTurn.Phase!=turn.Phase || oldTurn.Step!=turn.Step)
                current.Annotations.Add(new AnnotationInfo {Id=nextId++,Type={AnnotationType.PhaseOrStepModified},
                    Details={IntDetail("phase",(int)turn.Phase),IntDetail("step",(int)turn.Step)}});
        }
        CounterTransitions(previous,current,ref nextId);
        var old = previous.GameObjects.ToDictionary(c => c.InstanceId);
        string ChoiceKind(AnnotationInfo a)=>a.Details.FirstOrDefault(d=>d.Key=="ChooseLinkType")?.ValueString.FirstOrDefault()??"";
        bool SupportedChoice(AnnotationInfo a)=>a.Type.Contains(AnnotationType.LinkInfo) && ChoiceKind(a) is "Type" or "Color" or "Amount";
        var oldTypeChoices=previous.PersistentAnnotations.Where(SupportedChoice)
            .ToDictionary(a=>(a.AffectorId,ChoiceKind(a)),a=>a.AffectedIds.ToArray());
        foreach(var choice in current.PersistentAnnotations.Where(SupportedChoice)) {
            string kind=ChoiceKind(choice);
            if(choice.AffectedIds.Count==0 || kind=="Type" && (choice.AffectedIds.Count!=2 || choice.AffectedIds[0]!=(uint)TypeCategory.SubType)
                || oldTypeChoices.TryGetValue((choice.AffectorId,kind),out var previousChoice) && previousChoice.SequenceEqual(choice.AffectedIds))continue;
            var chooser=current.GameObjects.FirstOrDefault(c=>c.InstanceId==choice.AffectorId);
            if(chooser==null || chooser.ControllerSeatId==0)continue;
            var result=new AnnotationInfo {Id=nextId++,Type={AnnotationType.ChoiceResult},AffectorId=choice.AffectorId,
                AffectedIds={chooser.ControllerSeatId},Details={kind=="Amount"
                    ?new KeyValuePairInfo {Key="Choice_Domain",Type=KeyValuePairValueType.String,ValueString={"number"}}
                    :IntDetail("Choice_Domain",(int)(kind=="Color"?StaticList.Colors:StaticList.SubTypes)),
                    IntDetail("Choice_Sentiment",(int)HighlightType.Hot)}};
            var values=new KeyValuePairInfo {Key="Choice_Value",Type=KeyValuePairValueType.Int32};
            values.ValueInt32.Add((kind=="Type"?choice.AffectedIds.Skip(1):choice.AffectedIds).Select(v=>checked((int)v)));
            result.Details.Add(values);current.Annotations.Add(result);
        }
        foreach(var reveal in previous.GameObjects.Where(c=>c.Type==GameObjectType.RevealedCard && !current.GameObjects.Any(n=>n.InstanceId==c.InstanceId)))
            current.Annotations.Add(new AnnotationInfo {Id=nextId++,Type={AnnotationType.RevealedCardDeleted},AffectorId=reveal.InstanceId});
        var zones = current.Zones.ToDictionary(z => z.ZoneId, z => z.Type);
        var currentIds=current.GameObjects.Select(c=>c.InstanceId).ToHashSet();
        foreach(var ability in previous.GameObjects.Where(c=>c.Type==GameObjectType.Ability && !currentIds.Contains(c.InstanceId)))
            current.Annotations.Add(new AnnotationInfo {Id=nextId++,Type={AnnotationType.AbilityInstanceDeleted},AffectorId=ability.ParentId,AffectedIds={ability.InstanceId}});
        var oldAttachments=previous.PersistentAnnotations.Where(a=>a.Type.Contains(AnnotationType.Attachment))
            .ToDictionary(a=>a.AffectorId,a=>a.AffectedIds.Single());
        var newAttachments=current.PersistentAnnotations.Where(a=>a.Type.Contains(AnnotationType.Attachment))
            .ToDictionary(a=>a.AffectorId,a=>a.AffectedIds.Single());
        foreach(var source in oldAttachments.Keys.Union(newAttachments.Keys)) {
            if(!newAttachments.TryGetValue(source,out var target) || oldAttachments.GetValueOrDefault(source)==target)continue;
            current.Annotations.Add(new AnnotationInfo {Id=nextId++,Type={AnnotationType.AttachmentCreated},AffectorId=source,AffectedIds={target}});
        }
        foreach(var token in previous.GameObjects.Where(c=>c.Type==GameObjectType.Token && !currentIds.Contains(c.InstanceId)))
            current.Annotations.Add(new AnnotationInfo {Id=nextId++,Type={AnnotationType.TokenDeleted},AffectedIds={token.InstanceId}});
        foreach (var card in current.GameObjects) {
            if (!old.TryGetValue(card.InstanceId, out var before)) {
                if(card.Type==GameObjectType.Token)
                    current.Annotations.Add(new AnnotationInfo {Id=nextId++,Type={AnnotationType.TokenCreated},AffectedIds={card.InstanceId}});
                continue;
            }
            if(before.ControllerSeatId!=card.ControllerSeatId && before.ControllerSeatId!=0 && card.ControllerSeatId!=0)
                current.Annotations.Add(new AnnotationInfo {Id=nextId++,Type={AnnotationType.ControllerChanged},AffectedIds={card.InstanceId}});
            if(before.ZoneId!=card.ZoneId && zones.TryGetValue(before.ZoneId,out var oldZone) && zones.TryGetValue(card.ZoneId,out var newZone)
                && (oldZone==ZoneType.PhasedOut || newZone==ZoneType.PhasedOut)) {
                if(playback?.Zones.Contains(card.InstanceId)!=true)
                    current.Annotations.Add(new AnnotationInfo {Id=nextId++,Type={newZone==ZoneType.PhasedOut?AnnotationType.PhasedOut:AnnotationType.PhasedIn},AffectedIds={card.InstanceId}});
                continue;
            }
            if (playback?.Zones.Contains(card.InstanceId)!=true && before.ZoneId != card.ZoneId && zones.TryGetValue(before.ZoneId, out var from) && zones.TryGetValue(card.ZoneId, out var to)) {
                string category = from == ZoneType.Hand && to == ZoneType.Stack ? "CastSpell"
                    : from == ZoneType.Hand && to == ZoneType.Battlefield && card.CardTypes.Contains(CardType.Land) ? "PlayLand"
                    : from == ZoneType.Library && to == ZoneType.Hand ? current.GameInfo.Stage == GameStage.Start ? "OpeningHand" : "Draw"
                    : from == ZoneType.Stack && to == ZoneType.Battlefield ? "Resolve"
                    : from == ZoneType.Hand && to == ZoneType.Graveyard ? "Discard"
                    : from == ZoneType.Library && to == ZoneType.Graveyard ? "Mill"
                    : from == ZoneType.Battlefield && to == ZoneType.Hand ? "Bounce"
                    : to == ZoneType.Exile ? "Exile"
                    : "ZoneTransferWithoutIdChange";
                current.Annotations.Add(new AnnotationInfo { Id = nextId++, Type = { AnnotationType.ZoneTransfer },
                    AffectedIds = { card.InstanceId }, Details = {
                        new KeyValuePairInfo { Key = "zone_src", Type = (KeyValuePairValueType)2, ValueInt32 = { (int)before.ZoneId } },
                        new KeyValuePairInfo { Key = "zone_dest", Type = (KeyValuePairValueType)2, ValueInt32 = { (int)card.ZoneId } },
                        new KeyValuePairInfo { Key = "category", Type = (KeyValuePairValueType)6, ValueString = { category } }
                    } });
            }
            if (before.ZoneId!=30 || card.ZoneId!=30 || before.IsTapped == card.IsTapped || playback?.Taps.Contains(card.InstanceId)==true) continue;
            // Exact installed TappedUntappedPermanentAnnotationParser contract.
            current.Annotations.Add(new AnnotationInfo { Id = nextId++, Type = { AnnotationType.TappedUntappedPermanent },
                AffectedIds = { card.InstanceId }, Details = { new KeyValuePairInfo {
                    Key = "tapped", Type = (KeyValuePairValueType)2, ValueInt32 = { card.IsTapped ? 1 : 0 }
                } } });
        }
    }
    public static void Test()
    {
        using var fetch = JsonDocument.Parse("""
        {"legalActions":[
        {"instanceId":1001,"grpId":89183,"abilityId":9,"actionType":2,"isLand":false,"manaCost":"{0}","description":"Search your library for a basic land card, put it onto the battlefield tapped, then shuffle."},
        {"instanceId":1001,"grpId":89183,"abilityId":10,"actionType":2,"isLand":false,"manaCost":"{0}","description":"Target creature with power 2 or less can't be blocked this turn."},
        {"instanceId":1002,"grpId":72598,"abilityId":11,"actionType":2,"isLand":false,"manaCost":"{0}","description":"Search your library for a basic land card, put it onto the battlefield tapped, then shuffle."}]}
        """);
        if(!Actions(fetch.RootElement).Select(a=>a.AbilityGrpId).SequenceEqual(new uint[]{88024,170692,145470}))
            throw new Exception("Fetch-land ability menu IDs missing or merged");
        const string earthbend="Earthbend 2. Activate only as a sorcery. (Target land you control becomes a 0/0 creature with haste that's still a land. Put two +1/+1 counters on it. When it dies or is exiled, return it to the battlefield tapped.)";
        if(AbilityId(97547,earthbend,"Ba Sing Se",true)!=192869 || AbilityId(97547,"Add {G}.","Ba Sing Se",true)!=1005
            || AbilityId(96074,"Landfall — Whenever a land you control enters, double CARDNAME's power until end of turn.","Tifa Lockhart")!=189386)
            throw new Exception("Earthbend/reminder, mana symbols or Tifa ability identity failed");
        if (AbilityId(106487, "NICKNAME deals 4 damage to each creature except for tokens you control.", "Ajani Unrelenting", true) != 208374
            || AbilityId(106487, "Ajani deals 4 damage to each creature except for tokens you control.", "Ajani Unrelenting", true) != 208374
            || AbilityId(106487, "[-3]: Ajani deals 4 damage to each creature except for tokens you control.", "Ajani Unrelenting", true) != 208374)
            throw new Exception("Ajani Unrelenting -3 ability failed to match");
        using var ajaniActions = JsonDocument.Parse("""
        {"legalActions":[
        {"instanceId":1005,"grpId":106487,"abilityId":1,"actionType":2,"isLand":false,"manaCost":"{0}","cardName":"Ajani Unrelenting","description":"Creatures you control get +1/+0 and gain haste until end of turn."},
        {"instanceId":1005,"grpId":106487,"abilityId":2,"actionType":2,"isLand":false,"manaCost":"{0}","cardName":"Ajani Unrelenting","description":"Discard your hand, then draw a card for each creature you control."},
        {"instanceId":1005,"grpId":106487,"abilityId":3,"actionType":2,"isLand":false,"manaCost":"{0}","cardName":"Ajani Unrelenting","description":"NICKNAME deals 4 damage to each creature except for tokens you control."}]}
        """);
        if (!Actions(ajaniActions.RootElement).Select(a => a.AbilityGrpId).SequenceEqual(new uint[] { 143817, 208373, 208374 }))
            throw new Exception("Ajani Unrelenting loyalty abilities failed to match Arena ability IDs");
        using var input = JsonDocument.Parse("""
        {"legalActions":[{"instanceId":1001,"grpId":7,"abilityId":9,"actionType":1,"isLand":false,"manaCost":"{G}","autoTapSources":[1002]}]}
        """);
        var action = Actions(input.RootElement).Single();
        if (action.AutoTapSolution.AutoTapActions.Single().InstanceId != 1002 || action.ManaCost.Single().Color.Single() != ManaColor.Green)
            throw new Exception("Playable highlight/payment preview failed");
        var old = new GameStateMessage { GameObjects = { new GameObjectInfo { InstanceId = 1002, ZoneId = 30 } } };
        var state = new GameStateMessage { GameObjects = { new GameObjectInfo { InstanceId = 1002, ZoneId = 30, IsTapped = true } } };
        uint id = 1;
        Transitions(old, state, ref id);
        if (state.Annotations.Single().Details.Single().ValueInt32.Single() != 1) throw new Exception("Tap animation annotation missing");
        old = state.Clone();
        state.Annotations.Clear();
        Transitions(old, state, ref id);
        if (state.Annotations.Count != 0) throw new Exception("Unchanged tap state replayed an animation");
        if (AbilityId(93152, "Counter target spell unless its controller pays {3}.", "Supreme Will") != 21928
            || AbilityId(93152, "Look at the top four cards of your library. Put one of them into your hand and the rest on the bottom of your library in any order.", "Supreme Will") != 14026)
            throw new Exception("Supreme Will modal abilities failed to match Arena ability IDs");
        if (AbilityId(102753, "Creatures you control gain flying, vigilance, and double strike until end of turn.", "Akroma's Will") != 143975
            || AbilityId(102753, "Creatures you control gain lifelink, indestructible, and protection from each color until end of turn.", "Akroma's Will") != 189523)
            throw new Exception("Akroma's Will modal abilities failed to match Arena ability IDs");
        if (AbilityId(70248, "Each player discards a card.", "Rankle, Master of Pranks") != 136071
            || AbilityId(70248, "Each player loses 1 life and draws a card.", "Rankle, Master of Pranks") != 136072
            || AbilityId(70248, "Each player sacrifices a creature.", "Rankle, Master of Pranks") != 136280)
            throw new Exception("Rankle, Master of Pranks modal abilities failed to match Arena ability IDs");
        using var discountDoc = JsonDocument.Parse("""
        {"cards":[{"instanceId":1001,"grpId":93152,"manaCost":"{2}{U}"}],
         "legalActions":[{"instanceId":1001,"grpId":93152,"actionType":1,"isLand":false,"manaCost":"{1}{U}"}]}
        """);
        var discountState = new GameStateMessage();
        StateCostModifications(discountDoc.RootElement, discountState);
        if (AbilityId(84090, "You draw a card and you lose 1 life. Proliferate.", "Vraska, Betrayal's Sting", true, "0") != 165855)
            throw new Exception("Vraska 0 loyalty ability failed to match");
        if (AbilityId(94590, "Look at the top four cards of your library. You may reveal an Aura, creature, or planeswalker card from among them and put that card into your hand. Put the rest on the bottom of your library in any order.", "Ajani, Mentor of Heroes", true, "+1") != 20924)
            throw new Exception("Ajani, Mentor of Heroes +1 loyalty ability failed to match");
        if (AbilityId(94569, "Target player mills 10 cards.", "Jace, Memory Adept", true, "0") != 1164)
            throw new Exception("Jace Memory Adept 10 vs ten loyalty ability failed to match");
        if (AbilityId(94270, "CARDNAME gets +1/-1 until end of turn.", "Aetherling", true) != 86473
            || AbilityId(94270, "CARDNAME gets -1/+1 until end of turn.", "Aetherling", true) != 86474)
            throw new Exception("Aetherling pump abilities failed or collided");
        if (AbilityId(97106, "Until end of turn, this land becomes a 4/4 white and blue Elemental creature with flying and vigilance. It's still a land.", "Celestial Colonnade", true) != 95006)
            throw new Exception("Celestial Colonnade animation ability failed to match");
        if (AbilityId(90901, "You get {E}{E} (two energy counters).", "Bespoke Battlewagon", true) != 172219)
            throw new Exception("Bespoke Battlewagon energy ability failed to match");
        if (AbilityId(76512, "Add X mana of any one color, where X is the number of life you gained this turn.", "Accomplished Alchemist", true) != 142006)
            throw new Exception("Accomplished Alchemist ability failed to match");
        if (AbilityId(79368, "Conjure a Kraken Hatchling card into your hand.", "Kiora, the Tide's Fury", true, "+1") != 147030)
            throw new Exception("Kiora, the Tide's Fury conjure ability failed to match");
        if (AbilityId(79380, "Conjure a Shivan Dragon card into your hand.", "Sarkhan, Wanderer to Shiv", true, "+1") != 147051)
            throw new Exception("Sarkhan, Wanderer to Shiv conjure ability failed to match");
        if (discountState.PersistentAnnotations.Count != 1
            || !discountState.PersistentAnnotations[0].Type.Contains(AnnotationType.UseOrCostsManaCost)
            || discountState.PersistentAnnotations[0].Details[0].ValueString[0] != "1U")
            throw new Exception("Cost modification annotation failed for discounted spell");
        using var esikaDoc = JsonDocument.Parse("""
        {"legalActions":[
        {"instanceId":1001,"grpId":75212,"abilityId":10,"actionType":1,"isLand":false,"manaCost":"{1}{G}{G}","cardName":"Esika, God of the Tree"},
        {"instanceId":1001,"grpId":75213,"abilityId":11,"actionType":18,"isLand":false,"manaCost":"{W}{U}{B}{R}{G}","cardName":"The Prismatic Bridge"}]}
        """);
        var esikaActions = Actions(esikaDoc.RootElement);
        if (esikaActions.Count != 2
            || esikaActions[0].ActionType != ActionType.Cast || esikaActions[0].GrpId != 75212 || esikaActions[0].InstanceId != 1001
            || esikaActions[1].ActionType != ActionType.CastMdfc || esikaActions[1].GrpId != 75213 || esikaActions[1].InstanceId != 1001
            || esikaActions[1].SourceId != 1001)
            throw new Exception("MDFC multi-facet action generation failed for Esika // The Prismatic Bridge");
    }
}
