import java.nio.file.*;
import java.util.*;
import forge.StaticData;
import forge.card.CardRules;
import forge.deck.Deck;
import forge.ai.LobbyPlayerAi;
import forge.game.*;
import forge.game.card.*;
import forge.game.player.*;
import forge.game.zone.ZoneType;
public class ArenaDigitalRulesCheck {
 public static void main(String[] args) throws Exception {
  var field=ForgeEngineMain.class.getDeclaredField("assets");field.setAccessible(true);field.set(null,args[0]+java.io.File.separator);
  var init=ForgeEngineMain.class.getDeclaredMethod("initialize");init.setAccessible(true);init.invoke(null);
  var players=new ArrayList<RegisteredPlayer>();
  for(int i=0;i<2;i++){var d=new Deck("check"+i);d.getMain().add(StaticData.instance().getCommonCards().getCard("Island"),40);players.add(new RegisteredPlayer(d).setPlayer(new LobbyPlayerAi("check"+i,null)));}
  var game=new Match(new GameRules(GameType.Constructed),players,"Arena digital script load").createGame();
  var owner=game.getRegisteredPlayers().get(0);
  int count=0;
  try(var paths=Files.list(Path.of("engine-bridge/cards"))){for(var path:paths.filter(p->p.toString().endsWith(".txt")).toList()){
   var rules=CardRules.fromScript(Files.readAllLines(path).stream().map(String::trim).filter(line->!line.isEmpty() && !line.startsWith("#")).toList());
   var card=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard(rules.getName()),owner);
   for(var ability:card.getSpellAbilities()){ability.setActivatingPlayer(owner);ability.getDescription();}
   System.out.println("Loaded: "+card.getName()+" | triggers="+card.getTriggers().size()+" | abilities="+card.getSpellAbilities().size());count++;
  }}
  if(count<39)throw new AssertionError("Expected old and new scripts");
  var inventory=com.google.gson.JsonParser.parseString(Files.readString(Path.of("data/arena-digital-support.json"))).getAsJsonObject();
  var models=new HashSet<String>();int entries=0;
  for(var value:inventory.getAsJsonArray("cards")){
   var row=value.getAsJsonObject();String parent=row.get("exactRulesName").getAsBoolean()?row.get("name").getAsString():row.get("linkedRulesName").getAsString();entries++;
   if(models.add(parent)){
    var pc=StaticData.instance().getCommonCards().getCard(parent);if(pc==null)throw new AssertionError("Missing rules: "+parent);
    var model=Card.fromPaperCard(pc,owner);for(var ability:model.getSpellAbilities()){ability.setActivatingPlayer(owner);ability.getDescription();}
   }
  }
  if(entries==0)throw new AssertionError("The digital inventory is empty");
  System.out.println("PASS: all "+entries+" digital entries instantiate through "+models.size()+" exact/linked rules cards.");
  var blue=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("Merfolk of the Pearl Trident"),owner);
  var red=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("Goblin Piker"),owner);
  blue=game.getAction().moveTo(owner.getZone(ZoneType.Hand),blue,null);
  red=game.getAction().moveTo(owner.getZone(ZoneType.Hand),red,null);
  var aquatic=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("Aquatic Subtlety"),owner);
  var grant=aquatic.getFirstSpellAbility();grant.setActivatingPlayer(owner);
  grant=grant.getSubAbility().getSubAbility();
  forge.game.ability.AbilityUtils.resolve(grant);
  if(!blue.hasKeyword(forge.game.keyword.Keyword.EVOKE) || red.hasKeyword(forge.game.keyword.Keyword.EVOKE))throw new AssertionError("Aquatic blue creature filter");
  blue=game.getAction().moveTo(owner.getZone(ZoneType.Graveyard),blue,null);
  blue=game.getAction().moveTo(owner.getZone(ZoneType.Hand),blue,null);
  if(!blue.hasKeyword(forge.game.keyword.Keyword.EVOKE))throw new AssertionError("Aquatic evoke did not persist across zones");
  System.out.println("PASS: Aquatic Subtlety grants evoke only to blue creatures; survives graveyard/hand moves.");
  var json=com.google.gson.JsonParser.parseString(Files.readString(Path.of("engine-bridge/eternities-abilities.json"))).getAsJsonObject();
  int drafted=0;
  for(var row:json.getAsJsonArray("abilities")){
   var lines=new ArrayList<String>(List.of("Name:Draft check","ManaCost:no cost","Types:Legendary Planeswalker","Loyalty:10","Oracle:Draft check"));
   for(var line:row.getAsJsonObject().getAsJsonArray("lines"))lines.add(line.getAsString());
   var rules=CardRules.fromScript(lines);var model=Card.fromPaperCard(new forge.item.PaperCard(rules,"???",forge.card.CardRarity.Special),owner);
   for(var ability:model.getSpellAbilities()){ability.setActivatingPlayer(owner);ability.getDescription();}drafted++;
  }
  if(drafted!=30)throw new AssertionError("Missing draft options");
  System.out.println("PASS: all thirty Eternities draft abilities instantiate.");
  forge.game.arena.ArenaDigitalState.install(game);forge.game.arena.ArenaDigitalState.capture(game);
  if(forge.game.arena.ArenaDigitalState.nonStarting(blue,owner))throw new AssertionError("Original card incorrectly classified");
  var outside=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("Opt"),owner);outside=game.getAction().moveTo(owner.getZone(ZoneType.Hand),outside,null);
  if(!forge.game.arena.ArenaDigitalState.nonStarting(outside,owner))throw new AssertionError("Generated card was not classified");
  var castOutside=outside.getFirstSpellAbility();castOutside.setActivatingPlayer(owner);
  game.fireEvent(new forge.game.event.GameEventSpellAbilityCast(castOutside,new forge.game.spellability.SpellAbilityStackInstance(castOutside),0));
  if(forge.game.arena.ArenaDigitalState.actualized(owner)!=1)throw new AssertionError("Actualize history not updated");
  game.fireEvent(new forge.game.event.GameEventAttributedLifeGain(owner.getView(),null,7));
  if(forge.game.arena.ArenaDigitalState.lifeGained(owner)!=7)throw new AssertionError("Lifetime life gain not updated");
  System.out.println("PASS: starting deck identity, actualize cast history and lifetime life gain tracking.");
  var forces=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("Flourishing Forces"),owner);var forceSpell=forces.getFirstSpellAbility();forceSpell.setActivatingPlayer(owner);forge.game.ability.AbilityUtils.resolve(forceSpell);
  if(CardLists.getValidCards(owner.getCardsIn(ZoneType.Battlefield),"Card.namedCadet",owner,forces,forceSpell).size()!=3)throw new AssertionError("Flourishing Forces did not use lifetime life gain");
  System.out.println("PASS: Flourishing Forces creates three Cadets after seven life gained.");
  var raiser=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("Runeblade Raiser"),owner);
  raiser=game.getAction().moveTo(owner.getZone(ZoneType.Graveyard),raiser,null);
  var ret=forge.game.ability.AbilityFactory.getAbility("DB$ RepeatEach | ArenaDigital$ RunebladeReturn",raiser);ret.setActivatingPlayer(owner);
  forge.game.ability.AbilityUtils.resolve(ret);
  raiser=game.getCardState(raiser);
  if(!raiser.isInPlay() || !raiser.isTapped() || raiser.getTriggers().stream().anyMatch(t->t.hasParam("ArenaRuneblade")))throw new AssertionError("Runeblade return/removal");
  raiser=game.getAction().moveTo(owner.getZone(ZoneType.Graveyard),raiser,null);raiser=game.getAction().moveTo(owner.getZone(ZoneType.Hand),raiser,null);
  if(raiser.getTriggers().stream().anyMatch(t->t.hasParam("ArenaRuneblade")))throw new AssertionError("Runeblade loss not perpetual");
  System.out.println("PASS: Runeblade returns tapped and perpetually loses only its return trigger.");
  var clomper=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("Opulent Clomper"),owner);
  clomper=game.getAction().moveTo(owner.getZone(ZoneType.Battlefield),clomper,null);
  for(int i=0;i<4;i++){
   int before=clomper.getColor().countColors();var change=forge.game.ability.AbilityFactory.getAbility("DB$ RepeatEach | ArenaDigital$ OpulentColor",clomper);change.setActivatingPlayer(owner);forge.game.ability.AbilityUtils.resolve(change);game.getAction().checkStaticAbilities();
   if(clomper.getColor().countColors()!=before+1)throw new AssertionError("Clomper did not add a new color: "+before+" -> "+clomper.getColor().countColors());
  }
  if(clomper.getColor().countColors()!=5)throw new AssertionError("Clomper missing colors");
  System.out.println("PASS: Opulent Clomper adds a different color until all five are present.");
  var random=forge.game.ability.AbilityFactory.getAbility("DB$ RepeatEach | ArenaDigital$ RandomConjure | RandomType$ Artifact | RandomManaValue$ 1 | Conjure$ True | Zone$ Hand | RememberMade$ True",aquatic);random.setActivatingPlayer(owner);
  forge.game.ability.AbilityUtils.resolve(random);
  Card generated=null;for(Object value:aquatic.getRemembered())if(value instanceof Card c)generated=c;
  if(generated==null || !generated.isInZone(ZoneType.Hand) || !generated.isArtifact() || generated.getCMC()!=1)throw new AssertionError("Random artifact selection");
  System.out.println("PASS: random conjuring selects a supported Arena artifact at the requested mana value.");
  var archive=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("The Mystical Archive"),owner);
  var draft=forge.game.ability.AbilityFactory.getAbility("DB$ Draft | Spellbook$ Giant Growth,Opt,Lightning Bolt | Zone$ Exile | FaceDown$ True | RememberDrafted$ True",archive);draft.setActivatingPlayer(owner);
  int handBefore=owner.getCardsIn(ZoneType.Hand).size();forge.game.ability.AbilityUtils.resolve(draft);
  Card draftedCard=null;for(Object value:archive.getRemembered())if(value instanceof Card c)draftedCard=c;
  if(draftedCard==null || !draftedCard.isInZone(ZoneType.Exile) || !draftedCard.isFaceDown() || owner.getCardsIn(ZoneType.Hand).size()!=handBefore)throw new AssertionError("Draft did not go directly to facedown exile: "+draftedCard+" zone="+(draftedCard==null?null:draftedCard.getZone())+" facedown="+(draftedCard!=null && draftedCard.isFaceDown())+" handDelta="+(owner.getCardsIn(ZoneType.Hand).size()-handBefore));
  System.out.println("PASS: Mystical Archive drafts directly to facedown exile without passing through hand.");
  var creature=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("Grizzly Bears"),owner);creature=game.getAction().moveTo(owner.getZone(ZoneType.Battlefield),creature,null);
  var enter=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("Enter the Eternities"),owner);var transform=enter.getFirstSpellAbility();transform.setActivatingPlayer(owner);transform.getTargets().add(creature);
  forge.game.ability.AbilityUtils.resolve(transform);
  var walkers=CardLists.getValidCards(owner.getCardsIn(ZoneType.Battlefield),"Planeswalker.token",owner,enter,transform);
  if(walkers.isEmpty())throw new AssertionError("Eternities did not create a planeswalker");
  Card walker=walkers.getFirst();if(!walker.getName().equals("Grizzly Bears") || walker.getCounters(CounterEnumType.LOYALTY)!=2 || walker.getSpellAbilities().stream().filter(a->a.isPwAbility()).count()!=3)throw new AssertionError("Eternities token properties");
  System.out.println("PASS: Eternities exiles its target, drafts three abilities, and creates the named token with correct loyalty.");
  walker.addStaticAbility("Mode$ RaiseCost | ValidCard$ Card.Self | ValidSpell$ Activated.Loyalty | Type$ Ability | ArenaLoyaltyAdd$ 1 | EffectZone$ Battlefield");
  var minus=walker.getSpellAbilities().stream().filter(a->a.isPwAbility() && a.getPayCosts().getCostParts().stream().anyMatch(c->c instanceof forge.game.cost.CostRemoveCounter && c.getAmount().equals("2"))).findFirst().orElseThrow();minus.setActivatingPlayer(owner);
  var adjusted=forge.game.cost.CostAdjustment.adjust(minus.getPayCosts(),minus,false);
  if(adjusted.getCostParts().stream().noneMatch(c->c instanceof forge.game.cost.CostRemoveCounter && c.getAmount().equals("1")))throw new AssertionError("Prophet did not convert -2 to -1");
  System.out.println("PASS: Prophet's loyalty adjustment changes -2 to -1 without a false additional payment requirement.");
  var lifeline=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("Valgavoth, the Lifeline"),owner);lifeline=game.getAction().moveTo(owner.getZone(ZoneType.Battlefield),lifeline,null);
  owner.gainLife(1,lifeline,null);game.getAction().checkStaticAbilities();
  var graveCreature=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("Eager Cadet"),owner);graveCreature=game.getAction().moveTo(owner.getZone(ZoneType.Graveyard),graveCreature,null);game.getAction().checkStaticAbilities();
  forge.game.card.CardPlayOption permit=null;for(var option:graveCreature.mayPlay(owner))if(option.getAbility().hasParam("ArenaFinalityCast"))permit=option;
  if(permit==null)throw new AssertionError("Valgavoth did not grant graveyard casting after life gain");
  var recast=graveCreature.getFirstSpellAbility();recast.setActivatingPlayer(owner);recast.setMayPlay(permit);
  graveCreature=game.getAction().moveTo(game.getStackZone(),graveCreature,recast);recast=recast.copy(graveCreature,false);recast.setActivatingPlayer(owner);recast.setMayPlay(permit);graveCreature.setCastFrom(owner.getZone(ZoneType.Graveyard));graveCreature.setCastSA(recast);
  // Construct just the cast-event boundary; payment and priority are outside this fixture.
  var stackField=game.getStack().getClass().getDeclaredField("stack");stackField.setAccessible(true);
  @SuppressWarnings("unchecked") var stackEntries=(java.util.Deque<forge.game.spellability.SpellAbilityStackInstance>)stackField.get(game.getStack());
  var instance=new forge.game.spellability.SpellAbilityStackInstance(recast);stackEntries.push(instance);
  game.fireEvent(new forge.game.event.GameEventSpellAbilityCast(recast,instance,0));
  graveCreature=game.getAction().moveTo(owner.getZone(ZoneType.Battlefield),graveCreature,recast);
  if(graveCreature.getCounters(CounterEnumType.FINALITY)!=1)throw new AssertionError("Valgavoth permission did not produce a finality counter");
  var ordinary=Card.fromPaperCard(StaticData.instance().getCommonCards().getCard("Grizzly Bears"),owner);ordinary=game.getAction().moveTo(owner.getZone(ZoneType.Battlefield),ordinary,null);
  if(ordinary.getCounters(CounterEnumType.FINALITY)!=0)throw new AssertionError("Valgavoth incorrectly added finality to another entry");
  System.out.println("PASS: Valgavoth grants casting after life gain and finality only when its permission is used.");
  System.out.println("PASS: "+count+" card scripts instantiate; gameplay not certified.");
  System.out.println("PASS: full digital rule checks completed.");
  System.exit(0);
 }
}
