/* SPDX-License-Identifier: GPL-3.0-or-later */
package forge.game.arena;
import com.google.common.eventbus.Subscribe;
import forge.game.Game;
import forge.game.card.Card;
import forge.game.event.*;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.util.*;
/** Match-local histories. Card ids survive zone changes; conjured/copy ids do not. */
public final class ArenaDigitalState {
 private static final Map<Game,State> states=Collections.synchronizedMap(new WeakHashMap<>());
 private static final class State {
  final Map<Integer,Set<Integer>> original=new java.util.concurrent.ConcurrentHashMap<>();
  final Map<Integer,Integer> actualized=new java.util.concurrent.ConcurrentHashMap<>(), life=new java.util.concurrent.ConcurrentHashMap<>();
  volatile boolean captured; boolean installed;
 }
 private static State state(Game g){synchronized(states){return states.computeIfAbsent(g,k->new State());}}
 public static void install(Game g){
  synchronized(states){State s=states.computeIfAbsent(g,k->new State());if(s.installed)return;s.installed=true;}
  g.subscribeToEvents(new Observer(g));
 }
 public static void capture(Game g){
  State s=state(g); if(s.captured)return;s.captured=true;
  for(Player p:g.getRegisteredPlayers()){
   Set<Integer> ids=new HashSet<>();
   for(ZoneType z:List.of(ZoneType.Library,ZoneType.Hand,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Command))
    for(Card c:p.getCardsIn(z))if(!c.isToken() && !c.isImmutable())ids.add(c.getId());
   s.original.put(p.getId(),Set.copyOf(ids));
  }
 }
 public static boolean nonStarting(Card c,Player caster){
  State s=state(c.getGame());if(!s.captured)return false;
  return caster!=null && !s.original.getOrDefault(caster.getId(),Set.of()).contains(c.getId());
 }
 public static int startingSize(Player p){return state(p.getGame()).original.getOrDefault(p.getId(),Set.of()).size();}
 public static int actualized(Player p){return state(p.getGame()).actualized.getOrDefault(p.getId(),0);}
 public static int lifeGained(Player p){return state(p.getGame()).life.getOrDefault(p.getId(),0);}
 public static final class Observer {
  private final Game game;Observer(Game g){game=g;}
  @Subscribe public void started(GameEventGameStarted e){capture(game);}
  @Subscribe public void cast(GameEventSpellAbilityCast e){
   if(!e.sa().isSpell())return;
   for(var item:game.getStack())if(item.getId()==e.si().getId()){
    var spell=item.getSpellAbility();if(spell.isCopied())return;
    var permission=spell.getMayPlay();
    if(permission!=null && permission.hasParam("ArenaFinalityCast")){
     spell.getHostCard().setSVar("ArenaFinalityEntry","Event$ Moved | ValidCard$ Card.IsRemembered | Origin$ Stack | Destination$ Battlefield | ReplaceWith$ ArenaFinalityCounter | ReplacementResult$ Updated | Description$ This creature enters with a finality counter.");
     spell.getHostCard().setSVar("ArenaFinalityCounter","DB$ PutCounter | Defined$ ReplacedCard | CounterType$ FINALITY | CounterNum$ 1 | ETB$ True");
     var effect=forge.game.ability.AbilityFactory.getAbility("DB$ Effect | RememberObjects$ Self | ReplacementEffects$ ArenaFinalityEntry | Duration$ Permanent | ForgetOnMoved$ Stack",spell.getHostCard());
     effect.setActivatingPlayer(spell.getActivatingPlayer());forge.game.ability.AbilityUtils.resolve(effect);
    }
   }
   var view=e.si().getActivatingPlayer();
   for(Player p:game.getRegisteredPlayers())if(p.getId()==view.getId()){
    Card card=game.getCardsInGame().stream().filter(c->c.getId()==e.sa().getHostCard().getId()).findFirst().orElse(null);
    if(card!=null && nonStarting(card,p))state(game).actualized.merge(p.getId(),1,Integer::sum);
   }
  }
  @Subscribe public void gain(GameEventAttributedLifeGain e){if(e.amount()>0)state(game).life.merge(e.player().getId(),e.amount(),Integer::sum);}
 }
}
