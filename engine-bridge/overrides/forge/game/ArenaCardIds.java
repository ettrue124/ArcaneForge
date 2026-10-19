package forge.game;

import java.util.HashMap;
import java.util.Map;

/** Wire identities for the bridge's single active interactive game. */
public final class ArenaCardIds {
    private static final Map<Integer,Integer> current=new HashMap<>();
    private static final Map<Integer,Integer> reverse=new HashMap<>();
    private static final Map<Integer,Integer> pendingArrival=new HashMap<>();
    private static final Map<Integer,Integer> pendingExileSelections=new HashMap<>();
    private static final Map<Integer,Integer> exileSources=new HashMap<>();
    private static int nextArrival=500000;
    private ArenaCardIds() {}
    public static synchronized void reset() {
        current.clear();reverse.clear();pendingArrival.clear();pendingExileSelections.clear();exileSources.clear();nextArrival=500000;
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
    public static synchronized void prepareBattlefield(int forgeId) {
        if(pendingArrival.containsKey(forgeId))return;
        pendingArrival.put(forgeId,id(forgeId));
        enterBattlefield(forgeId);
    }
    public static synchronized Integer finishBattlefield(int forgeId) {
        return pendingArrival.remove(forgeId);
    }
    public static synchronized void selectedForExile(int card,int source) {
        // Capture this incarnation of the source. Resolving its Forge ID later
        // would attach old exiled cards to a permanent that left and returned.
        pendingExileSelections.put(card,id(source));
    }
    public static synchronized void exileZoneChanged(int card,String from,String to) {
        Integer source=pendingExileSelections.remove(card);
        if(from.equals("Exile"))exileSources.remove(card);
        // A selection is only a display relationship once that move succeeds.
        // Replaced moves must not affect a later, unrelated exile of this card.
        if(from.equals("Hand") && to.equals("Exile") && source!=null)exileSources.put(card,source);
    }
    public static synchronized void clearPendingExileSelections() {pendingExileSelections.clear();}
    /** Wire identity of the source at the time of the successful exile. */
    public static synchronized Integer exileSource(int card) {return exileSources.get(card);}
}
