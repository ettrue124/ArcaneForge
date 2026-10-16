package forge.game.mulligan;

import forge.game.card.Card;
import forge.game.card.CardCollection;
import forge.game.player.Player;
import forge.game.zone.ZoneType;

/** Arena bridge: redraw seven, decide to keep, then bottom once. */
public class LondonMulligan extends AbstractMulligan {
    public LondonMulligan(Player player, boolean firstMullFree) { super(player, firstMullFree); }
    @Override public boolean canMulligan() {
        return !kept && tuckCardsDuringMulligan() < player.getMaxHandSize();
    }
    @Override public int handSizeAfterNextMulligan() { return player.getMaxHandSize(); }
    @Override public void mulliganDraw() { player.drawCards(handSizeAfterNextMulligan()); }
    @Override public int tuckCardsDuringMulligan() {
        return Math.max(0, timesMulliganed - (firstMulliganFree ? 1 : 0));
    }
    @Override public void keep() {
        int count = tuckCardsDuringMulligan();
        if (count > 0) {
            CardCollection hand = new CardCollection(player.getCardsIn(ZoneType.Hand));
            for (Card card : player.getController().tuckCardsViaMulligan(hand, count))
                player.getGame().getAction().moveToLibrary(card, -1, null);
        }
        super.keep();
    }
}
