/* SPDX-License-Identifier: GPL-3.0-or-later */
import com.google.gson.*;
import forge.game.card.CardView;
import forge.game.player.Player;
import forge.game.player.PlayerView;
import forge.game.spellability.SpellAbilityView;
import forge.game.spellability.StackItemView;
import forge.gamemodes.net.event.GuiGameEvent;
import java.util.*;

/** Preserve the original Forge objects behind each native Arena decision. */
final class BattlePrompts {
    private static Map<String,Object> obj(Object... fields) { return BattleInputs.obj(fields); }
    private static List<?> list(Object value) {
        List<Object> out=new ArrayList<>();
        if(value instanceof Iterable<?> values)for(Object item:values)out.add(item);
        return out;
    }
    private static int id(Object value,Map<Player,Integer> seats) {
        if(value instanceof CardView card)return forge.game.ArenaCardIds.id(card.getId());
        if(value instanceof StackItemView stack)return stack.isAbility()?1000000+stack.getId():forge.game.ArenaCardIds.id(stack.getSourceCard().getId());
        if(value instanceof PlayerView player)for(var entry:seats.entrySet())
            if(entry.getKey().getId()==player.getId())return entry.getValue();
        throw new IllegalArgumentException("Not a game entity: "+value);
    }
    private static List<?> options(GuiGameEvent event) {
        Object[] a=event.getObjects();
        return switch(event.getMethod().name()) {
            case "getChoices" -> list(a[3]);
            case "getAbilityToPlay", "chooseSingleEntityForEffect", "chooseEntitiesForEffect", "one", "oneOrNone" -> list(a[1]);
            case "showOptionDialog" -> list(a[3]);
            case "confirm" -> list(a[3]);
            case "showConfirmDialog" -> List.of(a[2],a[3]);
            case "order" -> {List<Object> all=new ArrayList<>();all.addAll(list(a[5]));all.addAll(list(a[4]));yield all;}
            case "many" -> list(a[4]);
            default -> throw new IllegalArgumentException("Unsupported choice: "+event.getMethod());
        };
    }
    static Map<String,Object> describe(GuiGameEvent event,Map<Player,Integer> seats) {
        Object[] a=event.getObjects();String method=event.getMethod().name();
        Map<String,Object> out=obj("type","InputGuiChoice","method",method,"sourceId",0,"title",String.valueOf(a[0]));
        if(method.equals("order") && "Surveil".equals(a[0])) {
            out.putAll(obj("type","InputGuiGroups","surveil",true,"choices",list(a[4]).stream().map(c->id(c,seats)).toList(),
                "toTop",true,"toBottom",false));
            return out;
        }
        if(method.equals("showInputDialog")) {
            out.put("type","InputGuiText");out.put("initial",a[3]);return out;
        }
        if(method.equals("assignCombatDamage")) {
            CardView attacker=(CardView)a[0];int amount=(Integer)a[2];
            out.putAll(obj("type","InputGuiCombatDamage","sourceId",id(attacker,seats),"amount",amount,
                "ignoreOrder",a[4],"maySkip",a[5]));
            List<Object> targets=new ArrayList<>();
            for(Object value:list(a[1])) {
                CardView card=(CardView)value;
                int lethal=attacker.getCurrentState().hasDeathtouch()?1:Math.max(0,card.getLethalDamage());
                targets.add(obj("id",id(card,seats),"min",0,"max",amount,"lethal",lethal));
            }
            if(a[3]!=null && (attacker.getCurrentState().hasTrample() || Boolean.TRUE.equals(a[4])))
                targets.add(obj("id",id(a[3],seats),"min",0,"max",amount,"lethal",0));
            out.put("targets",targets);return out;
        }
        if(method.equals("assignGenericAmount")) {
            int amount=(Integer)a[2];out.putAll(obj("type","InputGuiDistribution","sourceId",a[0]==null?0:id(a[0],seats),
                "amount",amount,"minPerTarget",Boolean.TRUE.equals(a[3])?1:0,"title",a[4]));
            List<Object> targets=new ArrayList<>();
            for(var entry:((Map<?,?>)a[1]).entrySet())targets.add(obj("id",id(entry.getKey(),seats),"min",0,
                "max",Math.min(amount,(Integer)entry.getValue())));
            out.put("targets",targets);return out;
        }
        if(method.equals("manipulateCardList")) {
            out.putAll(obj("type","InputGuiGroups","choices",list(a[2]).stream().map(c->id(c,seats)).toList(),
                "toTop",a[3],"toBottom",a[4],"toAnywhere",a[5]));return out;
        }
        List<?> choices=options(event);int min=1,max=1;
        if(method.equals("getChoices") && choices.stream().anyMatch(c->c instanceof StackItemView)
            && choices.stream().allMatch(c->c instanceof StackItemView || "[FINISH TARGETING]".equals(c))) {
            boolean finish=choices.contains("[FINISH TARGETING]");
            var targets=choices.stream().filter(c->c instanceof StackItemView).map(c->id(c,seats)).toList();
            var menu=new ArrayList<Object>();
            var totals=new HashMap<String,Integer>();var positions=new HashMap<String,Integer>();
            for(Object choice:choices)if(choice instanceof StackItemView stack)
                totals.merge(stack.getSourceCard().getName(),1,Integer::sum);
            for(Object choice:choices)if(choice instanceof StackItemView stack) {
                String name=stack.getSourceCard().getName();
                int number=positions.merge(name,1,Integer::sum);
                boolean yours=seats.entrySet().stream().anyMatch(e->e.getValue()==1 && e.getKey().getId()==stack.getActivatingPlayer().getId());
                String label=(yours?"YOURS: ":"Opponent: ")+name+(totals.get(name)>1?" ("+number+")":"");
                menu.add(obj("id",id(stack,seats),"label",label));
            }
            out.putAll(obj("type","InputGuiStackTargets","targets",targets,"selected",List.of(),
                "min",finish?0:Math.max(0,(Integer)a[1]),"max",1,"targetIndex",0,"targetingPlayer",1,
                "finish",finish,"entityChoices",false,"options",menu,"readableStackTargets",true));
            return out;
        }
        if(method.equals("getAbilityToPlay"))min=0;
        if(method.equals("getChoices")) {
            min=Math.max(0,(Integer)a[1]);max=(Integer)a[2]<0?choices.size():(Integer)a[2];
            if((Integer)a[1]==-1 && (Integer)a[2]==-1)max=0; // reveal/acknowledge only
        }
        if(method.equals("chooseSingleEntityForEffect"))min=Boolean.TRUE.equals(a[3])?0:1;
        if(method.equals("chooseEntitiesForEffect")){min=(Integer)a[2];max=(Integer)a[3];}
        if(method.equals("order")) {
            int remainingMin=(Integer)a[2],remainingMax=(Integer)a[3];
            min=remainingMax<0?0:choices.size()-remainingMax;
            max=remainingMin<0?choices.size():choices.size()-remainingMin;
            if(remainingMin==0 && remainingMax==0)out.put("type","InputGuiOrder");
        }
        if(method.equals("many")) {
            min=Math.max(0,(Integer)a[2]);max=(Integer)a[3]<0?choices.size():(Integer)a[3];
            if(a.length>6 && a[6]!=null)out.put("sourceId",id(a[6],seats));
        }
        if(method.equals("one")) {min=1;max=1;}
        if(method.equals("oneOrNone")) {min=0;max=1;}
        boolean entities=choices.stream().allMatch(c->c instanceof CardView || c instanceof PlayerView);
        // OrderReq accepts card instances, but not arbitrary strings or ability views.
        if(out.get("type").equals("InputGuiOrder") && !entities)out.put("type","InputGuiChoice");
        out.put("entityChoices",entities);out.put("min",min);out.put("max",max);
        if(method.equals("getAbilityToPlay") && a[0]!=null)out.put("sourceId",id(a[0],seats));
        if(method.equals("confirm")) {out.put("title",String.valueOf(a[1]).replaceAll("\\s*\\(\\d+\\)", ""));if(a[0]!=null)out.put("sourceId",id(a[0],seats));}
        if(method.equals("order")) {out.put("title",String.valueOf(a[0]).replaceAll("\\s*\\(\\d+\\)", ""));out.put("top",String.valueOf(a[1]));if(a[6]!=null)out.put("sourceId",id(a[6],seats));}
        if(Integer.valueOf(0).equals(out.get("sourceId")) && !choices.isEmpty()) {
            if(choices.get(0) instanceof SpellAbilityView sav && sav.getHostCard()!=null)out.put("sourceId",id(sav.getHostCard(),seats));
            else if(choices.get(0) instanceof CardView cv)out.put("sourceId",id(cv,seats));
        }
        List<Object> described=new ArrayList<>();List<String> labels=new ArrayList<>();
        for(int i=0;i<choices.size();i++) {
            Object choice=choices.get(i);String label=String.valueOf(choice);
            if(method.equals("getChoices") && a.length>5 && a[5] instanceof forge.util.FSerializableFunction<?,?> display)
                label=String.valueOf(((forge.util.FSerializableFunction<Object,Object>)display).apply(choice));
            if(method.equals("getAbilityToPlay")) {
                if(a[0] instanceof forge.game.card.CardView cv) {
                    String prefix=cv.getName()+" - ";
                    if(label.startsWith(prefix))label=label.substring(prefix.length());
                    prefix=cv.getName()+": ";
                    if(label.startsWith(prefix))label=label.substring(prefix.length());
                }
                label=label.replace("\r\n"," ").replace('\n',' ').replaceAll("\\s+"," ").trim();
                if(label.isEmpty())label="Ability "+(i+1);
            }
            labels.add(label);
        }
        for(int i=0;i<choices.size();i++) {
            String label=labels.get(i);
            if(Collections.frequency(labels,label)>1)label+=" ("+(i+1)+")";
            described.add(obj("id",entities?id(choices.get(i),seats):i+1,"index",i,"label",label));
        }
        if(!choices.isEmpty() && choices.stream().allMatch(c->c instanceof forge.card.MagicColor.Color)) {
            out.put("nativeKind","color");
            for(int i=0;i<choices.size();i++)((Map<String,Object>)described.get(i)).put("nativeValue",((forge.card.MagicColor.Color)choices.get(i)).name());
        }
        if(!choices.isEmpty() && choices.stream().allMatch(c->c instanceof forge.game.card.CounterType)) {
            out.put("nativeKind","counterType");
            for(int i=0;i<choices.size();i++)((Map<String,Object>)described.get(i)).put("nativeValue",choices.get(i).toString());
        }
        if(!choices.isEmpty() && choices.stream().allMatch(c->c instanceof forge.game.replacement.ReplacementEffect)) {
            out.put("nativeKind","replacement");
            for(int i=0;i<choices.size();i++) {
                var effect=(forge.game.replacement.ReplacementEffect)choices.get(i);
                var data=(Map<String,Object>)described.get(i);
                data.put("sourceId",forge.game.ArenaCardIds.id(effect.getHostCard().getId()));
                data.put("description",effect.getDescription());
            }
        }
        out.put("options",described);
        if(entities)out.put("choices",choices.stream().map(c->id(c,seats)).toList());
        // Battlefield choices use native in-place selection; cards in other
        // zones use the face-up lineup. Players and stack items have their own
        // workflows and must not be converted to card-search placeholders.
        if(max>0 && !choices.isEmpty() && choices.stream().allMatch(c->c instanceof CardView))
            out.put("nativeKind",choices.stream().allMatch(c->((CardView)c).getZone()==forge.game.zone.ZoneType.Battlefield)
                ?"battlefield":"search");
        return out;
    }
    static Object answer(GuiGameEvent event,JsonElement value,Map<Player,Integer> seats) {
        Object[] a=event.getObjects();String method=event.getMethod().name();
        Map<String,Object> descriptor=describe(event,seats);
        if(descriptor.get("type").equals("InputGuiStackTargets")) {
            if(value==null || !value.isJsonArray())throw new IllegalArgumentException("Stack target IDs required");
            List<Object> selected=new ArrayList<>();var choices=options(event);
            for(var wire:value.getAsJsonArray()) {
                int target=integer(wire);
                selected.add(choices.stream().filter(c->c instanceof StackItemView && id(c,seats)==target).findFirst()
                    .orElseThrow(()->new IllegalArgumentException("Illegal stack target")));
            }
            if(selected.size()<(Integer)descriptor.get("min") || selected.size()>1)throw new IllegalArgumentException("Wrong stack target count");
            if(selected.isEmpty() && Boolean.TRUE.equals(descriptor.get("finish")))selected.add("[FINISH TARGETING]");
            return selected;
        }
        if(method.equals("showInputDialog")) {
            if(value==null || !value.isJsonPrimitive() || !value.getAsJsonPrimitive().isString())throw new IllegalArgumentException("Text required");
            String text=value.getAsString();
            if(Boolean.TRUE.equals(a[5]) && !text.matches("-?[0-9]+"))throw new IllegalArgumentException("Numeric text required");
            if(a[4]!=null && !list(a[4]).isEmpty() && !list(a[4]).contains(text))throw new IllegalArgumentException("Text is not an offered option");
            return text;
        }
        if(method.equals("assignCombatDamage") || method.equals("assignGenericAmount")) {
            if(value==null || !value.isJsonArray())throw new IllegalArgumentException("Damage allocations required");
            Map<Integer,Integer> allocated=new LinkedHashMap<>();int total=0;
            var targets=(List<Map<String,Object>>)descriptor.get("targets");
            for(JsonElement item:value.getAsJsonArray()) {
                int target=integer(item.getAsJsonObject().get("id")),amount=integer(item.getAsJsonObject().get("amount"));
                var legal=targets.stream().filter(t->(Integer)t.get("id")==target).findFirst().orElseThrow(()->new IllegalArgumentException("Illegal allocation recipient"));
                if(allocated.putIfAbsent(target,amount)!=null || amount>(Integer)legal.get("max"))throw new IllegalArgumentException("Invalid allocation");
                if(amount>0 && descriptor.containsKey("minPerTarget") && amount<(Integer)descriptor.get("minPerTarget"))throw new IllegalArgumentException("Allocation below minimum");
                total=Math.addExact(total,amount);
            }
            if(total!=(Integer)descriptor.get("amount"))throw new IllegalArgumentException("All damage/amount must be assigned");
            if(method.equals("assignGenericAmount") && Boolean.TRUE.equals(a[3]))
                for(var target:targets)if(allocated.getOrDefault((Integer)target.get("id"),0)<1)
                    throw new IllegalArgumentException("Each selected target requires at least one");
            if(method.equals("assignCombatDamage")) {
                if(a[3]!=null && allocated.getOrDefault(id(a[3],seats),0)>0)
                    for(Object blocker:list(a[1])) {
                        int blockerId=id(blocker,seats);
                        var target=targets.stream().filter(t->(Integer)t.get("id")==blockerId).findFirst().orElseThrow();
                        if(allocated.getOrDefault(blockerId,0)<(Integer)target.get("lethal"))
                            throw new IllegalArgumentException("Assign lethal damage to every blocker before trample damage");
                    }
                if(!Boolean.TRUE.equals(a[4]))for(int i=0;i<targets.size();i++) {
                    var t=targets.get(i);boolean later=false;
                    for(int j=i+1;j<targets.size();j++)if(allocated.getOrDefault((Integer)targets.get(j).get("id"),0)>0)later=true;
                    if(later && allocated.getOrDefault((Integer)t.get("id"),0)<(Integer)t.get("lethal"))throw new IllegalArgumentException("Assign lethal damage before later blockers/defender");
                }
                Map<CardView,Integer> result=new LinkedHashMap<>();
                for(Object blocker:list(a[1]))result.put((CardView)blocker,allocated.getOrDefault(id(blocker,seats),0));
                if(a[3]!=null)result.put(null,allocated.getOrDefault(id(a[3],seats),0));
                return result;
            }
            Map<Object,Integer> result=new LinkedHashMap<>();
            for(Object target:((Map<?,?>)a[1]).keySet())result.put(target,allocated.getOrDefault(id(target,seats),0));return result;
        }
        if(method.equals("order") && "Surveil".equals(a[0])) {
            if(value==null || !value.isJsonArray())throw new IllegalArgumentException("Surveil groups required");
            List<?> choices=list(a[4]);Set<Integer> used=new HashSet<>();List<Object> top=new ArrayList<>();
            for(var entry:value.getAsJsonArray()) {
                var group=entry.getAsJsonObject();int zone=integer(group.get("zoneType")),sub=integer(group.get("subZoneType"));
                if(!(zone==1 && sub==1 || zone==5 && sub==0))throw new IllegalArgumentException("Illegal surveil destination");
                for(var wire:group.getAsJsonArray("ids")) {
                    int target=integer(wire);
                    Object card=choices.stream().filter(c->id(c,seats)==target).findFirst().orElseThrow(()->new IllegalArgumentException("Card not revealed for surveil"));
                    if(!used.add(target))throw new IllegalArgumentException("Card grouped twice");
                    if(zone==1)top.add(card);
                }
            }
            if(used.size()!=choices.size())throw new IllegalArgumentException("Every surveil card must be grouped");
            return new forge.gui.interfaces.IGuiGame.OrderResult<>(top,false);
        }
        if(method.equals("manipulateCardList")) {
            if(value==null || !value.isJsonArray())throw new IllegalArgumentException("Card groups required");
            List<?> movable=list(a[2]);Set<Integer> used=new HashSet<>();List<Object> top=new ArrayList<>(),bottom=new ArrayList<>();
            for(var group:value.getAsJsonArray()) {
                int sub=integer(group.getAsJsonObject().get("subZoneType"));
                if(sub!=1 && sub!=2 || sub==1 && !Boolean.TRUE.equals(a[3]) || sub==2 && !Boolean.TRUE.equals(a[4]))throw new IllegalArgumentException("Illegal card destination");
                for(var wire:group.getAsJsonObject().getAsJsonArray("ids")) {
                    int target=integer(wire);
                    Object card=movable.stream().filter(c->id(c,seats)==target).findFirst().orElseThrow(()->new IllegalArgumentException("Card cannot be moved"));
                    if(!used.add(target))throw new IllegalArgumentException("Card grouped twice");(sub==1?top:bottom).add(card);
                }
            }
            if(used.size()!=movable.size())throw new IllegalArgumentException("Every revealed card must be grouped");
            List<Object> result=new ArrayList<>(top);
            for(Object c:list(a[1]))if(!movable.contains(c))result.add(c);
            Collections.reverse(bottom);result.addAll(bottom);return result;
        }
        if(value==null || !value.isJsonArray())throw new IllegalArgumentException("Choice IDs required");
        List<?> choices=options(event);List<Object> selected=new ArrayList<>();Set<Integer> used=new HashSet<>();
        boolean entities=(Boolean)descriptor.get("entityChoices");
        for(var entry:value.getAsJsonArray()) {
            int wire=integer(entry),index=entities?-1:wire-1;
            if(entities)for(int i=0;i<choices.size();i++)if(id(choices.get(i),seats)==wire)index=i;
            if(index<0 || index>=choices.size() || !used.add(index))throw new IllegalArgumentException("Illegal or duplicate choice");
            selected.add(choices.get(index));
        }
        if(selected.size()<(Integer)descriptor.get("min") || selected.size()>(Integer)descriptor.get("max"))throw new IllegalArgumentException("Choice count out of range");
        return switch(method) {
            case "confirm", "showConfirmDialog" -> selected.get(0).equals(choices.get(0));
            case "showOptionDialog" -> choices.indexOf(selected.get(0));
            case "getAbilityToPlay", "chooseSingleEntityForEffect", "one", "oneOrNone" -> selected.isEmpty()?null:selected.get(0);
            case "order" -> new forge.gui.interfaces.IGuiGame.OrderResult<>(selected,false);
            default -> selected;
        };
    }
    private static int integer(JsonElement value) {
        if(value==null || !value.toString().matches("[0-9]+"))throw new IllegalArgumentException("Nonnegative integer required");
        return value.getAsInt();
    }
}
