package forge.game.event;

import forge.game.card.CardView;
import java.util.List;

/** The actual chosen destinations, before any player moves their library cards. */
public record GameEventScryOutcome(CardView source, List<Integer> topIds, List<Integer> bottomIds) implements GameEvent {
    @Override public <T> T visit(IGameEventVisitor<T> visitor) { return null; }
}
