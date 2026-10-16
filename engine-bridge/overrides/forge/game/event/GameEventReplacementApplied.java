package forge.game.event;

import forge.game.GameEntityView;
import forge.game.card.CardView;

/** Emitted only after a replacement actually applies, never for a declined choice. */
public record GameEventReplacementApplied(CardView source, GameEntityView affected,
        String description, boolean damage) implements GameEvent {
    @Override public <T> T visit(IGameEventVisitor<T> visitor) { return null; }
}
