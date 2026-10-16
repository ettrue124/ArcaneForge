using System.Reflection;
using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

static class BattleShockMenuTests
{
    static void Check(bool value,string message) {if(!value)throw new Exception(message);}
    public static void Run(Assembly native) {
        var greType=native.GetType("GreClient.Rules.GreInterface",true)!;
        var database=native.GetType("Wotc.Mtga.Cards.Database.NullCardDatabaseAdapter",true)!.GetField("Default")!.GetValue(null)!;
        var gre=Activator.CreateInstance(greType,new object?[]{database,null,null,true,null})!;
        var process=greType.GetMethod("ProcessMessage",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var retrieve=greType.GetMethod("RetrieveClientUpdates")!;
        GameStateMessage? previous=null;
        foreach(bool pending in new[]{true,false}) {
            uint sequence=pending?1u:2u;
            string json=JsonSerializer.Serialize(new {matchId="shock-menu",gameStateId=sequence,msgId=sequence,viewerSeat=1,
                turn=new {number=1,phase="MAIN1",activeSeat=1,prioritySeat=1},players=new[]{new {seat=1,life=20},new {seat=2,life=20}},
                cards=new[]{new {instanceId=1001,grpId=69399,ownerSeat=1,controllerSeat=1,zone="Hand",cardTypes=new[]{"Land"}}},
                input=pending?(object)new {type="InputConfirm",sourceId=1001,cardName="Hallowed Fountain",entryReplacementDescription="As Hallowed Fountain enters, you may pay 2 life. If you don't, it enters tapped."}:new {type="InputPassPriority"}});
            var board=ForgeEncoder.Encode(json).GreToClientEvent.GreToClientMessages.Single().GameStateMessage;
            process.Invoke(gre,new object[]{new GREToClientMessage {Type=GREMessageType.GameStateMessage,SystemSeatIds={1},MsgId=sequence,
                GameStateId=sequence,GameStateMessage=BattleUpdates.WireState(board,previous)}});
            previous=board.Clone();
            var update=((System.Collections.IEnumerable)retrieve.Invoke(gre,null)!).Cast<object>().Single(u=>u.GetType().Name=="GameStateUpdate");
            var state=update.GetType().GetField("NewState")!.GetValue(update)!;
            var replacements=(System.Collections.IDictionary)state.GetType().GetField("ReplacementEffects")!.GetValue(state)!;
            if(!pending) {Check(replacements.Count==0,"Entry menu context survived its decision");continue;}
            Check(replacements.Contains(1001u),"Native client lost the entry-payment source");
            var effects=((System.Collections.IEnumerable)replacements[1001u]!).Cast<object>().ToArray();
            var effect=effects.Single();
            Check(Convert.ToUInt32(effect.GetType().GetProperty("AbilityId")!.GetValue(effect))==90846,"Native shock layout identity missing");
            var sourceIds=(System.Collections.IEnumerable)effect.GetType().GetField("SourceIds")!.GetValue(effect)!;
            Check(sourceIds.Cast<object>().Select(Convert.ToUInt32).SequenceEqual(new[]{1001u}),"Native browser cannot find its source card");
        }
        string altDirectory=@"C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Downloads\ALT";
        using var tree=JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(altDirectory,"ALT_Browser_*.mtga").Single()));
        var layout=tree.RootElement.GetProperty("ALT_Browser.OptionalAction.SpecialBrowserLayout");
        string? node=null;
        foreach(var connection in layout.GetProperty("Connections").EnumerateArray())
            if(connection.GetProperty("Child").ValueKind==JsonValueKind.Object && connection.GetProperty("Child").TryGetProperty("90846",out var target))node=target.GetString();
        Check(node!=null,"Installed client has no layout for the shock-land ability");
        var data=layout.GetProperty("Nodes").EnumerateArray().Single(n=>n.GetProperty("NodeId").GetString()==node).GetProperty("Payload").GetProperty("Data");
        Check(data.GetProperty("SourceCardType").GetInt32()==5 && data.GetProperty("YesText").GetString()=="DuelScene/ClientPrompt/Pay2Life"
            && data.GetProperty("NoText").GetString()=="DuelScene/ClientPrompt/EntersTapped","Installed shock menu labels/source differ");
        Console.WriteLine("PASS: native replacement context selects installed Pay2Life/EntersTapped layout and clears after the decision.");
    }
}
