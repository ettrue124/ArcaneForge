package forge.game.ability.effects;

import java.util.Map;

import org.apache.commons.lang3.mutable.MutableBoolean;

import com.google.common.collect.Maps;

import forge.game.Game;
import forge.game.GameEntityCounterTable;
import forge.game.ability.AbilityUtils;
import forge.game.card.Card;
import forge.game.card.CardCollectionView;
import forge.game.card.CardLists;
import forge.game.card.CardPredicates;
import forge.game.card.CardZoneTable;
import forge.game.card.CounterEnumType;
import forge.game.card.token.TokenInfo;
import forge.game.event.GameEventCombatChanged;
import forge.game.event.GameEventTokenCreated;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;
import forge.util.Lang;
import forge.util.Localizer;

public class EmpowerEffect extends TokenEffectBase {

    @Override
    protected String getStackDescription(SpellAbility sa) {
        final StringBuilder sb = new StringBuilder("Empower ");
        final Card card = sa.getHostCard();
        final int amount = AbilityUtils.calculateAmount(card, sa.getParamOrDefault("Num", "1"), sa);
        final String type = sa.getParam("Type");

        sb.append(type).append(" ").append(amount).append(" (Put ");

        sb.append(Lang.nounWithNumeral(amount, "loyalty counter"));

        // TODO fix reminder after CR
        sb.append(" on a ").append(type).append(" token you control.");
        sb.append(" If you don't control one, first create a blue ").append(type).append(" planeswalker token with \"-1: Surveil 1.\" and \"-3: Draw a card.\")");

        return sb.toString();
    }

    @Override
    public void resolve(SpellAbility sa) {
        final Card source = sa.getHostCard();
        final Game game = source.getGame();
        final Player p = getTargetPlayers(sa).getFirst();
        if (p == null) {
            return;
        }
        final int amount = AbilityUtils.calculateAmount(source, sa.getParamOrDefault("Num", "1"), sa);
        final String type = sa.getParam("Type");

        if ("Jace".equals(type)) {
            boolean redirect=false;
            for(Card ca:p.getCardsIn(ZoneType.Battlefield))for(var ability:ca.getStaticAbilities())
                if(ability.hasParam("ArenaEmpowerRedirect") && ability.checkConditions())redirect=true;
            var jaces=CardLists.getValidCards(p.getCardsIn(ZoneType.Battlefield),"Jace",p,source,sa);
            if(redirect && !jaces.isEmpty() && p.getController().confirmAction(sa,null,"Put "+amount+" loyalty counters on a Jace you control instead of empowering Jace?",null)) {
                Card chosen=p.getController().chooseSingleEntityForEffect(jaces,sa,"Choose a Jace",false,null);
                if(chosen!=null){GameEntityCounterTable counters=new GameEntityCounterTable();chosen.addCounter(CounterEnumType.LOYALTY,amount,p,counters);counters.replaceCounterEffect(game,sa);return;}
            }
        }
        if (!p.getCardsIn(ZoneType.Battlefield).anyMatch(CardPredicates.isType(type).and(CardPredicates.TOKEN))) {
            CardZoneTable triggerList = new CardZoneTable();
            MutableBoolean combatChanged = new MutableBoolean(false);

            StringBuilder sb = new StringBuilder("u_empower_");
            sb.append(sa.getOriginalParam("Type").toLowerCase());

            Card result = TokenInfo.getProtoType(sb.toString(), sa, p, false);
            if (result == null) //Custom Empower type.
                result = TokenInfo.getProtoType("u_empower", sa, p, false);
            // need to alter the token to add the Type from the Parameter
            result.addType(type);
            result.setName(type + " Token");
            result.setTokenSpawningAbility(sa);

            makeTokenTable(makeTokenTableInternal(p, result, 1), false, triggerList, combatChanged, sa);

            triggerList.triggerChangesZoneAll(game, sa);

            game.fireEvent(new GameEventTokenCreated());

            if (combatChanged.isTrue()) {
                game.updateCombatForView();
                game.fireEvent(new GameEventCombatChanged());
            }
        }

        CardCollectionView tgtCards = CardLists.filter(p.getCardsIn(ZoneType.Battlefield), CardPredicates.isType(type).and(CardPredicates.TOKEN));
        if (tgtCards.isEmpty()) {
            return;
        }

        Map<String, Object> params = Maps.newHashMap();
        params.put("CounterType", CounterEnumType.LOYALTY);
        params.put("Amount", amount);
        Card tgt = p.getController().chooseSingleEntityForEffect(tgtCards, sa, Localizer.getInstance().getMessage("lblChooseaCard"), false, params);

        GameEntityCounterTable table = new GameEntityCounterTable();
        tgt.addCounter(CounterEnumType.LOYALTY, amount, p, table);
        table.replaceCounterEffect(game, sa);
    }
}
