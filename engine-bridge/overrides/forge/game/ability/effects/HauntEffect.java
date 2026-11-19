package forge.game.ability.effects;

import java.util.Map;

import forge.game.Game;
import forge.game.ability.AbilityKey;
import forge.game.ability.SpellAbilityEffect;
import forge.game.card.Card;
import forge.game.card.CardZoneTable;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;

public class HauntEffect extends SpellAbilityEffect {

    @Override
    public void resolve(SpellAbility sa) {
        Card host = sa.getHostCard();
        // Kaya exiles herself from the battlefield, rather than a dead card
        // from a Haunt trigger. Keep the ordinary keyword path below intact.
        if (sa.hasParam("HauntFromBattlefield")) {
            hauntFromBattlefield(sa, host);
            return;
        }
        if (host.isPermanent() && sa.hasTriggeringObject(AbilityKey.NewCard)) {
            // get new version instead of battlefield lki
            host = (Card) sa.getTriggeringObject(AbilityKey.NewCard);
        }
        final Game game = host.getGame();
        Card card = game.getCardState(host, null);
        if (card == null) {
            return;
        } else if (sa.usesTargeting() && !card.isToken() && host.equalsWithGameTimestamp(card)) {
            // haunt target but only if card is no token and still in grave
            Map<AbilityKey, Object> moveParams = AbilityKey.newMap();
            CardZoneTable zoneMovements = AbilityKey.addCardZoneTableParams(moveParams, sa);
            final Card moved = game.getAction().exile(card, sa, moveParams);
            sa.getTargetCard().addHauntedBy(moved);
            zoneMovements.triggerChangesZoneAll(game, sa);
        } else if (!sa.usesTargeting() && card.getHaunting() != null) {
            // unhaunt
            card.getHaunting().removeHauntedBy(card);
            card.setHaunting(null);
        }
    }

    private static void hauntFromBattlefield(final SpellAbility sa, final Card host) {
        final Game game = host.getGame();
        final Card source = game.getCardState(host, null);
        final Card target = sa.getTargetCard();
        if (source == null || !source.isInPlay() || !host.equalsWithGameTimestamp(source)
                || target == null || !target.isInPlay() || !target.isCreature()) {
            return;
        }
        final Card currentTarget = game.getCardState(target, null);
        if (currentTarget == null || !currentTarget.equalsWithGameTimestamp(target)
                || !sa.canTarget(currentTarget)) {
            return;
        }

        final Map<AbilityKey, Object> moveParams = AbilityKey.newMap();
        final CardZoneTable zoneMovements = AbilityKey.addCardZoneTableParams(moveParams, sa);
        final Card moved = game.getAction().exile(source, sa, moveParams);
        // Replacement effects may prevent exile or move Kaya elsewhere. Those
        // outcomes must not leave a creature marked as haunted by her.
        if (moved != null && moved.isInZone(ZoneType.Exile)) {
            final Card stillPresent = game.getCardState(currentTarget, null);
            if (stillPresent != null && stillPresent.isInPlay()
                    && stillPresent.equalsWithGameTimestamp(currentTarget)) {
                stillPresent.addHauntedBy(moved);
            } else {
                // A replacement can perform additional moves while exiling.
                // The "for as long as" interval has already ended in that case.
                game.getAction().moveToPlay(moved, moved.getOwner(), sa, moveParams);
            }
        }
        zoneMovements.triggerChangesZoneAll(game, sa);
    }

}
