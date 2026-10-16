/* SPDX-License-Identifier: GPL-3.0-or-later */
import com.google.gson.*;
import forge.gamemodes.net.ProtocolMethod;
import forge.gamemodes.net.event.GuiGameEvent;
import java.util.*;

/** Validate real Forge dialog contracts, including decline and stale/illegal IDs. */
public class BattlePromptsTest {
    static void check(boolean condition,String message) {if(!condition)throw new AssertionError(message);}
    static JsonElement json(String value) {return JsonParser.parseString(value);}
    static void reject(GuiGameEvent event,String answer) {
        try {BattlePrompts.answer(event,json(answer),Map.of());throw new AssertionError("Illegal answer accepted: "+answer);}
        catch(IllegalArgumentException expected) {}
    }
    public static void main(String[] args) {
        var choice=new GuiGameEvent(ProtocolMethod.getChoices,"Choose modes",0,2,List.of("Draw","Gain life"),null,null);
        check(((List<?>)BattlePrompts.answer(choice,json("[2]"),Map.of())).equals(List.of("Gain life")),"Wrong modal choice");
        check(((List<?>)BattlePrompts.answer(choice,json("[]"),Map.of())).isEmpty(),"Optional decline lost");
        reject(choice,"[1,1]");reject(choice,"[3]");reject(choice,"[1.5]");
        var order=new GuiGameEvent(ProtocolMethod.order,"Order costs","First",0,0,List.of("A","B","C"),null,null,false,false);
        var ordered=(forge.gui.interfaces.IGuiGame.OrderResult<?>)BattlePrompts.answer(order,json("[3,1,2]"),Map.of());
        check(ordered.ordered().equals(List.of("C","A","B")),"Order lost");reject(order,"[1,2]");
        var optionalOrder=new GuiGameEvent(ProtocolMethod.order,"Scry","Bottom",-1,-1,List.of("A","B"),null,null,false,false);
        check(((forge.gui.interfaces.IGuiGame.OrderResult<?>)BattlePrompts.answer(optionalOrder,json("[]"),Map.of())).ordered().isEmpty(),"Optional scry lost");
        var confirm=new GuiGameEvent(ProtocolMethod.showConfirmDialog,"Pay?","Cost","Pay","Decline",true);
        check(Boolean.FALSE.equals(BattlePrompts.answer(confirm,json("[2]"),Map.of())),"Decline answered yes");
        var text=new GuiGameEvent(ProtocolMethod.showInputDialog,"Choose number","Number",null,"",null,true);
        check(BattlePrompts.answer(text,json("\"42\""),Map.of()).equals("42"),"Numeric text lost");reject(text,"\"NaN\"");
        var reveal=new GuiGameEvent(ProtocolMethod.getChoices,"Reveal",-1,-1,List.of("A","B"),null,null);
        check(BattlePrompts.describe(reveal,Map.of()).get("max").equals(0),"Reveal requested card selection");
        reject(reveal,"[1]");
        System.out.println("PASS: Forge optional/mode/order/confirmation/text/reveal validation");
    }
}
