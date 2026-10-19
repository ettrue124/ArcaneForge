/* SPDX-License-Identifier: GPL-3.0-or-later */
import forge.game.ArenaCardIds;
import forge.game.ability.AbilityKey;
import forge.game.card.Card;
import forge.game.spellability.SpellAbility;
import java.util.ArrayList;
import java.util.Map;

/** Causal cards for native trigger relationship highlights. */
final class BattleTriggerLinks {
    static void add(SpellAbility ability, Map<String,Object> entry) {
        if (ability.getTrigger() == null) return;
        var ids = new ArrayList<Integer>();
        // These keys name the event's causal object. Targets and defenders
        // are deliberately excluded: being affected is not causing a trigger.
        for (var key : new AbilityKey[]{AbilityKey.Card, AbilityKey.SpellAbility, AbilityKey.Attacker}) {
            Object value = ability.getTriggeringObject(key);
            Card card = value instanceof Card c ? c : value instanceof SpellAbility sa ? sa.getHostCard() : null;
            if (card == null) continue;
            Card current = card.getGame().getCardState(card, null);
            if (current == null || !current.equalsWithGameTimestamp(card)) continue;
            int id = ArenaCardIds.id(current.getId());
            if (!ids.contains(id)) ids.add(id);
        }
        if (!ids.isEmpty()) entry.put("triggeringCards", ids);
    }
}
