import com.google.gson.*;
import forge.game.Game;
import forge.game.card.*;
import forge.game.zone.ZoneType;
import java.util.*;

/** Opt-in prepared positions for local manual feature checks. */
final class FeatureScenario {
    static void apply(Game game, JsonObject scenario) {
        var setups=scenario.getAsJsonArray("players");
        int index=0;
        for(var player:game.getRegisteredPlayers()) {
            var setup=setups.get(index++).getAsJsonObject();
            var pool=new ArrayList<Card>();
            for(var card:player.getAllCards())pool.add(card);
            for(var card:new ArrayList<Card>(pool))
                if(card.isInZone(ZoneType.Hand))game.getAction().moveTo(player.getZone(ZoneType.Library),card,null);
            // Moving zones can replace card objects; collect the current library.
            pool.clear();
            for(var card:player.getCardsIn(ZoneType.Library))pool.add(card);
            for(String pile:List.of("battlefield","hand")) {
                for(var name:setup.getAsJsonArray(pile)) {
                    Card chosen=null;
                    for(var card:pool)if(card.getName().equals(name.getAsString())) {chosen=card;break;}
                    if(chosen==null)throw new IllegalArgumentException("Missing scenario card: "+name);
                    pool.remove(chosen);
                    var placed=game.getAction().moveTo(player.getZone(pile.equals("hand")?ZoneType.Hand:ZoneType.Battlefield),chosen,null);
                    if(pile.equals("battlefield")) {
                        placed.setSickness(false);
                        if(placed.isPlaneswalker())placed.setCounters(CounterEnumType.LOYALTY,setup.get("loyalty").getAsInt());
                    }
                }
            }
        }
    }
}

