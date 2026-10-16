using System.Reflection;
using System.Text.Json;
using Google.Protobuf;
using Wotc.Mtgo.Gre.External.Messaging;

static class CommanderTests
{
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    public static void Run(Assembly native)
    {
        var greType=native.GetType("GreClient.Rules.GreInterface",true)!;
        var database=native.GetType("Wotc.Mtga.Cards.Database.NullCardDatabaseAdapter",true)!.GetField("Default")!.GetValue(null)!;
        var process=greType.GetMethod("ProcessMessage",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var retrieve=greType.GetMethod("RetrieveClientUpdates")!;
        foreach(int count in new[]{2,4}) {
            var json=JsonSerializer.Serialize(new {matchId="commander-native-test",gameStateId=1,msgId=1,viewerSeat=1,
                format="Commander",startingLife=40,turn=new{number=1,phase="MAIN1",activeSeat=count,prioritySeat=1},
                players=Enumerable.Range(1,count).Select(s=>new{seat=s,life=40,counters=s==1?new[]{new{type="ENERGY",count=3},new{type="POISON",count=2}}:Array.Empty<object>()}),
                cards=Enumerable.Range(1,count).SelectMany(s=>new object[]{
                    new{instanceId=1000+s,grpId=90503,ownerSeat=s,controllerSeat=s,zone="Command",commander=true,commanderCastCount=2},
                    new{instanceId=2000+s,grpId=6993,ownerSeat=s,controllerSeat=s,zone="Hand",commander=false,commanderCastCount=0}})});
            var message=ForgeEncoder.Encode(json).GreToClientEvent.GreToClientMessages.Single();
            var board=message.GameStateMessage;
            Check(board.GameInfo.Variant==GameVariant.Commander && board.GameInfo.Type==(count==4?GameType.MultiPlayer:GameType.Duel),"Wrong Commander game mode");
            Check(board.Players.Count==count && board.Players.All(p=>p.StartingLifeTotal==40),"Commander lost player seats/life");
            Check(board.PersistentAnnotations.Count(a=>a.Type.Contains(AnnotationType.Designation))==count,"Commander designation missing");
            Check(board.PersistentAnnotations.Count(a=>a.Type.Contains(AnnotationType.Counter) && a.AffectedIds.Single()==1)==2,"Player energy/poison counters missing");
            Check(board.GameObjects.Where(c=>c.ZoneId!=102 && c.ZoneId%100==2).All(c=>c.GrpId==0),"Four-player hands leaked");
            var gre=Activator.CreateInstance(greType,new object?[]{database,null,null,true,null})!;
            var action=new GREToClientMessage {Type=GREMessageType.ActionsAvailableReq,GameStateId=1,MsgId=2,SystemSeatIds={1},
                ActionsAvailableReq=new ActionsAvailableReq{Actions={new Wotc.Mtgo.Gre.External.Messaging.Action{ActionType=ActionType.Pass}}}};
            var batch=BattleUpdates.Batch(message,new[]{action});
            foreach(var part in batch.GreToClientEvent.GreToClientMessages)process.Invoke(gre,new object[]{part});
            var updates=((System.Collections.IEnumerable)retrieve.Invoke(gre,null)!).Cast<object>();
            var update=updates.Single(u=>u.GetType().Name=="GameStateUpdate");
            var state=update.GetType().GetField("NewState")!.GetValue(update)!;
            object?[] entityArguments={1u,null};
            Check((bool)state.GetType().GetMethod("TryGetEntity")!.Invoke(state,entityArguments)!,"Native player entity missing");
            var playerCounters=(System.Collections.IDictionary)entityArguments[1]!.GetType().GetField("Counters")!.GetValue(entityArguments[1])!;
            Check((int)(playerCounters[CounterType.Energy]!)==3 && (int)(playerCounters[CounterType.Poison]!)==2,"Installed Arena lost player energy/poison state");
            Check((bool)state.GetType().GetProperty("IsMultiplayer")!.GetValue(state)! == (count==4),"Installed Arena did not recognize multiplayer");
            var commanders=((System.Collections.IEnumerable)state.GetType().GetField("Designations")!.GetValue(state)!).Cast<object>().ToArray();
            Check(commanders.Length==count,"Installed Arena did not consume commander designations");
            // Elimination must retain seat identity; it cannot end other players' game.
            using var eliminated=JsonDocument.Parse(json.Replace("\"seat\":2,\"life\":40","\"seat\":2,\"life\":0,\"lost\":true"));
            var removed=ForgeEncoder.Encode(eliminated.RootElement.GetRawText()).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
            Check(removed.Players.Single(p=>p.SystemSeatNumber==2).Status==PlayerStatus.Removed && removed.GameInfo.Stage!=GameStage.GameOver,"Elimination ended an ongoing game or changed seat");
            BattleUpdatesTests.CheckRealUpdate(board,removed);
            using var final=JsonDocument.Parse(json.TrimEnd('}')+",\"stage\":3,\"winnerSeat\":"+count+",\"lossReason\":\"LifeReachedZero\"}");
            var result=ForgeEncoder.Encode(final.RootElement.GetRawText()).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
            uint id=1;BattleCompletion.LossAnnotation(final.RootElement,result,ref id);
            Check(result.GameInfo.Results.All(r=>r.WinningTeamId==count) && result.Annotations.Single().AffectedIds.All(result.Players.Select(p=>p.SystemSeatNumber).Contains),"Four-player completion invented a seat/winner");
        }
        using var returnPrompt=JsonDocument.Parse("""
        {"input":{"type":"InputConfirm","startingPlayer":false,"sourceId":1001,"message":"Move your commander to the command zone?","cardName":"Bristly Bill, Spine Sower"}}
        """);
        Check(BattleRequests.Create(returnPrompt.RootElement,1,1)!.OptionalActionMessage.Prompt.PromptId==144,"Commander return used sacrifice prompt");
        using var attack=JsonDocument.Parse("""
        {"input":{"type":"InputAttack","attackers":[{"instanceId":1001,"defenders":[2,3,4],"selected":0}]}}
        """);
        var defenders=BattleRequests.Create(attack.RootElement,1,1)!.DeclareAttackersReq.Attackers.Single().LegalDamageRecipients;
        Check(defenders.All(r=>r.Type==DamageRecType.Player) && defenders.Select(BattleRequests.RecipientId).SequenceEqual(new uint[]{2,3,4}),"Fourth combat defender was treated as a planeswalker");
        Console.WriteLine("PASS: installed Arena parses two/four-seat Commander, designations/tax, private hands, elimination and fourth-seat results.");
    }
}
