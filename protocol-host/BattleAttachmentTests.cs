using System.Reflection;
using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

static class BattleAttachmentTests
{
    static void Check(bool condition,string message) { if(!condition)throw new Exception(message); }
    public static void Run(Assembly native)
    {
        var greType=native.GetType("GreClient.Rules.GreInterface",true)!;
        var database=native.GetType("Wotc.Mtga.Cards.Database.NullCardDatabaseAdapter",true)!.GetField("Default")!.GetValue(null)!;
        var gre=Activator.CreateInstance(greType,new object?[]{database,null,null,true,null})!;
        var process=greType.GetMethod("ProcessMessage",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var retrieve=greType.GetMethod("RetrieveClientUpdates")!;
        GameStateMessage? previous=null;uint sequence=0,annotationId=1;
        object[] lastChanges=Array.Empty<object>();
        GameStateMessage Board(uint target,string zone="Battlefield",bool hidden=false) {
            var cards=new[] {
                new {instanceId=1001,grpId=67562,ownerSeat=1,controllerSeat=1,zone,attachedTo=target,viewers=hidden?new[]{2}:new[]{1,2}},
                new {instanceId=1002,grpId=79334,ownerSeat=1,controllerSeat=1,zone="Battlefield",attachedTo=0u,viewers=new[]{1,2}},
                new {instanceId=1003,grpId=79334,ownerSeat=1,controllerSeat=1,zone="Battlefield",attachedTo=0u,viewers=new[]{1,2}}
            };
            return ForgeEncoder.Encode(JsonSerializer.Serialize(new {matchId="attachment-check",gameStateId=++sequence,msgId=sequence,viewerSeat=1,
                turn=new {number=1,phase="MAIN1",activeSeat=1,prioritySeat=1},players=new[]{new {seat=1,life=20},new {seat=2,life=20}},cards}))
                .GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
        }
        object Publish(GameStateMessage board) {
            BattlePresentation.Transitions(previous,board,ref annotationId);
            var message=new GREToClientMessage {Type=GREMessageType.GameStateMessage,MsgId=sequence,GameStateId=sequence,SystemSeatIds={1},
                GameStateMessage=BattleUpdates.WireState(board,previous)};
            process.Invoke(gre,new object[]{message});
            var updates=((System.Collections.IEnumerable)retrieve.Invoke(gre,null)!).Cast<object>();
            var update=updates.Single(u=>u.GetType().Name=="GameStateUpdate");
            lastChanges=((System.Collections.IEnumerable)update.GetType().GetField("Changes")!.GetValue(update)!).Cast<object>().ToArray();
            previous=board.Clone();return update.GetType().GetField("NewState")!.GetValue(update)!;
        }
        void Relationship(object state,uint target) {
            var cards=(System.Collections.IDictionary)state.GetType().GetField("AllCards")!.GetValue(state)!;
            var equipment=cards[1001u]!;
            Check((uint)equipment.GetType().GetField("AttachedToId")!.GetValue(equipment)! == target,"Native attachment target is stale or missing");
            foreach(uint candidate in new uint[]{1002,1003}) {
                var creature=cards[candidate]!;
                var attached=(HashSet<uint>)creature.GetType().GetField("AttachedWithIds")!.GetValue(creature)!;
                Check(attached.Contains(1001)==(target==candidate),"Native creature retained a stale attachment or lost its equipment");
            }
        }
        Relationship(Publish(Board(0)),0);
        foreach(uint target in new uint[]{1002,1002,1003,0,1002}) {
            uint beforeTarget=previous!.PersistentAnnotations.FirstOrDefault(a=>a.Type.Contains(AnnotationType.Attachment))?.AffectedIds.Single()??0;
            var board=Board(target);Relationship(Publish(board),target);
            Check(board.Annotations.Count(a=>a.Type.Contains(AnnotationType.AttachmentCreated))==(target!=0 && target!=beforeTarget?1:0),
                "Attachment event was duplicated or omitted");
        }
        Relationship(Publish(Board(0,"Graveyard")),0);
        Check(Board(9999).PersistentAnnotations.All(a=>!a.Type.Contains(AnnotationType.Attachment)),"Missing attachment target was encoded");
        Check(Board(1002,hidden:true).PersistentAnnotations.All(a=>!a.Type.Contains(AnnotationType.Attachment)),"Hidden attachment leaked its relationship");
        // A fresh client must restore the relationship without replaying attach effects.
        gre=Activator.CreateInstance(greType,new object?[]{database,null,null,true,null})!;
        previous=null;var reconnect=Board(1003);Relationship(Publish(reconnect),1003);
        Check(reconnect.Annotations.Count==0,"Reconnect replayed attachment effects");
        using(var observation=JsonDocument.Parse("""
            {"playbackId":1,"playbackEvents":[{"eventId":1,"type":"illegalAttachment","sourceId":1003,"targetId":1001,
            "sourceGrpId":79334,"cardName":"Grizzly Bears","description":"Protection from white"}]}
            """)) {
            var detached=Board(0,"Graveyard");
            BattlePresentation.Playback(observation.RootElement,previous,detached,0,ref annotationId);
            Relationship(Publish(detached),0);
            var illegal=lastChanges.Single(e=>e.GetType().Name=="IllegalAttachmentEvent");
            Check((uint)illegal.GetType().GetField("AffectedId")!.GetValue(illegal)! ==1001
                && (uint)illegal.GetType().GetField("InvalidatingGrpid")!.GetValue(illegal)! ==185,
                "Native illegal attachment effect lost its attachment or protection ability");
            var repeated=Board(0,"Graveyard");
            BattlePresentation.Playback(observation.RootElement,previous,repeated,1,ref annotationId);
            Publish(repeated);
            Check(!lastChanges.Any(e=>e.GetType().Name=="IllegalAttachmentEvent"),"Illegal attachment replayed after advancing the journal cursor");
        }
        Console.WriteLine("PASS: installed Arena attachment pipeline: attach, repeat, move, detach, graveyard, hidden targets and reconnect.");
        Console.WriteLine("PASS: installed Arena protection removal uses the actual invalidating ability and does not replay.");
    }
}
