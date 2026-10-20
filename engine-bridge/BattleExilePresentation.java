/* SPDX-License-Identifier: GPL-3.0-or-later */
import forge.game.Game;
import forge.game.card.Card;
import forge.game.CardTraitBase;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;
import java.util.HashMap;
import java.util.Map;

/** Active linked-exile relationships for the native under-card display. */
final class BattleExilePresentation {
    static Map<Integer,Integer> underPermanents(Game game) {
        var links = new HashMap<Integer,Integer>();
        // Resolve relationships from game objects rather than card-name lists.
        for (Card parent : game.getCardsIn(ZoneType.Battlefield)) {
            if(parent.isPhasedOut())continue;
            // Banishing effects such as Brutal Cathar already track exactly
            // which cards return when this battlefield object leaves. Feed
            // that active relationship to the existing under-card renderer.
            for(Card exiled : parent.getUntilLeavesBattlefield()) {
                link(game,links,parent,exiled);
            }
            // Older linked triggers (Fiend Hunter, Mesmeric Fiend), and
            // cards such as Skyclave Apparition, remember the actual object.
            for(Object remembered : parent.getRemembered())
                if(remembered instanceof Card exiled)link(game,links,parent,exiled);
            // Oblivion Ring, hideaway, and ongoing play/copy permissions use
            // ExiledWith rather than remembered objects. Require a rules
            // reference: ordinary exile provenance alone is not a link.
            if(parent.hasExiledCard() && refersToLinkedExile(parent))
                for(Card exiled : parent.getExiledCards())link(game,links,parent,exiled);
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
                link(game,links,parent,imprinted);
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
    private static void link(Game game,Map<Integer,Integer> links,Card parent,Card linked) {
        Card current=game.getCardState(linked,null);
        if(current!=null && current.isInZone(ZoneType.Exile)
            && current.equalsWithGameTimestamp(linked))
            links.put(current.getId(),forge.game.ArenaCardIds.id(parent.getId()));
    }
    private static boolean refersToLinkedExile(Card parent) {
        if(parent.getSVars().values().stream().anyMatch(v->v.contains("ExiledWith")))return true;
        for(var ability:parent.getSpellAbilities())
            if(refersToLinkedExile(ability,new java.util.HashSet<>()))return true;
        for(var ability:parent.getStaticAbilities())if(refersToLinkedExile(ability))return true;
        for(var trigger:parent.getTriggers()) {
            if(refersToLinkedExile(trigger)
                || refersToLinkedExile(trigger.getOverridingAbility(),new java.util.HashSet<>()))return true;
        }
        for(var replacement:parent.getReplacementEffects())if(refersToLinkedExile(replacement))return true;
        return false;
    }
    private static boolean refersToLinkedExile(CardTraitBase trait) {
        return trait.getMapParams().values().stream().anyMatch(v->v.contains("ExiledWith"))
            || trait.getSVars().values().stream().anyMatch(v->v.contains("ExiledWith"));
    }
    private static boolean refersToLinkedExile(SpellAbility ability,java.util.Set<SpellAbility> seen) {
        if(ability==null || !seen.add(ability))return false;
        if(refersToLinkedExile(ability) || refersToLinkedExile(ability.getSubAbility(),seen))return true;
        for(var child:ability.getAdditionalAbilities().values())if(refersToLinkedExile(child,seen))return true;
        for(var children:ability.getAdditionalAbilityLists().values())
            for(var child:children)if(refersToLinkedExile(child,seen))return true;
        return false;
    }
    static Card printedSource(Card source) {
        var seen = new java.util.HashSet<Card>();
        while (source != null && source.getEffectSource() != null && seen.add(source))
            source = source.getEffectSource();
        return source;
    }
}
