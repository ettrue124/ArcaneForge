package forge.game.event;

import forge.game.card.CardView;
import forge.game.player.PlayerView;

/** Presentation boundaries for an actual rules effect, with no extra decisions. */
public record GameEventMultistepEffect(CardView source, PlayerView player, String category, boolean starting) implements GameEvent {
    @Override public <T> T visit(IGameEventVisitor<T> visitor) { return null; }
}
