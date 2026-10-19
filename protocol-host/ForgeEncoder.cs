using System.Text.Json;
using Google.Protobuf;
using Wotc.Mtgo.Gre.External.Messaging;

static class ForgeEncoder
{
    static readonly Lazy<Dictionary<string,SubType>> LocalPlaneswalkerSubtypes=new(()=>
        JsonSerializer.Deserialize<Dictionary<string,uint>>(File.ReadAllText("data/custom-arena-cards/planeswalker-subtypes.json"))!
        .ToDictionary(p=>System.Text.RegularExpressions.Regex.Replace(p.Key,"[^a-zA-Z0-9]",""),p=>(SubType)p.Value,StringComparer.OrdinalIgnoreCase));
    static readonly Lazy<Dictionary<string, JsonElement>> CardMetadata = new(() =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(
            Path.Combine(Environment.CurrentDirectory, "data", "arena-card-metadata.json")))
        ?? throw new InvalidDataException("Missing Arena card metadata"));
    static readonly Lazy<Dictionary<string, Dictionary<string, uint>>> LinkedFaces = new(() =>
        JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, uint>>>(File.ReadAllText(
            Path.Combine(Environment.CurrentDirectory, "data", "arena-linked-faces.json")))
        ?? new());
    static int Zone(string value) => value.ToUpperInvariant() switch { "LIBRARY" => 1, "HAND" => 2, "BATTLEFIELD" => 3, "STACK" => 4, "GRAVEYARD" => 5, "EXILE" => 6, "COMMAND" => 7, "LIMBO" => 9, "SIDEBOARD" => 10, "PHASEDOUT" => 12, _ => throw new InvalidDataException("Unsupported Forge zone " + value) };
    static (int phase, int step) Phase(string value) => value.ToUpperInvariant() switch {
        "NONE" => (2, 0),
        "UNTAP" => (1, 1), "UPKEEP" => (1, 2), "DRAW" => (1, 3), "MAIN1" => (2, 0), "COMBAT_BEGIN" => (3, 4),
        "COMBAT_DECLARE_ATTACKERS" => (3, 5), "COMBAT_DECLARE_BLOCKERS" => (3, 6), "COMBAT_FIRST_STRIKE_DAMAGE" => (3, 11),
        "COMBAT_DAMAGE" => (3, 7), "COMBAT_END" => (3, 8), "MAIN2" => (4, 0), "END_OF_TURN" => (5, 9), "CLEANUP" => (5, 10),
        _ => throw new InvalidDataException("Unsupported Forge phase " + value) };
    public static MatchServiceToClientMessage Encode(string json, uint? overrideMsgId = null, uint? overrideGameStateId = null, string? overrideMatchId = null)
    {
        using var input = JsonDocument.Parse(json);
        var root = input.RootElement;
        uint viewer = root.GetProperty("viewerSeat").GetUInt32();
        uint stateId = overrideGameStateId ?? root.GetProperty("gameStateId").GetUInt32();
        uint msgId = overrideMsgId ?? root.GetProperty("msgId").GetUInt32();
        if (viewer < 1 || viewer > 4 || stateId == 0 || msgId == 0) throw new InvalidDataException("Invalid viewer seat or sequence IDs");
        string matchId = overrideMatchId ?? root.GetProperty("matchId").GetString() ?? throw new InvalidDataException("Missing match ID");
        if (string.IsNullOrWhiteSpace(matchId)) throw new InvalidDataException("Empty match ID");
        var players = root.GetProperty("players").EnumerateArray().Select(p => new {
            seat = p.GetProperty("seat").GetUInt32(), life = p.GetProperty("life").GetInt32(),
            manaPool = p.TryGetProperty("manaPool", out var pool) ? pool.EnumerateArray().ToArray() : Array.Empty<JsonElement>()
        }).ToArray();
        var seats=players.Select(p=>p.seat).ToHashSet();
        if (players.Length is not (2 or 4) || !seats.Order().SequenceEqual(Enumerable.Range(1,players.Length).Select(i=>(uint)i)) || !seats.Contains(viewer))
            throw new InvalidDataException("Matches require two or four distinct consecutive seats");
        var zones = new Dictionary<uint, Dictionary<string, object>>();
        // Pending is the client's casting area while costs/X are announced.
        foreach (int type in new[] { 3, 4, 6, 7, 9, 12, (int)ZoneType.Pending }) {
            // Pending's enum value would otherwise collide with seat one's
            // sideboard ID (110) in this adapter's per-player numbering.
            uint zoneId = type==(int)ZoneType.Pending ? 80u : (uint)(type * 10);
            zones[zoneId] = new() { ["zoneId"] = zoneId, ["type"] = type, ["visibility"] = 1, ["ownerSeatId"] = 0u, ["objectInstanceIds"] = new List<uint>() };
        }
        // Per-player zones: Library (1), Hand (2), Graveyard (5), Revealed (8), Sideboard (10)
        foreach (var owner in seats.Order()) {
            foreach (int type in new[] { 1, 2, 5, 8, 10 }) {
                uint zoneId = owner * 100 + (uint)type;
                zones[zoneId] = new() { ["zoneId"] = zoneId, ["type"] = type, ["visibility"] = type == 1 ? 3 : type is 2 or 10 ? 2 : 1, ["ownerSeatId"] = owner, ["objectInstanceIds"] = new List<uint>() };
            }
        }
        var objects = new List<Dictionary<string, object>>();
        var ids = new HashSet<uint>();
        var revealedChoices = new HashSet<uint>();
        if (root.TryGetProperty("input", out var currentInput) && currentInput.TryGetProperty("choices", out var choices))
            foreach (var choice in choices.EnumerateArray()) revealedChoices.Add(choice.GetUInt32());
        foreach (var c in root.GetProperty("cards").EnumerateArray()) {
            uint id = c.GetProperty("instanceId").GetUInt32(), owner = c.GetProperty("ownerSeat").GetUInt32(), controller = c.GetProperty("controllerSeat").GetUInt32();
            int type = Zone(c.GetProperty("zone").GetString()!);
            // ChooseX draws only the selector. Pending uses the stack card
            // holder; Limbo instead hides the casting card in library storage.
            if(type==2 && root.TryGetProperty("input",out var castingInput)
                && castingInput.GetProperty("type").GetString()=="InputChooseNumber"
                && castingInput.TryGetProperty("isX",out var choosingX) && choosingX.GetBoolean()
                && castingInput.GetProperty("sourceId").GetUInt32()==id)
                type=(int)ZoneType.Pending;
            if (id == 0 || zones.ContainsKey(id) || !ids.Add(id) || !seats.Contains(owner) || !seats.Contains(controller)) throw new InvalidDataException("Invalid/duplicate card IDs, zone ID collision or seats");
            bool isShared = type is 3 or 4 or 6 or 7 or 9 or 12 || type==(int)ZoneType.Pending;
            uint zoneId = type==(int)ZoneType.Pending ? 80u : isShared ? (uint)(type * 10) : (owner * 100 + (uint)type);
            ((List<uint>)zones[zoneId]["objectInstanceIds"]).Add(id);
            var viewers=c.TryGetProperty("viewers",out var permitted)
                ? permitted.EnumerateArray().Select(v=>v.GetUInt32()).ToArray() : Array.Empty<uint>();
            bool visible = revealedChoices.Contains(id) || (c.TryGetProperty("viewers",out _) ? viewers.Contains(viewer)
                : (type != 1 && (!(type is 2 or 10) || owner == viewer)));
            bool publicFaceDown=c.TryGetProperty("faceDown",out var faceDown) && faceDown.GetBoolean() && type is 3 or 4 or 12;
            bool knowsFace=visible;
            if(publicFaceDown)visible=true;
            var otherViewers=viewers.Where(v=>v!=viewer).ToArray();
            var obj = new Dictionary<string, object> { ["instanceId"] = id, ["type"] = 1, ["zoneId"] = zoneId, ["ownerSeatId"] = owner, ["controllerSeatId"] = controller,
                ["visibility"] = visible ? (type is 1 or 2 or 10 && otherViewers.Length == 0 ? 2 : 1) : 3 };
            if(c.TryGetProperty("isToken",out var token) && token.GetBoolean())obj["type"]=2;
            bool emblem=c.TryGetProperty("isEmblem",out var isEmblem) && isEmblem.GetBoolean();
            if(emblem)obj["type"]=4;
            // Public here means face-up in this viewer's sanitized state. Viewers
            // preserves which player actually has the look permission.
            if(visible && owner==viewer && type is 1 or 2 or 10 && otherViewers.Length>0)obj["viewers"]=otherViewers;
            if (visible) {
                uint grp = c.GetProperty("grpId").GetUInt32();
                if (grp == 0) throw new InvalidDataException("Visible card lacks resolved Arena grpId");
                obj["grpId"] = grp;
                bool isCopy=c.TryGetProperty("isCopy",out var copied) && copied.GetBoolean();
                if(isCopy)obj["isCopy"]=true;
                if(c.TryGetProperty("originalGrpId",out var original) && original.GetUInt32()!=grp) {
                    obj["objectSourceGrpId"]=original.GetUInt32();
                    if(!isCopy)obj["othersideGrpId"]=original.GetUInt32();
                } else if(LinkedFaces.Value.TryGetValue(grp.ToString(),out var linked)) {
                    var otherFace=linked.Values.FirstOrDefault(f=>f!=grp);
                    if(otherFace!=0)obj["othersideGrpId"]=otherFace;
                }
                // GreInterface does not populate these from the printing: omitted
                // uniqueAbilities clears rules text even when card art is correct.
                if (CardMetadata.Value.TryGetValue(grp.ToString(), out var metadata))
                    foreach (var property in metadata.EnumerateObject())
                        obj[property.Name] = property.Value;
                // A printed land can become a creature (earthbend). Printed
                // metadata alone leaves Arena rendering it as an ordinary land.
                if(c.TryGetProperty("cardTypes",out var currentTypes))obj["cardTypes"]=currentTypes.EnumerateArray()
                    .Select(t=>Enum.TryParse<CardType>(t.GetString(),true,out var parsed)?parsed
                        : throw new InvalidDataException("Unsupported Forge card type "+t.GetString())).ToArray();
                if(c.TryGetProperty("colors",out var currentColors))obj["color"]=currentColors.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                if(c.TryGetProperty("subtypes",out var currentSubtypes))obj["subtypes"]=currentSubtypes.EnumerateArray()
                    .Select(t=>System.Text.RegularExpressions.Regex.Replace(t.GetString()!,"[^a-zA-Z0-9]",""))
                    .Select(t=>Enum.TryParse<SubType>(t,true,out var parsed) && Enum.IsDefined(parsed)?parsed:LocalPlaneswalkerSubtypes.Value.GetValueOrDefault(t,SubType.None))
                    .Where(t=>t!=SubType.None).Distinct().ToArray();
                if(c.TryGetProperty("supertypes",out var currentSupertypes))obj["superTypes"]=currentSupertypes.EnumerateArray()
                    .Select(t=>Enum.TryParse<SuperType>(t.GetString(),true,out var parsed) && Enum.IsDefined(parsed)?parsed:SuperType.None)
                    .Where(t=>t!=SuperType.None).Distinct().ToArray();
                if(emblem) {
                    obj["objectSourceGrpId"]=grp;
                    obj["cardTypes"]=Array.Empty<CardType>();obj["subtypes"]=Array.Empty<SubType>();obj["superTypes"]=Array.Empty<SuperType>();
                    obj["uniqueAbilities"]=BattlePresentation.EmblemAbilities(c);
                }
                // Forge excludes basic spell actions when testing permanent
                // abilities. That flag must never erase a spell's rules in hand.
                if(c.GetProperty("zone").GetString() is "Battlefield" or "PhasedOut"
                    && (c.TryGetProperty("hasNoAbilities",out var noAbilities) && noAbilities.GetBoolean()
                    || c.TryGetProperty("suppressPrintedAbilities",out var suppressed) && suppressed.GetBoolean()))obj["uniqueAbilities"]=Array.Empty<object>();
                if(c.TryGetProperty("suppressedKeywords",out var removedKeywords) && obj.TryGetValue("uniqueAbilities",out var originalAbilities)) {
                    var removed=removedKeywords.EnumerateArray().Select(k=>BattlePresentation.KeywordAbilityId(k.GetString()!)).Where(k=>k!=0).ToHashSet();
                    obj["uniqueAbilities"]=JsonSerializer.SerializeToElement(originalAbilities).EnumerateArray()
                        .Where(a=>!removed.Contains(a.GetProperty("grpId").GetUInt32())).ToArray();
                }
                if(c.TryGetProperty("grantedAbilities",out var granted)) {
                    var abilities = obj.TryGetValue("uniqueAbilities",out var printed)
                        ? JsonSerializer.SerializeToElement(printed).EnumerateArray().ToList() : new List<JsonElement>();
                    foreach(var identity in granted.EnumerateArray()) {
                        uint ability = BattlePresentation.GrantedAbilityId(identity);
                        if(ability==0)continue;
                        abilities.Add(JsonSerializer.SerializeToElement(new {grpId=ability,id=identity.GetProperty("abilityId").GetUInt32()}));
                    }
                    obj["uniqueAbilities"]=abilities;
                }
                if(c.TryGetProperty("grantedKeywords",out var newKeywords)) {
                    var abilities=obj.TryGetValue("uniqueAbilities",out var existing)?JsonSerializer.SerializeToElement(existing).EnumerateArray().ToList():new List<JsonElement>();
                    var present=abilities.Select(a=>a.GetProperty("grpId").GetUInt32()).ToHashSet();
                    foreach(var keyword in newKeywords.EnumerateArray()) {
                        uint ability=BattlePresentation.KeywordAbilityId(keyword.GetString()!);
                        if(ability!=0 && present.Add(ability))abilities.Add(JsonSerializer.SerializeToElement(new {grpId=ability}));
                    }
                    obj["uniqueAbilities"]=abilities;
                }
                obj["isTapped"] = c.TryGetProperty("tapped", out var tapped) && tapped.GetBoolean();
                obj["hasSummoningSickness"] = c.TryGetProperty("summoningSick", out var sick) && sick.GetBoolean();
                if (c.TryGetProperty("damage", out var damage)) obj["damage"] = damage.GetUInt32();
                if (c.TryGetProperty("attackTarget", out var attack)) {
                    obj["attackState"] = c.TryGetProperty("attackState",out var attackState)?attackState.GetInt32():2;
                    obj["attackInfo"] = new { targetId = attack.GetUInt32(), orderedBlockers =
                        c.TryGetProperty("blockedBy",out var blockedBy)?blockedBy.EnumerateArray().Select(b=>new {instanceId=b.GetUInt32()}).ToArray():Array.Empty<object>() };
                }
                if(c.TryGetProperty("blockState",out var blockState))obj["blockState"]=blockState.GetInt32();
                if (c.TryGetProperty("blocking", out var blocking)) {
                    if(!c.TryGetProperty("blockState",out _))obj["blockState"] = 2;
                    obj["blockInfo"] = new { attackerIds = blocking.EnumerateArray().Select(id => id.GetUInt32()).ToArray(),
                        orderedAttackers = blocking.EnumerateArray().Select(id=>new {instanceId=id.GetUInt32()}).ToArray() };
                }
                if (c.TryGetProperty("power", out var power) && power.ValueKind == JsonValueKind.Number) obj["power"] = new { value = power.GetInt32() };
                if (c.TryGetProperty("toughness", out var toughness) && toughness.ValueKind == JsonValueKind.Number) obj["toughness"] = new { value = toughness.GetInt32() };
                if (c.TryGetProperty("loyalty", out var loyalty) && loyalty.ValueKind == JsonValueKind.Number) obj["loyalty"] = new { value = loyalty.GetInt32() };
                if(publicFaceDown) {
                    obj["grpId"]=3u;obj["name"]=0u;
                    obj.Remove("othersideGrpId");obj.Remove("objectSourceGrpId");
                    if(knowsFace)obj["objectSourceGrpId"]=grp;
                    obj["uniqueAbilities"]=c.TryGetProperty("faceDownReason",out var downReason) && downReason.GetString() is "Cloak" or "Disguise"
                        ? new[]{new {grpId=141939u}} : Array.Empty<object>();
                }
            }
            objects.Add(obj);
            // Adventure text is read from a separate linked spell panel.
            if(visible && !publicFaceDown && LinkedFaces.Value.TryGetValue(c.GetProperty("grpId").GetUInt32().ToString(),out var adventureFaces)) {
                foreach(uint face in adventureFaces.Values.Distinct()) {
                    if(face==c.GetProperty("grpId").GetUInt32() || !CardMetadata.Value.TryGetValue(face.ToString(),out var adventure)
                        || !adventure.GetProperty("subtypes").EnumerateArray().Any(s=>s.GetUInt32()==(uint)SubType.Adventure))continue;
                    var panel=new Dictionary<string,object> { ["instanceId"]=checked(10_000_000u+id*2),
                        ["type"]=(int)GameObjectType.Adventure,["grpId"]=face,["parentId"]=id,["zoneId"]=zoneId,
                        ["ownerSeatId"]=owner,["controllerSeatId"]=controller,["visibility"]=1 };
                    foreach(var property in adventure.EnumerateObject())panel[property.Name]=property.Value;
                    objects.Add(panel);
                }
            }
        }
        // A spell copy chooses targets before Forge adds it to a game zone.
        // Give Arena a pending casting preview for that authoritative copy ID.
        if(root.TryGetProperty("input",out var copyInput)
            && copyInput.TryGetProperty("sourceIsSpellCopy",out var isPendingCopy) && isPendingCopy.GetBoolean()
            && copyInput.TryGetProperty("abilityIdentity",out var copyIdentity)) {
            uint copyId=copyInput.GetProperty("sourceId").GetUInt32(),printing=copyIdentity.GetProperty("sourceGrpId").GetUInt32();
            uint controller=copyInput.TryGetProperty("targetingPlayer",out var copyingPlayer)?copyingPlayer.GetUInt32():viewer;
            if(!ids.Contains(copyId) && !zones.ContainsKey(copyId) && seats.Contains(controller)
                && CardMetadata.Value.TryGetValue(printing.ToString(),out var metadata)) {
                var preview=new Dictionary<string,object> { ["instanceId"]=copyId,["grpId"]=printing,
                    ["type"]=(int)GameObjectType.Card,["zoneId"]=80u,["ownerSeatId"]=controller,
                    ["controllerSeatId"]=controller,["visibility"]=(int)Visibility.Public,["isCopy"]=true };
                foreach(var property in metadata.EnumerateObject())preview[property.Name]=property.Value;
                ids.Add(copyId);objects.Add(preview);((List<uint>)zones[80]["objectInstanceIds"]).Add(copyId);
            }
        }
        if(viewer==1 && root.TryGetProperty("revealedCards",out var revealedCards))foreach(var reveal in revealedCards.EnumerateArray()) {
            uint source=reveal.GetProperty("instanceId").GetUInt32(),id=checked(100_000_000u+source);
            var original=root.GetProperty("cards").EnumerateArray().FirstOrDefault(c=>c.GetProperty("instanceId").GetUInt32()==source);
            if(original.ValueKind==JsonValueKind.Undefined && !reveal.TryGetProperty("grpId",out _))continue;
            uint owner=reveal.GetProperty("ownerSeat").GetUInt32(),zoneId=owner*100+8;
            uint revGrp=reveal.TryGetProperty("grpId",out var revealPrinting)?revealPrinting.GetUInt32():original.GetProperty("grpId").GetUInt32();
            if(revGrp==0)continue;
            var obj=new Dictionary<string,object> { ["instanceId"]=id,["grpId"]=revGrp,
                ["type"]=(int)GameObjectType.RevealedCard,["zoneId"]=zoneId,["ownerSeatId"]=owner,["controllerSeatId"]=owner,["visibility"]=1 };
            if(CardMetadata.Value.TryGetValue(revGrp.ToString(),out var metadata))
                foreach(var property in metadata.EnumerateObject())obj[property.Name]=property.Value;
            if(LinkedFaces.Value.TryGetValue(revGrp.ToString(),out var linked)) {
                var otherFace=linked.Values.FirstOrDefault(f=>f!=revGrp);
                if(otherFace!=0)obj["othersideGrpId"]=otherFace;
            }
            objects.Add(obj);((List<uint>)zones[zoneId]["objectInstanceIds"]).Add(id);
        }
        var stackEntries=new Dictionary<uint,JsonElement>();
        var liveStack=new HashSet<uint>();
        foreach(string key in new[]{"playbackObjects","stack"})
            if(root.TryGetProperty(key,out var entries))foreach(var entry in entries.EnumerateArray()) {
                uint id=entry.GetProperty("instanceId").GetUInt32(); stackEntries[id]=entry;
                if(key=="stack")liveStack.Add(id);
            }
        foreach(var entry in stackEntries.Values.Where(e=>e.GetProperty("isAbility").GetBoolean())) {
            uint id=entry.GetProperty("instanceId").GetUInt32(), parent=entry.GetProperty("parentId").GetUInt32();
            uint source=entry.GetProperty("sourceGrpId").GetUInt32(), controller=entry.GetProperty("controllerSeat").GetUInt32();
            uint ability=BattlePresentation.AbilityId(source,entry.GetProperty("description").GetString()!,entry.GetProperty("cardName").GetString()!);
            bool annihilatorKeyword=ability!=0 && (entry.GetProperty("description").GetString()??"").TrimStart().StartsWith("Annihilator ",StringComparison.OrdinalIgnoreCase);
            if(!annihilatorKeyword && entry.TryGetProperty("abilityIdentity",out var grantedIdentity) && (ability==0
                || grantedIdentity.TryGetProperty("granted",out var isGranted) && isGranted.GetBoolean())) {
                ability=BattlePresentation.GrantedAbilityId(grantedIdentity);
                source=grantedIdentity.GetProperty("sourceGrpId").GetUInt32();
            }
            if(ability==0 || !ids.Contains(parent) || !seats.Contains(controller) || !ids.Add(id))continue;
            uint zoneId=liveStack.Contains(id)?40u:90u;
            ((List<uint>)zones[zoneId]["objectInstanceIds"]).Add(id);
            var obj=new Dictionary<string,object> { ["instanceId"]=id,["type"]=3,["grpId"]=ability,["parentId"]=parent,["objectSourceGrpId"]=source,
                ["zoneId"]=zoneId,["visibility"]=1,["ownerSeatId"]=controller,["controllerSeatId"]=controller,
                ["abilityOriginalCardGrpIds"]=new[]{source},["uniqueAbilities"]=new[]{new{grpId=ability}} };
            if(CardMetadata.Value.TryGetValue(source.ToString(),out var metadata)) { obj["name"]=metadata.GetProperty("name"); obj["color"]=metadata.GetProperty("color"); }
            objects.Add(obj);
        }
        foreach(var entry in stackEntries.Values) {
            if(!entry.TryGetProperty("selectedModes",out var selectedModes))continue;
            uint source=entry.GetProperty("sourceGrpId").GetUInt32();
            var selected=selectedModes.EnumerateArray().Select(mode=>BattlePresentation.AbilityId(source,mode.GetString()!,entry.GetProperty("cardName").GetString()!)).ToArray();
            if(selected.Length==0 || selected.Any(id=>id==0))continue;
            var stackObject=objects.FirstOrDefault(obj=>(uint)obj["instanceId"]==entry.GetProperty("instanceId").GetUInt32());
            if(stackObject==null)continue;
            // Use the native child ability definitions, which carry the chosen
            // text, rather than the parent list containing every possible mode.
            stackObject["uniqueAbilities"]=selected.Select(id=>new {grpId=id}).ToArray();
        }
        // Forge lists these piles oldest first; Arena displays the first
        // instance at the front of the graveyard/exile browser.
        foreach(var zone in zones.Values)
            if((int)zone["type"] is 5 or 6)
                ((List<uint>)zone["objectInstanceIds"]).Reverse();
        // Native stack cardholders read zone order. Card iteration and journal
        // insertion order are not the order of the actual Forge stack.
        if(root.TryGetProperty("stack",out var orderedStack)) {
            var stackIds=(List<uint>)zones[40]["objectInstanceIds"];
            var ordered=orderedStack.EnumerateArray().Select(e=>e.GetProperty("instanceId").GetUInt32()).Where(stackIds.Contains).ToArray();
            var remaining=stackIds.Except(ordered).ToArray();
            stackIds.Clear();stackIds.AddRange(ordered);stackIds.AddRange(remaining);
        }
        var turn = root.GetProperty("turn"); var (phase, step) = Phase(turn.GetProperty("phase").GetString()!);
        uint active = turn.GetProperty("activeSeat").GetUInt32(), priority = turn.GetProperty("prioritySeat").GetUInt32();
        if (active == 0) active = 1;
        if (priority == 0) priority = active;
        if (!seats.Contains(active) || !seats.Contains(priority)) throw new InvalidDataException("Invalid active/priority seat");
        int gameStage = root.TryGetProperty("stage", out var stage) ? stage.GetInt32() : 2;
        var results = BattleCompletion.Results(root).Select(r => new {
            scope = (int)r.Scope, result = (int)r.Result, winningTeamId = r.WinningTeamId, reason = (int)r.Reason
        }).ToArray();
        // Forge can mark the game over just before publishing its outcome.
        // Do not lock Arena's scene until we can supply its intermission result.
        if(gameStage==3 && results.Length==0)gameStage=2;
        var state = new {
            type = 1, gameStateId = stateId,
            gameInfo = new { matchID = matchId, gameNumber = 1, stage = gameStage, type = players.Length>2?2:1,
                variant = root.TryGetProperty("format",out var format) && format.GetString() is "Commander" or "Commander4" or "CommanderUnbanned"?4:1,
                matchState = gameStage == 3 ? 3 : 1, matchWinCondition = 1, mulliganType = 3, results },
            teams = players.Select(p => new { id = p.seat, playerIds = new[] { p.seat } }),
            players = players.Select(p => new { lifeTotal = p.life, systemSeatNumber = p.seat, manaPool = p.manaPool,
                status = root.GetProperty("players").EnumerateArray().Single(s=>s.GetProperty("seat").GetUInt32()==p.seat).TryGetProperty("lost",out var lost) && lost.GetBoolean()?2:1,
                maxHandSize = root.GetProperty("players").EnumerateArray().Single(s=>s.GetProperty("seat").GetUInt32()==p.seat)
                    .TryGetProperty("maxHandSize",out var handLimit)?handLimit.GetInt32():7,
                controllerSeatId = p.seat, controllerType = 1, teamId = p.seat,
                startingLifeTotal = root.TryGetProperty("startingLife",out var starting)?starting.GetInt32():20,
                mulliganCount = p.seat == viewer && root.TryGetProperty("mulliganCount", out var count) ? count.GetUInt32() : 0 }),
            turnInfo = new { phase, step, turnNumber = turn.GetProperty("number").GetUInt32(), activePlayer = active, priorityPlayer = priority, decisionPlayer = priority },
            // Arena CreateSubCardHolder indexes PlayerTypeMap, which is initialized
            // by CreateCardHolder for an owned zone. Send those zones first.
            zones = zones.Values.OrderBy(z => (uint)z["ownerSeatId"] == 0 ? 1 : 0), gameObjects = objects, update = 1
        };
        var messageJson = JsonSerializer.Serialize(new { type = 1, systemSeatIds = new[] { viewer }, msgId, gameStateId = stateId, gameStateMessage = state });
        var gre = JsonParser.Default.Parse< GREToClientMessage>(messageJson);
        BattlePresentation.StateCounters(root,gre.GameStateMessage);
        BattleTurnHistory.Apply(root,gre.GameStateMessage);
        BattlePresentation.StatePlayerProtection(root,gre.GameStateMessage);
        BattlePresentation.StateEntryPayment(root,gre.GameStateMessage);
        BattlePresentation.StateStatModifications(root,gre.GameStateMessage);
        BattlePresentation.StateBlockingRestrictions(root,gre.GameStateMessage);
        foreach(var card in gre.GameStateMessage.GameObjects) {
            if(card.GrpId==0 || card.GrpId==3 || !CardMetadata.Value.TryGetValue(card.GrpId.ToString(),out var printing)
                || !printing.TryGetProperty("color",out var printedColors))continue;
            if(card.Color.Select(c=>(int)c).SequenceEqual(printedColors.EnumerateArray().Select(c=>c.GetInt32())))continue;
            var colorDetail=new KeyValuePairInfo {Key="color",Type=KeyValuePairValueType.Int32};
            colorDetail.ValueInt32.AddRange(card.Color.Select(c=>(int)c));
            gre.GameStateMessage.PersistentAnnotations.Add(new AnnotationInfo {Id=checked(3_200_000_000u+card.InstanceId),
                Type={AnnotationType.ModifiedColor},AffectedIds={card.InstanceId},Details={colorDetail,
                    new KeyValuePairInfo {Key="modificationType",Type=KeyValuePairValueType.String,ValueString={"Set"}}}});
        }
        BattlePresentation.StateAttachments(root,gre.GameStateMessage);
        BattlePresentation.StateCastingChoices(root,gre.GameStateMessage);
        BattleTriggerLinks.Apply(root,gre.GameStateMessage);
        BattlePresentation.StateCommanders(root,gre.GameStateMessage);
        BattlePresentation.StateBattleProtectors(root,gre.GameStateMessage);
        BattlePresentation.StateChosenTypes(root,gre.GameStateMessage);
        BattlePresentation.StateExileLinks(root,gre.GameStateMessage);
        BattlePresentation.StateOpponentKnowledge(root,gre.GameStateMessage,viewer);
        BattlePresentation.StateLoyaltyActivations(root,gre.GameStateMessage);
        BattlePresentation.StateCostModifications(root,gre.GameStateMessage);
        BattlePresentation.StateActivationCosts(root,gre.GameStateMessage);
        foreach (var action in BattlePresentation.Actions(root))
            gre.GameStateMessage.Actions.Add(new ActionInfo { ActionId = action.Selection, SeatId = viewer, Action = action });
        BattlePresentation.StateAdventures(root,gre.GameStateMessage);
        var result = new MatchServiceToClientMessage { GreToClientEvent = new GreToClientEvent() };
        result.GreToClientEvent.GreToClientMessages.Add(gre);
        return result;
    }
    public static void Export(string input, string prefix)
    {
        var message = Encode(File.ReadAllText(input));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(prefix))!);
        File.WriteAllBytes(prefix + ".pb", message.ToByteArray());
        File.WriteAllBytes(prefix + ".frame", Frames.Encode(new Frame(4, 1, 2, message.ToByteArray())));
        File.WriteAllText(prefix + ".json", JsonFormatter.Default.Format(message));
        Console.WriteLine($"Encoded Forge snapshot as GRE full state: {prefix}.pb/.frame/.json. Translation prototype; stock-client integration unverified.");
    }
    public static void Test()
    {
        BattlePresentation.Test();
        BattleFlow.Test();
        const string snapshot = """
        {"matchId":"forge-test","gameStateId":1,"msgId":1,"viewerSeat":1,"turn":{"number":1,"phase":"MAIN1","activeSeat":1,"prioritySeat":1},"players":[{"seat":1,"life":20},{"seat":2,"life":18}],"cards":[{"instanceId":1001,"grpId":7,"ownerSeat":1,"controllerSeat":1,"zone":"Hand"},{"instanceId":1002,"grpId":8,"ownerSeat":2,"controllerSeat":2,"zone":"Hand","power":99},{"instanceId":1003,"grpId":9,"ownerSeat":1,"controllerSeat":1,"zone":"Library"},{"instanceId":1004,"grpId":10,"ownerSeat":2,"controllerSeat":2,"zone":"Battlefield","power":3,"toughness":4,"tapped":true}]}
        """;
        var bytes = Encode(snapshot).ToByteArray();
        var manaState = Encode(snapshot.Replace("\"seat\":1,\"life\":20", "\"seat\":1,\"life\":20,\"manaPool\":[{\"manaId\":16069,\"color\":5,\"srcInstanceId\":1004,\"count\":1}]"))
            .GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        var greenMana = manaState.Players.Single(p => p.SystemSeatNumber == 1).ManaPool.Single();
        if (greenMana.Color != ManaColor.Green || greenMana.Count != 1 || greenMana.SrcInstanceId != 1004)
            throw new Exception("Floating mana lost in Arena translation");
        var metadataState = Encode(snapshot.Replace("\"grpId\":7", "\"grpId\":27212"))
            .GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        var metadataCard = metadataState.GameObjects.Single(c => c.InstanceId == 1001);
        if (metadataCard.Name != 4654 || metadataCard.CardTypes.Count == 0 ||
            !metadataCard.UniqueAbilities.Any(a => a.GrpId == 86621))
            throw new Exception("Installed printing title/types/rules abilities lost in translation");
        var mdfcState = Encode(snapshot.Replace("\"grpId\":7", "\"grpId\":75212"))
            .GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        var mdfcCard = mdfcState.GameObjects.Single(c => c.InstanceId == 1001);
        if (mdfcCard.OthersideGrpId != 75213)
            throw new Exception("MDFC othersideGrpId missing in translation");
        var state = MatchServiceToClientMessage.Parser.ParseFrom(bytes).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        if (state.Type != GameStateType.Full || state.Players.Count != 2 || state.Players[1].LifeTotal != 18 || state.TurnInfo.Phase != Wotc.Mtgo.Gre.External.Messaging.Phase.Main1) throw new Exception("Forge state mapping failed");
        var own = state.GameObjects.Single(c => c.InstanceId == 1001); var opponent = state.GameObjects.Single(c => c.InstanceId == 1002); var library = state.GameObjects.Single(c => c.InstanceId == 1003); var field = state.GameObjects.Single(c => c.InstanceId == 1004);
        if (own.GrpId != 7 || opponent.GrpId != 0 || opponent.Power != null || library.GrpId != 0 || field.Power?.Value != 3 || !field.IsTapped) throw new Exception("Forge visibility or card mapping failed");
        foreach (var card in state.GameObjects) if (!state.Zones.Single(z => z.ZoneId == card.ZoneId).ObjectInstanceIds.Contains(card.InstanceId)) throw new Exception("Forge zone/object referential integrity failed");
        var bfZone = state.Zones.Single(z => z.Type == ZoneType.Battlefield);
        var initializedPlayers = new HashSet<uint>();
        foreach (var zone in state.Zones) {
            if (zone.OwnerSeatId != 0) initializedPlayers.Add(zone.OwnerSeatId);
            else if (!initializedPlayers.SetEquals(new uint[] { 1, 2 }))
                throw new Exception("Shared zone precedes player card-holder initialization");
        }
        if (bfZone.OwnerSeatId != 0) throw new Exception("Battlefield zone must have OwnerSeatId == 0");
        const string facedownSnapshot="""
        {"matchId":"face-test","gameStateId":1,"msgId":1,"viewerSeat":1,"turn":{"number":1,"phase":"MAIN1","activeSeat":1,"prioritySeat":1},"players":[{"seat":1,"life":20},{"seat":2,"life":20}],"cards":[
        {"instanceId":1051,"grpId":75212,"ownerSeat":1,"controllerSeat":1,"zone":"Battlefield","viewers":[1],"faceDown":true,"faceDownReason":"Morph","power":2,"toughness":2,"cardTypes":["Creature"],"subtypes":[],"supertypes":[],"colors":[],"counters":[]},
        {"instanceId":1052,"grpId":75212,"ownerSeat":2,"controllerSeat":2,"zone":"Battlefield","viewers":[2],"faceDown":true,"faceDownReason":"Disguise","power":2,"toughness":2,"cardTypes":["Creature"],"subtypes":[],"supertypes":[],"colors":[],"counters":[]}]}
        """;
        var facedown=Encode(facedownSnapshot).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        var knownFace=facedown.GameObjects.Single(c=>c.InstanceId==1051);
        var unknownFace=facedown.GameObjects.Single(c=>c.InstanceId==1052);
        if(knownFace.GrpId!=3 || unknownFace.GrpId!=3 || knownFace.ObjectSourceGrpId!=75212 || unknownFace.ObjectSourceGrpId!=0
            || unknownFace.OthersideGrpId!=0 || unknownFace.Name!=0 || unknownFace.Power?.Value!=2 || unknownFace.Subtypes.Count!=0
            || unknownFace.SuperTypes.Count!=0 || knownFace.UniqueAbilities.Count!=0 || !unknownFace.UniqueAbilities.Any(a=>a.GrpId==141939))
            throw new Exception("Face-down display leaked identity, retained printed rules or lost disguise ward");
        var typed=Encode(facedownSnapshot.Replace("\"faceDown\":true","\"faceDown\":false").Replace("\"subtypes\":[]","\"subtypes\":[\"Elf\"]"))
            .GreToClientEvent.GreToClientMessages.Single().GameStateMessage.GameObjects.Single(c=>c.InstanceId==1051);
        if(!typed.Subtypes.SequenceEqual(new[]{SubType.Elf}) || typed.SuperTypes.Count!=0)
            throw new Exception("Current type changes were replaced by printed metadata");
        var exileZone = state.Zones.Single(z => z.Type == ZoneType.Exile);
        if (exileZone.OwnerSeatId != 0) throw new Exception("Exile zone must have OwnerSeatId == 0");
        try { Encode(snapshot.Replace("\"grpId\":7", "\"grpId\":0")); throw new Exception("Unmapped visible card accepted"); } catch (InvalidDataException) { }
        try { Encode(snapshot.Replace("\"instanceId\":1002", "\"instanceId\":1001")); throw new Exception("Duplicate object accepted"); } catch (InvalidDataException) { }
        Console.WriteLine("PASS: Forge GRE full state mapping, hidden card sanitization, zone consistency, unresolved/duplicate rejection.");
    }
}
