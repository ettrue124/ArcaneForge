import java.util.*;
import com.google.gson.JsonParser;
import forge.gamemodes.net.ProtocolMethod;
import forge.gamemodes.net.event.GuiGameEvent;

public class BattleModesTest {
    public static void main(String[] args) {
        var both=BattlePlayerControllerHuman.modeSelections(2,1,2);
        if(!both.equals(List.of(List.of(0),List.of(1),List.of(0,1))))throw new AssertionError(both);
        var single=BattlePlayerControllerHuman.modeSelections(2,1,1);
        if(single.contains(List.of(0,1)) || single.size()!=2)throw new AssertionError(single);
        var event=new GuiGameEvent(ProtocolMethod.getChoices,"Choose a mode",1,1,
            List.of("Add mana","Exile cards","Both modes: Add mana / Exile cards"),null,null);
        var selected=(List<?>)BattlePrompts.answer(event,JsonParser.parseString("[3]"),Map.of());
        if(!selected.equals(List.of("Both modes: Add mana / Exile cards")) || both.get(2).size()!=2)
            throw new AssertionError("Both did not map to the two original modes");
        var prompt=BattlePrompts.describe(event,Map.of());
        if(!prompt.get("min").equals(1) || !prompt.get("max").equals(1))throw new AssertionError(prompt);
        System.out.println("PASS: single and both mode legality, labeled choices, and Both answer mapping.");
    }
}
