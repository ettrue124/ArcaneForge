using System.Text.Json;
using Google.Protobuf;
using Wotc.Mtgo.Gre.External.Messaging;

static class BattlePlaybackTests
{
    public static void Probe()
    {
        var flow=new BattleFlow(); GameStateMessage? previous=null; long cursor=0; uint id=1;
        while(Console.ReadLine() is { } line) {
            using var input=JsonDocument.Parse(line); var root=input.RootElement;
            var board=ForgeEncoder.Encode(line).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
            BattlePresentation.StateTargets(root,board,ref id);
            var playback=BattlePresentation.Playback(root,previous,board,cursor,ref id);
            BattlePresentation.Transitions(previous,board,ref id,playback);
            var request=BattleRequests.Create(root,1,1);
            Console.WriteLine(JsonSerializer.Serialize(new {
                shouldPass=flow.ShouldPass(root), abilities=board.GameObjects.Count(c=>c.Type==GameObjectType.Ability),
                lifePayment=request?.OptionalActionMessage?.OptionalActionTypes.Contains(CardMechanicType.PayLife)==true
                    ?new {prompt=request.Prompt.PromptId,amount=request.Prompt.Parameters.Single(p=>p.Type==ParameterType.Number).NumberValue,source=request.OptionalActionMessage.SourceId}:null,
                exploreCards=request?.OptionalActionMessage?.OptionalActionTypes.Contains(CardMechanicType.Explore)==true
                    ?request.OptionalActionMessage.RecipientIds.ToArray():Array.Empty<uint>(),
                actions=BattlePresentation.Actions(root).Select(a=>new {id=a.InstanceId,ability=a.AbilityGrpId,type=(int)a.ActionType,source=a.SourceId}),
                tokens=board.GameObjects.Where(c=>c.Type==GameObjectType.Token).Select(c=>new {id=c.InstanceId,grpId=c.GrpId,zone=c.ZoneId,abilities=c.UniqueAbilities.Select(a=>a.GrpId).ToArray()}),
                stats=board.GameObjects.Where(c=>c.Type==GameObjectType.Card && c.GrpId!=0).Select(c=>new {id=c.InstanceId,power=c.Power?.Value,toughness=c.Toughness?.Value,types=c.CardTypes.Select(t=>t.ToString()).ToArray()}),
                cardAbilities=board.GameObjects.Where(c=>c.Type==GameObjectType.Card && c.GrpId!=0).Select(c=>new {id=c.InstanceId,abilities=c.UniqueAbilities.Select(a=>a.GrpId).ToArray()}),
                printedFaces=board.GameObjects.Where(c=>c.Type==GameObjectType.Card && c.GrpId!=0 && c.ObjectSourceGrpId!=0).Select(c=>new {id=c.InstanceId,grpId=c.GrpId,source=c.ObjectSourceGrpId,other=c.OthersideGrpId}),
                targetLinks=board.Annotations.Where(a=>a.Type.Contains(AnnotationType.TargetSpec)).Select(a=>new {source=a.AffectorId,targets=a.AffectedIds.ToArray()}),
                attachments=board.PersistentAnnotations.Where(a=>a.Type.Contains(AnnotationType.Attachment)).Select(a=>new {source=a.AffectorId,target=a.AffectedIds.Single()}),
                targetingAbility=request?.SelectTargetsReq?.AbilityGrpId??0,
                targetingSource=request?.SelectTargetsReq?.SourceId??0,
                targetingPrompt=request?.SelectTargetsReq?.Targets.FirstOrDefault()?.Prompt?.PromptId??0,
                targetingIndex=request?.SelectTargetsReq?.Targets.FirstOrDefault()?.TargetIdx,
                requestType=request?.Type.ToString(),
                entryPayments=board.PersistentAnnotations.Where(a=>a.Type.Contains(AnnotationType.ReplacementEffect)).Select(a=>new {
                    source=a.AffectorId,ability=a.Details.Single(d=>d.Key=="grpid").ValueInt32.Single()}),
                cantBlock=board.PersistentAnnotations.Where(a=>a.Type.Contains(AnnotationType.Qualification)
                    && a.Details.Any(d=>d.Key=="QualificationType" && d.ValueInt32.Contains((int)QualificationType.CantBlock)))
                    .Select(a=>a.AffectedIds.Single()).ToArray(),
                offeredActions=request?.ActionsAvailableReq?.Actions.Select(a=>new {id=a.InstanceId,source=a.SourceId,type=a.ActionType.ToString()}).ToArray(),
                identityChanges=board.Annotations.Where(a=>a.Type.Contains(AnnotationType.ObjectIdChanged)).Select(a=>new {
                    oldId=a.Details.Single(d=>d.Key=="orig_id").ValueInt32.Single(),newId=a.Details.Single(d=>d.Key=="new_id").ValueInt32.Single()}),
                blockers=request?.DeclareBlockersReq?.Blockers.Select(b=>new {id=b.BlockerInstanceId,max=b.MaxAttackers,targets=b.AttackerInstanceIds.ToArray(),selected=b.SelectedAttackerInstanceIds.ToArray()}),
                choiceIds=request?.SelectNReq?.Ids.ToArray(),
                choiceLabels=request?.SelectNReq?.Prompt?.Parameters.Select(p=>p.StringValue).ToArray(),
                orderIds=request?.OrderReq?.Ids.ToArray(),
                damageAssigners=request?.AssignDamageReq?.DamageAssigners.Select(a=>new {source=a.InstanceId,total=a.TotalDamage,assignments=a.Assignments.Select(v=>new {id=v.InstanceId,min=v.MinDamage,max=v.MaxDamage,amount=v.AssignedDamage})}),
                numberRequest=request?.CastingTimeOptionsReq?.CastingTimeOptionReq.FirstOrDefault()?.NumericInputReq??request?.NumericInputReq,
                xValues=board.PersistentAnnotations.Where(a=>a.Type.Contains(AnnotationType.CastingTimeOption)).Select(a=>a.Details.Single(d=>d.Key=="value").ValueInt32.Single()),
                stack=board.Zones.Single(z=>z.ZoneId==40).ObjectInstanceIds.ToArray(),
                stackAbilities=board.GameObjects.Where(c=>c.Type==GameObjectType.Ability && c.ZoneId==40).Select(c=>new {id=c.InstanceId,ability=c.GrpId,parent=c.ParentId,controller=c.ControllerSeatId}),
                library=board.Zones.Where(z=>z.Type==ZoneType.Library).Select(z=>new {seat=z.OwnerSeatId,ids=z.ObjectInstanceIds.ToArray(),
                    visible=board.GameObjects.Where(c=>c.ZoneId==z.ZoneId && c.GrpId!=0).Select(c=>c.InstanceId).ToArray()}),
                counters=board.PersistentAnnotations.Where(a=>a.Type.Contains(AnnotationType.Counter)).Select(a=>new {
                    target=a.AffectedIds.Single(),type=a.Details.Single(d=>d.Key=="counter_type").ValueInt32.Single(),count=a.Details.Single(d=>d.Key=="count").ValueInt32.Single()}),
                multistep=board.Annotations.Where(a=>a.Type.Contains(AnnotationType.MultistepEffectStarted)||a.Type.Contains(AnnotationType.MultistepEffectComplete))
                    .Select(a=>new {source=a.AffectorId,player=a.AffectedIds.Single(),starting=a.Type.Contains(AnnotationType.MultistepEffectStarted),
                        category=((AbilitySubCategory)a.Details.Single(d=>d.Key=="SubCategory").ValueInt32.Single()).ToString()}),
                designations=board.PersistentAnnotations.Where(a=>a.Type.Contains(AnnotationType.Designation))
                    .Select(a=>new {target=a.AffectedIds.Single(),designation=((Designation)a.Details.Single(d=>d.Key=="DesignationType").ValueInt32.Single()).ToString()}),
                annotations=board.Annotations.SelectMany(a=>a.Type).Select(t=>t.ToString()).ToArray(),
                statMarkers=board.PersistentAnnotations.Where(a=>a.Type.Contains(AnnotationType.ModifiedPower)||a.Type.Contains(AnnotationType.ModifiedToughness))
                    .Select(a=>new {target=a.AffectedIds.Single(),power=a.Type.Contains(AnnotationType.ModifiedPower),toughness=a.Type.Contains(AnnotationType.ModifiedToughness)}),
                phasedCards=board.GameObjects.Where(c=>board.Zones.Single(z=>z.ZoneId==c.ZoneId).Type==ZoneType.PhasedOut)
                    .Select(c=>c.InstanceId).ToArray(),
                blockingRestrictions=board.PersistentAnnotations.Where(a=>a.Type.Contains(AnnotationType.Qualification)
                    && a.Details.Any(d=>d.Key=="QualificationType" && d.ValueInt32.Contains((int)QualificationType.CantBeBlocked)))
                    .Select(a=>new {target=a.AffectedIds.Single(),ability=a.Details.Single(d=>d.Key=="grpid").ValueInt32.Single()}),
                choiceResults=board.Annotations.Where(a=>a.Type.Contains(AnnotationType.ChoiceResult)).Select(a=>new {
                    source=a.AffectorId,player=a.AffectedIds.Single(),
                    domain=a.Details.Single(d=>d.Key=="Choice_Domain").ValueInt32.FirstOrDefault(),
                    domainString=a.Details.Single(d=>d.Key=="Choice_Domain").ValueString.FirstOrDefault(),
                    values=a.Details.Single(d=>d.Key=="Choice_Value").ValueInt32.ToArray()})
            }));
            cursor=playback.Cursor; previous=board.Clone();
        }
    }
    static void Check(bool value, string message) { if(!value)throw new Exception(message); }
    public static void Run()
    {
        var correlation=new MatchSessionState {ButtonEventId=9,InputType="InputSelectTargets"};
        correlation.RequestMsgId=10; correlation.RequestMsgId=11;
        Check(correlation.IsCurrentResponse(10),"Board update invalidated the same pending input");
        correlation.ButtonEventId=12;
        Check(!correlation.IsCurrentResponse(10),"Old input response was accepted for a new Forge choice");
        var before=new GameStateMessage {
            GameObjects={new GameObjectInfo { InstanceId=1001,GrpId=283,ZoneId=40 },new GameObjectInfo { InstanceId=1002,GrpId=284,ZoneId=30 }},
            Players={new PlayerInfo { SystemSeatNumber=1 },new PlayerInfo { SystemSeatNumber=2 }},
            Zones={new ZoneInfo { ZoneId=30 },new ZoneInfo { ZoneId=40 },new ZoneInfo { ZoneId=105 }}
        };
        var board=before.Clone(); board.GameObjects[0].ZoneId=105; board.GameObjects[1].IsTapped=true;
        using var input=JsonDocument.Parse("""
        {"playbackId":7,"playbackGap":false,"playbackEvents":[
        {"eventId":1,"type":"resolutionStart","sourceId":1001},
        {"eventId":2,"type":"damage","sourceId":1001,"targetId":2,"amount":2,"damageType":2},
        {"eventId":3,"type":"zoneTransfer","targetId":1001,"fromZone":40,"toZone":105,"category":"ZoneTransferWithoutIdChange"},
        {"eventId":4,"type":"resolutionComplete","sourceId":1001},
        {"eventId":5,"type":"tap","targetId":1002,"tapped":true},
        {"eventId":6,"type":"shuffle","playerId":1},
        {"eventId":7,"type":"damage","sourceId":9999,"targetId":2,"amount":2,"damageType":2}]}
        """);
        uint id=50;
        var playback=BattlePresentation.Playback(input.RootElement,before,board,0,ref id);
        BattlePresentation.Transitions(before,board,ref id,playback);
        var expected=new[]{AnnotationType.ResolutionStart,AnnotationType.DamageDealt,AnnotationType.ZoneTransfer,AnnotationType.ResolutionComplete,AnnotationType.TappedUntappedPermanent,AnnotationType.Shuffle};
        Check(board.Annotations.Select(a=>a.Type.Single()).SequenceEqual(expected),"Playback ordering, unknown-source filtering or transition dedup failed");
        Check(board.Annotations.Select(a=>a.Id).SequenceEqual(Enumerable.Range(50,6).Select(n=>(uint)n)),"Annotation IDs were reused");
        var damage=board.Annotations[1];
        Check(damage.AffectorId==1001 && damage.AffectedIds.Single()==2 && damage.Details.Single(d=>d.Key=="damage").ValueInt32.Single()==2,"Damage source/target/amount mismatch");
        Check(damage.Details.Single(d=>d.Key=="markDamage").ValueInt32.Single()==0,"Snapshot damage would be applied twice");
        var roundtrip=GameStateMessage.Parser.ParseFrom(board.ToByteArray());
        Check(roundtrip.Annotations.Count==6 && playback.Cursor==7,"Playback protobuf roundtrip/cursor failed");
        var duplicate=board.Clone(); duplicate.Annotations.Clear();
        BattlePresentation.Playback(input.RootElement,board,duplicate,7,ref id);
        Check(duplicate.Annotations.Count==0,"Acknowledged effects replayed");
        using var extraPlayback=JsonDocument.Parse("""
        {"playbackId":2,"playbackEvents":[
        {"eventId":1,"type":"scryResult","topIds":[1001],"bottomIds":[1002]},
        {"eventId":2,"type":"regenerated","targetId":1002}]}
        """);
        var extraBoard=before.Clone();extraBoard.GameObjects[1].ControllerSeatId=2;
        var extraBefore=before.Clone();extraBefore.GameObjects[1].ControllerSeatId=1;
        var extra=BattlePresentation.Playback(extraPlayback.RootElement,extraBefore,extraBoard,0,ref id);
        BattlePresentation.Transitions(extraBefore,extraBoard,ref id,extra);
        var extraRoundtrip=GameStateMessage.Parser.ParseFrom(extraBoard.ToByteArray());
        Check(extraRoundtrip.Annotations.SelectMany(a=>a.Type).SequenceEqual(new[]{AnnotationType.Scry,AnnotationType.PermanentRegenerated,AnnotationType.ControllerChanged}),
            "Scry, regeneration or controller change playback missing");
        var scry=extraRoundtrip.Annotations[0];
        Check(scry.AffectedIds.SequenceEqual(new uint[]{1001,1002}) && scry.Details.Single(d=>d.Key=="topIds").ValueInt32.Single()==1001
            && scry.Details.Single(d=>d.Key=="bottomIds").ValueInt32.Single()==1002,"Scry destinations were swapped");
        var repeat=extraBoard.Clone();repeat.Annotations.Clear();
        BattlePresentation.Playback(extraPlayback.RootElement,extraBoard,repeat,2,ref id);
        BattlePresentation.Transitions(extraBoard,repeat,ref id);
        Check(repeat.Annotations.Count==0,"Scry/regeneration/control change replayed on unchanged board");
        using var randomPlayback=JsonDocument.Parse("""
        {"playbackId":3,"playbackEvents":[
        {"eventId":1,"type":"randomOutcome","sourceId":1001,"targetId":1,"kind":"coin","results":[1,0,1]},
        {"eventId":2,"type":"randomOutcome","sourceId":1001,"targetId":1,"kind":"die","sides":20,"natural":[14],"results":[16],"ignored":false},
        {"eventId":3,"type":"randomOutcome","sourceId":1001,"targetId":1,"kind":"die","sides":20,"natural":[3],"results":[3],"ignored":true}]}
        """);
        var randomBoard=before.Clone();
        BattlePresentation.Playback(randomPlayback.RootElement,before,randomBoard,0,ref id);
        Check(randomBoard.Annotations.SelectMany(a=>a.Type).SequenceEqual(new[]{AnnotationType.CoinFlip,AnnotationType.DieRoll,AnnotationType.DieRoll}),"Random outcome annotations missing");
        Check(randomBoard.Annotations[0].Details.Single().ValueInt32.SequenceEqual(new[]{1,0,1}),"Coin multiplicity/results lost");
        Check(randomBoard.Annotations[1].Details.Single(d=>d.Key=="NaturalResult").ValueInt32.Single()==14
            && randomBoard.Annotations[1].Details.Single(d=>d.Key=="Result").ValueInt32.Single()==16
            && randomBoard.Annotations[2].Details.Single(d=>d.Key=="Ignored").ValueString.Single()=="true","Die modifiers/ignored outcomes lost");
        var chosenTypeBoard=before.Clone();chosenTypeBoard.GameObjects[1].ControllerSeatId=1;
        chosenTypeBoard.PersistentAnnotations.Add(new AnnotationInfo {Type={AnnotationType.LinkInfo},AffectorId=1002,
            AffectedIds={(uint)TypeCategory.SubType,(uint)SubType.Elf},Details={new KeyValuePairInfo {Key="ChooseLinkType",ValueString={"Type"}}}});
        BattlePresentation.Transitions(before,chosenTypeBoard,ref id);
        var typeResult=chosenTypeBoard.Annotations.Single(a=>a.Type.Contains(AnnotationType.ChoiceResult));
        Check(typeResult.AffectorId==1002 && typeResult.AffectedIds.Single()==1
            && typeResult.Details.Single(d=>d.Key=="Choice_Domain").ValueInt32.Single()==(int)StaticList.SubTypes
            && typeResult.Details.Single(d=>d.Key=="Choice_Value").ValueInt32.Single()==(int)SubType.Elf,"Chosen type result lost its player, domain or value");
        var repeatedType=chosenTypeBoard.Clone();repeatedType.Annotations.Clear();
        BattlePresentation.Transitions(chosenTypeBoard,repeatedType,ref id);
        Check(repeatedType.Annotations.Count==0,"Chosen creature type effect replayed on unchanged board");
        var chosenDomains=before.Clone();chosenDomains.GameObjects[1].ControllerSeatId=1;
        foreach(var kind in new[]{"Color","Amount"}) {
            var choice=new AnnotationInfo {Type={AnnotationType.LinkInfo},AffectorId=1002,
                Details={new KeyValuePairInfo {Key="ChooseLinkType",ValueString={kind}}}};
            choice.AffectedIds.Add(kind=="Color"?new uint[]{1,3}:new uint[]{0});
            chosenDomains.PersistentAnnotations.Add(choice);
        }
        BattlePresentation.Transitions(before,chosenDomains,ref id);
        var domainResults=chosenDomains.Annotations.Where(a=>a.Type.Contains(AnnotationType.ChoiceResult)).ToArray();
        Check(domainResults.Length==2 && domainResults[0].Details.Single(d=>d.Key=="Choice_Domain").ValueInt32.Single()==(int)StaticList.Colors
            && domainResults[0].Details.Single(d=>d.Key=="Choice_Value").ValueInt32.SequenceEqual(new[]{1,3})
            && domainResults[1].Details.Single(d=>d.Key=="Choice_Domain").ValueString.Single()=="number"
            && domainResults[1].Details.Single(d=>d.Key=="Choice_Value").ValueInt32.Single()==0,"Chosen colors or zero number lost their native domain/value");
        var repeatedDomains=chosenDomains.Clone();repeatedDomains.Annotations.Clear();
        BattlePresentation.Transitions(chosenDomains,repeatedDomains,ref id);
        Check(!repeatedDomains.Annotations.Any(a=>a.Type.Contains(AnnotationType.ChoiceResult)),"Color/number choice replayed on unchanged board");
        var initial=before.Clone();
        Check(BattlePresentation.Playback(input.RootElement,null,initial,0,ref id).Cursor==7 && initial.Annotations.Count==0,"Reconnect replayed historical effects");
        using var broken=JsonDocument.Parse("""{"playbackId":2,"playbackEvents":[{"eventId":2,"type":"shuffle","playerId":1},{"eventId":1,"type":"shuffle","playerId":1}]}""");
        try { BattlePresentation.Playback(broken.RootElement,before,before.Clone(),0,ref id); throw new Exception("Unordered journal accepted"); }
        catch(InvalidDataException) { }
        var pacer=new StackPlaybackPacer();
        Check(!pacer.Ready(8,true,1000) && !pacer.Ready(8,true,1100) && pacer.Pending,"Automatic stack step was not held for presentation");
        Check(pacer.Ready(8,true,1150) && pacer.Pending,"Automatic stack step did not resume or was acknowledged too early");
        pacer.Reset();Check(!pacer.Pending,"Acknowledged stack step remained pending");
        Check(!pacer.Ready(9,true,1700),"Next stack step skipped its pause");
        pacer.Reset();Check(!pacer.Pending && pacer.Ready(10,false,1800),"Full control reset or empty-stack progression failed");
        const string landfall="Landfall — Whenever a land you control enters, put a +1/+1 counter on target creature.";
        Check(BattlePresentation.AbilityId(90503,landfall,"Bristly Bill, Spine Sower")==171897,"Bristly Bill landfall printing mapped to the wrong ability");
        using var billInput=JsonDocument.Parse(JsonSerializer.Serialize(new {
            cards=new[]{new { instanceId=1001,grpId=90503,ownerSeat=1,zone="Battlefield" }},
            input=new { type="InputSelectTargets",divided=false,sourceId=1001,min=1,max=1,
                targets=new[]{1001,1002},selected=new[]{1002,1002},validTargetTypes=new[]{"Creature"},
                description=landfall,cardName="Bristly Bill, Spine Sower",targetIndex=0,targetingPlayer=1 }
        }));
        var billRequest=GREToClientMessage.Parser.ParseFrom(BattleRequests.Create(billInput.RootElement,31,4)!.ToByteArray());
        var billTargets=billRequest.SelectTargetsReq.Targets.Single();
        Check(billRequest.Type==GREMessageType.SelectTargetsReq && billRequest.SelectTargetsReq.AbilityGrpId==171897
            && billTargets.TargetingAbilityGrpId==171897 && billTargets.TargetSourceZoneId==30 && billTargets.TargetingPlayer==1,
            "Bill landfall did not use the battlefield targeting interaction and landfall ability");
        Check((billTargets.Prompt.PromptId == 1463 || billTargets.Prompt.PromptId == 1629) && billRequest.Prompt.Equals(billTargets.Prompt), "Bill landfall showed a card-picking prompt");
        Check(billTargets.Targets.Count==2 && billTargets.SelectedTargets==1 && billTargets.Targets.Single(t=>t.TargetInstanceId==1002).LegalAction==SelectAction.Unselect
            && billTargets.Targets.Single(t=>t.TargetInstanceId==1001).LegalAction==SelectAction.Select,"Selected targets had duplicate or incorrect selection actions");
        using var thoughtseizeInput=JsonDocument.Parse(JsonSerializer.Serialize(new {
            cards=new[]{new { instanceId=1001,grpId=28269,ownerSeat=1,zone="Stack" }},
            input=new { type="InputSelectTargets",divided=false,sourceId=1001,min=1,max=1,
                targets=new[]{1,2},selected=Array.Empty<int>(),validTargetTypes=new[]{"Player"},
                description="Target player reveals their hand.",cardName="Thoughtseize",targetIndex=0,targetingPlayer=1 }
        }));
        var thoughtseizeReq=GREToClientMessage.Parser.ParseFrom(BattleRequests.Create(thoughtseizeInput.RootElement,32,5)!.ToByteArray());
        var tsTargets=thoughtseizeReq.SelectTargetsReq.Targets.Single();
        Check(tsTargets.TargetSourceZoneId==0 && tsTargets.Targets.Count==2 && tsTargets.Prompt.PromptId==1019,
            "Player targeting should have TargetSourceZoneId=0 and PromptId=1019 so Arena does not open a stack browser");
        const string counterFixture="""
        {"matchId":"counter-test","gameStateId":1,"msgId":1,"viewerSeat":1,"turn":{"number":1,"phase":"MAIN1","activeSeat":1,"prioritySeat":1},"players":[{"seat":1,"life":20},{"seat":2,"life":20}],"cards":[
        {"instanceId":1001,"grpId":93820,"ownerSeat":1,"controllerSeat":1,"zone":"Battlefield","viewers":[1,2],"counters":[{"type":"P1P1","count":2},{"type":"Flying","count":1}]},
        {"instanceId":1003,"grpId":6993,"ownerSeat":1,"controllerSeat":1,"zone":"Library","viewers":[1],"counters":[]},
        {"instanceId":1004,"grpId":6993,"ownerSeat":1,"controllerSeat":1,"zone":"Library","viewers":[],"counters":[]}]}
        """;
        var counterBefore=ForgeEncoder.Encode(counterFixture).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        var counterAfter=ForgeEncoder.Encode(counterFixture.Replace("\"count\":2","\"count\":4")).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        BattlePresentation.Transitions(counterBefore,counterAfter,ref id);
        Check(counterAfter.PersistentAnnotations.Count==2 && counterAfter.Annotations.Single().Type.Single()==AnnotationType.CounterAdded,
            "Counter types/state/addition were lost");
        Check(counterAfter.Annotations.Single().Details.Single(d=>d.Key=="transaction_amount").ValueInt32.Single()==2,"Counter growth counted twice");
        var counterEmpty=ForgeEncoder.Encode(counterFixture.Replace("\"count\":2","\"count\":0")).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        BattlePresentation.Transitions(counterAfter,counterEmpty,ref id);
        Check(counterEmpty.Annotations.Single().Type.Single()==AnnotationType.CounterRemoved && counterEmpty.PersistentAnnotations.Count==1,"Counter removal lost or removed another type");
        Check(counterBefore.Zones.Single(z=>z.ZoneId==101).ObjectInstanceIds.SequenceEqual(new uint[]{1003,1004}) && counterBefore.GameObjects.Single(c=>c.InstanceId==1003).GrpId!=0 && counterBefore.GameObjects.Single(c=>c.InstanceId==1004).GrpId==0,"Library top order/look permission failed");
        Check(counterBefore.GameObjects.Single(c=>c.InstanceId==1003).Visibility==Visibility.Private,"Player-only library visibility must be Private, not Public, to prevent eye badge");
        Check(!counterBefore.PersistentAnnotations.Any(a=>a.Type.Contains(AnnotationType.InstanceRevealedToOpponent) && a.AffectedIds.Contains(1003)),"Library card should not have InstanceRevealedToOpponent annotation");
        var otherViewer=ForgeEncoder.Encode(counterFixture.Replace("\"viewerSeat\":1","\"viewerSeat\":2")).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        Check(otherViewer.GameObjects.Where(c=>c.ZoneId==101).All(c=>c.GrpId==0),"Private top-card identity leaked to opponent");
        var revoked=ForgeEncoder.Encode(counterFixture.Replace("\"viewers\":[1]","\"viewers\":[]")).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        Check(revoked.GameObjects.Where(c=>c.ZoneId==101).All(c=>c.GrpId==0),"Top-card permission survived removal");
        const string tokenFixture="""
        {"matchId":"token-test","gameStateId":1,"msgId":1,"viewerSeat":1,"turn":{"number":1,"phase":"MAIN1","activeSeat":1,"prioritySeat":1},"players":[{"seat":1,"life":20},{"seat":2,"life":20}],"cards":[
        {"instanceId":1999,"grpId":95424,"ownerSeat":1,"controllerSeat":1,"zone":"Battlefield","viewers":[1,2],"isToken":true}]}
        """;
        var tokenState=ForgeEncoder.Encode(tokenFixture).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        var tokenOld=tokenState.Clone();tokenOld.GameObjects.Clear();tokenOld.Zones.Single(z=>z.ZoneId==30).ObjectInstanceIds.Clear();
        BattlePresentation.Transitions(tokenOld,tokenState,ref id);
        Check(tokenState.GameObjects.Single().Type==GameObjectType.Token && tokenState.GameObjects.Single().UniqueAbilities.Count>0,
            "Native token object/rules text missing");
        Check(tokenState.Annotations.Single().Type.Single()==AnnotationType.TokenCreated,"Token entry animation missing");
        var tokenGone=tokenOld.Clone();BattlePresentation.Transitions(tokenState,tokenGone,ref id);
        Check(tokenGone.Annotations.Single().Type.Single()==AnnotationType.TokenDeleted,"Token sacrifice animation missing");
        // Exercise the exact installed event parsers; these require no Unity scene.
        string managed=@"C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed";
        if(File.Exists(Path.Combine(managed,"SharedClientCore.dll"))) {
            System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (context,name)=> {
                string path=Path.Combine(managed,name.Name+".dll");
                return File.Exists(path)?context.LoadFromAssemblyPath(path):null;
            };
            var assembly=System.Reflection.Assembly.LoadFrom(Path.Combine(managed,"SharedClientCore.dll"));
            BattleAnimationStateTests.Run(assembly);
            BattleArrivalTests.Run(assembly);
            BattleShockMenuTests.Run(assembly);
            BattleGuiTests.Run(assembly);
            using var numericInput=JsonDocument.Parse("""
            {"buttonEventId":91,"input":{"type":"InputChooseNumber","min":0,"max":5,"allowed":[],"sourceId":1057,"grpId":94071,"isX":true}}
            """);
            var numericMessage=GREToClientMessage.Parser.ParseFrom(BattleRequests.Create(numericInput.RootElement,32,4)!.ToByteArray());
            var numericOption=numericMessage.CastingTimeOptionsReq.CastingTimeOptionReq.Single();
            var numericType=assembly.GetType("GreClient.Rules.CastingTimeOption_NumericInputRequest",true)!;
            var nativeNumeric=Activator.CreateInstance(numericType,new object[]{numericOption,numericMessage})!;
            Check((uint)numericType.GetProperty("Max")!.GetValue(nativeNumeric)! == 5 && numericOption.CtoId==91
                && numericOption.NumericInputReq.SourceId==1057 && numericOption.NumericInputReq.NumericInputType==NumericInputType.ChooseX,
                "Installed Arena lost X range, source or request identity");
            using var fightInput=JsonDocument.Parse("""
            {"cards":[],"input":{"type":"InputSelectTargets","divided":false,"sourceId":1057,"min":1,"max":1,"targets":[1002],"selected":[],"validTargetTypes":["Creature.YouDontCtrl"],"description":"Target creature you control fights target creature you don't control.","targetIndex":1}}
            """);
            Check(BattleRequests.Create(fightInput.RootElement,33,4)!.SelectTargetsReq.Targets.Single().Prompt.PromptId==1014,
                "Second fight target showed the friendly creature menu");
            var targetingType=assembly.GetType("GreClient.Rules.SelectTargetsRequest",true)!;
            var nativeTargeting=Activator.CreateInstance(targetingType,new object[]{billRequest.SelectTargetsReq,billRequest})!;
            var nativeSelections=(System.Collections.IList)targetingType.GetField("TargetSelections")!.GetValue(nativeTargeting)!;
            Check((uint)targetingType.GetField("AbilityGrpId")!.GetValue(nativeTargeting)! == 171897
                && (((TargetSelection)nativeSelections[0]!).Prompt.PromptId == 1463 || ((TargetSelection)nativeSelections[0]!).Prompt.PromptId == 1629), "Installed Arena target parser lost Bill's landfall prompt/ability");
            BattleUpdatesTests.Run(assembly);
            BattleAttachmentTests.Run(assembly);
            BattleCombatTests.Run(assembly);
            CommanderTests.Run(assembly);
            GameModeTests.Run(assembly);
            using var targetInput=JsonDocument.Parse("""
            {"stack":[{"instanceId":1001,"sourceGrpId":89183,"description":"Target creature with power 2 or less can't be blocked this turn.","cardName":"Escape Tunnel","targets":[{"index":1,"ids":[1002],"distributions":[]}]}]}
            """);
            var targetBoard=before.Clone(); BattlePresentation.StateTargets(targetInput.RootElement,targetBoard,ref id);
            Check(targetBoard.Annotations.Single().Type.Single()==AnnotationType.TargetSpec,"Persistent target relationship missing");
            var nativeStateType=assembly.GetType("GreClient.Rules.MtgGameState",true)!;
            var nativeState=Activator.CreateInstance(nativeStateType)!;
            var nativeCardType=assembly.GetType("GreClient.Rules.MtgCardInstance",true)!;
            var tokenNative=Activator.CreateInstance(nativeCardType)!;
            nativeCardType.GetField("InstanceId")!.SetValue(tokenNative,1999u);
            ((System.Collections.IDictionary)nativeStateType.GetField("AllCards")!.GetValue(nativeState)!).Add(1999u,tokenNative);
            ((System.Collections.IDictionary)nativeStateType.GetField("VisibleCards")!.GetValue(nativeState)!).Add(1999u,tokenNative);
            ((HashSet<uint>)nativeStateType.GetField("ObjectIds")!.GetValue(nativeState)!).Add(1999u);
            foreach(var pair in new[]{(tokenState,"TokenCreatedEventParser","CardCreatedEvent"),(tokenGone,"TokenDeletedEventParser","CardDeletedEvent")}) {
                var tokenParserType=assembly.GetType("GreClient.Rules.Annotations."+pair.Item2,true)!;
                var tokenParser=Activator.CreateInstance(tokenParserType)!;
                if(tokenParserType.GetProperty("newState") is {} tokenNewState)tokenNewState.SetValue(tokenParser,nativeState);
                var tokenEvents=(System.Collections.IEnumerable)tokenParserType.GetMethod("Process")!.Invoke(tokenParser,new object[]{pair.Item1.Annotations.Single()})!;
                Check(tokenEvents.Cast<object>().Single().GetType().Name==pair.Item3,"Installed Arena rejected "+pair.Item2);
            }
            var xCard=Activator.CreateInstance(nativeCardType)!;
            nativeCardType.GetField("InstanceId")!.SetValue(xCard,1057u);
            ((System.Collections.IDictionary)nativeStateType.GetField("AllCards")!.GetValue(nativeState)!).Add(1057u,xCard);
            ((System.Collections.IDictionary)nativeStateType.GetField("VisibleCards")!.GetValue(nativeState)!).Add(1057u,xCard);
            ((HashSet<uint>)nativeStateType.GetField("ObjectIds")!.GetValue(nativeState)!).Add(1057u);
            using var xState=JsonDocument.Parse("""
            {"stack":[{"instanceId":1057,"isAbility":false,"xValue":2}]}
            """);
            var xBoard=new GameStateMessage();BattlePresentation.StateCastingChoices(xState.RootElement,xBoard);
            var xParser=assembly.GetType("GreClient.Rules.Annotations.CastingTimeOptionAnnotationParser",true)!;
            xParser.GetMethod("Process")!.Invoke(Activator.CreateInstance(xParser),new[]{(object)xBoard.PersistentAnnotations.Single(),nativeState,nativeState});
            Check(Convert.ToUInt32(nativeCardType.GetField("ChooseXResult")!.GetValue(xCard))==2,"Native stack display lost selected X");
            using var shieldSnapshot=JsonDocument.Parse("""
            {"cards":[{"instanceId":1057,"counters":[],"regenerationShields":2}],"players":[]}
            """);
            var shieldBoard=new GameStateMessage {GameObjects={new GameObjectInfo {InstanceId=1057,GrpId=75212}}};
            BattlePresentation.StateCounters(shieldSnapshot.RootElement,shieldBoard);
            var shieldAnnotation=shieldBoard.PersistentAnnotations.Single();
            Check(shieldAnnotation.Type.Contains(AnnotationType.RegeneratePending) && shieldAnnotation.AffectedIds.SequenceEqual(new uint[]{1057,1057}),
                "Multiple regeneration shields were collapsed");
            var shieldParser=assembly.GetType("GreClient.Rules.Annotations.RegeneratePendingStateAnnotationParser",true)!;
            shieldParser.GetMethod("Process")!.Invoke(Activator.CreateInstance(shieldParser),new object[]{shieldAnnotation,nativeState,nativeState});
            Check(Convert.ToInt32(nativeCardType.GetField("RegenerateCount")!.GetValue(xCard))==2,"Installed Arena lost pending regeneration shields");
            object MakeCounterState(GameStateMessage encoded) {
                var state=Activator.CreateInstance(nativeStateType)!;var card=Activator.CreateInstance(nativeCardType)!;
                nativeCardType.GetField("InstanceId")!.SetValue(card,1001u);
                ((System.Collections.IDictionary)nativeStateType.GetField("AllCards")!.GetValue(state)!).Add(1001u,card);
                ((System.Collections.IDictionary)nativeStateType.GetField("VisibleCards")!.GetValue(state)!).Add(1001u,card);
                ((HashSet<uint>)nativeStateType.GetField("ObjectIds")!.GetValue(state)!).Add(1001u);
                var parser=assembly.GetType("GreClient.Rules.Annotations.CounterStateAnnotationParser",true)!;
                foreach(var annotation in encoded.PersistentAnnotations)parser.GetMethod("Process")!.Invoke(Activator.CreateInstance(parser),new[]{(object)annotation,state,state});
                return state;
            }
            foreach(var pair in new[]{(counterBefore,counterAfter,"CounterAddedAnnotationParser",4),(counterAfter,counterEmpty,"CounterRemovedAnnotationParser",0)}) {
                var oldNative=MakeCounterState(pair.Item1);var nextNative=MakeCounterState(pair.Item2);
                var parserType=assembly.GetType("GreClient.Rules.Annotations."+pair.Item3,true)!;var parser=Activator.CreateInstance(parserType)!;
                parserType.GetProperty("oldState")!.SetValue(parser,oldNative);parserType.GetProperty("newState")!.SetValue(parser,nextNative);
                parserType.GetProperty("changes")!.SetValue(parser,Array.CreateInstance(assembly.GetType("GreClient.Rules.GameRulesEvent",true)!,0));
                var output=(System.Collections.IEnumerable)parserType.GetMethod("Process")!.Invoke(parser,new object[]{pair.Item2.Annotations.Single()})!;
                var change=output.Cast<object>().Single();
                var entity=change.GetType().GetField("NewInstance")!.GetValue(change)!;
                var counters=(System.Collections.IDictionary)entity.GetType().GetField("Counters")!.GetValue(entity)!;
                Check((counters.Contains(CounterType.P1P1)?(int)counters[CounterType.P1P1]!:0)==pair.Item4,"Native counter parser rejected typed count/change");
            }
            var specParserType=assembly.GetType("GreClient.Rules.Annotations.TargetSpecStateAnnotationParser",true)!;
            specParserType.GetMethod("Process")!.Invoke(Activator.CreateInstance(specParserType),new[]{(object)targetBoard.Annotations.Single(),nativeState,nativeState});
            var targetSpecs=(System.Collections.IList)nativeStateType.GetField("TargetInfo")!.GetValue(nativeState)!;
            Check(targetSpecs.Count==1 && (uint)targetSpecs[0]!.GetType().GetProperty("Index")!.GetValue(targetSpecs[0])! == 0,"Installed target parser rejected one-based targeting index");
            var connect=new GREToClientMessage { Type=GREMessageType.ConnectResp,
                ConnectResp=new ConnectResp { Status=ConnectionStatus.Success,Settings=new BattleFlow().SnapshotSettings() } };
            var connectType=assembly.GetType("GreClient.Rules.Notifications.ConnectResponseNotificationParser",true)!;
            var notification=connectType.GetMethod("Translate")!.Invoke(Activator.CreateInstance(connectType),new object[]{GREToClientMessage.Parser.ParseFrom(connect.ToByteArray())})!;
            Check(notification.GetType().GetField("Settings")!.GetValue(notification)!=null,"Connect response produced null native settings");
            foreach(var pair in new[]{(0,"ResolutionStartEventParser"),(3,"ResolutionCompleteEventParser"),(5,"ShuffleAnnotationParser")}) {
                var parserType=assembly.GetType("GreClient.Rules.Annotations."+pair.Item2,true)!;
                var parser=Activator.CreateInstance(parserType)!;
                var output=(System.Collections.IEnumerable)parserType.GetMethod("Process")!.Invoke(parser,new object[]{board.Annotations[pair.Item1]})!;
                Check(output.Cast<object>().Count()==1,"Installed Arena parser rejected "+pair.Item2);
            }
            foreach(var pair in new[]{(randomBoard.Annotations[0],"CoinFlipEventParser"),(randomBoard.Annotations[1],"DieRollResultEventAnnotationParser"),
                    (randomBoard.Annotations[2],"DieRollResultEventAnnotationParser"),(extraBoard.Annotations[1],"PermanentRegeneratedEventAnnotationParser"),
                    (typeResult,"ChoiceResultEventParser")}) {
                var parserType=assembly.GetType("GreClient.Rules.Annotations."+pair.Item2,true)!;
                var parser=Activator.CreateInstance(parserType)!;
                parserType.GetProperty("oldState")?.SetValue(parser,nativeState);
                parserType.GetProperty("newState")?.SetValue(parser,nativeState);
                var output=(System.Collections.IEnumerable)parserType.GetMethod("Process")!.Invoke(parser,new object[]{pair.Item1})!;
                Check(output.Cast<object>().Any(),"Installed Arena parser rejected "+pair.Item2);
            }
        }
        Console.WriteLine("PASS: ordered playback, native resolution/shuffle parsers, damage data, no duplicate transitions or historical replay.");
    }
    public static void RunFile(string path)
    {
        Run();
        using var fixture=JsonDocument.Parse(File.ReadAllText(path));
        int annotations=0, damage=0, resolutions=0;
        uint id=1;
        foreach(var pair in fixture.RootElement.EnumerateArray()) {
            var previous=pair.GetProperty("previous"); var current=pair.GetProperty("current");
            var before=ForgeEncoder.Encode(previous.GetRawText()).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
            var board=ForgeEncoder.Encode(current.GetRawText()).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
            BattlePresentation.StateTargets(current,board,ref id);
            var result=BattlePresentation.Playback(current,before,board,previous.GetProperty("playbackId").GetInt64(),ref id);
            BattlePresentation.Transitions(before,board,ref id,result);
            Check(GameStateMessage.Parser.ParseFrom(board.ToByteArray()).Annotations.Count==board.Annotations.Count,"Real Forge playback failed roundtrip");
            BattleUpdatesTests.CheckRealUpdate(before,board);
            damage+=board.Annotations.Count(a=>a.Type.Contains(AnnotationType.DamageDealt));
            resolutions+=board.Annotations.Count(a=>a.Type.Contains(AnnotationType.ResolutionStart));
            annotations+=board.Annotations.Count;
        }
        Check(damage>0 && resolutions>0,"Real Forge effects were filtered out");
        Console.WriteLine($"PASS: real Forge snapshots encoded {annotations} playback annotations, including {damage} damage and {resolutions} resolutions.");
    }
}
