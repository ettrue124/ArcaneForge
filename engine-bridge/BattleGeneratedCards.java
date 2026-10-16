/* SPDX-License-Identifier: GPL-3.0-or-later */
import com.google.gson.*;
import forge.game.card.Card;
import java.nio.file.*;
import java.util.*;

/** Resolve generated tokens using real printing links and their printed traits. */
final class BattleGeneratedCards {
    private static final JsonObject catalogue=load();
    private static JsonObject load() {
        try {return JsonParser.parseString(Files.readString(Path.of("data/arena-generated-cards.json"))).getAsJsonObject();}
        catch(java.io.IOException error){throw new IllegalStateException("Generated-card catalogue missing",error);}
    }
    private static boolean statMatches(String printed,int actual) {
        return printed.isEmpty() || !printed.matches("-?[0-9]+") || Integer.parseInt(printed)==actual;
    }
    private static boolean matches(Card card,JsonObject token) {
        String name=token.get("name").getAsString();
        return (name.equals(card.getName()) || (name+" Token").equals(card.getName()))
            && statMatches(token.get("power").getAsString(),card.getBasePower())
            && statMatches(token.get("toughness").getAsString(),card.getBaseToughness());
    }
    static Integer printing(Card card,Map<Integer,Integer> identities) {
        Card copy=card.getCopiedPermanent();
        if(copy!=null && identities.containsKey(copy.getId()))return identities.get(copy.getId());
        var tokens=catalogue.getAsJsonObject("tokens");List<Integer> candidates=new ArrayList<>();
        var spawn=card.getTokenSpawningAbility();
        if(spawn!=null) {
            int parent=identities.getOrDefault(spawn.getHostCard().getId(),0);
            var linked=catalogue.getAsJsonObject("links").getAsJsonArray(Integer.toString(parent));
            if(linked!=null)for(var value:linked) {
                int id=value.getAsInt();if(matches(card,tokens.getAsJsonObject(Integer.toString(id))))candidates.add(id);
            }
        }
        // Equivalent printings can have different art. Never substitute a token
        // with different printed abilities just because its name matches.
        if(candidates.isEmpty())for(var entry:tokens.entrySet())if(matches(card,entry.getValue().getAsJsonObject()))
            candidates.add(Integer.parseInt(entry.getKey()));
        Set<String> signatures=new HashSet<>();for(int id:candidates)signatures.add(tokens.getAsJsonObject(Integer.toString(id)).get("signature").getAsString());
        if(candidates.isEmpty() || signatures.size()!=1)return null;
        return Collections.min(candidates);
    }
}
