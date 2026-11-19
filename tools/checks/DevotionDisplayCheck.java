import java.util.*;
import forge.StaticData;
import forge.ai.LobbyPlayerAi;
import forge.deck.Deck;
import forge.game.*;
import forge.game.card.Card;
import forge.game.player.*;
import forge.game.zone.ZoneType;
public class DevotionDisplayCheck {
 static void check(int actual,int expected,String label) {
  if(actual!=expected)throw new AssertionError(label+": "+actual+" != "+expected);
 }
 static Card permanent(String name,Player owner) {
  Card c=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard(name),owner);
  return owner.getGame().getAction().moveTo(owner.getZone(ZoneType.Battlefield),c,null);
 }
 public static void main(String[] args) throws Exception {
  var assets=ForgeEngineMain.class.getDeclaredField("assets");assets.setAccessible(true);assets.set(null,args[0]+java.io.File.separator);
  var init=ForgeEngineMain.class.getDeclaredMethod("initialize");init.setAccessible(true);init.invoke(null);
  var players=new ArrayList<RegisteredPlayer>();
  for(int i=0;i<2;i++){var deck=new Deck("devotion"+i);deck.getMain().add(StaticData.instance().getCommonCards().getCard("Forest"),40);players.add(new RegisteredPlayer(deck).setPlayer(new LobbyPlayerAi("devotion"+i,null)));}
  var game=new Match(new GameRules(GameType.Constructed),players,"Devotion verification").createGame();
  var owner=game.getRegisteredPlayers().get(0);var opponent=game.getRegisteredPlayers().get(1);
  Card nykthos=permanent("Nykthos, Shrine to Nyx",owner);
  check(BattleDevotionDisplay.preview(nykthos),0,"empty battlefield");
  Card rofellos=permanent("Rofellos, Llanowar Emissary",owner);
  permanent("Burning-Tree Emissary",owner);permanent("Kitchen Finks",owner);
  permanent("Dryad Arbor",owner);permanent("Caged Sun",owner);
  permanent("Primalcrux",opponent);
  check(BattleDevotionDisplay.preview(nykthos),6,"highest own devotion including hybrid symbols");
  for(var entry:Map.of("Green",6,"Red",2,"White",2,"Blue",0,"Black",0).entrySet()) {
   nykthos.setChosenColors(List.of(entry.getKey()));
   check(BattleDevotionDisplay.preview(nykthos),entry.getValue(),"chosen "+entry.getKey());
   var mana=nykthos.getSpellAbilities().stream().filter(a->a.hasParam("SubAbility")).findFirst().orElseThrow().getSubAbility();
   mana.setActivatingPlayer(owner);
   mana.getRootAbility().setActivatingPlayer(owner);
   check(forge.game.ability.AbilityUtils.calculateAmount(nykthos,"X",mana),entry.getValue(),"rules amount "+entry.getKey());
   byte color=forge.card.mana.ManaAtom.fromName(entry.getKey());
   int before=owner.getManaPool().getAmountOfColor(color);
   forge.game.ability.AbilityUtils.resolve(mana);
   check(owner.getManaPool().getAmountOfColor(color)-before,entry.getValue(),"mana actually produced "+entry.getKey());
  }
  nykthos.setChosenColors(List.of());
  Card god=permanent("Karametra, God of Harvests",owner);
  // Karametra adds one white and one green; each hybrid symbol counts once
  // toward combined devotion, rather than once for each of its two colors.
  check(BattleDevotionDisplay.preview(god),8,"combined green/white devotion");
  game.getAction().moveTo(owner.getZone(ZoneType.Graveyard),rofellos,null);
  check(BattleDevotionDisplay.preview(nykthos),5,"badge updates after permanent leaves");
  System.out.println("PASS: devotion badge and Nykthos rules amounts, all five colors, hybrid/combined symbols, opponent exclusion and zone updates.");
  System.exit(0);
 }
}
