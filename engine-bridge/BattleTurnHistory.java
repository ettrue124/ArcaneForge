/* SPDX-License-Identifier: GPL-3.0-or-later */
import forge.game.ArenaCardIds;
import forge.game.card.Card;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/** Public turn history used by Arena's battlefield relationship indicators. */
final class BattleTurnHistory {
    private BattleTurnHistory() {}

    static void add(Card card, Map<String,Object> encoded) {
        if (!card.isInPlay()) return;
        // Marked damage can be removed by regeneration. Forge's damage history
        // still records that this permanent was dealt damage during this turn.
        if (card.getAssignedDamage() > 0) encoded.put("damagedThisTurn", true);
        addParticipants(card, encoded, "crewedByThisTurn", card.getCrewedByThisTurn());
        addParticipants(card, encoded, "saddledByThisTurn", card.getSaddledByThisTurn());
    }

    private static void addParticipants(Card host, Map<String,Object> encoded, String key,
                                        Iterable<Card> participants) {
        if (participants == null) return;
        List<Integer> ids = new ArrayList<>();
        for (Card participant : participants) {
            Card current = host.getGame().getCardState(participant, null);
            // A creature which left and returned is a different permanent. Its
            // reused Forge ID must not connect the vehicle to its new wire ID.
            if (current == null || !current.isInPlay() || !current.equalsWithGameTimestamp(participant)) continue;
            int id = ArenaCardIds.id(current.getId());
            if (!ids.contains(id)) ids.add(id);
        }
        if (!ids.isEmpty()) encoded.put(key, ids);
    }
}
