/* SPDX-License-Identifier: GPL-3.0-or-later */
import com.google.common.eventbus.Subscribe;
import forge.game.Game;
import forge.game.card.CardView;
import forge.game.event.*;
import forge.game.player.PlayerView;
import forge.game.spellability.SpellAbility;
import java.util.*;

/** Non-destructive, ordered UI journal. A reader advances its own cursor. */
public final class ForgePlayback {
    private static final int LIMIT = 8192;
    private final Game game;
    private final Map<Integer,Integer> identities;
    private final Map<Integer,Integer> seats = new HashMap<>();
    private final Deque<Map<String,Object>> journal = new ArrayDeque<>();
    private long sequence;
    private int resolvingAbility = -1;
    private forge.game.spellability.SpellAbilityView completedSpell;
    private boolean fizzled;
    private final Map<Integer,Integer> stackIds = new java.util.concurrent.ConcurrentHashMap<>();
    private int targetInput = -1;
    private int cyclingCard = -1;
    private final Map<Integer,String> pendingMovementReasons=new HashMap<>();
    private final Map<Integer,Set<Integer>> knownTo=new HashMap<>();
    private final Map<Integer,Map<String,Object>> revealed=new LinkedHashMap<>();
    private static final Map<GameEventCardChangeZone,int[]> battlefieldEvents=Collections.synchronizedMap(new WeakHashMap<>());
    private static int[] battlefieldIdentity(GameEventCardChangeZone event) {
        synchronized(battlefieldEvents) {
            int[] existing=battlefieldEvents.get(event);
            if(existing!=null)return existing;
            int old=forge.game.ArenaCardIds.id(event.card().getId());
            Integer prepared=forge.game.ArenaCardIds.finishBattlefield(event.card().getId());
            int next;
            if(prepared!=null) {old=prepared;next=forge.game.ArenaCardIds.id(event.card().getId());}
            else next=forge.game.ArenaCardIds.enterBattlefield(event.card().getId());
            int[] pair={old,next};battlefieldEvents.put(event,pair);return pair;
        }
    }
    public synchronized List<Integer> knownViewers(int card) {return new ArrayList<>(knownTo.getOrDefault(card,Set.of()));}
    public synchronized List<Map<String,Object>> revealedCards() {return new ArrayList<>(revealed.values());}
    public synchronized void rememberControlledHands() {
        for(var player:game.getRegisteredPlayers()) {
            var master=player.getControllingPlayer();
            if(master==null || seat(master.getView())!=1)continue;
            for(var card:player.getCardsIn(forge.game.zone.ZoneType.Hand)) {
                int cardId=id(card.getView());
                knownTo.computeIfAbsent(cardId,k->new HashSet<>()).add(1);
                revealed.put(cardId,Map.of("instanceId",cardId,"ownerSeat",seat(player.getView()),
                    "sourceZone",seat(player.getView())*100+2,"grpId",identities.getOrDefault(card.getId(),0)));
            }
        }
    }
    public synchronized void initiating(SpellAbility ability) {
        cyclingCard=ability.isCycling()?id(ability.getHostCard().getView()):-1;
    }
    private void movementReason(CardView card,String reason) {
        var entries=new ArrayList<>(journal);
        for(int i=entries.size()-1;i>=0;i--) {
            var entry=entries.get(i);
            if(entry.get("type").equals("zoneTransfer") && Objects.equals(entry.get("targetId"),id(card))
                    && Objects.equals(entry.get("toZone"),30)) {
                entry.put("category",reason);return;
            }
        }
    }
    public ForgePlayback(Game game, Map<forge.game.player.Player,Integer> players, Map<Integer,Integer> identities) {
        this.game = game;
        this.identities=identities;
        players.forEach((p,s) -> seats.put(p.getId(),s));
    }
    private int seat(PlayerView player) { return player == null ? 0 : seats.getOrDefault(player.getId(),0); }
    private int id(CardView card) { return card == null ? 0 : forge.game.ArenaCardIds.id(card.getId()); }
    private int source(SpellAbility ability) { return ability.isSpell()?id(ability.getHostCard().getView()):stackIds.getOrDefault(ability.getId(),0); }
    public static String abilityDescription(SpellAbility ability) {
        // Trigger effects often have no SpellDescription at all. Their printed
        // identity includes the condition, such as Bristly Bill's Landfall.
        var trigger=ability.getTrigger();
        if(trigger!=null && trigger.hasParam("TriggerDescription"))return trigger.getParam("TriggerDescription");
        if(ability.getSubAbility()!=null) {
            String full=ability.toUnsuppressedString();
            if(full!=null && !full.isEmpty())return full;
        }
        String description=ability.getParamOrDefault("SpellDescription","");
        return description.isEmpty()?ability.getRootAbility().getParamOrDefault("SpellDescription",""):description;
    }
    public Map<String,Object> stackEntry(forge.game.spellability.SpellAbilityStackInstance item, Map<Integer,Integer> identities) {
        var ability=item.getSpellAbility(); var card=ability.getHostCard();
        Map<String,Object> entry=new LinkedHashMap<>();
        entry.put("instanceId",ability.isSpell()?forge.game.ArenaCardIds.id(card.getId()):1000000+item.getId());
        entry.put("parentId",forge.game.ArenaCardIds.id(card.getId())); entry.put("sourceGrpId",identities.getOrDefault(card.getId(),0));
        entry.put("isAbility",!ability.isSpell()); entry.put("controllerSeat",seat(ability.getActivatingPlayer().getView()));
        entry.put("description",abilityDescription(ability)); entry.put("cardName",card.getName());
        entry.put("abilityIdentity",ForgeEngineMain.abilityIdentity(ability,identities));
        BattleTriggerLinks.add(ability,entry);
        if(ability.isSpell()) {
            var paidKickers=new ArrayList<Integer>();
            if(ability.isOptionalCostPaid(forge.game.spellability.OptionalCost.Kicker1))paidKickers.add(0);
            if(ability.isOptionalCostPaid(forge.game.spellability.OptionalCost.Kicker2))paidKickers.add(1);
            if(!paidKickers.isEmpty())entry.put("paidKickers",paidKickers);
        }
        var selectedModes=new ArrayList<String>();
        for(var mode=ability.getSubAbility();mode!=null;mode=mode.getSubAbility())
            if(mode.getSVarInt("CharmOrder")!=null && mode.getSVarInt("CharmOrder")>0)
                selectedModes.add(mode.getParamOrDefault("SpellDescription",mode.toUnsuppressedString()));
        if(!selectedModes.isEmpty())entry.put("selectedModes",selectedModes);
        if(ability.isSpell() && ability.costHasManaX() && ability.getXManaCostPaid()!=null)
            entry.put("xValue",ability.getXManaCostPaid());
        List<Object> targets=new ArrayList<>(); int index=1;
        for(var current=ability;current!=null;current=current.getSubAbility()) {
            if(!current.usesTargeting())continue;
            List<Integer> ids=new ArrayList<>(), distributions=new ArrayList<>();
            for(var target:current.getTargets()) {
                int targetId=target instanceof forge.game.player.Player p?seat(p.getView()):target instanceof forge.game.card.Card c?forge.game.ArenaCardIds.id(c.getId())
                    :target instanceof SpellAbility sa?source(sa):0;
                if(targetId==0)continue;
                ids.add(targetId); Integer divided=current.getTargets().getDividedValue(target); distributions.add(divided==null?0:divided);
            }
            if(!ids.isEmpty())targets.add(Map.of("index",index,"ids",ids,"distributions",distributions.stream().anyMatch(n->n>0)?distributions:List.of()));
            index++;
        }
        entry.put("targets",targets); return entry;
    }
    public synchronized void selecting(SpellAbility ability) {
        if(ability==null) { targetInput=-1;return; }
        if(targetInput==ability.getId())return;
        targetInput=ability.getId();
        add("targetsSelecting","sourceId",id(ability.getHostCard().getView()),"playerId",seat(ability.getActivatingPlayer().getView()));
    }
    private int zone(forge.game.zone.ZoneView zone) {
        if(zone==null)return 0;
        int type=switch(zone.zoneType()) {
            case Library -> 1; case Hand -> 2; case Battlefield -> 3; case Stack -> 4;
            case Graveyard -> 5; case Exile -> 6; case Command -> 7; case Sideboard -> 10;
            default -> 0;
        };
        return type==0 ? 0 : type==3 || type==4 || type==6 || type==7 ? type*10 : seat(zone.player())*100+type;
    }
    private void add(String type, Object... fields) {
        Map<String,Object> entry = new LinkedHashMap<>();
        entry.put("eventId",++sequence); entry.put("type",type);
        for(int i=0;i<fields.length;i+=2)entry.put((String)fields[i],fields[i+1]);
        journal.addLast(entry);
        if(journal.size()>LIMIT)journal.removeFirst();
    }
    private void startResolution(SpellAbility ability) {
        if(ability == null || source(ability)==0 || resolvingAbility == ability.getId())return;
        resolvingAbility = ability.getId();
        add("resolutionStart","sourceId",source(ability));
    }
    @Subscribe public synchronized void onEvent(GameEvent event) {
        if(event instanceof GameEventCardsRevealed reveal) {
            int viewer=seat(reveal.viewer());
            for(var card:reveal.cards()) {
                int cardId=id(card);boolean newlyKnown=knownTo.computeIfAbsent(cardId,k->new HashSet<>()).add(viewer);
                int owner=seat(reveal.owner());
                if(owner==0)owner=seat(card.getOwner());
                // Tutor reveals are sometimes sent only to the opponents:
                // Forge assumes the searching owner already knows the card.
                // Arena still needs that owner's persistent library visibility.
                if(reveal.zone()==forge.game.zone.ZoneType.Library && owner==1 && viewer!=1)
                    knownTo.computeIfAbsent(cardId,k->new HashSet<>()).add(1);
                if(viewer==1 && (owner!=1 || reveal.zone()==forge.game.zone.ZoneType.Library) && newlyKnown) {
                    int sourceZone=switch(reveal.zone()) {case Hand->owner*100+2;case Library->owner*100+1;case Graveyard->owner*100+5;case Exile->60;default->30;};
                    revealed.put(cardId,Map.of("instanceId",cardId,"ownerSeat",owner,"sourceZone",sourceZone,
                        "grpId",identities.getOrDefault(card.getId(),0)));
                    add("reveal","targetId",cardId,"ownerSeat",owner,"sourceZone",sourceZone);
                }
            }
        }
        // Removal events fire after popping the stack while isResolving is still true.
        // Never peek an empty stack or mistake the next spell for this resolution.
        if(!(event instanceof GameEventSpellRemovedFromStack) && game.getStack().isResolving() && !game.getStack().isEmpty()) {
            var ability=game.getStack().peekAbility();
            if(game.getStack().isResolving(ability.getHostCard()))startResolution(ability);
        }
        if(event instanceof GameEventSpellAbilityCast e && e.sa()!=null && e.si()!=null) {
            stackIds.put(e.sa().getId(),e.sa().isSpell()?id(e.sa().getHostCard()):1000000+e.si().getId());
            for(var item:game.getStack())if(item.getId()==e.si().getId()) {
                var object=stackEntry(item,identities);
                var cast=item.getSpellAbility();
                for(var mana:cast.getPayingMana()) {
                    int producer=id(mana.getSourceCard().getView());
                    int color=switch(forge.card.MagicColor.toShortString(mana.getColor())) {case "W"->1;case "U"->2;case "B"->3;case "R"->4;case "G"->5;default->12;};
                    add("manaPaid","sourceId",producer,"targetId",(Integer)object.get("instanceId"),"color",color,"manaId",producer*16+color);
                }
                if(cast.isSpell() || cast.isActivatedAbility())add("userAction","sourceId",id(cast.getHostCard().getView()),
                    "playerId",seat(e.si().getActivatingPlayer()),"actionType",BattleActionTypes.type(cast),"object",object);
                if(!e.sa().isSpell())add("abilityCreated","sourceId",stackIds.get(e.sa().getId()),"object",object);
                if(!((List<?>)object.get("targets")).isEmpty())add("targetsSubmitted","sourceId",id(e.sa().getHostCard()),"playerId",seat(e.si().getActivatingPlayer()),"object",object);
                break;
            }
        }
        if(event instanceof GameEventMultistepEffect e) {
            add("multistepEffect","sourceId",id(e.source()),"targetId",seat(e.player()),"category",e.category(),"starting",e.starting());
        }
        else if(event instanceof GameEventReplacementApplied e) {
            int affected=e.affected() instanceof CardView c?id(c):e.affected() instanceof PlayerView p?seat(p):0;
            if(affected!=0)add("replacementApplied","sourceId",id(e.source()),"targetId",affected,
                "sourceGrpId",identities.getOrDefault(e.source().getId(),0),"cardName",e.source().getName(),
                "description",e.description(),"damage",e.damage());
        }
        else if(event instanceof GameEventIllegalAttachment e) {
            add("illegalAttachment","sourceId",id(e.source()),"targetId",id(e.attachment()),
                "sourceGrpId",identities.getOrDefault(e.source().getId(),0),"cardName",e.source().getName(),"description",e.description());
        }
        else if(event instanceof GameEventRandomOutcome e) {
            add("randomOutcome","sourceId",id(e.source()),"targetId",seat(e.player()),
                "kind",e.kind(),"sides",e.sides(),"natural",e.natural(),"results",e.results(),"ignored",e.ignored());
        }
        else if(event instanceof GameEventCardRegenerated e) {
            for(var card:e.cards())add("regenerated","targetId",id(card));
        }
        else if(event instanceof GameEventScryOutcome e) {
            if(!e.topIds().isEmpty() || !e.bottomIds().isEmpty())
                add("scryResult","topIds",e.topIds(),"bottomIds",e.bottomIds(),
                    "sourceGrpId",e.source()==null?0:identities.getOrDefault(e.source().getId(),0));
        }
        else if(event instanceof GameEventAttributedLifeGain e && e.amount()>0)
            add("lifeGain","sourceId",e.source()==null?seat(e.player()):id(e.source()),"targetId",seat(e.player()),"amount",e.amount());
        else if(event instanceof GameEventPlayerDamaged e && e.source()!=null && e.amount()>0)
            add("damage","sourceId",id(e.source()),"targetId",seat(e.target()),"amount",e.amount(),"damageType",e.combat()?1:2,"infect",e.infect());
        else if(event instanceof GameEventPermanentDamaged e && e.source()!=null && e.amount()>0)
            add("damage","sourceId",id(e.source()),"targetId",id(e.card()),"amount",e.amount(),"damageType",e.combat()?1:2);
        else if(event instanceof GameEventCardDamaged e && e.source()!=null && e.amount()>0) {
            String phase = String.valueOf(game.getPhaseHandler().getPhase());
            boolean combat = !game.getStack().isResolving() && (phase.equals("COMBAT_DAMAGE") || phase.equals("COMBAT_FIRST_STRIKE_DAMAGE"));
            add("damage","sourceId",id(e.source()),"targetId",id(e.card()),"amount",e.amount(),"damageType",combat?1:2);
        } else if(event instanceof GameEventCardChangeZone e && zone(e.to())>0) {
            String from=zone(e.from())>0?e.from().zoneType().name():"None", to=e.to().zoneType().name();
            forge.game.ArenaCardIds.exileZoneChanged(e.card().getId(),from,to);
            if(to.equals("Library") && !from.equals("Library")) {knownTo.remove(id(e.card()));revealed.remove(id(e.card()));}
            else if(to.equals("Hand") && !from.equals("Library"))knownTo.put(id(e.card()),new HashSet<>(seats.values()));
            String category=from.equals("Hand") && to.equals("Stack") ? "CastSpell"
                : from.equals("Hand") && to.equals("Battlefield") && e.card().getCurrentState().getType().isLand() ? "PlayLand"
                : from.equals("Library") && to.equals("Hand") ? game.getPhaseHandler().getTurn()==0 ? "OpeningHand" : "Draw"
                : from.equals("Stack") && to.equals("Battlefield") ? "Resolve" : "ZoneTransferWithoutIdChange";
            if(from.equals("Hand") && to.equals("Graveyard")) {
                category=id(e.card())==cyclingCard?"Cycle":"Discard";
                if(id(e.card())==cyclingCard)cyclingCard=-1;
            } else if(from.equals("Library") && to.equals("Graveyard"))category="Mill";
            else if(from.equals("Battlefield") && to.equals("Hand"))category="Bounce";
            else if(to.equals("Exile"))category="Exile";
            String explicit=pendingMovementReasons.remove(id(e.card()));
            if(explicit!=null)category=explicit;
            int oldId=id(e.card());
            if(to.equals("Battlefield") && !from.equals("Battlefield")) {
                int[] transition=battlefieldIdentity(e);
                oldId=transition[0];int newId=transition[1];
                if(knownTo.containsKey(oldId))knownTo.put(newId,knownTo.remove(oldId));
                // Keep the reveal panel even when the revealed permanent gets
                // its new battlefield identity before the next snapshot.
                if(zone(e.from())>0)add("objectIdChanged","sourceId",oldId,"targetId",newId);
            }
            if(zone(e.from())>0)add("zoneTransfer","targetId",id(e.card()),"oldId",oldId,"fromZone",zone(e.from()),"toZone",zone(e.to()),"category",category);
        } else if(event instanceof GameEventCardPhased e) {
            add("phasing","targetId",id(e.card()),"phasedOut",e.phaseState());
        } else if(event instanceof GameEventLandPlayed e) {
            movementReason(e.land(),"PlayLand");
            add("userAction","sourceId",id(e.land()),"playerId",seat(e.player()),"actionType",3);
        } else if(event instanceof GameEventCardSacrificed e) {
            // Forge announces sacrifice before moving the card.
            pendingMovementReasons.put(id(e.card()),"Sacrifice");
        } else if(event instanceof GameEventCardTapped e)
            add("tap","targetId",id(e.card()),"tapped",e.tapped());
        else if(event instanceof GameEventShuffle e && seat(e.player())!=0) {
            for(var player:game.getRegisteredPlayers())if(player.getId()==e.player().getId())
                for(var card:player.getCardsIn(forge.game.zone.ZoneType.Library)) {knownTo.remove(id(card.getView()));revealed.remove(id(card.getView()));}
            add("shuffle","playerId",seat(e.player()));
        }
        else if(event instanceof GameEventSpellResolved e && e.spell()!=null) {
            forge.game.ArenaCardIds.clearPendingExileSelections();
            // No intermediate event is required for an empty/fizzled resolution.
            if(resolvingAbility != e.spell().getId()) {
                add("resolutionStart","sourceId",stackIds.getOrDefault(e.spell().getId(),id(e.spell().getHostCard())));
            }
            completedSpell=e.spell(); fizzled=e.hasFizzled();
        } else if(event instanceof GameEventSpellRemovedFromStack e && completedSpell!=null && e.sa()!=null && e.sa().getId()==completedSpell.getId()) {
            add("resolutionComplete","sourceId",stackIds.getOrDefault(completedSpell.getId(),id(completedSpell.getHostCard())),"fizzled",fizzled);
            resolvingAbility=-1; completedSpell=null;
        }
    }
    public synchronized Map<String,Object> read(long after) {
        if(after<0 || after>sequence)throw new IllegalArgumentException("Invalid playback cursor");
        List<Map<String,Object>> entries = journal.stream().filter(e -> (Long)e.get("eventId")>after).toList();
        boolean gap = !journal.isEmpty() && after < (Long)journal.getFirst().get("eventId")-1;
        var objects=entries.stream().filter(e->e.containsKey("object")).map(e->e.get("object")).toList();
        return Map.of("playbackEvents",entries,"playbackId",sequence,"playbackGap",gap,"playbackObjects",objects);
    }
}
