/* SPDX-License-Identifier: GPL-3.0-or-later */
package forge.game.ability.effects;
import com.google.gson.*;
import forge.card.*;
import forge.game.*;
import forge.game.ability.*;
import forge.game.card.*;
import forge.game.event.*;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;
import forge.item.PaperCard;
import java.nio.file.*;
import java.util.*;
import org.apache.commons.lang3.mutable.MutableBoolean;
/** The shipped client defines thirty draft abilities, ten at each loyalty cost. */
public final class ArenaEternities extends TokenEffectBase {
 private record Option(int id,String cost,String text,List<String> lines){}
 private static List<Option> options;
 private static synchronized List<Option> options(){
  if(options==null)try{
   var root=JsonParser.parseString(Files.readString(Path.of("engine-bridge/eternities-abilities.json"))).getAsJsonObject();
   var rows=new ArrayList<Option>();
   for(var value:root.getAsJsonArray("abilities")){
    var row=value.getAsJsonObject();var lines=new ArrayList<String>();for(var line:row.getAsJsonArray("lines"))lines.add(line.getAsString());
    rows.add(new Option(row.get("id").getAsInt(),row.get("cost").getAsString(),row.get("text").getAsString(),List.copyOf(lines)));
   }options=List.copyOf(rows);
  }catch(Exception e){throw new IllegalStateException("Enter the Eternities ability definitions are unavailable",e);}
  return options;
 }
 @Override public void resolve(SpellAbility sa){
  var player=sa.getActivatingPlayer();var game=player.getGame();var host=sa.getHostCard();
  if(sa.getTargets().getTargetCards().isEmpty())return;
  Card target=sa.getTargets().getTargetCards().get(0);Card current=game.getCardState(target);
  if(current==null || !current.equalsWithGameTimestamp(target) || !current.isInPlay() || !current.canBeTargetedBy(sa))return;
  String name=current.getName(),image=current.getImageKey();ColorSet color=current.getColor();int power=Math.max(0,current.getNetPower());
  Card exiled=game.getAction().moveTo(current.getOwner().getZone(ZoneType.Exile),current,sa);
  if(exiled==null || !exiled.isInZone(ZoneType.Exile))return;
  var selected=new ArrayList<Option>();
  for(String cost:List.of("+1","-2","-8")){
   var pool=new ArrayList<Option>();for(var option:options())if(option.cost().equals(cost))pool.add(option);
   Collections.shuffle(pool,forge.util.MyRandom.getRandom());pool=new ArrayList<>(pool.subList(0,Math.min(3,pool.size())));
   var abilities=new ArrayList<SpellAbility>();var map=new IdentityHashMap<SpellAbility,Option>();
   for(var option:pool){
    for(String line:option.lines())if(line.startsWith("SVar:")){int colon=line.indexOf(':',5);host.setSVar(line.substring(5,colon),line.substring(colon+1));}
    var ability=AbilityFactory.getAbility(option.lines().get(0).substring(2),host);
    ability.setActivatingPlayer(player);ability.setDescription("["+cost+"] "+option.text());abilities.add(ability);map.put(ability,option);
   }
   var chosen=player.getController().chooseSingleSpellForEffect(abilities,sa,"Draft a ["+cost+"] loyalty ability",Map.of());
   if(chosen==null || !map.containsKey(chosen))throw new IllegalStateException("A loyalty ability must be drafted");selected.add(map.get(chosen));
  }
  var script=new ArrayList<String>(List.of("Name:"+name,"ManaCost:no cost","Types:Legendary Planeswalker","Loyalty:0"));
  for(var option:selected)script.addAll(option.lines());
  script.add("Oracle:"+String.join("\\n",selected.stream().map(o->"["+o.cost()+"]: "+o.text()).toList()));
  CardRules rules=CardRules.fromScript(script);
  var prototype=Card.fromPaperCard(new PaperCard(rules,CardEdition.UNKNOWN_CODE,CardRarity.Special),player);
  prototype.setName(name);prototype.setColor(color);prototype.setImageKey(image);prototype.setGamePieceType(GamePieceType.TOKEN);prototype.setTokenSpawningAbility(sa);
  var before=new HashSet<Object>();host.getRemembered().forEach(before::add);String previous=sa.getParam("RememberTokens");sa.putParam("RememberTokens","True");
  var zones=new CardZoneTable();var combat=new MutableBoolean(false);
  try{makeTokenTable(makeTokenTableInternal(player,prototype,1),false,zones,combat,sa);}finally{if(previous==null)sa.removeParam("RememberTokens");else sa.putParam("RememberTokens",previous);}
  var counters=new GameEntityCounterTable();var made=new ArrayList<Card>();
  for(Object item:host.getRemembered())if(item instanceof Card card && !before.contains(item)){card.addCounter(CounterEnumType.LOYALTY,power,player,counters);made.add(card);}
  counters.replaceCounterEffect(game,sa);for(Card card:made)host.removeRemembered(card);
  zones.triggerChangesZoneAll(game,sa);game.fireEvent(new GameEventTokenCreated());
  if(combat.isTrue()){game.updateCombatForView();game.fireEvent(new GameEventCombatChanged());}
 }
}
