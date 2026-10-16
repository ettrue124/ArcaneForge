/* SPDX-License-Identifier: GPL-3.0-or-later */
import com.google.gson.*;
import java.nio.file.*;

/** Only select a printing explicitly linked to the submitted original. */
final class BattleFaces {
    private static final JsonObject faces=read();
    private static JsonObject read() {
        try {return JsonParser.parseString(Files.readString(Path.of("data/arena-linked-faces.json"))).getAsJsonObject();}
        catch(java.io.IOException e) {throw new IllegalStateException("Export linked Arena faces before starting Forge",e);}
    }
    static int printing(int original,String currentName) {
        var linked=faces.getAsJsonObject(Integer.toString(original));
        return linked!=null && linked.has(currentName)?linked.get(currentName).getAsInt():original;
    }
    static Integer copiedPrinting(forge.game.card.Card card,java.util.Map<Integer,Integer> identities) {
        return copiedPrinting(card,identities,new java.util.HashSet<>());
    }
    private static Integer copiedPrinting(forge.game.card.Card card,java.util.Map<Integer,Integer> identities,java.util.Set<Integer> visited) {
        if(!visited.add(card.getId()))return null;
        forge.game.card.Card origin=card.getCopiedPermanent();
        if(card.isCloned()) {
            var latest=card.getCloneStates().entrySet().stream().max(java.util.Map.Entry.comparingByKey());
            if(latest.isPresent())origin=latest.get().getValue().getOrigin();
        }
        if(origin==null || origin==card)return null;
        Integer original=copiedPrinting(origin,identities,visited);
        if(original==null)original=identities.get(origin.getId());
        // Copy exceptions can change the name; don't select unrelated art.
        return original==null || !origin.getName().equals(card.getName())?null:printing(original,card.getName());
    }
}
