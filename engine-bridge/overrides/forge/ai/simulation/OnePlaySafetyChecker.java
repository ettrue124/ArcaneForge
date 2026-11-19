package forge.ai.simulation;

import forge.ai.simulation.GameStateEvaluator.Score;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;

public final class OnePlaySafetyChecker {
    private static final ThreadLocal<Boolean> CHECKING = ThreadLocal.withInitial(() -> false);

    public static boolean isAcceptable(Player player, SpellAbility sa) {
        if(sa!=null && !CHECKING.get() && sa.getHostCard()!=null
                && "Pact of Negation".equals(sa.getHostCard().getName())
                && !canFundPactUpkeep(player,sa))return false;

        // A zero-toughness clone with no creature to copy simply dies.
        // Hybrid scoring must not approve spending UU for Flesh Duplicate
        // merely because its optional entry replacement permits no choice.
        if(sa!=null && sa.isSpell() && sa.getHostCard()!=null
                && "Flesh Duplicate".equals(sa.getHostCard().getName())
                && player.getGame().getCardsIn(ZoneType.Battlefield).stream().noneMatch(Card::isCreature))
            return false;

        // A temporary Chrome Mox copy does not copy the original's imprint.
        // Molten Duplication spends two mana, then another card to produce at
        // most one mana before the token is sacrificed. Permanent scoring
        // overvalues the transient artifact and misses this net resource loss.
        if(sa!=null && sa.getHostCard()!=null
                && "Molten Duplication".equals(sa.getHostCard().getName())
                && sa.getTargets().getTargetCards().stream().anyMatch(c->"Chrome Mox".equals(c.getName())))
            return false;

        // A targetless Subtlety evoke spends a blue card and sacrifices the
        // creature for no effect. The normal hybrid check skips stack responses,
        // so reject this specific mistake before that early return.
        if(sa!=null && !CHECKING.get() && sa.isEvoke() && sa.getHostCard()!=null
                && "Subtlety".equals(sa.getHostCard().getName())) {
            boolean target=false;
            for(var entry:player.getGame().getStack()) {
                var spell=entry.getSpellAbility();
                if(spell.isSpell() && spell.getActivatingPlayer()!=null
                        && spell.getActivatingPlayer().isOpponentOf(player)
                        && (spell.getHostCard().isCreature() || spell.getHostCard().isPlaneswalker())) {
                    target=true;break;
                }
            }
            if(!target)return false;
        }

        // Necropotence's card arrives at the next end step. A one-action
        // simulation sees only life lost and wrongly vetoes every activation.
        // ChangeZoneAi already applies Necropotence's hand-size, queued-card,
        // phase, library and life-cost checks before this safety layer.
        if(sa!=null && sa.isActivatedAbility() && sa.getHostCard()!=null
                && "Necropotence".equals(sa.getHostCard().getName())
                && "Necropotence".equals(sa.getParamOrDefault("AILogic","")))return true;

        // Forge keeps the parent ability on the stack while it resolves. Score actions offered
        // during that resolution incrementally; priority responses need two full stack-resolution
        // branches and are not supported yet.
        if (sa == null || CHECKING.get()
                || (!player.getGame().getStack().isEmpty() && !player.getGame().getStack().isResolving())) {
            return true;
        }

        CHECKING.set(true);
        try {
            SimulationController controller = new SimulationController(new Score(0), 0);
            GameSimulator simulator = new GameSimulator(controller, player.getGame(), player, null);
            Score originalScore = simulator.getScoreForOrigGame();
            Score resultScore = simulator.simulateSpellAbility(sa);

            // desperate plays are ok if next combat was already likely to kill AI
            return resultScore.value == Integer.MIN_VALUE
                    || resultScore.value >= (long) originalScore.value - expectedCardScoreLoss(player, sa, simulator);
        } finally {
            CHECKING.remove();
        }
    }

    private static boolean canFundPactUpkeep(Player player, SpellAbility pact) {
        // Forecast on an isolated game: never untap real cards or consume the
        // live mana pool just to decide whether to cast a free counterspell.
        CHECKING.set(true);
        try {
            GameCopier copier=new GameCopier(player.getGame());
            var game=copier.makeCopy();
            Player future=copier.find(player);
            future.getManaPool().clearPool(false);
            boolean restrictedUntap=false;
            for(Card card:game.getCardsIn(ZoneType.Battlefield)) {
                String name=card.getName();
                if("Stasis".equals(name) || !card.isTapped()
                        && ("Winter Orb".equals(name) || "Static Orb".equals(name)))restrictedUntap=true;
            }
            for(Card card:future.getCardsIn(ZoneType.Battlefield)) {
                // Do not assume automatic untaps for special rocks or through
                // untap locks. This forecast deliberately avoids future draws,
                // new land drops and speculative combo wins.
                boolean specialRock=java.util.Set.of("Mana Vault","Grim Monolith","Basalt Monolith").contains(card.getName());
                if(!restrictedUntap && !specialRock && (card.isLand() || card.isArtifact())
                        && !card.hasKeyword("CARDNAME doesn't untap during your untap step."))card.setTapped(false);
            }
            Card source=copier.find(pact.getHostCard());
            var payment=forge.game.ability.AbilityFactory.getAbility("DB$ LosesGame | Defined$ You",source);
            payment.setActivatingPlayer(future);
            payment.setPayCosts(new forge.game.cost.Cost("3 U U",false));
            return forge.ai.ComputerUtilMana.canPayManaCost(payment,future,0,true);
        } catch(RuntimeException error) {
            // An unsupported forecast must never approve a lethal obligation.
            return false;
        } finally {
            CHECKING.remove();
        }
    }

    // sometimes simulation might not see the effect of some heuristics directly, so at least negate any card disadvantage
    private static int expectedCardScoreLoss(Player player, SpellAbility sa, GameSimulator simulator) {
        Card source = sa.getHostCard();
        if (source == null || !source.isInZone(ZoneType.Hand)) {
            return 0;
        }
        Card simulatedSource = simulator.getGameCopier().find(source);
        if (simulatedSource.isInZone(ZoneType.Hand)) {
            return 0;
        }
        // Match GameStateEvaluator: excess cards are worth one point, other hand cards five.
        return !player.isUnlimitedHandSize() && player.getCardsIn(ZoneType.Hand).size() > player.getMaxHandSize() ? 1 : 5;
    }
}
