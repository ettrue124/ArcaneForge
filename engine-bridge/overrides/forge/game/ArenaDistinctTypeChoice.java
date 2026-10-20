/* SPDX-License-Identifier: GPL-3.0-or-later */
package forge.game;
import java.util.List;
import forge.game.card.Card;
import forge.game.spellability.SpellAbility;
public interface ArenaDistinctTypeChoice {
    List<Card> chooseDistinctTypeCards(List<Card> cards, SpellAbility source);
}
