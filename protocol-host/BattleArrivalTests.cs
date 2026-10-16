using System.Reflection;
using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

static class BattleArrivalTests
{
    static void Check(bool value,string message) {if(!value)throw new Exception(message);}
    public static void Run(Assembly native) {
        var greType=native.GetType("GreClient.Rules.GreInterface",true)!;
        var database=native.GetType("Wotc.Mtga.Cards.Database.NullCardDatabaseAdapter",true)!.GetField("Default")!.GetValue(null)!;
        var gre=Activator.CreateInstance(greType,new object?[]{database,null,null,true,null})!;
        var process=greType.GetMethod("ProcessMessage",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var retrieve=greType.GetMethod("RetrieveClientUpdates")!;
        uint sequence=0,id=1;long cursor=0;GameStateMessage? previous=null;
        object[] Publish(uint instance,string zone,object[] events) {
            string json=JsonSerializer.Serialize(new {matchId="arrivals",gameStateId=++sequence,msgId=sequence,viewerSeat=1,
                turn=new {number=1,phase="MAIN1",activeSeat=1,prioritySeat=1},players=new[]{new {seat=1,life=20},new {seat=2,life=20}},
                cards=new[]{new {instanceId=instance,grpId=75212,ownerSeat=1,controllerSeat=1,zone,viewers=new[]{1},
                    power=1,toughness=1,cardTypes=new[]{"Creature"},subtypes=new[]{"Elf"},supertypes=Array.Empty<string>(),colors=new[]{5},counters=Array.Empty<object>()}},
                playbackId=events.Length==0?cursor:cursor+events.Length,playbackEvents=events});
            using var snapshot=JsonDocument.Parse(json);
            var board=ForgeEncoder.Encode(json).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
            var playback=BattlePresentation.Playback(snapshot.RootElement,previous,board,cursor,ref id);
            BattlePresentation.Transitions(previous,board,ref id,playback);cursor=playback.Cursor;
            process.Invoke(gre,new object[]{new GREToClientMessage {Type=GREMessageType.GameStateMessage,SystemSeatIds={1},MsgId=sequence,
                GameStateId=sequence,GameStateMessage=BattleUpdates.WireState(board,previous)}});
            previous=board.Clone();
            var update=((System.Collections.IEnumerable)retrieve.Invoke(gre,null)!).Cast<object>().Single(u=>u.GetType().Name=="GameStateUpdate");
            var state=update.GetType().GetField("NewState")!.GetValue(update)!;
            var cards=(System.Collections.IDictionary)state.GetType().GetField("AllCards")!.GetValue(state)!;
            Check(cards.Contains(instance),"Native client lost the arriving card");
            Check(!cards.Contains(instance==1001?900000u:1001u),"Native client retained the replaced identity");
            return ((System.Collections.IEnumerable)update.GetType().GetField("Changes")!.GetValue(update)!).Cast<object>().ToArray();
        }
        Publish(1001,"Hand",Array.Empty<object>());
        var changes=Publish(900000,"Battlefield",new object[]{
            new {eventId=1,type="objectIdChanged",sourceId=1001,targetId=900000},
            new {eventId=2,type="zoneTransfer",targetId=900000,fromZone=101,toZone=30,category="Resolve"}});
        Check(changes.Count(c=>c.GetType().Name=="InstanceIdChange")==1 && changes.Count(c=>c.GetType().Name=="ZoneChangeEvent")==1,
            "Native client lost arrival identity/zone events");
        Check(!Publish(900000,"Battlefield",Array.Empty<object>()).Any(c=>c.GetType().Name is "InstanceIdChange" or "ZoneChangeEvent"),"Arrival replayed on unchanged state");
        Publish(900000,"Hand",new object[]{new {eventId=3,type="zoneTransfer",targetId=900000,fromZone=30,toZone=101,category="Bounce"}});
        var returned=Publish(899999,"Battlefield",new object[]{
            new {eventId=4,type="objectIdChanged",sourceId=900000,targetId=899999},
            new {eventId=5,type="zoneTransfer",targetId=899999,fromZone=101,toZone=30,category="Resolve"}});
        Check(returned.Count(c=>c.GetType().Name=="InstanceIdChange")==1,"Returning card did not receive its new arrival identity");
        using var confirm=JsonDocument.Parse("""
        {"input":{"type":"InputConfirm","startingPlayer":false,"sourceId":900001,"freeCastCardId":1002},
         "cards":[{"instanceId":1002,"grpId":69147}],"stack":[{"instanceId":1000100,"parentId":900001}]}
        """);
        var request=BattleRequests.Create(confirm.RootElement,1,1)!;
        var core=Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(native.Location)!,"Core.dll"));
        var translation=core.GetType("Wotc.Mtga.DuelScene.Interactions.ActionsAvailable.ResolutionCastTranslation",true)!;
        var route=translation.GetMethods().Single(m=>m.Name=="IsResolutionCast" && m.GetParameters()[0].ParameterType==typeof(uint));
        Check((bool)route.Invoke(null,new object[]{1000100u,request.ActionsAvailableReq.Actions,request.ActionsAvailableReq.InactiveActions})!,
            "Installed Arena did not route free casting to its resolution browser");
        Console.WriteLine("PASS: installed Arena merges arrival identities once and selects its native free-cast browser.");
    }
}
