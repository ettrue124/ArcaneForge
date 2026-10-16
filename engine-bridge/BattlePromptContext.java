import java.util.LinkedHashMap;
import java.util.Map;
import java.util.function.Supplier;

/** Semantic context captured on the rules thread before a GUI call blocks. */
final class BattlePromptContext {
    private static final ThreadLocal<Map<String,Object>> CURRENT = ThreadLocal.withInitial(Map::of);
    static Map<String,Object> capture() { return new LinkedHashMap<>(CURRENT.get()); }
    static <T> T with(Map<String,Object> values, Supplier<T> action) {
        var previous=CURRENT.get();
        var next=new LinkedHashMap<>(previous);next.putAll(values);CURRENT.set(next);
        try {return action.get();} finally {CURRENT.set(previous);}
    }
}
