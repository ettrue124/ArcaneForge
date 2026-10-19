using System.Text.Json;
using Wotc.Mtgo.Gre.External.Messaging;

/// <summary>Native battlefield history, separate from current damage and tap state.</summary>
static class BattleTurnHistory
{
    public static void Apply(JsonElement snapshot, GameStateMessage state)
    {
        if (!snapshot.TryGetProperty("cards", out var cards)) return;
        var battlefield = state.GameObjects.Where(c => c.ZoneId == 30 && c.GrpId != 0)
            .Select(c => c.InstanceId).ToHashSet();
        foreach (var card in cards.EnumerateArray()) {
            uint id = card.GetProperty("instanceId").GetUInt32();
            if (!battlefield.Contains(id)) continue;
            if (card.TryGetProperty("damagedThisTurn", out var damaged) && damaged.GetBoolean())
                state.PersistentAnnotations.Add(new AnnotationInfo {
                    Id = checked(2_350_000_000u + id * 3),
                    Type = { AnnotationType.DamagedThisTurn }, AffectedIds = { id }
                });
            AddRelationship(card, "crewedByThisTurn", AnnotationType.CrewedThisTurn, 1, id, battlefield, state);
            AddRelationship(card, "saddledByThisTurn", AnnotationType.SaddledThisTurn, 2, id, battlefield, state);
        }
    }

    static void AddRelationship(JsonElement card, string key, AnnotationType type, uint slot,
                                uint id, HashSet<uint> battlefield, GameStateMessage state)
    {
        if (!card.TryGetProperty(key, out var participants)) return;
        var linked = participants.EnumerateArray().Select(c => c.GetUInt32())
            .Where(c => c != id && battlefield.Contains(c)).Distinct().ToArray();
        if (linked.Length == 0) return;
        // The native parser reads the vehicle/mount as affector and the
        // creatures that paid the crew/saddle cost as affected objects.
        state.PersistentAnnotations.Add(new AnnotationInfo {
            Id = checked(2_350_000_000u + id * 3 + slot),
            Type = { type }, AffectorId = id, AffectedIds = { linked }
        });
    }
}
