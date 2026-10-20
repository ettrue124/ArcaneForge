/* SPDX-License-Identifier: GPL-3.0-or-later */
package forge.game;

import forge.game.card.Card;
import forge.game.phase.ExtraTurn;
import forge.game.phase.PhaseHandler;
import forge.game.spellability.SpellAbility;
import java.lang.reflect.Field;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.WeakHashMap;

/** Source identities for the existing native pending-extra-turn display. */
public final class ArenaExtraTurns {
    private static final Map<ExtraTurn,Card> sources=new WeakHashMap<>();
    private static final Field queue;
    static {
        try {
            queue=PhaseHandler.class.getDeclaredField("extraTurns");
            queue.setAccessible(true);
        } catch(ReflectiveOperationException e) {throw new ExceptionInInitializerError(e);}
    }
    public record Pending(forge.game.player.Player player,Card source) {}
    public static synchronized void record(ExtraTurn turn,SpellAbility ability) {
        sources.put(turn,ability.getHostCard());
    }
    public static synchronized List<Pending> pending(Game game) {
        try {
            var turns=(List<?>)queue.get(game.getPhaseHandler());
            var result=new ArrayList<Pending>();
            // Forge's bottom entry resumes normal turn order; it is not extra.
            // Keep bottom-to-top order: Arena inserts each annotation at front.
            for(int i=1;i<turns.size();i++) {
                ExtraTurn turn=(ExtraTurn)turns.get(i);
                if(turn.getPlayer().isInGame())result.add(new Pending(turn.getPlayer(),sources.get(turn)));
            }
            return result;
        } catch(IllegalAccessException e) {throw new IllegalStateException("Cannot read pending extra turns",e);}
    }
}
