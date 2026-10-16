using System.Reflection;
using Wotc.Mtgo.Gre.External.Messaging;

static class BattleUpdatesTests
{
    static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    public static void Run(Assembly native)
    {
        var greType=native.GetType("GreClient.Rules.GreInterface",true)!;
        var databaseType=native.GetType("Wotc.Mtga.Cards.Database.NullCardDatabaseAdapter",true)!;
        var database=databaseType.GetField("Default")!.GetValue(null)!;
        var gre=Activator.CreateInstance(greType,new object?[]{database,null,null,true,null})!;
        var merge=greType.GetMethod("MergeDiffMessageIntoFullMessage",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var before=new GameStateMessage {
            Type=GameStateType.Full,GameStateId=1,
            GameObjects={
                new GameObjectInfo {InstanceId=1001,ZoneId=30,IsTapped=true},
                new GameObjectInfo {InstanceId=1002,ZoneId=101,GrpId=6993,Visibility=Visibility.Public,Viewers={1}},
                new GameObjectInfo {InstanceId=1003,ZoneId=30},
                new GameObjectInfo {InstanceId=1000001,Type=GameObjectType.Ability,ZoneId=40}},
            Zones={new ZoneInfo {ZoneId=101,ObjectInstanceIds={1002}},new ZoneInfo {ZoneId=40,ObjectInstanceIds={1000001}}},
            PersistentAnnotations={new AnnotationInfo {Id=99,Type={AnnotationType.Counter}},new AnnotationInfo {Id=100,Type={AnnotationType.Counter}}},
            Players={new PlayerInfo {SystemSeatNumber=1,ManaPool={new ManaInfo {ManaId=16069,Color=ManaColor.Green,Count=1,SrcInstanceId=1001}}}}
        };
        var current=before.Clone();current.GameStateId=2;
        current.GameObjects[0].IsTapped=false;
        current.GameObjects[1].GrpId=0;current.GameObjects[1].Visibility=Visibility.Hidden;current.GameObjects[1].Viewers.Clear();
        current.GameObjects.RemoveAt(3);current.Zones[1].ObjectInstanceIds.Clear();
        current.PersistentAnnotations.RemoveAt(0);current.PersistentAnnotations[0].AffectedIds.Add(1003);
        current.Players[0].ManaPool.Clear();
        var wire=BattleUpdates.WireState(current,before);
        Check(wire.Type==GameStateType.Diff && wire.PrevGameStateId==1 && wire.GameObjects.Count==2,"Incremental update resent unchanged objects or lost sequence");
        var merged=before.Clone();merge.Invoke(gre,new object[]{merged,wire});
        Check(merged.GameObjects.OrderBy(c=>c.InstanceId).SequenceEqual(current.GameObjects.OrderBy(c=>c.InstanceId)),"Native merge left stale taps, library visibility or removed stack objects");
        Check(merged.PersistentAnnotations.SequenceEqual(current.PersistentAnnotations) && merged.Zones.SequenceEqual(current.Zones),"Native merge retained deleted counters or old zone membership");
        Check(merged.Players[0].ManaPool.Count==0,"Native merge retained spent mana");
        Check(BattleUpdates.WireState(current,null).Type==GameStateType.Full,"Initial connection did not receive a full board");

        // Exercise the actual client pipeline: no board should reach playback
        // until its interaction and settings messages have both arrived.
        const string fixture="""
        {"matchId":"batch-test","gameStateId":1,"msgId":1,"viewerSeat":1,"turn":{"number":1,"phase":"MAIN1","activeSeat":1,"prioritySeat":1},"players":[{"seat":1,"life":20},{"seat":2,"life":20}],"cards":[]}
        """;
        var state=ForgeEncoder.Encode(fixture).GreToClientEvent.GreToClientMessages.Single();
        var settings=new GREToClientMessage {Type=GREMessageType.SetSettingsResp,GameStateId=1,MsgId=2,SystemSeatIds={1},SetSettingsResp=new SetSettingsResp {Settings=new BattleFlow().SnapshotSettings()}};
        var prompt=new GREToClientMessage {Type=GREMessageType.ActionsAvailableReq,GameStateId=1,MsgId=3,SystemSeatIds={1},ActionsAvailableReq=new ActionsAvailableReq {Actions={new Wotc.Mtgo.Gre.External.Messaging.Action {ActionType=ActionType.Pass}}}};
        prompt.AllowUndo=true;
        var batch=BattleUpdates.Batch(state,new[]{settings,prompt});
        var process=greType.GetMethod("ProcessMessage",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var retrieve=greType.GetMethod("RetrieveClientUpdates")!;
        object[] Updates()=>((System.Collections.IEnumerable)retrieve.Invoke(gre,null)!).Cast<object>().ToArray();
        process.Invoke(gre,new object[]{batch.GreToClientEvent.GreToClientMessages[0]});
        Check(!Updates().Any(c=>c.GetType().Name=="GameStateUpdate"),"Board escaped before its prompt arrived");
        process.Invoke(gre,new object[]{settings});
        Check(!Updates().Any(c=>c.GetType().Name=="GameStateUpdate"),"Settings prematurely released board playback");
        process.Invoke(gre,new object[]{prompt});
        var update=Updates().Single(c=>c.GetType().Name=="GameStateUpdate");
        Check(update.GetType().GetField("Interaction")!.GetValue(update)!=null,"Native board update lost its interaction");
        var interaction=update.GetType().GetField("Interaction")!.GetValue(update)!;
        Check((bool)interaction.GetType().GetProperty("AllowUndo")!.GetValue(interaction)!,"Native interaction did not enable undo");
        ClientToGREMessage? undoMessage=null;
        interaction.GetType().GetField("OnSubmit")!.SetValue(interaction,new System.Action<ClientToGREMessage>(m=>undoMessage=m));
        interaction.GetType().GetMethod("Undo")!.Invoke(interaction,null);
        Check(undoMessage?.Type==ClientMessageType.UndoReq && undoMessage.RespId==0 && undoMessage.GameStateId==1,"Installed Arena emitted an unexpected undo request");
        state.GameStateMessage.GameStateId=2;state.GameStateId=2;
        state.GameStateMessage.TurnInfo.TurnNumber=2;state.GameStateMessage.TurnInfo.ActivePlayer=2;
        state.GameStateMessage.TurnInfo.Phase=Phase.Combat;state.GameStateMessage.TurnInfo.Step=Step.DeclareAttack;
        uint phaseAnnotation=500;
        BattlePresentation.Transitions(ForgeEncoder.Encode(fixture).GreToClientEvent.GreToClientMessages.Single().GameStateMessage,state.GameStateMessage,ref phaseAnnotation);
        state.GameStateMessage=BattleUpdates.WireState(state.GameStateMessage,ForgeEncoder.Encode(fixture).GreToClientEvent.GreToClientMessages.Single().GameStateMessage);
        prompt.GameStateId=2;prompt.MsgId=5;
        var nextBatch=BattleUpdates.Batch(state,new[]{prompt});
        foreach(var message in nextBatch.GreToClientEvent.GreToClientMessages)process.Invoke(gre,new object[]{message});
        var nextUpdate=Updates().Single(c=>c.GetType().Name=="GameStateUpdate");
        Check(!(bool)nextUpdate.GetType().GetField("IsFullGameState")!.GetValue(nextUpdate)! && nextUpdate.GetType().GetField("Interaction")!.GetValue(nextUpdate)!=null,"Native diff update or grouped interaction failed");
        var changes=((System.Collections.IEnumerable)nextUpdate.GetType().GetField("Changes")!.GetValue(nextUpdate)!).Cast<object>().ToArray();
        Check(changes.Count(c=>c.GetType().Name=="TurnChangeEvent")==1 && changes.Count(c=>c.GetType().Name=="GamePhaseChangeEvent")==1,"Native diff did not generate turn/phase ladder playback events");
        var phaseEvent=changes.Single(c=>c.GetType().Name=="GamePhaseChangeEvent");
        Check((Phase)phaseEvent.GetType().GetField("Phase")!.GetValue(phaseEvent)! == Phase.Combat
            && (Step)phaseEvent.GetType().GetField("Step")!.GetValue(phaseEvent)! == Step.DeclareAttack,"Native phase ladder event used the wrong phase/step");
        var samePhase=state.GameStateMessage.Clone();samePhase.Annotations.Clear();
        BattlePresentation.Transitions(state.GameStateMessage,samePhase,ref phaseAnnotation);
        Check(samePhase.Annotations.Count==0,"Unchanged phase/turn replayed its playback events");
        foreach(var ending in new[] {
            (Winner:1,Draw:false,Loss:"LifeReachedZero",NativeLoss:"LifeTotal",Reason:ResultReason.Game),
            (Winner:2,Draw:false,Loss:"Conceded",NativeLoss:"Concede",Reason:ResultReason.Concede),
            (Winner:0,Draw:true,Loss:"IntentionalDraw",NativeLoss:"Other",Reason:ResultReason.Game)
        }) {
            gre=Activator.CreateInstance(greType,new object?[]{database,null,null,true,null})!;
            var initial=ForgeEncoder.Encode(fixture).GreToClientEvent.GreToClientMessages.Single();
            process.Invoke(gre,new object[]{initial});Updates();
            string endingJson=fixture.Replace("\"cards\":[]",$"\"cards\":[],\"stage\":3,\"winnerSeat\":{ending.Winner},\"draw\":{ending.Draw.ToString().ToLowerInvariant()},\"lossReason\":\"{ending.Loss}\"");
            using var outcome=System.Text.Json.JsonDocument.Parse(endingJson);
            var terminal=ForgeEncoder.Encode(endingJson,10,2).GreToClientEvent.GreToClientMessages.Single();
            uint lossId=1000;
            BattleCompletion.LossAnnotation(outcome.RootElement,terminal.GameStateMessage,ref lossId);
            var finalBoard=terminal.GameStateMessage.Clone();
            terminal.GameStateMessage=BattleUpdates.WireState(finalBoard,initial.GameStateMessage);
            var intermission=BattleCompletion.Intermission(finalBoard,11,1);
            var finalBatch=BattleUpdates.Batch(terminal,new[]{intermission});
            process.Invoke(gre,new object[]{finalBatch.GreToClientEvent.GreToClientMessages[0]});
            Check(!Updates().Any(c=>c.GetType().Name=="GameStateUpdate"),"Terminal board escaped without Arena intermission");
            process.Invoke(gre,new object[]{intermission});
            var ended=Updates().Single(c=>c.GetType().Name=="GameStateUpdate");
            var endedInteraction=ended.GetType().GetField("Interaction")!.GetValue(ended)!;
            Check(endedInteraction.GetType().Name=="IntermissionRequest","Terminal update lost the workflow that releases Arena's match scene lock");
            Check((uint)endedInteraction.GetType().GetField("WinningTeamId")!.GetValue(endedInteraction)! == (uint)ending.Winner,"Intermission declared wrong winner");
            var endedChanges=((System.Collections.IEnumerable)ended.GetType().GetField("Changes")!.GetValue(ended)!).Cast<object>().ToArray();
            var endEvent=endedChanges.Single(c=>c.GetType().Name=="GameEndEvent");
            Check((ResultReason)endEvent.GetType().GetField("Reason")!.GetValue(endEvent)! == ending.Reason,"Native game-end playback lost its reason");
            var nativeBoard=ended.GetType().GetField("NewState")!.GetValue(ended)!;
            var lossData=nativeBoard.GetType().GetField("GameLossData")!.GetValue(nativeBoard)!;
            string parsedLoss=lossData.GetType().GetField("Reason")!.GetValue(lossData)!.ToString()!;
            Check(parsedLoss==ending.NativeLoss,$"Native defeat animation received {parsedLoss}, expected {ending.NativeLoss}");
            var notification=BattleCompletion.Notification(finalBoard).MatchGameRoomStateChangedEvent.GameRoomInfo;
            Check(notification.StateType==MatchGameRoomStateType.MatchCompleted && notification.FinalMatchResult.ResultList.SequenceEqual(finalBoard.GameInfo.Results),"Room completion omitted/disagreed with canonical results");
        }
        using(var ongoing=System.Text.Json.JsonDocument.Parse(fixture.Replace("\"life\":20","\"life\":-10")))
            Check(BattleCompletion.Results(ongoing.RootElement).Length==0,"Adapter declared a winner from life totals before Forge's state-based actions");
        Console.WriteLine("PASS: installed Arena consumes grouped lethal/concede/draw intermissions with game-end events and matching final results.");
        Console.WriteLine("PASS: installed Arena merges incremental state and waits for grouped settings/input before playback.");
    }
    public static void CheckRealUpdate(GameStateMessage previous,GameStateMessage current)
    {
        var native=Assembly.LoadFrom(@"C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed\SharedClientCore.dll");
        var greType=native.GetType("GreClient.Rules.GreInterface",true)!;
        var gre=System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(greType);
        var merged=previous.Clone();
        greType.GetMethod("MergeDiffMessageIntoFullMessage",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(gre,new object[]{merged,BattleUpdates.WireState(current,previous)});
        Check(merged.GameObjects.OrderBy(c=>c.InstanceId).SequenceEqual(current.GameObjects.OrderBy(c=>c.InstanceId))
            && merged.PersistentAnnotations.OrderBy(c=>c.Id).SequenceEqual(current.PersistentAnnotations.OrderBy(c=>c.Id))
            && merged.Zones.SequenceEqual(current.Zones) && merged.Players.SequenceEqual(current.Players)
            && merged.Annotations.SequenceEqual(current.Annotations) && merged.Actions.SequenceEqual(current.Actions),
            "Real Forge incremental update diverged from canonical board in installed Arena merger");
    }
}
