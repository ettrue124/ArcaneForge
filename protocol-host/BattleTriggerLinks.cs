using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

/// <summary>Connect stack triggers to their visible causal cards.</summary>
static class BattleTriggerLinks
{
    public static void Apply(JsonElement snapshot, GameStateMessage state)
    {
        if(!snapshot.TryGetProperty("stack",out var stack))return;
        var visible=state.GameObjects.Where(c=>c.GrpId!=0 && c.Visibility!=Visibility.Hidden)
            .ToDictionary(c=>c.InstanceId);
        foreach(var entry in stack.EnumerateArray()) {
            if(!entry.GetProperty("isAbility").GetBoolean() || !entry.TryGetProperty("triggeringCards",out var causes))continue;
            uint id=entry.GetProperty("instanceId").GetUInt32();
            if(!visible.TryGetValue(id,out var ability) || ability.Type!=GameObjectType.Ability)continue;
            var ids=causes.EnumerateArray().Select(c=>c.GetUInt32()).Where(visible.ContainsKey).Distinct().ToArray();
            if(ids.Length==0)continue;
            state.PersistentAnnotations.Add(new AnnotationInfo {
                Id=checked(4_000_000_000u+id),Type={AnnotationType.TriggeringObject},AffectorId=id,AffectedIds={ids}
            });
        }
    }
}
