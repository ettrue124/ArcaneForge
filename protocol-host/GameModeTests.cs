using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

static class GameModeTests
{
    public static void Run(Assembly shared)
    {
        var fixtures=JObject.Parse(File.ReadAllText("analysis/startup-fixtures.json"))["commands"]!;
        var response=fixtures["EventGetActiveEventsV2"]!["payload"]!.ToObject<Wizards.Unification.Models.Event.ActiveEventsResponseV2>()!;
        var helper=shared.GetType("Core.Shared.Code.Network.Utils.AwsEventHelpers",true)!;
        var events=((System.Collections.IEnumerable)helper.GetMethod("ConvertToEventsList")!.Invoke(null,new object?[]{null,response})!).Cast<object>().ToList();
        File.WriteAllText("analysis/commander/native-mode-events.json",JsonConvert.SerializeObject(events,Formatting.Indented));
        var core=Assembly.LoadFrom(Path.Combine(Path.GetDirectoryName(shared.Location)!,"Core.dll"));
        var bladeType=core.GetType("Wizards.Mtga.PlayBlade.BladeEventInfo",true)!;
        var bladeList=(System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(bladeType))!;
        foreach(var evt in events.Where(e=>e.GetType().GetField("InternalEventName")!.GetValue(e)?.ToString()!="DirectGame")) {
            string name=evt.GetType().GetField("InternalEventName")!.GetValue(evt)!.ToString()!;
            var ux=evt.GetType().GetField("EventUXInfo")!.GetValue(evt)!;
            if(ux.GetType().GetField("Joinability")!.GetValue(ux)!.ToString()!="Open" || ux.GetType().GetField("EventBladeBehavior")!.GetValue(ux)!.ToString()!="Queue")
                throw new Exception("Native mode is not an open queue: "+name);
            var basicType=core.GetType("Wotc.Mtga.Events.BasicPlayerEvent",true)!;
            var basic=Activator.CreateInstance(basicType,new object?[]{evt,null,null,null,null})!;
            if(!(bool)basicType.GetMethod("ShowInPlayblade")!.Invoke(basic,new object?[]{null})!)throw new Exception("Native play blade hides mode: "+name);
            var blade=Activator.CreateInstance(bladeType)!;
            bladeType.GetField("EventName")!.SetValue(blade,name);
            bladeList.Add(blade);
        }
        var config=fixtures["GetPlayBladeQueueConfig"]!["payload"]!.ToObject<List<Wizards.Unification.Models.PlayBlade.PlayBladeQueueEntry>>()!;
        var hydrate=core.GetType("Wizards.Mtga.PlayBlade.BladeData",true)!.GetMethod("HydrateMockWithRealEvents",BindingFlags.NonPublic|BindingFlags.Static)!;
        var queues=(System.Collections.IDictionary)hydrate.Invoke(null,new object[]{bladeList,config})!;
        if(queues.Values.Cast<System.Collections.ICollection>().Sum(c=>c.Count)!=3)throw new Exception("Native selector did not hydrate all three modes");
        if(LocalStartup.GameMode("CommanderAI4")!="commander4" || LocalStartup.EventName(JObject.Parse("{\"EventName\":\"CommanderAI\"}"),"AIBotMatch")!="CommanderAI")
            throw new Exception("Selected native mode was lost during matchmaking");
        Console.WriteLine("PASS: installed Arena shows three open game queues and hydrates the game-mode selector.");
    }
}
