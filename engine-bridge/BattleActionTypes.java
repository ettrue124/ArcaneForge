/* SPDX-License-Identifier: GPL-3.0-or-later */
import forge.card.CardStateName;
import forge.game.spellability.SpellAbility;

/** Installed GRE action values, selected from actual Forge ability state. */
final class BattleActionTypes {
    static int type(SpellAbility ability) {
        var card=ability.getHostCard();var state=ability.getCardStateName();
        if(ability.isLandAbility())return card.isModal() && state==CardStateName.Backside?19:3;
        if(ability.isTurnFaceUp())return 8;
        if(ability.isSpell()) {
            if(ability.isAdventure())return 16;
            if(ability.isOmen())return 24;
            if(ability.hasParam("Prototype"))return 21;
            if(state==CardStateName.LeftSplit)return card.isRoom()?22:10;
            if(state==CardStateName.RightSplit)return card.isRoom()?23:11;
            if(card.isModal() && state==CardStateName.Backside)return 18;
            return 1;
        }
        return ability.isManaAbility()?4:2;
    }
}
