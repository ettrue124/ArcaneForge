package forge.game;

import java.util.HashMap;
import java.util.Map;

/** Wire identities for the bridge's single active interactive game. */
public final class ArenaCardIds {
    private static final Map<Integer,Integer> current=new HashMap<>();
    private static final Map<Integer,Integer> reverse=new HashMap<>();
    private static int nextArrival=500000;
    private ArenaCardIds() {}
    public static synchronized void reset() {
        current.clear();reverse.clear();nextArrival=500000;
    }
    public static synchronized int id(int forgeId) {
        return current.getOrDefault(forgeId,1000+forgeId);
    }
    public static synchronized int forgeId(int wireId) {
        return reverse.getOrDefault(wireId,wireId-1000);
    }
    public static synchronized int enterBattlefield(int forgeId) {
        // Arena sorts stack ages in descending ID order. The local player's
        // on-screen layout places increasing arrival identities on the right.
        // Preserve monotonic arrivals independently of shuffled Forge IDs.
        if(nextArrival>=900000)throw new IllegalStateException("Battlefield identity space exhausted");
        int wireId=nextArrival++;
        current.put(forgeId,wireId);reverse.put(wireId,forgeId);
        return wireId;
    }
}
