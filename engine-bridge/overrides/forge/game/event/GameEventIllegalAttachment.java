package forge.game.event;

import forge.game.card.CardView;

/** Observes an actual state-based detach caused by a CantAttach ability. */
public record GameEventIllegalAttachment(CardView source, CardView attachment,
        String description) implements GameEvent {
    @Override public <T> T visit(IGameEventVisitor<T> visitor) { return null; }
}
