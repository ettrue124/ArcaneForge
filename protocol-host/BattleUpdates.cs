using Wotc.Mtgo.Gre.External.Messaging;

static class BattleUpdates
{
    public static GameStateMessage WireState(GameStateMessage current,GameStateMessage? previous)
    {
        var wire=current.Clone();
        if(previous==null)return wire;
        wire.Type=GameStateType.Diff;wire.PrevGameStateId=previous.GameStateId;
        // The installed client replaces each repeated record by ID, rather than
        // protobuf-merging scalars. Complete changed records can clear false,
        // zero and empty values without leaving stale mana/taps/visibility.
        var oldObjects=previous.GameObjects.ToDictionary(c=>c.InstanceId);
        // Commander tax lives on a designation, rather than the card record.
        // Resend its card when the tax changes so the hover model is rebuilt
        // even if the card's ordinary fields have stayed the same.
        var oldCommanderCosts=previous.PersistentAnnotations
            .Where(a=>a.Type.Contains(AnnotationType.Designation)
                && a.Details.Any(d=>d.Key=="DesignationType" && d.ValueInt32.Contains((int)Designation.Commander)))
            .SelectMany(a=>a.AffectedIds.Select(id=>(id,cost:a.Details.FirstOrDefault(d=>d.Key=="CostIncrease")?.ValueInt32.FirstOrDefault()??0)))
            .ToDictionary(x=>x.id,x=>x.cost);
        var refreshCommanders=current.PersistentAnnotations
            .Where(a=>a.Type.Contains(AnnotationType.Designation)
                && a.Details.Any(d=>d.Key=="DesignationType" && d.ValueInt32.Contains((int)Designation.Commander)))
            .SelectMany(a=>a.AffectedIds.Select(id=>(id,cost:a.Details.FirstOrDefault(d=>d.Key=="CostIncrease")?.ValueInt32.FirstOrDefault()??0)))
            .Where(x=>!oldCommanderCosts.TryGetValue(x.id,out var oldCost) || oldCost!=x.cost)
            .Select(x=>x.id).ToHashSet();
        wire.GameObjects.Clear();
        wire.GameObjects.AddRange(current.GameObjects.Where(c=>refreshCommanders.Contains(c.InstanceId) || !oldObjects.TryGetValue(c.InstanceId,out var old) || !c.Equals(old)));
        wire.DiffDeletedInstanceIds.AddRange(previous.GameObjects.Select(c=>c.InstanceId).Except(current.GameObjects.Select(c=>c.InstanceId)));
        var oldAnnotations=previous.PersistentAnnotations.ToDictionary(a=>a.Id);
        wire.PersistentAnnotations.Clear();
        wire.PersistentAnnotations.AddRange(current.PersistentAnnotations.Where(a=>!oldAnnotations.TryGetValue(a.Id,out var old) || !a.Equals(old)));
        wire.DiffDeletedPersistentAnnotationIds.AddRange(previous.PersistentAnnotations.Select(a=>a.Id).Except(current.PersistentAnnotations.Select(a=>a.Id)));
        return wire;
    }
    public static MatchServiceToClientMessage Batch(GREToClientMessage state,IEnumerable<GREToClientMessage> followups)
    {
        var messages=followups.ToArray();
        if(messages.Any(m=>m.GameStateId!=state.GameStateId))throw new InvalidDataException("Gameplay batch contains mismatched state IDs");
        state.GameStateMessage.PendingMessageCount=(uint)messages.Length;
        return new MatchServiceToClientMessage { GreToClientEvent=new GreToClientEvent {GreToClientMessages={state,messages}} };
    }
}
