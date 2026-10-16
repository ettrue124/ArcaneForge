package forge.game.event;

import forge.game.card.CardView;
import forge.game.player.PlayerView;
import java.util.List;

/** Observational outcome data; never changes rolls, choices, costs or triggers. */
public record GameEventRandomOutcome(CardView source, PlayerView player, String kind,
        int sides, List<Integer> natural, List<Integer> results, boolean ignored) implements GameEvent {
    @Override public <T> T visit(IGameEventVisitor<T> visitor) { return null; }
}
