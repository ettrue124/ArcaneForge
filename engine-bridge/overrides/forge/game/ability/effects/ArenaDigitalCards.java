/* SPDX-License-Identifier: GPL-3.0-or-later */
package forge.game.ability.effects;
import com.google.gson.*;
import forge.StaticData;
import forge.card.MagicColor;
import forge.game.ability.*;
import forge.game.card.*;
import forge.game.card.perpetual.PerpetualAbilities;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import forge.game.trigger.Trigger;
import forge.game.zone.ZoneType;
import forge.item.PaperCard;
import forge.util.Aggregates;
import java.nio.file.*;
import java.util.*;
/** Native digital actions that have no equivalent Forge script primitive. */
public final class ArenaDigitalCards {
 private record Entry(String name,String set){}
 private static List<Entry> pool;
 private static Set<String> poolNames;
 public static synchronized boolean isRandomPoolCard(PaperCard card){
  if(poolNames==null){var names=new HashSet<String>();for(var entry:pool())names.add(entry.name());poolNames=Set.copyOf(names);}
  return poolNames.contains(card.getName());
 }
 private static synchronized List<Entry> pool(){
  if(pool==null)try{
   var root=JsonParser.parseString(Files.readString(Path.of("engine-bridge/arena-random-pool.json"))).getAsJsonObject();
   var rows=new ArrayList<Entry>();
   for(var value:root.getAsJsonArray("cards")){var row=value.getAsJsonObject();rows.add(new Entry(row.get("name").getAsString(),row.get("set").getAsString()));}
   pool=List.copyOf(rows);
  }catch(Exception e){throw new IllegalStateException("Arena random-card pool is unavailable",e);}
  return pool;
 }
 public static void resolve(SpellAbility sa){
  switch(sa.getParam("ArenaDigital")){
   case "RandomConjure" -> randomConjure(sa,sa.hasParam("RandomManaValue")?AbilityUtils.calculateAmount(sa.getHostCard(),sa.getParam("RandomManaValue"),sa):null);
   case "SalvagedImitations" -> {int x=AbilityUtils.calculateAmount(sa.getHostCard(),"X",sa);for(int i=1;i<=x;i++)randomConjure(sa,i);}
   case "RunebladeReturn" -> runeblade(sa);
   case "BeholdPlaneswalker" -> behold(sa);
   case "OpulentColor" -> color(sa);
   case "BanescaleTypes" -> banescale(sa);
   case "EnterEternities" -> new ArenaEternities().resolve(sa);
   default -> throw new IllegalArgumentException("Unknown digital action: "+sa.getParam("ArenaDigital"));
  }
 }
 private static void randomConjure(SpellAbility sa,Integer mv){
  Card host=sa.getHostCard();Player player=sa.getActivatingPlayer();
  var candidates=new ArrayList<String>();
  String type=sa.getParamOrDefault("RandomType","Card");String set=sa.getParam("RandomSet");
  for(Entry entry:pool()){
   if(set!=null && !set.equals(entry.set()))continue;
   PaperCard pc=StaticData.instance().getCommonCards().getCard(entry.name());
   if(pc==null || pc.getRules().isVariant())continue;
   if(mv!=null && pc.getRules().getManaCost().getCMC()!=mv)continue;
   boolean matches=type.equals("Card") || Arrays.stream(type.split(",")).anyMatch(t->pc.getRules().getType().hasStringType(t));
   if(matches)candidates.add(entry.name());
  }
  if(candidates.isEmpty())return;
  String previous=sa.getParam("Name");sa.putParam("Name",Aggregates.random(candidates));
  try{new MakeCardEffect().resolve(sa);}finally{if(previous==null)sa.removeParam("Name");else sa.putParam("Name",previous);}
 }
 private static void runeblade(SpellAbility sa){
  Card card=sa.getHostCard().getGame().getCardState(sa.getHostCard());
  if(card==null || !card.isInZone(ZoneType.Graveyard))return;
  Card returned=card.getGame().getAction().moveTo(card.getOwner().getZone(ZoneType.Battlefield),card,sa);
  if(returned==null)return;
  long timestamp=card.getGame().getNextTimestamp();
  var changes=new CardTraitChanges(List.of(),List.of(),List.of(),List.of(),trait->trait instanceof Trigger && trait.hasParam("ArenaRuneblade"));
  returned.addChangedCardTraits(changes,timestamp,0,true);
  returned.addPerpetual(new PerpetualAbilities(timestamp,changes));returned.updateStateForView();
 }
 private static void behold(SpellAbility sa){
  Card host=sa.getHostCard();Player player=sa.getActivatingPlayer();
  var choices=CardLists.getValidCards(player.getCardsIn(List.of(ZoneType.Hand,ZoneType.Battlefield)),"Planeswalker",player,host,sa);
  if(choices.isEmpty())return;
  Card chosen=player.getController().chooseSingleEntityForEffect(choices,sa,"You may behold a planeswalker",true,null);
  if(chosen==null)return;
  if(chosen.isInZone(ZoneType.Hand))host.getGame().getAction().reveal(new CardCollection(chosen),player);
  host.addRemembered(chosen);
 }
 private static void color(SpellAbility sa){
  Card host=sa.getHostCard();var choices=new ArrayList<String>();
  for(String name:MagicColor.Constant.ONLY_COLORS)if(!host.getColor().hasAnyColor(MagicColor.fromName(name)))choices.add(name);
  if(choices.isEmpty())return;
  host.setChosenColors(List.of(Aggregates.random(choices)));
  var animate=AbilityFactory.getAbility("DB$ Animate | Defined$ Self | Colors$ ChosenColor | Duration$ Permanent",host);
  animate.setActivatingPlayer(sa.getActivatingPlayer());AbilityUtils.resolve(animate);host.setChosenColors(List.of());
 }
 private static void banescale(SpellAbility sa){
  Card host=sa.getHostCard();Player player=sa.getActivatingPlayer();var types=new LinkedHashSet<String>();
  for(Player opponent:player.getOpponents()){
   var counts=new HashMap<String,Integer>();int max=0;
   for(Card c:opponent.getCardsIn(ZoneType.Library))for(String type:c.getType().getCreatureTypes())counts.merge(type,1,Integer::sum);
   for(int n:counts.values())max=Math.max(max,n);
   if(max>0){var tied=new ArrayList<String>();for(var e:counts.entrySet())if(e.getValue()==max)tied.add(e.getKey());
    String chosen=tied.size()==1?tied.get(0):player.getController().chooseSomeType("Choose the most prevalent creature type",sa,tied,false);
    if(chosen!=null)types.add(chosen);
   }
  }
  if(types.isEmpty())return;
  String keywords=String.join(" & ",types.stream().map(t->"Protection:"+t+":"+t+"s").toList());
  var animate=AbilityFactory.getAbility("DB$ Animate | Defined$ Self | Keywords$ "+keywords+" | Duration$ Permanent",host);
  animate.setActivatingPlayer(player);AbilityUtils.resolve(animate);
 }
}
