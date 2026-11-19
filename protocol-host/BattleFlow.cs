using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

sealed class BattleFlow
{
    public AutoPassOption Mode = (AutoPassOption)6; // Arena's normal resolve-my-effects setting.
    public AutoPassOption DefaultMode = (AutoPassOption)6;
    AutoPassOption stackMode=AutoPassOption.Clear;
    public AutoTapStopsSetting PaymentStops;
    public bool PassAfterAction;
    uint modeTurn;
    uint passedTurn;
    string? passedPhase;
    public void ManualPass(JsonElement snapshot) {
        lock(gate) {
            if(snapshot.GetProperty("input").GetProperty("type").GetString()!="InputPassPriority")return;
            if(snapshot.TryGetProperty("stack",out var stack) && stack.GetArrayLength()>0)return;
            passedTurn=snapshot.GetProperty("turn").GetProperty("number").GetUInt32();
            passedPhase=snapshot.GetProperty("turn").GetProperty("phase").GetString();
        }
    }
    readonly Dictionary<(StopType, SettingScope), Stop> stops = new();
    readonly Dictionary<(StopType, SettingScope), Stop> transientStops = new();
    readonly Dictionary<(StopType, SettingScope),(uint Turn,uint Active,int Phase)> reachedStops = new();
    readonly SettingsMessage settingsView=new();
    long settingsRevision;
    public long SettingsRevision { get { lock(gate)return settingsRevision; } }
    readonly object gate = new();
    public SettingsMessage SnapshotSettings()
    {
        lock(gate) {
            var result=settingsView.Clone();
            result.AutoPassOption=Mode; result.DefaultAutoPassOption=DefaultMode; result.StackAutoPassOption=stackMode; result.AutoTapStopsSetting=PaymentStops; result.ClearAllStops=default;
            result.Stops.Clear(); result.TransientStops.Clear();
            result.Stops.AddRange(stops.Values.Select(s=>s.Clone()));
            result.TransientStops.AddRange(transientStops.Values.Select(s=>s.Clone()));
            return result;
        }
    }
    public void Settings(SettingsMessage settings, uint turn)
    {
        lock (gate) ApplySettings(settings, turn);
    }
    void ApplySettings(SettingsMessage settings, uint turn)
    {
        settingsView.MergeFrom(settings);
        if ((int)settings.ClearAllStops == 1) { stops.Clear();transientStops.Clear();reachedStops.Clear(); }
        foreach (var stop in settings.Stops) {
            var key = (stop.StopType, stop.AppliesTo);
            if ((int)stop.Status == 1) stops[key] = stop.Clone();
            else if ((int)stop.Status == 2) stops.Remove(key);
        }
        foreach(var stop in settings.TransientStops) {
            var key=(stop.StopType,stop.AppliesTo); reachedStops.Remove(key);
            if((int)stop.Status==1)transientStops[key]=stop.Clone();
            else if((int)stop.Status==2)transientStops.Remove(key);
        }
        if ((int)settings.DefaultAutoPassOption != 0) DefaultMode = settings.DefaultAutoPassOption;
        if ((int)settings.AutoPassOption != 0) {
            Mode = (int)settings.AutoPassOption == 4 ? DefaultMode : settings.AutoPassOption;
            modeTurn = turn;
        }
        if ((int)settings.AutoTapStopsSetting != 0) PaymentStops = settings.AutoTapStopsSetting;
        if(settings.StackAutoPassOption!=AutoPassOption.None)stackMode=settings.StackAutoPassOption;
    }
    public bool ShouldPass(JsonElement snapshot)
    {
        lock (gate) return Evaluate(snapshot);
    }
    public void PriorityPassed() {lock(gate)PassAfterAction=false;}
    bool Evaluate(JsonElement snapshot)
    {
        if (!snapshot.TryGetProperty("input", out var input)) return false;
        var type = input.GetProperty("type").GetString() ?? "";
        uint turn = snapshot.GetProperty("turn").GetProperty("number").GetUInt32();
        if ((int)Mode is 1 or 3 && modeTurn != 0 && turn != modeTurn) { Mode = DefaultMode; PassAfterAction = false; settingsRevision++; }
        if ((int)Mode == 7 || snapshot.TryGetProperty("pendingCount", out var pending) && pending.GetInt32() != 0) return false;
        if (type.StartsWith("InputPayMana", StringComparison.Ordinal))
            return (int)PaymentStops != 1 && (input.TryGetProperty("autoTapSources",out var paymentSources)
                && paymentSources.GetArrayLength()>0 || input.TryGetProperty("autoTapPool",out var paymentPool)
                && paymentPool.GetArrayLength()>0);
        if (type != "InputPassPriority" && type != "InputAttack" && type != "InputBlock") return false;
        // Floating mana represents an unfinished player decision. Do not
        // advance priority/phase merely because affordability sees no play.
        // Explicit Pass actions still go directly to Forge.
        // ...except under Resolve All while the stack is busy: storm turns float mana constantly and the
        // hold made every stack item stop for a click (trace local-20261118-050500-46fe5477).
        bool resolvingAll=stackMode==AutoPassOption.ResolveAll
            && (snapshot.TryGetProperty("stack",out var busyStack) && busyStack.GetArrayLength()>0
                || snapshot.GetProperty("cards").EnumerateArray().Any(c=>c.GetProperty("zone").GetString()=="Stack"));
        if(type=="InputPassPriority" && !resolvingAll && snapshot.TryGetProperty("players",out var manaPlayers)
            && manaPlayers.EnumerateArray().Any(p=>p.GetProperty("seat").GetUInt32()==1
                && p.TryGetProperty("manaPool",out var pool)
                && pool.EnumerateArray().Any(m=>m.GetProperty("count").GetUInt32()>0))) {
            PassAfterAction=false;
            return false;
        }
        var phase = snapshot.GetProperty("turn").GetProperty("phase").GetString();
        int stopType = phase switch {
            "UPKEEP" => 1, "DRAW" => 2, "MAIN1" => 3, "COMBAT_BEGIN" => 4,
            "COMBAT_DECLARE_ATTACKERS" => 5, "COMBAT_DECLARE_BLOCKERS" => 6,
            "COMBAT_DAMAGE" => 7, "COMBAT_END" => 8, "MAIN2" => 9,
            "END_OF_TURN" => 10, "COMBAT_FIRST_STRIKE_DAMAGE" => 11, _ => 0
        };
        uint active = snapshot.GetProperty("turn").GetProperty("activeSeat").GetUInt32();
        bool onStack=snapshot.TryGetProperty("stack",out var priorityStack) && priorityStack.GetArrayLength()>0
            || snapshot.GetProperty("cards").EnumerateArray().Any(c=>c.GetProperty("zone").GetString()=="Stack");
        var stack=snapshot.TryGetProperty("stack",out var stackEntries)?stackEntries.EnumerateArray().ToArray():Array.Empty<JsonElement>();
        var spellCards=snapshot.GetProperty("cards").EnumerateArray().Where(c=>c.GetProperty("zone").GetString()=="Stack").ToArray();
        bool ownTop=stack.Length>0?stack[0].GetProperty("controllerSeat").GetUInt32()==1:spellCards.Length>0 && spellCards.All(c=>c.GetProperty("controllerSeat").GetUInt32()==1);
        bool ownAbility=ownTop && stack.Length>0 && stack[0].TryGetProperty("isAbility",out var abilityTop) && abilityTop.GetBoolean();
        bool opponentTop=onStack && !ownTop;
        if(stackMode==AutoPassOption.ResolveAll && !onStack && type=="InputPassPriority") {
            stackMode=AutoPassOption.Clear;settingsRevision++;
        }
        if(passedPhase!=null && (passedTurn!=turn || onStack || phase is "MAIN1" or "MAIN2" && phase!=passedPhase))passedPhase=null;
        bool Matches(Stop s)=>(int)s.StopType==stopType && ((int)s.AppliesTo==1 || (int)s.AppliesTo==2 && active!=1 || (int)s.AppliesTo==3 && active==1);
        var now=(turn,active,stopType);
        foreach(var key in reachedStops.Where(e=>e.Value!=now).Select(e=>e.Key).ToArray()) {
            reachedStops.Remove(key);transientStops.Remove(key);settingsRevision++;
        }
        foreach(var entry in transientStops.Where(e=>Matches(e.Value)))reachedStops[entry.Key]=now;
        bool hasPlay=snapshot.TryGetProperty("legalActions",out var available) && available.EnumerateArray().Any(a=>
            (!a.TryGetProperty("highlight",out var highlight) || highlight.GetInt32()==3)
            && (!a.TryGetProperty("cycling",out var cycling) || !cycling.GetBoolean() || onStack
                || active==1 && phase is "MAIN1" or "MAIN2")
            // Mana abilities alone are not a play: holding priority for them stopped on every
            // opponent spell/trigger. Only a castable spell, activated ability or land drop counts.
            && (!a.TryGetProperty("actionType",out var availableType) || availableType.GetInt32() is 1 or 2
                || availableType.GetInt32()==3 && active==1 && phase is "MAIN1" or "MAIN2")
            && (!a.TryGetProperty("isLand",out var land) || !land.GetBoolean()
                || a.TryGetProperty("actionType",out var landAction) && (landAction.GetInt32()==2
                    || landAction.GetInt32()==3 && active==1 && phase is "MAIN1" or "MAIN2")));
        // Forge affordability misses variable mana sources such as Vivi
        // Ornitier's "X mana in any combination", so in your own main phase a
        // spell that fits your untapped mana sources still keeps priority.
        if(!hasPlay && type=="InputPassPriority" && !onStack && active==1 && phase is "MAIN1" or "MAIN2")
            hasPlay=PossiblyAffordable(snapshot);
        // Normal play should not require a click at a phase stop when all
        // spells/abilities are unusable. Full control is handled above.
        bool annihilatorTrigger=stack.Any(e=>e.TryGetProperty("isAbility",out var isAbility) && isAbility.GetBoolean()
            && e.TryGetProperty("description",out var description)
            && (description.GetString()??"").TrimStart().StartsWith("Annihilator ",StringComparison.OrdinalIgnoreCase));
        if(type=="InputPassPriority" && annihilatorTrigger && (int)Mode is 5 or 6 && stackMode!=AutoPassOption.ResolveAll)return false;
        if(type=="InputPassPriority" && !hasPlay)return true;
        if(type=="InputPassPriority" && onStack && stackMode==AutoPassOption.ResolveAll)return true;
        if (stops.Values.Any(Matches) || transientStops.Values.Any(Matches)) return false;
        if(type=="InputPassPriority" && !onStack && passedPhase!=null)return true;
        // With no eligible combatants there is no assignment for the user to make.
        if (type == "InputAttack") return input.GetProperty("attackers").GetArrayLength() == 0;
        if (type == "InputBlock") return input.GetProperty("blockers").GetArrayLength() == 0;
        // Casting can pause at targeting/payment before the object reaches the stack.
        // Keep the client's pass intent until priority returns over that object.
        if (PassAfterAction && ownTop && !ownAbility) return true;
        if ((int)Mode == 1) return true;
        if ((int)Mode == 3) return phase != "END_OF_TURN";
        if ((int)Mode == 9 && onStack) return true;
        // Your triggered/activated ability is a response window too (for
        // example Tainted Pact over your Thassa's Oracle trigger).
        if ((int)Mode == 6 && ownTop) return !ownAbility;
        if ((int)Mode == 5) return !onStack || ownTop && !ownAbility;
        if ((int)Mode is not (2 or 6 or 9)) return false;
        // Forge legality is authoritative even if a display adapter cannot yet
        // represent an advanced cost. Do not skip that response opportunity.
        return !hasPlay;
    }
    // True when a cast Forge marked unaffordable has a mana value no larger
    // than the mana your untapped sources could make. Each source counts 1,
    // or its power for "X mana ... where X is CARDNAME's power" abilities.
    static bool PossiblyAffordable(JsonElement snapshot)
    {
        if(!snapshot.TryGetProperty("legalActions",out var actions))return false;
        var power=new Dictionary<uint,int>();
        foreach(var card in snapshot.GetProperty("cards").EnumerateArray())
            if(card.TryGetProperty("power",out var p) && p.ValueKind==JsonValueKind.Number)
                power[card.GetProperty("instanceId").GetUInt32()]=p.GetInt32();
        var sources=new Dictionary<uint,int>();
        foreach(var a in actions.EnumerateArray()) {
            if(!a.TryGetProperty("actionType",out var t) || t.GetInt32()!=4 || !a.TryGetProperty("instanceId",out var id))continue;
            uint source=id.GetUInt32();
            string text=a.TryGetProperty("description",out var d)?d.GetString()??"":"";
            int amount=text.Contains("where X is CARDNAME's power",StringComparison.Ordinal)
                ?Math.Max(1,power.GetValueOrDefault(source)):1;
            sources[source]=Math.Max(sources.GetValueOrDefault(source),amount);
        }
        int available=sources.Values.Sum();
        if(available==0)return false;
        // Only second-guess Forge when a variable-mana source (Vivi's "X mana in any combination")
        // is present; otherwise Forge's affordability is trusted and nothing castable means no stop.
        bool variableSource=actions.EnumerateArray().Any(a=>a.TryGetProperty("actionType",out var vt) && vt.GetInt32()==4
            && a.TryGetProperty("description",out var vd) && (vd.GetString()??"").Contains("where X is CARDNAME's power",StringComparison.Ordinal));
        if(!variableSource)return false;
        return actions.EnumerateArray().Any(a=>a.TryGetProperty("actionType",out var t) && t.GetInt32()==1
            && a.TryGetProperty("highlight",out var h) && h.GetInt32()==1
            && a.TryGetProperty("manaCost",out var cost) && ManaValue(cost.GetString()) is int value
            && value>0 && value<=available);
    }
    static int ManaValue(string? cost)
    {
        int total=0;
        foreach(System.Text.RegularExpressions.Match symbol in System.Text.RegularExpressions.Regex.Matches(cost??"",@"\{([^}]+)\}"))
            total+=int.TryParse(symbol.Groups[1].Value,out var generic)?generic:symbol.Groups[1].Value=="X"?0:1;
        return total;
    }
    public static void Test()
    {
        using var noAction = JsonDocument.Parse("""
        {"input":{"type":"InputPassPriority"},"turn":{"number":1,"phase":"MAIN1","activeSeat":1},"cards":[],"legalActions":[]}
        """);
        var flow = new BattleFlow();
        if (!flow.ShouldPass(noAction.RootElement)) throw new Exception("Empty priority should auto-pass");
        // Undo history alone must not hold priority. (Floating mana deliberately does; see Evaluate.)
        using var reversibleMana=JsonDocument.Parse(noAction.RootElement.GetRawText().Replace("\"legalActions\":[]","\"canUndo\":true,\"players\":[{\"seat\":1,\"manaPool\":[{\"count\":0}]}],\"legalActions\":[]"));
        if(!flow.ShouldPass(reversibleMana.RootElement))throw new Exception("Undo history held empty priority without a playable action");
        var yielded=new BattleFlow {Mode=(AutoPassOption)1};
        if(!yielded.ShouldPass(reversibleMana.RootElement))throw new Exception("Undo opportunity overrode explicit yield");
        flow.Settings(new SettingsMessage { AutoPassOption = (AutoPassOption)7 }, 1);
        if (flow.ShouldPass(noAction.RootElement)) throw new Exception("Full control auto-passed");
        flow.Settings(new SettingsMessage { AutoPassOption = (AutoPassOption)2, Stops = { new Stop { StopType = (StopType)3, AppliesTo = (SettingScope)3, Status = (SettingStatus)1 } } }, 1);
        if (!flow.ShouldPass(noAction.RootElement)) throw new Exception("Empty phase stop held unusable priority");
        using var spell = JsonDocument.Parse("""
        {"input":{"type":"InputPassPriority"},"turn":{"number":1,"phase":"MAIN1","activeSeat":1},"cards":[],"legalActions":[{"instanceId":1001,"grpId":7,"actionType":1,"isLand":false,"manaCost":"{0}"}]}
        """);
        if (new BattleFlow().ShouldPass(spell.RootElement)) throw new Exception("Skipped a playable spell");
        var manual=new BattleFlow(); manual.ManualPass(spell.RootElement);
        if(!manual.ShouldPass(spell.RootElement))throw new Exception("Repeated empty priority after pass");
        using var nextMain=JsonDocument.Parse(spell.RootElement.GetRawText().Replace("MAIN1","MAIN2"));
        if(manual.ShouldPass(nextMain.RootElement))throw new Exception("Manual pass skipped next main phase");
        using var cycling=JsonDocument.Parse(spell.RootElement.GetRawText().Replace("\"actionType\":1","\"actionType\":2,\"cycling\":true").Replace("MAIN1","END_OF_TURN"));
        if(!new BattleFlow().ShouldPass(cycling.RootElement))throw new Exception("Cycling held empty end-step priority");
        using var cycleResponse=JsonDocument.Parse(cycling.RootElement.GetRawText().Replace("\"cards\":[]","\"cards\":[],\"stack\":[{\"controllerSeat\":2,\"isAbility\":false}]"));
        var responseFlow=new BattleFlow(); responseFlow.ManualPass(cycling.RootElement);
        if(responseFlow.ShouldPass(cycleResponse.RootElement))throw new Exception("Manual pass skipped a new opponent stack response");
        var resolveAll=new BattleFlow();
        resolveAll.Settings(new SettingsMessage {StackAutoPassOption=AutoPassOption.ResolveAll},1);
        if(resolveAll.SnapshotSettings().StackAutoPassOption!=AutoPassOption.ResolveAll
            || !resolveAll.ShouldPass(cycleResponse.RootElement) || !resolveAll.ShouldPass(cycleResponse.RootElement))
            throw new Exception("Native stack Resolve All did not continue over opponent effects");
        using var resolveChoice=JsonDocument.Parse(cycleResponse.RootElement.GetRawText().Replace("InputPassPriority","InputSelectTargets"));
        if(resolveAll.ShouldPass(resolveChoice.RootElement) || resolveAll.SnapshotSettings().StackAutoPassOption!=AutoPassOption.ResolveAll)
            throw new Exception("Resolve All answered a decision or forgot the remaining stack");
        resolveAll.ShouldPass(spell.RootElement);
        if(resolveAll.SnapshotSettings().StackAutoPassOption!=AutoPassOption.Clear || resolveAll.SettingsRevision==0
            || resolveAll.ShouldPass(cycleResponse.RootElement))throw new Exception("Resolve All persisted onto a later stack");
        resolveAll.Settings(new SettingsMessage {StackAutoPassOption=AutoPassOption.ResolveAll},1);
        resolveAll.Settings(new SettingsMessage {StackAutoPassOption=AutoPassOption.Clear},1);
        if(resolveAll.ShouldPass(cycleResponse.RootElement))throw new Exception("Resolve All cancel was ignored");
        using var landOnly=JsonDocument.Parse(spell.RootElement.GetRawText().Replace("\"actionType\":1,\"isLand\":false","\"actionType\":3,\"isLand\":true"));
        using var manaOnly=JsonDocument.Parse(spell.RootElement.GetRawText().Replace("\"actionType\":1","\"actionType\":4"));
        using var threatenedRock=JsonDocument.Parse(manaOnly.RootElement.GetRawText().Replace("\"cards\":[]",
            "\"cards\":[],\"stack\":[{\"controllerSeat\":2,\"isAbility\":false}]"));
        using var threatenedRockOnOpponentTurn=JsonDocument.Parse(threatenedRock.RootElement.GetRawText().Replace("\"activeSeat\":1","\"activeSeat\":2"));
        using var ownSpellMana=JsonDocument.Parse(threatenedRock.RootElement.GetRawText().Replace("\"controllerSeat\":2","\"controllerSeat\":1"));
        var manaResponse=new BattleFlow();manaResponse.ManualPass(manaOnly.RootElement);
        // Mana abilities alone are not a play (user preference 2026-11-17): no stop over any spell.
        if(!manaResponse.ShouldPass(threatenedRock.RootElement) || !new BattleFlow().ShouldPass(threatenedRockOnOpponentTurn.RootElement)
            || !new BattleFlow().ShouldPass(ownSpellMana.RootElement)
            || !new BattleFlow {PassAfterAction=true}.ShouldPass(threatenedRock.RootElement))
            throw new Exception("Mana abilities alone held priority over a spell");
        using var landAbility=JsonDocument.Parse(spell.RootElement.GetRawText().Replace("\"actionType\":1,\"isLand\":false","\"actionType\":2,\"isLand\":true"));
        using var unpayable=JsonDocument.Parse(spell.RootElement.GetRawText().Replace("\"actionType\":1","\"actionType\":1,\"highlight\":1"));
        using var opponentLand=JsonDocument.Parse(landOnly.RootElement.GetRawText().Replace("\"activeSeat\":1","\"activeSeat\":2"));
        using var secondMainLand=JsonDocument.Parse(landOnly.RootElement.GetRawText().Replace("MAIN1","MAIN2"));
        using var combatLand=JsonDocument.Parse(landOnly.RootElement.GetRawText().Replace("MAIN1","COMBAT_BEGIN"));
        if(new BattleFlow().ShouldPass(landOnly.RootElement) || new BattleFlow().ShouldPass(secondMainLand.RootElement)
            || !new BattleFlow().ShouldPass(opponentLand.RootElement) || !new BattleFlow().ShouldPass(combatLand.RootElement)
            || !new BattleFlow().ShouldPass(manaOnly.RootElement)
            || !new BattleFlow().ShouldPass(unpayable.RootElement) || new BattleFlow().ShouldPass(landAbility.RootElement))
            throw new Exception("Land play opportunity skipped or mana actions held response priority");
        if(flow.ShouldPass(spell.RootElement))throw new Exception("Phase stop skipped an available play");
        using var payment = JsonDocument.Parse("""
        {"input":{"type":"InputPayMana","autoTapSources":[1001]},"turn":{"number":1,"phase":"MAIN1","activeSeat":1},"cards":[],"legalActions":[]}
        """);
        var paymentFlow = new BattleFlow();
        if (!paymentFlow.ShouldPass(payment.RootElement)) throw new Exception("Normal mana payment should auto-pay");
        paymentFlow.Settings(new SettingsMessage { AutoTapStopsSetting = (AutoTapStopsSetting)1 }, 1);
        if (paymentFlow.ShouldPass(payment.RootElement)) throw new Exception("Explicit mana payment stop ignored");
        using var target = JsonDocument.Parse("""
        {"input":{"type":"InputSelectTargets"},"turn":{"number":1,"phase":"MAIN1","activeSeat":1},"cards":[],"legalActions":[]}
        """);
        if (new BattleFlow().ShouldPass(target.RootElement)) throw new Exception("Target selection auto-passed");
        var arenaDefault = new BattleFlow();
        arenaDefault.Settings(new SettingsMessage { AutoPassOption = (AutoPassOption)6, DefaultAutoPassOption = (AutoPassOption)6 }, 1);
        if (!arenaDefault.ShouldPass(noAction.RootElement) || arenaDefault.ShouldPass(spell.RootElement))
            throw new Exception("Arena's default resolve-my-effects setting bypassed smart priority handling");
        using var ownAbility=JsonDocument.Parse("""
        {"input":{"type":"InputPassPriority"},"turn":{"number":1,"phase":"MAIN1","activeSeat":1},"cards":[],"stack":[{"controllerSeat":1,"isAbility":true},{"controllerSeat":2,"isAbility":true}],"legalActions":[{"instanceId":1001,"grpId":7,"actionType":1,"isLand":false,"manaCost":"{0}"}]}
        """);
        // Your own ability on top is a response window when you have a play (e.g. Tainted Pact over Thassa's Oracle).
        if(new BattleFlow().ShouldPass(ownAbility.RootElement))throw new Exception("Own ability on a mixed stack skipped the response window");
        using var ownSpellColdPlay=JsonDocument.Parse("""
        {"input":{"type":"InputPassPriority"},"turn":{"number":5,"phase":"MAIN1","activeSeat":1},"cards":[],
         "stack":[{"instanceId":1074,"controllerSeat":1,"cardName":"Platinum Angel"}],
         "legalActions":[{"instanceId":1056,"highlight":1,"actionType":1,"isLand":false,"manaCost":"{8}{U}{U}"}]}
        """);
        if(!new BattleFlow().ShouldPass(ownSpellColdPlay.RootElement))throw new Exception("Own spell on stack with unpayable actions did not auto-pass");
        var intent=new BattleFlow { Mode=(AutoPassOption)2,PassAfterAction=true };
        if(intent.ShouldPass(spell.RootElement) || !intent.PassAfterAction)throw new Exception("Pass intent was lost before casting finished");
        // Cast pass intent applies over your own spell (not an ability, which is a response window).
        using var ownSpellTop=JsonDocument.Parse(ownAbility.RootElement.GetRawText().Replace("{\"controllerSeat\":1,\"isAbility\":true}","{\"controllerSeat\":1,\"isAbility\":false}"));
        if(!intent.ShouldPass(ownSpellTop.RootElement) || !intent.PassAfterAction)throw new Exception("Pass intent was consumed before priority actually passed");
        intent.PriorityPassed();if(intent.PassAfterAction)throw new Exception("Passed priority did not clear the cast pass intent");
        var held=new BattleFlow(); held.Settings(new SettingsMessage {AutoPassOption=(AutoPassOption)7},1);
        if(held.ShouldPass(ownAbility.RootElement))throw new Exception("Full control skipped an ability response window");
        using var advancedCost=JsonDocument.Parse("""
        {"input":{"type":"InputPassPriority"},"turn":{"number":1,"phase":"MAIN1","activeSeat":1},"cards":[],"legalActions":[{"instanceId":1001,"grpId":7,"actionType":1,"isLand":false,"manaCost":"{W/P}"}]}
        """);
        if(new BattleFlow().ShouldPass(advancedCost.RootElement))throw new Exception("Unmapped display cost erased a legal response window");
        var temporary=new BattleFlow();
        temporary.Settings(new SettingsMessage {TransientStops={new Stop {StopType=(StopType)3,AppliesTo=(SettingScope)3,Status=(SettingStatus)1}}},1);
        if(!temporary.ShouldPass(noAction.RootElement))throw new Exception("Temporary phase stop held empty priority");
        using var laterPhase=JsonDocument.Parse(noAction.RootElement.GetRawText().Replace("MAIN1","MAIN2"));
        temporary.ShouldPass(laterPhase.RootElement);
        if(!temporary.ShouldPass(noAction.RootElement) || temporary.SnapshotSettings().TransientStops.Count!=0 || temporary.SettingsRevision==0)
            throw new Exception("Temporary phase stop persisted after leaving its phase");
    }
}
