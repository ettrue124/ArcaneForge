using System.Reflection;
using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

static class BattleCombatTests
{
    public static void Run(Assembly native)
    {
        var type=native.GetType("GreClient.Rules.GreInterface",true)!;
        var database=native.GetType("Wotc.Mtga.Cards.Database.NullCardDatabaseAdapter",true)!.GetField("Default")!.GetValue(null)!;
        var gre=Activator.CreateInstance(type,new object?[]{database,null,null,true,null})!;
        var process=type.GetMethod("ProcessMessage",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var retrieve=type.GetMethod("RetrieveClientUpdates")!;
        GameStateMessage? previous=null; uint sequence=0;
        foreach(uint target in new uint[]{0,1,1003,0}) {
            bool attacking=target!=0;
            var cards=new object[]{
                new {instanceId=1001,grpId=79334,ownerSeat=2,controllerSeat=2,zone="Battlefield",attackTarget=target,attackState=attacking?1:0,blockState=attacking?3:0,blockedBy=attacking?new[]{1002}:Array.Empty<int>()},
                new {instanceId=1002,grpId=79334,ownerSeat=1,controllerSeat=1,zone="Battlefield",blockState=attacking?1:0,blocking=attacking?new[]{1001}:Array.Empty<int>()},
                new {instanceId=1003,grpId=79334,ownerSeat=1,controllerSeat=1,zone="Battlefield"}
            };
            var board=ForgeEncoder.Encode(JsonSerializer.Serialize(new {matchId="combat-check",gameStateId=++sequence,msgId=sequence,viewerSeat=1,
                turn=new {number=1,phase="COMBAT_DECLARE_BLOCKERS",activeSeat=2,prioritySeat=1},players=new[]{new {seat=1,life=20},new {seat=2,life=20}},cards}))
                .GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
            process.Invoke(gre,new object[]{new GREToClientMessage {Type=GREMessageType.GameStateMessage,MsgId=sequence,GameStateId=sequence,SystemSeatIds={1},GameStateMessage=BattleUpdates.WireState(board,previous)}});
            var update=((System.Collections.IEnumerable)retrieve.Invoke(gre,null)!).Cast<object>().Single(u=>u.GetType().Name=="GameStateUpdate");
            var state=update.GetType().GetField("NewState")!.GetValue(update)!;
            var objects=(System.Collections.IDictionary)state.GetType().GetField("AllCards")!.GetValue(state)!;
            var attacker=objects[1001u]!;var blocker=objects[1002u]!;
            if((uint)attacker.GetType().GetField("AttackTargetId")!.GetValue(attacker)! != target)throw new Exception("Native attack target missing");
            var blockedBy=(System.Collections.IEnumerable)attacker.GetType().GetField("BlockedByIds")!.GetValue(attacker)!;
            var blocking=(System.Collections.IEnumerable)blocker.GetType().GetField("BlockingIds")!.GetValue(blocker)!;
            if(blockedBy.Cast<uint>().Contains(1002u)!=attacking || blocking.Cast<uint>().Contains(1001u)!=attacking)throw new Exception("Native combat links missing or stale");
            if(previous!=null) {
                var changes=((System.Collections.IEnumerable)update.GetType().GetField("Changes")!.GetValue(update)!).Cast<object>();
                if(!changes.Any(c=>c.GetType().Name=="AttackSelectionEvent"))throw new Exception("Native attack target animation event missing");
            }
            previous=board.Clone();
        }
        var pacer=new StackPlaybackPacer();
        if(pacer.Ready(1,true,0,650) || pacer.Ready(1,true,649,650) || !pacer.Ready(1,true,650,650))throw new Exception("Combat advances before its animation pause");
        Console.WriteLine("PASS: native combat targets, attacker/blocker links, cleanup, attack animation events and combat pacing.");
    }
}
