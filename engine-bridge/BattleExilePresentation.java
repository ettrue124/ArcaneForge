/* SPDX-License-Identifier: GPL-3.0-or-later */
import forge.game.Game;
import forge.game.card.Card;
import forge.game.zone.ZoneType;
import java.util.HashMap;
import java.util.Map;

/** Display links carried by an active remembered/imprinted granting effect. */
final class BattleExilePresentation {
    static Map<Integer,Integer> underPermanents(Game game) {
        var links = new HashMap<Integer,Integer>();
        // Imprint usually belongs directly to the battlefield permanent
        // (Chrome Mox, Isochron Scepter, and similar cards).
        for (Card parent : game.getCardsIn(ZoneType.Battlefield)) {
            if(parent.isPhasedOut())continue;
            // Caverns' opening-hand exile is not an imprint, but belongs in
            // its linked-card display just like the opening luck counter.
            if("Gemstone Caverns".equals(parent.getName())) {
                for(Card exiled:game.getCardsIn(ZoneType.Exile)) {
                    Card source=exiled.getExiledWith();
                    boolean linked=source!=null && source.equalsWithGameTimestamp(parent);
                    if(source==null)linked=forge.game.ArenaCardIds.exileSource(exiled.getId())
                        ==forge.game.ArenaCardIds.id(parent.getId());
                    if(linked)links.put(exiled.getId(),forge.game.ArenaCardIds.id(parent.getId()));
                }
            }
            for(Card imprinted : parent.getImprintedCards()) {
                Card current=game.getCardState(imprinted,null);
                if(current!=null && current.isInZone(ZoneType.Exile)
                    && current.equalsWithGameTimestamp(imprinted))
                    links.put(current.getId(),forge.game.ArenaCardIds.id(parent.getId()));
            }
        }
        for (Card effect : game.getCardsIn(ZoneType.Command)) {
            if (effect.getImprintedCards().isEmpty() || effect.getEffectSource() == null) continue;
            boolean grantsRemembered = effect.getStaticAbilities().stream().anyMatch(a ->
                a.hasParam("AddAbility") && a.getParamOrDefault("Affected", "").contains("IsRemembered")
                && a.getParamOrDefault("Affected", "").contains("!IsImprinted"));
            if (!grantsRemembered) continue;
            Card parent = null;
            boolean ambiguous = false;
            for (Object object : effect.getRemembered()) {
                if (!(object instanceof Card remembered) || effect.getImprintedCards().contains(remembered)) continue;
                Card current = game.getCardState(remembered, null);
                if (current == null || !current.isInPlay() || current.isPhasedOut()
                    || !current.equalsWithGameTimestamp(remembered)) continue;
                if (parent != null && !parent.equalsWithGameTimestamp(current)) { ambiguous = true; break; }
                parent = current;
            }
            if (parent == null || ambiguous) continue;
            for (Card imprinted : effect.getImprintedCards()) {
                Card current = game.getCardState(imprinted, null);
                if (current != null && current.isInZone(ZoneType.Exile)
                    && current.equalsWithGameTimestamp(imprinted))
                    links.put(current.getId(), forge.game.ArenaCardIds.id(parent.getId()));
            }
        }
        return links;
    }
    static Card printedSource(Card source) {
        var seen = new java.util.HashSet<Card>();
        while (source != null && source.getEffectSource() != null && seen.add(source))
            source = source.getEffectSource();
        return source;
    }
}
