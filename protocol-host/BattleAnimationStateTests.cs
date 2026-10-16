using System.Reflection;
using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

/** Consume full/diff boards through the installed client, not a mock parser. */
static class BattleAnimationStateTests
{
    static void Check(bool value,string message) {if(!value)throw new Exception(message);}
    public static void Run(Assembly native) {
        var greType=native.GetType("GreClient.Rules.GreInterface",true)!;
        var database=native.GetType("Wotc.Mtga.Cards.Database.NullCardDatabaseAdapter",true)!.GetField("Default")!.GetValue(null)!;
        var gre=Activator.CreateInstance(greType,new object?[]{database,null,null,true,null})!;
        var process=greType.GetMethod("ProcessMessage",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var retrieve=greType.GetMethod("RetrieveClientUpdates")!;
        uint sequence=0,id=1;GameStateMessage? previous=null;
        object Publish(int power,int shields,string subtype,int color,bool down=false,bool copied=false,int classLevel=1,bool entered=false,bool hasNoAbilities=false,bool monstrous=false,bool renowned=false,bool monarchOnFirst=false,bool monarchOnSecond=false,bool blessingOnFirst=false,uint printing=75212,bool suppressPrintedAbilities=false,string[]? suppressedKeywords=null,string[]? grantedKeywords=null,string[]? chosenColors=null,int? chosenNumber=null,string zone="Battlefield",bool powerModified=false,bool toughnessModified=false,bool blockingRestriction=false,bool decayed=false) {
            var board=ForgeEncoder.Encode(JsonSerializer.Serialize(new {matchId="animation-state",gameStateId=++sequence,msgId=sequence,viewerSeat=1,
                turn=new {number=1,phase="MAIN1",activeSeat=1,prioritySeat=1},players=new[]{new {seat=1,life=20,monarch=monarchOnFirst,citysBlessing=blessingOnFirst},new {seat=2,life=20,monarch=monarchOnSecond,citysBlessing=false}},
                cards=new[]{new {instanceId=1001,grpId=copied?27212:printing,originalGrpId=printing,isCopy=copied,copyFromGrpId=27212,ownerSeat=1,controllerSeat=1,zone,viewers=new[]{1},
                    blockingRestrictions=blockingRestriction?new[]{new {sourceGrpId=70318,cardName="Questing Beast",description="CARDNAME can't be blocked by creatures with power 2 or less."}}:Array.Empty<object>(),chosenColors=chosenColors??Array.Empty<string>(),chosenNumber,suppressPrintedAbilities,suppressedKeywords=suppressedKeywords??Array.Empty<string>(),grantedKeywords=grantedKeywords??Array.Empty<string>(),
                    decayed,power,toughness=power,powerModified,toughnessModified,classLevel,hasNoAbilities,monstrous,renowned,enteredZoneThisTurn=entered,regenerationShields=shields,subtypes=new[]{subtype},supertypes=Array.Empty<string>(),
                    cardTypes=new[]{"Creature"},colors=new[]{color},counters=Array.Empty<object>(),faceDown=down,faceDownReason="Morph"}}}))
                .GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
            BattlePresentation.Transitions(previous,board,ref id);
            process.Invoke(gre,new object[]{new GREToClientMessage {Type=GREMessageType.GameStateMessage,SystemSeatIds={1},MsgId=sequence,
                GameStateId=sequence,GameStateMessage=BattleUpdates.WireState(board,previous)}});
            previous=board.Clone();
            return ((System.Collections.IEnumerable)retrieve.Invoke(gre,null)!).Cast<object>().Single(u=>u.GetType().Name=="GameStateUpdate");
        }
        string[] Properties(object update)=>((System.Collections.IEnumerable)update.GetType().GetField("Changes")!.GetValue(update)!).Cast<object>()
            .Where(e=>e.GetType().Name=="CardChangedEvent").Select(e=>e.GetType().GetField("Property")!.GetValue(e)!.ToString()!).ToArray();
        object Card(object update) {
            var state=update.GetType().GetField("NewState")!.GetValue(update)!;
            return ((System.Collections.IDictionary)state.GetType().GetField("AllCards")!.GetValue(state)!)[1001u]!;
        }
        Publish(1,0,"Elf",5);
        var changed=Publish(4,2,"Hydra",2);var properties=Properties(changed);
        foreach(string property in new[]{"PowerUp","ToughnessUp","SubType","RegenerateCount"})
            Check(properties.Count(p=>p==property)==1,"Installed Arena did not generate exactly one "+property+" animation event");
        var colors=((System.Collections.IEnumerable)Card(changed).GetType().GetField("Colors")!.GetValue(Card(changed))!).Cast<object>().Select(Convert.ToInt32).ToArray();
        Check(colors.SequenceEqual(new[]{2}),"Native card color did not follow Forge");
        Check(Convert.ToInt32(Card(changed).GetType().GetField("RegenerateCount")!.GetValue(Card(changed)))==2,"Native regeneration count differs from Forge");
        var repeated=Publish(4,2,"Hydra",2);
        Check(!Properties(repeated).Intersect(new[]{"PowerUp","ToughnessUp","SubType","Colors","RegenerateCount"}).Any(),"Unchanged board replayed model animations or accumulated shields");
        var cleared=Publish(4,0,"Hydra",2);
        Check(Convert.ToInt32(Card(cleared).GetType().GetField("RegenerateCount")!.GetValue(Card(cleared)))==0,"Expired regeneration shield remained in native state");
        var down=Publish(2,0,"",0,true);
        var face=Card(down).GetType().GetField("FaceDownState")!.GetValue(Card(down))!;
        Check((bool)face.GetType().GetProperty("IsFaceDown")!.GetValue(face)!,"Native face-down annotation was ignored");
        var up=Publish(4,0,"Hydra",2);
        Check(Properties(up).Contains("FaceDown"),"Turning face up did not produce the native model transition");
        var copy=Publish(4,0,"Hydra",2,copied:true);
        Check(Properties(copy).Count(p=>p=="GrpId")==1,"Copying did not produce exactly one native printing transition");
        Check((bool)Card(copy).GetType().GetField("IsCopy")!.GetValue(Card(copy))!,"Native copied-object flag was ignored");
        Check(Convert.ToUInt32(Card(copy).GetType().GetField("CopyObjectGrpId")!.GetValue(Card(copy)))==27212,"Native copied printing reference was lost");
        Check(Convert.ToUInt32(Card(copy).GetType().GetField("OthersideGrpId")!.GetValue(Card(copy)))==0,"Copying created a false reverse side");
        Check(!Properties(Publish(4,0,"Hydra",2,copied:true)).Contains("GrpId"),"Unchanged copy replayed a printing transition");
        var leveled=Publish(4,0,"Hydra",2,classLevel:3,entered:true);
        Check(Convert.ToInt32(Card(leveled).GetType().GetField("ClassLevel")!.GetValue(Card(leveled)))==3,"Native Class level was lost");
        Check(Card(leveled).GetType().GetField("EnteredZoneThisTurn")!.GetValue(Card(leveled))!.ToString()=="Battlefield","Native entry marker was lost");
        Check(((System.Collections.ICollection)Card(leveled).GetType().GetField("ColorModifications")!.GetValue(Card(leveled))!).Count==1,"Native modified color marker was lost or duplicated");
        var noEntry=Publish(4,0,"Hydra",5);
        Check(Card(noEntry).GetType().GetField("EnteredZoneThisTurn")!.GetValue(Card(noEntry))!.ToString()=="None","Native entry marker did not expire");
        Check(((System.Collections.ICollection)Card(noEntry).GetType().GetField("ColorModifications")!.GetValue(Card(noEntry))!).Count==0,"Native color marker survived returning to printed colors");
        var blanked=Publish(4,0,"Hydra",5,hasNoAbilities:true);
        Check(((System.Collections.ICollection)Card(blanked).GetType().GetField("Abilities")!.GetValue(Card(blanked))!).Count==0,"Printed abilities remained after Forge removed all abilities");
        var restored=Publish(4,0,"Hydra",5);
        Check(((System.Collections.ICollection)Card(restored).GetType().GetField("Abilities")!.GetValue(Card(restored))!).Count>0,"Printed abilities did not return when the effect ended");
        foreach(string spellZone in new[]{"Hand","Stack","Graveyard","Exile","Library"}) {
            var spell=Publish(0,0,"",0,printing:92407,zone:spellZone,hasNoAbilities:true,suppressPrintedAbilities:true);
            Check(((System.Collections.ICollection)Card(spell).GetType().GetField("Abilities")!.GetValue(Card(spell))!).Count>0,
                "Permanent ability suppression erased Damnation rules in "+spellZone);
        }
        Publish(4,0,"Hydra",5);
        Console.WriteLine("PASS: permanent ability suppression preserves spell rules in hand, stack, graveyard, exile and revealed library.");
        string[] Events(object update)=>((System.Collections.IEnumerable)update.GetType().GetField("Changes")!.GetValue(update)!).Cast<object>().Select(e=>e.GetType().Name).ToArray();
        var domainChoices=Publish(4,0,"Hydra",5,chosenColors:new[]{"White","Black"},chosenNumber:0);
        Check(Events(domainChoices).Count(n=>n=="ChoiceResultEvent")==2,"Installed Arena dropped color/number choice results");
        Check(((System.Collections.ICollection)Card(domainChoices).GetType().GetField("LinkedInfoText")!.GetValue(Card(domainChoices))!).Count==2,"Native color choices were not retained on the card");
        var notedNumber=((System.Collections.IEnumerable)Card(domainChoices).GetType().GetField("ActiveAbilityWords")!.GetValue(Card(domainChoices))!).Cast<object>()
            .Single(w=>w.GetType().GetField("AbilityWord")!.GetValue(w)!.ToString()=="LinkInfoAmount");
        Check(notedNumber.GetType().GetField("AdditionalDetail")!.GetValue(notedNumber)!.ToString()=="0","Native linked number discarded a zero choice");
        Check(!Events(Publish(4,0,"Hydra",5,chosenColors:new[]{"Black","White"},chosenNumber:0)).Contains("ChoiceResultEvent"),"Same color set/zero number replayed native choice results");
        var clearedChoices=Publish(4,0,"Hydra",5);
        Check(((System.Collections.ICollection)Card(clearedChoices).GetType().GetField("LinkedInfoText")!.GetValue(Card(clearedChoices))!).Count==0,"Expired color choices remained linked on the native card");
        Console.WriteLine("PASS: native color and number choice results, multi-color linked state, zero choices and no repeated playback.");
        bool StatMarked(object update,string property)=>((System.Collections.IEnumerable)Card(update).GetType().GetField("LayeredEffects")!.GetValue(Card(update))!).Cast<object>()
            .Any(e=>(bool)e.GetType().GetProperty(property)!.GetValue(e)!);
        var boosted=Publish(4,0,"Hydra",5,powerModified:true);
        Check(StatMarked(boosted,"IsPowerModification") && !StatMarked(boosted,"IsToughnessModification"),"Power-only boost marked the wrong native stat");
        Check(Convert.ToInt32(Card(boosted).GetType().GetField("Power")!.GetValue(Card(boosted))!.GetType().GetProperty("Value")!.GetValue(Card(boosted).GetType().GetField("Power")!.GetValue(Card(boosted))))==4,"Stat marker applied the power boost twice");
        Check(!Properties(Publish(4,0,"Hydra",5,powerModified:true)).Intersect(new[]{"PowerUp","PowerDown"}).Any(),"Unchanged buff replayed stat animations");
        var bothBoosted=Publish(4,0,"Hydra",5,powerModified:true,toughnessModified:true);
        Check(StatMarked(bothBoosted,"IsPowerModification") && StatMarked(bothBoosted,"IsToughnessModification"),"Two-stat boost markers missing");
        var expiredBoost=Publish(4,0,"Hydra",5);
        Check(!StatMarked(expiredBoost,"IsPowerModification") && !StatMarked(expiredBoost,"IsToughnessModification"),"Expired stat boost remained marked in native state");
        Console.WriteLine("PASS: native nonperpetual stat markers distinguish power/toughness, expire and do not double stats or replay.");
        var phased=Publish(4,0,"Hydra",5,zone:"PhasedOut");
        Check((bool)Card(phased).GetType().GetProperty("IsPhasedOut")!.GetValue(Card(phased))!,"Native phase-out state missing");
        Check(Properties(phased).Count(p=>p=="PhaseState")==1,"Phase out did not produce exactly one native transition");
        Check(!Properties(Publish(4,0,"Hydra",5,zone:"PhasedOut")).Contains("PhaseState"),"Unchanged phased-out board replayed");
        var phasedIn=Publish(4,0,"Hydra",5);
        Check(!(bool)Card(phasedIn).GetType().GetProperty("IsPhasedOut")!.GetValue(Card(phasedIn))!,"Native card did not phase back in");
        Check(Properties(phasedIn).Count(p=>p=="PhaseState")==1,"Phase in did not produce exactly one native transition");
        Console.WriteLine("PASS: native phasing out/in state and exactly-once transitions.");
        var restricted=Publish(4,0,"Beast",5,printing:70318,blockingRestriction:true);
        var qualification=((System.Collections.IEnumerable)Card(restricted).GetType().GetField("AffectedByQualifications")!.GetValue(Card(restricted))!).Cast<object>().Single();
        Check(qualification.GetType().GetField("Type")!.GetValue(qualification)!.ToString()=="CantBeBlocked"
            && (uint)qualification.GetType().GetProperty("AbilityId")!.GetValue(qualification)! ==87941,"Questing Beast blocking marker lost its conditional rule identity");
        var removedRestriction=Publish(4,0,"Beast",5,printing:70318);
        Check(((System.Collections.ICollection)Card(removedRestriction).GetType().GetField("AffectedByQualifications")!.GetValue(Card(removedRestriction))!).Count==0,"Removed blocking restriction survived in native state");
        Console.WriteLine("PASS: native Questing Beast conditional blocking qualification and removal.");
        var decaying=Publish(2,0,"Zombie",3,printing:78657,decayed:true);
        var decayedQualification=((System.Collections.IEnumerable)Card(decaying).GetType().GetField("AffectedByQualifications")!.GetValue(Card(decaying))!).Cast<object>().Single();
        Check(decayedQualification.GetType().GetField("Type")!.GetValue(decayedQualification)!.ToString()=="CantBlock"
            && (uint)decayedQualification.GetType().GetProperty("AbilityId")!.GetValue(decayedQualification)! ==214,
            "Decayed Zombie lost its native blocking-status identity");
        var noDecay=Publish(2,0,"Zombie",3,printing:78657,hasNoAbilities:true);
        Check(((System.Collections.ICollection)Card(noDecay).GetType().GetField("AffectedByQualifications")!.GetValue(Card(noDecay))!).Count==0,
            "Decayed status survived ability removal");
        Console.WriteLine("PASS: native decayed blocking status appears and clears when the ability is removed.");
        var designated=Publish(4,0,"Hydra",5,monstrous:true,renowned:true);
        Check(Events(designated).Count(n=>n=="DesignationCreatedEvent")==2,"Native monstrous/renowned designation creation missing");
        Check(!Events(Publish(4,0,"Hydra",5,monstrous:true,renowned:true)).Any(n=>n.StartsWith("Designation")),"Unchanged designations replayed native events");
        Check(Events(Publish(4,0,"Hydra",5)).Count(n=>n=="DesignationDeletedEvent")==2,"Native monstrous/renowned designation removal missing");
        Console.WriteLine("PASS: native monstrous and renowned designations appear, persist without replay and clear.");
        Check(Events(Publish(4,0,"Hydra",5,monarchOnFirst:true,blessingOnFirst:true)).Count(n=>n=="DesignationCreatedEvent")==2,"Native monarch/blessing player designations missing");
        var transfer=Events(Publish(4,0,"Hydra",5,monarchOnSecond:true,blessingOnFirst:true));
        Check(transfer.Count(n=>n=="DesignationCreatedEvent")==1 && transfer.Count(n=>n=="DesignationDeletedEvent")==1,"Monarch did not transfer to the other player while retaining blessing");
        Check(!Events(Publish(4,0,"Hydra",5,monarchOnSecond:true,blessingOnFirst:true)).Any(n=>n.StartsWith("Designation")),"Unchanged player designations replayed");
        Publish(4,0,"Hydra",5);
        Console.WriteLine("PASS: native monarch transfers between players; city blessing persists without replay.");
        Check(BattlePresentation.KeywordAbilityId("Flying")==8 && BattlePresentation.KeywordAbilityId("Vigilance")==15,"Shared keyword identity resolution differs from installed data");
        using(var grant=JsonDocument.Parse(JsonSerializer.Serialize(new {
            sourceGrpId=77129,hostCardName="Serra Angel",cardName="Minimus Containment",
            description="Add one mana of any color."})))
            Check(BattlePresentation.GrantedAbilityId(grant.RootElement)==183,
                "Quoted Treasure activation cost failed to distinguish tap/sacrifice from other mana abilities");
        int AbilityCount(object u)=>((System.Collections.ICollection)Card(u).GetType().GetField("Abilities")!.GetValue(Card(u))!).Count;
        Check(AbilityCount(Publish(3,0,"Angel",1,printing:67170))==2,"Native Serra Angel lost printed keywords");
        Check(AbilityCount(Publish(3,0,"Angel",1,printing:67170,suppressedKeywords:new[]{"Flying"}))==1,"Partial keyword suppression removed the wrong number of abilities");
        Check(AbilityCount(Publish(3,0,"Angel",1,printing:67170,suppressedKeywords:new[]{"Flying"},grantedKeywords:new[]{"Haste"}))==2,"Granted keyword was lost after partial suppression");
        Check(AbilityCount(Publish(3,0,"Angel",1,printing:67170,suppressPrintedAbilities:true,grantedKeywords:new[]{"Hexproof"}))==1,"New keyword was lost after suppressing printed abilities");
        Check(AbilityCount(Publish(3,0,"Angel",1,printing:67170))==2,"Suppressed printed keywords did not restore");
        Publish(4,0,"Hydra",5);
        Console.WriteLine("PASS: native partial keyword removal, removal followed by grants, and restoration.");
        var effectBoard=previous!.Clone();effectBoard.GameStateId=++sequence;effectBoard.Annotations.Clear();
        foreach(var category in new[]{AbilitySubCategory.Scry,AbilitySubCategory.Surveil,AbilitySubCategory.Explore,AbilitySubCategory.Proliferate,AbilitySubCategory.Connive})
            foreach(bool starting in new[]{true,false})effectBoard.Annotations.Add(new AnnotationInfo {Id=id++,AffectorId=1001,AffectedIds={1},
                Type={starting?AnnotationType.MultistepEffectStarted:AnnotationType.MultistepEffectComplete},
                Details={new KeyValuePairInfo {Key="SubCategory",Type=KeyValuePairValueType.Int32,ValueInt32={(int)category}}}});
        process.Invoke(gre,new object[]{new GREToClientMessage {Type=GREMessageType.GameStateMessage,SystemSeatIds={1},MsgId=sequence,
            GameStateId=sequence,GameStateMessage=BattleUpdates.WireState(effectBoard,previous)}});
        var effectsUpdate=((System.Collections.IEnumerable)retrieve.Invoke(gre,null)!).Cast<object>().Single(u=>u.GetType().Name=="GameStateUpdate");
        var effects=((System.Collections.IEnumerable)effectsUpdate.GetType().GetField("Changes")!.GetValue(effectsUpdate)!).Cast<object>()
            .Where(e=>e.GetType().Name=="MultistepEffectEvent").ToArray();
        Check(effects.Length==10,"Installed Arena dropped multistep effect boundaries");
        for(int i=0;i<effects.Length;i++) {
            Check((bool)effects[i].GetType().GetProperty("IsStarting")!.GetValue(effects[i])! == (i%2==0),"Native multistep start/finish order changed");
            Check(effects[i].GetType().GetProperty("Affected")!.GetValue(effects[i])!=null,"Native multistep player was lost");
            Check(effects[i].GetType().GetProperty("AbilityCategory")!.GetValue(effects[i])!.ToString()==new[]{"Scry","Surveil","Explore","Proliferate","Connive"}[i/2],"Native multistep category changed");
        }
        Console.WriteLine("PASS: installed Arena consumes ordered start/finish events for scry, surveil, explore, proliferate and connive.");
        Console.WriteLine("PASS: installed Arena generates stat/type/face-up animation events once, updates colors, preserves regeneration shields and clears expired shields.");
    }
}
