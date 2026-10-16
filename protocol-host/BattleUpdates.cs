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
        wire.GameObjects.Clear();
        wire.GameObjects.AddRange(current.GameObjects.Where(c=>!oldObjects.TryGetValue(c.InstanceId,out var old) || !c.Equals(old)));
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
