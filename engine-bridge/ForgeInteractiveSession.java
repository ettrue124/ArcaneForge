/* SPDX-License-Identifier: GPL-3.0-or-later */
import com.google.gson.*;
import forge.deck.Deck;
import forge.game.GameType;
import forge.game.card.CardView;
import forge.game.player.PlayerView;
import forge.game.player.RegisteredPlayer;
import forge.gamemodes.match.HostedMatch;
import forge.gamemodes.net.IRemote;
import forge.gamemodes.net.ProtocolGuiGame;
import forge.gamemodes.net.event.*;
import forge.gui.FThreads;
import forge.interfaces.IGameController;
import forge.player.LobbyPlayerHuman;
import forge.ai.LobbyPlayerAi;
import java.util.*;
import java.util.concurrent.*;

/** One human-controlled Forge game. Prompts remain pending until explicitly answered. */
final class ForgeInteractiveSession implements IRemote {
    private HostedMatch match = new HostedMatch();
    private int participantIndex;
    private List<ForgeInteractiveSession> peers=List.of(this);
    ForgeInteractiveSession participant(int index) {
        if(index<0 || index>=peers.size())throw new IllegalArgumentException("Invalid human participant");
        return peers.get(index);
    }
    private final ProtocolGuiGame gui = new ProtocolGuiGame(this) {
        @Override public Integer getInteger(String message,int min,int max,boolean descending) { return chooseNumber(message,min,max,null); }
        @Override public Integer getInteger(String message,int min,int max,int cutoff) { return chooseNumber(message,min,max,null); }
    };
    private final BlockingQueue<Map<String,Object>> events = new ArrayBlockingQueue<>(2048);
    private final Map<Long,Pending> pending = new ConcurrentHashMap<>();
    private volatile NumberPending numberPending;
    private volatile forge.game.spellability.SpellAbility castingAbility;
    private record NumberPending(long id,String title,int min,int max,List<Integer> allowed,CompletableFuture<Integer> answer,Map<String,Object> context) {}
    private final List<Map<String,Integer>> groupIds;
    private final Map<Integer,Integer> identities = new ConcurrentHashMap<>();
    private final Map<forge.game.player.Player,Integer> seats = new LinkedHashMap<>();
    private volatile PlayerView human;
    private volatile boolean closed;
    private volatile boolean actionQueued;
    private volatile ForgePlayback playback;
    private long sequence;
    // EventBus callbacks must never wait for the snapshot/action monitor:
    // snapshot affordability checks can themselves emit game events.
    private final Object eventLock=new Object();
    private volatile long buttonEvent;
    private int mulliganCount;
    private final boolean commanderMode;
    private final int playerCount;
    private final Map<Integer,forge.game.spellability.SpellAbility> offeredAbilities=new ConcurrentHashMap<>();
    private final Map<String,Integer> stableActionIds=new HashMap<>();
    private long stableActionEvent=-1;
    private record Pending(GuiGameEvent event, CompletableFuture<Object> answer, Map<String,Object> context) {}
    private Integer chooseNumber(String title,int min,int max,List<Integer> allowed) {
        var source=castingAbility;
        if(source!=null && source.getPayCosts()!=null && source.getPayCosts().hasXInAnyCostPart()
                && title.matches("(?is).*\\bX\\b.*")) {
            // Bound X by Forge's actual payment check, including multiple X
            // symbols and nonmana costs. Preserve the announcement state.
            Integer previous=source.getXManaCostPaid();
            try {
                int low=min,high=Math.min(max,Math.max(1,min));
                while(high<max && canPayX(source,high)) {
                    low=high;high=(int)Math.min((long)max,Math.max((long)high+1,(long)high*2));
                }
                if(canPayX(source,high))low=high;
                else while(low+1<high) {
                    int middle=low+(high-low)/2;
                    if(canPayX(source,middle))low=middle;else high=middle;
                }
                max=low;
            } finally {source.setXManaCostPaid(previous);}
        }
        if(min==max)return min;
        if(allowed!=null) {
            final int ceiling=max;
            allowed=allowed.stream().filter(n->n>=min && n<=ceiling).toList();
        }
        NumberPending prompt;
        synchronized(eventLock) {
            prompt=new NumberPending(++sequence,title,min,max,allowed,new CompletableFuture<>(),BattlePromptContext.capture());
            numberPending=prompt;
            events.offer(obj("eventId",prompt.id(),"method","chooseNumber","blocking",true,"arguments",List.of(title,min,max)));
        }
        try { return prompt.answer().get(10,TimeUnit.MINUTES); }
        catch(Exception error) { close();throw new IllegalStateException("Number prompt was not answered",error); }
        finally { numberPending=null; }
    }
    private boolean canPayX(forge.game.spellability.SpellAbility source,int value) {
        source.setXManaCostPaid(value);
        var payer=source.getActivatingPlayer();
        return forge.ai.ComputerUtilMana.canPayManaCost(source,payer,0,false)
            && forge.game.cost.CostPayment.canPayAdditionalCosts(source.getPayCosts(),source,false,payer);
    }
    private static Map<String,Object> obj(Object... fields) {
        Map<String,Object> value=new LinkedHashMap<>();
        for(int i=0;i<fields.length;i+=2)value.put((String)fields[i],fields[i+1]);
        return value;
    }
    ForgeInteractiveSession(Deck player, Deck opponent) {
        this(player, opponent, List.of(Map.of(), Map.of()));
    }
    ForgeInteractiveSession(Deck player, Deck opponent, List<Map<String,Integer>> groupIds) {
        this(List.of(player,opponent),groupIds,false);
    }
    ForgeInteractiveSession(List<Deck> decks, List<Map<String,Integer>> groupIds, boolean commander) {
        this(decks,groupIds,commander,null);
    }
    ForgeInteractiveSession(List<Deck> decks, List<Map<String,Integer>> groupIds, boolean commander, JsonObject scenario) {
        this(decks,groupIds,commander,scenario,false,false);
    }
    private ForgeInteractiveSession(HostedMatch match,List<Map<String,Integer>> groupIds,boolean commander,int count,int index) {
        this.match=match;this.groupIds=groupIds;this.commanderMode=commander;
        this.playerCount=count;this.participantIndex=index;
    }
    ForgeInteractiveSession(List<Deck> decks, List<Map<String,Integer>> groupIds, boolean commander, JsonObject scenario,boolean humanOpponent,boolean brawl) {
        this.commanderMode=commander;
        this.playerCount=decks.size();
        this.groupIds = groupIds != null ? groupIds : List.of();
        if(humanOpponent) {
            if(decks.size()!=2)throw new IllegalArgumentException("Human matchmaking currently requires two players");
            var second=new ForgeInteractiveSession(match,this.groupIds,commander,playerCount,1);
            peers=List.of(this,second);second.peers=peers;
        }
        match.setStartGameHook(() -> {
            forge.game.Game g = match.getGame();
            if (g != null) {
                forge.game.ArenaCardIds.reset();
                if(scenario!=null)FeatureScenario.apply(g,scenario);
                for(var player:g.getPlayers())
                    for(var card:player.getCardsIn(forge.game.zone.ZoneType.Battlefield,false))
                        forge.game.ArenaCardIds.enterBattlefield(card.getId());
                for(var peer:peers) {
                    peer.ensureIdentities(g);
                    peer.playback = new ForgePlayback(g,peer.seats,peer.identities);
                    g.subscribeToEvents(peer.playback);
                }
            }
        });
        List<RegisteredPlayer> registered=new ArrayList<>();
        for(int i=0;i<decks.size();i++) {
            var participant=commander?RegisteredPlayer.forCommander(decks.get(i)):new RegisteredPlayer(decks.get(i));
            if(brawl)participant.setStartingLife(25);
            participant.setPlayer(i==0 || humanOpponent?new BattleLobbyPlayerHuman("Player "+(i+1)):new LobbyPlayerAi(decks.size()==2?"Forge AI":"Forge AI "+i,null));
            registered.add(participant);
        }
        GameType variant=brawl?GameType.Brawl:commander?GameType.Commander:GameType.Constructed;
        var rules = new forge.game.GameRules(variant);
        rules.setGamesPerMatch(1);
        rules.setWarnAboutAICards(false);
        if(humanOpponent) {
            Map<RegisteredPlayer,forge.gui.interfaces.IGuiGame> guis=new LinkedHashMap<>();
            guis.put(registered.get(0),gui);guis.put(registered.get(1),peers.get(1).gui);
            match.startMatch(rules,commander?Set.of(variant):null,registered,guis,null);
        } else match.startMatch(rules, commander ? Set.of(variant) : null, registered, registered.get(0), gui);
    }
    private long publish(GuiGameEvent event, boolean blocking) {
        synchronized(eventLock) {
        long id=++sequence;
        Object[] args=event.getObjects();
        if(event.getMethod().name().equals("updateButtons"))buttonEvent=id;
        if(event.getMethod().name().equals("openView") && args[0] instanceof Iterable<?> players)
            for(Object p:players) { human=(PlayerView)p; break; }
        List<Object> values=new ArrayList<>();
        for(Object arg:args) values.add(describe(arg));
        if(!events.offer(obj("eventId",id,"method",event.getMethod().name(),"blocking",blocking,"arguments",values))) {
            close(); throw new IllegalStateException("Interactive consumer stopped reading events");
        }
        return id;
        }
    }
    private static Object describe(Object value) {
        if(value==null || value instanceof String || value instanceof Number || value instanceof Boolean)return value;
        if(value instanceof CardView c)return obj("kind","card","instanceId",forge.game.ArenaCardIds.id(c.getId()),"label",c.toString());
        if(value instanceof PlayerView p)return obj("kind","player","id",p.getId(),"label",p.getName());
        if(value instanceof Iterable<?> list){List<Object> out=new ArrayList<>();for(Object item:list)out.add(describe(item));return out;}
        return obj("kind",value.getClass().getSimpleName(),"label",value.toString());
    }
    @Override public void send(NetEvent event) { if(!closed)publish((GuiGameEvent)event,false); }
    @Override public Object sendAndWait(IdentifiableNetEvent event) {
        GuiGameEvent ev=(GuiGameEvent)event;
        // Mana-color allocations have scalar keys rather than battlefield
        // recipients. Ask a bounded number for each color; don't invent card IDs.
        if(ev.getMethod().name().equals("assignGenericAmount") && ev.getObjects()[1] instanceof Map<?,?> targets
                && targets.keySet().stream().anyMatch(v->!(v instanceof CardView || v instanceof PlayerView))) {
            Map<Object,Integer> assigned=new LinkedHashMap<>();int remaining=(Integer)ev.getObjects()[2];
            var entries=new ArrayList<>(targets.entrySet());
            for(int i=0;i<entries.size();i++) {
                var entry=entries.get(i);int capacityAfter=0;
                for(int j=i+1;j<entries.size();j++)capacityAfter=Math.addExact(capacityAfter,(Integer)entries.get(j).getValue());
                int required=Boolean.TRUE.equals(ev.getObjects()[3])?1:0;
                int min=Math.max(required,remaining-capacityAfter),max=Math.min(remaining-required*(entries.size()-i-1),(Integer)entry.getValue());
                if(max<min)throw new IllegalArgumentException("Allocation cannot satisfy target minimums");
                int chosen=chooseNumber(ev.getObjects()[4]+": "+entry.getKey(),min,max,null);
                assigned.put(entry.getKey(),chosen);remaining-=chosen;
            }
            if(remaining!=0)throw new IllegalArgumentException("Unassigned amount");
            return assigned;
        }
        // Forge asks again while paying an activation's costs, even when the
        // source has exactly one mana ability. There is no choice to present.
        if (ev.getMethod().name().equals("getAbilityToPlay")
                && ev.getObjects()[1] instanceof List<?> choices && choices.size() == 1
                && choices.get(0) instanceof forge.game.spellability.SpellAbilityView single && !single.promptIfOnlyPossibleAbility())
            return choices.get(0);
        // Arena normally orders simultaneous triggers automatically. Preserve
        // Forge's proposed order, including a remembered destination order.
        if(ev.getMethod().name().equals("order") && ev.getObjects().length==9
                && ev.getObjects()[4] instanceof List<?> source && ev.getObjects()[5] instanceof List<?> destination) {
            List<Object> ordered=new ArrayList<>();ordered.addAll(destination);ordered.addAll(source);
            if(!ordered.isEmpty() && ordered.stream().allMatch(v->v instanceof forge.game.spellability.SpellAbilityView)
                    && (Integer)ev.getObjects()[2]==0 && (Integer)ev.getObjects()[3]==0)
                return new forge.gui.interfaces.IGuiGame.OrderResult<>(ordered,false);
        }
        // Only omit decisions with one forced answer. Optional choices and
        // modal/color/entity selections must reach the native client.
        if (ev.getMethod().name().equals("getChoices") && ev.getObjects().length >= 4
                && ev.getObjects()[3] instanceof List<?> choices) {
            int min = (Integer)ev.getObjects()[1];
            int max = (Integer)ev.getObjects()[2];
            // -1 is Forge's unlimited-selection sentinel, except the
            // (-1,-1) reveal-only convention. It does not mean choose none.
            if ((max == 0 || (min == -1 && max == -1))
                    && !Boolean.TRUE.equals(BattlePromptContext.capture().get("privateLook"))) return Collections.emptyList();
            if(!choices.isEmpty() && min>0 && max==1
                    && choices.stream().allMatch(v->v instanceof Integer n && n>=0)) {
                List<Integer> numbers=choices.stream().map(v->(Integer)v).toList();
                int low=Collections.min(numbers),high=Collections.max(numbers);
                // The numeric wire format enumerates every disallowed value.
                // Sparse large ranges must remain a finite labeled choice.
                if((long)high-low<=10000)
                    return List.of(chooseNumber((String)ev.getObjects()[0],low,high,numbers));
            }
            if (choices.isEmpty()) return Collections.emptyList();
            if(min>=choices.size())return new ArrayList<>(choices);
        }
        CompletableFuture<Object> future=new CompletableFuture<>();
        long id;
        synchronized(eventLock) { id=sequence+1;pending.put(id,new Pending(ev,future,BattlePromptContext.capture()));publish(ev,true); }
        try { return future.get(10,TimeUnit.MINUTES); }
        catch(Exception error) { close();throw new IllegalStateException("Human prompt was not answered",error); }
        finally { pending.remove(id); }
    }
    private long currentButtonEventId(Object activeInput) {
        if (actionQueued || activeInput == null || activeInput instanceof forge.gamemodes.match.input.InputLockUI) {
            return 0;
        }
        synchronized(eventLock) {
            if (buttonEvent == 0) {
                buttonEvent = ++sequence;
            }
            return buttonEvent;
        }
    }
    Object poll() {
        List<Map<String,Object>> batch=new ArrayList<>();events.drainTo(batch,128);
        forge.game.GameView view=gui.getGameView();
        Object state=view==null || human==null?null:obj("turn",view.getTurn(),"phase",String.valueOf(view.getPhase()),
            "humanTurn",human.equals(view.getPlayerTurn()),"hand",describe(human.getHand()),"battlefield",describe(human.getBattlefield()),"gameOver",view.isGameOver());
        List<Map<String,Object>> pendingList=new ArrayList<>();
        for (Map.Entry<Long,Pending> entry : pending.entrySet()) {
            pendingList.add(obj("eventId", entry.getKey(), "method", entry.getValue().event.getMethod().name()));
        }
        if(numberPending!=null)pendingList.add(obj("eventId",numberPending.id(),"method","chooseNumber"));
        Object activeInput = null;
        String inputType="";
        if(human!=null && decisionController() instanceof forge.player.PlayerControllerHuman controller) {
            activeInput=controller.getInputProxy().getInput();
            inputType=BattleInputs.type(activeInput);
        }
        long buttonEventId = numberPending != null ? numberPending.id()
            : !pending.isEmpty() ? pending.keySet().stream().min(Long::compareTo).orElse(0L)
            : currentButtonEventId(activeInput);
        return obj("events",batch,"closed",closed,"state",state,"pending",pendingList,"inputType",inputType,"buttonEventId",buttonEventId,"mulliganCount",mulliganCount);
    }
    synchronized void ensureIdentities(forge.game.Game game) {
        int s = 1;
        // getPlayers() removes defeated players. Wire seats must stay tied to
        // the registered participants, including while publishing the outcome.
        for (forge.game.player.Player p : game.getRegisteredPlayers()) {
            seats.put(p, (s-1-participantIndex+playerCount)%playerCount+1);
            Map<String,Integer> map = (s <= groupIds.size()) ? groupIds.get(s - 1) : Map.of();
            for (forge.game.card.Card c : p.getAllCards()) {
                if (!identities.containsKey(c.getId()) && c.getPaperCard() != null) {
                    Integer gid = map.get(c.getPaperCard().getName());
                    if (gid == null) gid = fallbackGrpId(c.getPaperCard().getName());
                    if (gid != null && gid > 0) identities.put(c.getId(), gid);
                }
            }
            s++;
        }
        for(var p:game.getRegisteredPlayers())for(var c:p.getAllCards())if((c.isToken() || c.isEmblem()) && !identities.containsKey(c.getId())) {
            Integer token=c.isEmblem() && c.getEffectSource()!=null?identities.get(c.getEffectSource().getId()):BattleGeneratedCards.printing(c,identities);
            if(token!=null)identities.put(c.getId(),token);
        }
    }
    private static int fallbackGrpId(String name) {
        return switch(name) {
            case "Plains" -> 280;
            case "Island" -> 281;
            case "Swamp" -> 282;
            case "Mountain" -> 283;
            case "Forest" -> 284;
            default -> 0;
        };
    }
    synchronized Object snapshot(JsonObject input) {
        ConcurrentModificationException last=null;
        for(int attempt=0;attempt<20;attempt++) {
            try {
                // Affordability previews can recalculate continuous effects.
                // Share GameAction's static-recalculation monitor so previews
                // cannot clear/rebuild that map under the actual game loop.
                synchronized(match.getGame().getAction()) {
                    boolean previous=BattlePlayerControllerHuman.PREVIEW_PAYMENT.get();
                    BattlePlayerControllerHuman.PREVIEW_PAYMENT.set(true);
                    try {return captureSnapshot(input);}
                    finally {BattlePlayerControllerHuman.PREVIEW_PAYMENT.set(previous);}
                }
            }
            catch(ConcurrentModificationException error) {
                last=error;
                try {Thread.sleep(10);}catch(InterruptedException interrupted){Thread.currentThread().interrupt();throw new IllegalStateException(interrupted);}
            }
        }
        throw new IllegalStateException("Forge state did not stabilize for snapshot",last);
    }
    private IGameController decisionController() {
        IGameController root=gui.getGameController(human);
        if(root instanceof forge.player.PlayerControllerHuman owner
            && owner.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputBase input) {
            var acting=input.getController();
            if(acting.getPlayer()==owner.getPlayer()
                || acting.getPlayer().getControllingPlayer()==owner.getPlayer())return acting;
        }
        return root;
    }
    boolean canPreviewActions() {
        if(actionQueued || human==null || !(decisionController() instanceof forge.player.PlayerControllerHuman controller))return false;
        var active=controller.getInputProxy().getInput();
        return active instanceof forge.gamemodes.match.input.InputPassPriority
            || active instanceof forge.gamemodes.match.input.InputPayMana;
    }
    private Object captureSnapshot(JsonObject input) {
        forge.game.Game game = match.getGame();
        if (game == null) throw new IllegalStateException("Game not started");
        ensureIdentities(game);
        Map<String,Object> snapshot=(Map<String,Object>)ForgeEngineMain.snapshot(game, identities, seats, input);
        if(playback!=null) {
            playback.rememberControlledHands();
            for(var value:(List<?>)snapshot.get("cards")) {
                var card=(Map<String,Object>)value;
                var viewers=new LinkedHashSet<Integer>((List<Integer>)card.get("viewers"));
                viewers.addAll(playback.knownViewers((Integer)card.get("instanceId")));
                card.put("viewers",new ArrayList<>(viewers));
            }
            snapshot.put("revealedCards",playback.revealedCards());
        }
        Object activeInput = human != null && decisionController() instanceof forge.player.PlayerControllerHuman hc
            ? hc.getInputProxy().getInput() : null;
        String inputType = BattleInputs.type(activeInput);
        snapshot.put("stage", game.isGameOver() ? 3 : game.getPhaseHandler().getTurn()==0 || inputType.contains("Mulligan") ? 1 : 2);
        snapshot.put("mulliganCount", mulliganCount);
        snapshot.put("format",commanderMode?"Commander":"Historic");
        snapshot.put("startingLife",commanderMode?40:20);
        snapshot.put("playerCount",playerCount);
        snapshot.put("bottomCount",activeInput instanceof forge.gamemodes.match.input.InputLondonMulligan
            ? BattleInputs.field(activeInput,"toReturn") : Math.max(0,mulliganCount-(playerCount>2?1:0)));
        if(game.isGameOver() && game.getOutcome()!=null) {
            var outcome=game.getOutcome();
            int winner=0;
            for(var entry:seats.entrySet())if(outcome.isWinner(entry.getKey().getLobbyPlayer()))winner=entry.getValue();
            snapshot.put("winnerSeat",winner);
            snapshot.put("draw",outcome.isDraw());
            for(var entry:seats.entrySet()) {
                var playerOutcome=entry.getKey().getStats().getOutcome();
                if(playerOutcome!=null && !playerOutcome.hasWon()) {
                    snapshot.put("loserSeat",entry.getValue());
                    snapshot.put("lossReason",String.valueOf(playerOutcome.lossState));
                    break;
                }
            }
        }
        if(human!=null && decisionController() instanceof forge.player.PlayerControllerHuman controller) {
            var descriptor=BattleInputs.describe(game,controller,seats);
            if(activeInput instanceof forge.gamemodes.match.input.InputSelectTargets) {
                var ability=(forge.game.spellability.SpellAbility)BattleInputs.field(activeInput,"sa");
                descriptor.put("abilityIdentity",ForgeEngineMain.abilityIdentity(ability,identities));
            }
            snapshot.put("input",descriptor);
        }
        snapshot.put("canUndo",human!=null && decisionController() instanceof forge.player.PlayerControllerHuman hc
            && ((hc.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputPassPriority && hc.canUndoLastAction())
                || hc.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputPayMana));
        List<Object> legalActions=new ArrayList<>();
        offeredAbilities.clear();
        long actionEvent=currentButtonEventId(activeInput);
        if(stableActionEvent!=actionEvent) {stableActionIds.clear();stableActionEvent=actionEvent;}
        var actionOccurrences=new HashMap<String,Integer>();
        if(canPreviewActions() && human!=null && decisionController() instanceof forge.player.PlayerControllerHuman controller) {
            boolean hasPriority=controller.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputPassPriority;
            var actionableCards=new LinkedHashSet<forge.game.card.Card>();
            for(var participant:game.getRegisteredPlayers())for(var card:participant.getAllCards()) {
                if(card.getOwner()==controller.getPlayer()
                    || card.isInZone(forge.game.zone.ZoneType.Battlefield) && card.getController()==controller.getPlayer()
                    || !card.mayPlay(controller.getPlayer()).isEmpty())actionableCards.add(card);
            }
            for(forge.game.card.Card card:actionableCards) {
                boolean inHandOrCommand = card.getOwner()==controller.getPlayer()
                    && (card.isInZone(forge.game.zone.ZoneType.Hand) || card.isInZone(forge.game.zone.ZoneType.Command));
                boolean permittedOtherZone=!card.mayPlay(controller.getPlayer()).isEmpty();
                if(!hasPriority && !inHandOrCommand)continue;
                var abilities=new ArrayList<forge.game.spellability.SpellAbility>(card.getAllPossibleAbilities(controller.getPlayer(),!(inHandOrCommand || permittedOtherZone)));
                if((inHandOrCommand || permittedOtherZone) && abilities.stream().noneMatch(a->a.isSpell()))
                    for(var printed:card.getSpellAbilities())if(printed.isSpell())abilities.add(printed.copy(controller.getPlayer()));
                // Forge's general option enumeration can discard hand
                // activations before the bridge performs payment checks.
                // Keep printed hand abilities and validate them below.
                if(inHandOrCommand && card.isInZone(forge.game.zone.ZoneType.Hand))
                    for(var printed:card.getSpellAbilities())
                        if(printed.isActivatedAbility() && "Hand".equals(printed.getParamOrDefault("ActivationZone",""))
                            && abilities.stream().noneMatch(a->a.getId()==printed.getId()))
                            abilities.add(printed.copy(controller.getPlayer()));
                for(var ability:abilities) {
                    ability.setActivatingPlayer(controller.getPlayer());
                    if(!hasPriority && !ability.isSpell())continue;
                    // Cold spell actions retain adjusted cost display while the
                    // opponent is deciding. They are never offered for execution.
                    boolean canCast = hasPriority;
                    if(canCast && !ability.isLandAbility() && (!BattleManaAffordability.canPay(ability,controller.getPlayer())
                        || !forge.game.cost.CostPayment.canPayAdditionalCosts(ability.getPayCosts(),ability,false,controller.getPlayer())))canCast = false;
                    if(canCast) {
                        for(var group=ability;group!=null;group=group.getSubAbility()) {
                            if(group.usesTargeting() && group.getMinTargets()>0
                                && group.getTargetRestrictions().getNumCandidates(group)<group.getMinTargets()) {canCast=false;break;}
                        }
                    }
                    if(canCast && !ability.canPlay(true))canCast = false;
                    if(!canCast && (!(inHandOrCommand || permittedOtherZone) || !ability.isSpell()))continue;
                    Integer grp=identities.get(card.getId());
                    if(grp!=null) {
                        String desc=ForgePlayback.abilityDescription(ability);
                        if(desc==null || desc.isEmpty())desc=ability.getDescription();
                        if(desc==null || desc.isEmpty())desc=ability.toUnsuppressedString();
                        if(desc==null)desc="";
                        String actionName=card.getName();
                        var stateName=ability.getCardStateName();
                        if(stateName!=null && card.hasState(stateName)) {
                            actionName=card.getState(stateName).getName();
                            grp=BattleFaces.printing(grp,actionName);
                        }
                        String actionKey=forge.game.ArenaCardIds.id(card.getId())+"|"+stateName+"|"+ability.getApi()+"|"+desc+"|"+ability.getPayCosts();
                        int occurrence=actionOccurrences.merge(actionKey,1,Integer::sum);
                        int offeredId=stableActionIds.computeIfAbsent(actionKey+"|"+occurrence,k->ability.getId());
                        var action=obj("instanceId",forge.game.ArenaCardIds.id(card.getId()),"grpId",grp,
                            "description",desc,
                            "cardName",actionName,
                            // Hand mana abilities need the explicit activation
                            // chooser; battlefield mana uses Arena's tap workflow.
                            "abilityId",offeredId,"actionType",ability.isManaAbility()
                                && (card.isInZone(forge.game.zone.ZoneType.Hand)
                                    || card.isImmutable() && card.isInZone(forge.game.zone.ZoneType.Command)
                                        && card.getEffectSource()!=null)?2:BattleActionTypes.type(ability),
                            "isLand",ability.isLandAbility(),
                            "cycling",ability.isCycling(),
                            "evoke",ability.isEvoke(),
                            "faceDownCast",ability.isCastFaceDown(),
                            "faceDownKind",ability.isCastFaceDown() && ability.getStackDescription().startsWith("Disguise")?"Disguise":"Morph",
                            "turnFaceUpKind",ability.isManifestUp()?"Manifest":ability.isCloakUp()?"Cloak":ability.isDisguiseUp()?"Disguise":ability.isMorphUp()?"Morph":"",
                            "highlight",canCast?3:1,
                            "manaCost",ability.getPayCosts()==null?"{0}":BattlePlayerControllerHuman.displayManaCost(ability,controller.getPlayer()));
                        var identity=ForgeEngineMain.abilityIdentity(ability, identities);
                        identity.put("abilityId",offeredId);
                        action.put("abilityIdentity",identity);
                        if(card.isInZone(forge.game.zone.ZoneType.Sideboard) && permittedOtherZone) {
                            var option=ability.getMayPlayOption();
                            if(option==null)option=card.mayPlay(controller.getPlayer()).get(0);
                            var permissionSource=BattleExilePresentation.printedSource(option.getHost());
                            if(permissionSource!=null)action.put("sideboardSourceId",forge.game.ArenaCardIds.id(permissionSource.getId()));
                        }
                        var loyaltyPreview=BattlePlayerControllerHuman.displayLoyaltyCost(ability);
                        if(loyaltyPreview!=null)action.put("adjustedLoyaltyCost",loyaltyPreview);
                        if(ability.getPayCosts()!=null) {
                            int lifeCost=0;
                            for(var part:ability.getPayCosts().getCostParts())
                                if(part instanceof forge.game.cost.CostPayLife life)lifeCost+=life.getAbilityAmount(ability);
                            if(lifeCost>0)action.put("lifeCost",lifeCost);
                        }
                        if(canCast) {
                            offeredAbilities.put(offeredId,ability);
                            if(!ability.isLandAbility() && ability.getPayCosts()!=null) {
                                var cost=forge.ai.ComputerUtilMana.calculateManaCost(ability.getPayCosts(),ability,controller.getPlayer(),true,0,false);
                                var sources=forge.ai.ComputerUtilMana.getManaSourcesToPayCost(cost,ability,controller.getPlayer(),false);
                                if(sources!=null)action.put("autoTapSources",sources.stream().map(c->forge.game.ArenaCardIds.id(c.getId())).distinct().toList());
                                // Retain the payment selection before refundMana clears it.
                                // This operates on a disposable cost and refunds in finally.
                                if(sources!=null && sources.isEmpty()) {
                                    var poolCost=forge.ai.ComputerUtilMana.calculateManaCost(ability.getPayCosts(),ability,controller.getPlayer(),true,0,false);
                                    List<forge.game.mana.Mana> spent=new ArrayList<>();
                                    try {
                                        if(controller.getPlayer().getManaPool().payManaCostFromPool(poolCost,ability,false,spent))
                                            action.put("autoTapPool",spent.stream().map(m->obj("manaId",ForgeEngineMain.manaWireId(m,manaColor(m)),
                                                "srcInstanceId",forge.game.ArenaCardIds.id(m.getSourceCard().getId()),"color",manaColor(m),"count",1,
                                                "restricted",m.isRestricted())).toList());
                                    } finally { controller.getPlayer().getManaPool().refundMana(spent); }
                                }
                            }
                        }
                        legalActions.add(action);
                    }
                }
            }
        }
        if(human!=null && decisionController() instanceof forge.player.PlayerControllerHuman controller
            && controller.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputPayMana payment) {
            var paymentCards=new ArrayList<forge.game.card.Card>();
            paymentCards.addAll(controller.getPlayer().getCardsIn(forge.game.zone.ZoneType.Battlefield));
            paymentCards.addAll(controller.getPlayer().getCardsIn(forge.game.zone.ZoneType.Command));
            paymentCards.addAll(controller.getPlayer().getCardsIn(forge.game.zone.ZoneType.Hand));
            for(var card:paymentCards) {
                Integer grp=identities.get(card.getId());if(grp==null)continue;
                for(var ability:payment.getUsefulManaAbilities(card)) {
                    if(!ability.canPlay())continue;
                    offeredAbilities.put(ability.getId(),ability);
                    var action=obj("instanceId",forge.game.ArenaCardIds.id(card.getId()),"grpId",grp,"abilityId",ability.getId(),
                        "actionType",4,"isLand",false,"manaCost","{0}","highlight",3,"description",ForgePlayback.abilityDescription(ability),"cardName",card.getName());
                    int lifeCost=0;
                    for(var part:ability.getPayCosts().getCostParts())
                        if(part instanceof forge.game.cost.CostPayLife life)lifeCost+=life.getAbilityAmount(ability);
                    if(lifeCost>0)action.put("lifeCost",lifeCost);
                    legalActions.add(action);
                }
            }
        }
        snapshot.put("legalActions",legalActions);
        NumberPending number=numberPending;
        if(number!=null) {
            var source=castingAbility;
            snapshot.put("input",obj("type","InputChooseNumber","min",number.min(),"max",number.max(),
                "allowed",number.allowed()==null?List.of():number.allowed(),"title",number.title(),
                "sourceId",number.context().getOrDefault("numberSourceId",source==null?0:forge.game.ArenaCardIds.id(source.getHostCard().getId())),
                "grpId",source==null?0:identities.getOrDefault(source.getHostCard().getId(),0),
                "isX",source!=null && source.getPayCosts()!=null && source.getPayCosts().hasXInAnyCostPart() && number.title().matches("(?is).*\\bX\\b.*")));
        }
        long decisionId=number==null?currentButtonEventId(activeInput):number.id();
        if(number==null && !pending.isEmpty()) {
            var entry=pending.entrySet().stream().min(Map.Entry.comparingByKey()).orElseThrow();
            var prompt=BattlePrompts.describe(entry.getValue().event(),seats);
            prompt.putAll(entry.getValue().context());
            if(prompt.get("type").equals("InputGuiChoice") && Integer.valueOf(0).equals(prompt.get("sourceId"))
                && String.valueOf(prompt.get("title")).contains("Choose a mode") && castingAbility!=null)
                prompt.put("sourceId",forge.game.ArenaCardIds.id(castingAbility.getHostCard().getId()));
            if(prompt.get("type").equals("InputGuiStackTargets") && castingAbility!=null) {
                prompt.put("sourceId",forge.game.ArenaCardIds.id(castingAbility.getHostCard().getId()));
                prompt.put("description",ForgePlayback.abilityDescription(castingAbility));
                prompt.put("cardName",castingAbility.getHostCard().getName());
                prompt.put("validTargetTypes",Arrays.asList(castingAbility.getTargetRestrictions().getValidTgts()));
            }
            snapshot.put("input",prompt);
            decisionId=entry.getKey();
        }
        if(actionQueued && number==null && pending.isEmpty()) {
            snapshot.put("input",obj("type","InputLockUI"));decisionId=0;
        }
        snapshot.put("buttonEventId",decisionId);
        if(actionQueued || number!=null || !pending.isEmpty()) {legalActions.clear();offeredAbilities.clear();}
        snapshot.put("pendingCount",pending.size()+(number==null?0:1));
        List<Object> stack=new ArrayList<>();
        if(playback!=null) {
            for(var item:game.getStack())stack.add(playback.stackEntry(item,identities));
            Object current=human!=null && decisionController() instanceof forge.player.PlayerControllerHuman hc ? hc.getInputProxy().getInput():null;
            playback.selecting(current instanceof forge.gamemodes.match.input.InputSelectTargets ? (forge.game.spellability.SpellAbility)BattleInputs.field(current,"sa"):null);
        }
        snapshot.put("stack",stack);
        snapshot.putAll(playback!=null ? playback.read(input.has("afterPlaybackId")?input.get("afterPlaybackId").getAsLong():0)
            : Map.of("playbackEvents",List.of(),"playbackId",0L,"playbackGap",false));
        return snapshot;
    }
    private static int manaColor(forge.game.mana.Mana mana) {
        return switch(forge.card.MagicColor.toShortString(mana.getColor())) {
            case "W" -> 1; case "U" -> 2; case "B" -> 3;
            case "R" -> 4; case "G" -> 5; default -> 12;
        };
    }
    Object answer(JsonObject input) {
        long id=input.get("eventId").getAsLong();Pending p=pending.get(id);
        NumberPending number=numberPending;
        if(number!=null && number.id()==id) {
            JsonElement value=input.get("answer");
            if(value==null || !value.toString().matches("[0-9]+"))throw new IllegalArgumentException("Integer answer required");
            int n=value.getAsInt();
            if(n<number.min() || n>number.max() || number.allowed()!=null && !number.allowed().contains(n))throw new IllegalArgumentException("Number out of range");
            if(!number.answer().complete(n))throw new IllegalArgumentException("Prompt already answered");
            return obj("accepted",true);
        }
        if(p==null || p.answer.isDone())throw new IllegalArgumentException("Unknown or stale prompt");
        Object result=BattlePrompts.answer(p.event,input.get("answer"),seats);
        if(!p.answer.complete(result))throw new IllegalArgumentException("Prompt already answered");
        return obj("accepted",true);
    }
    private static int index(JsonElement value,List<?> options) {
        if(value==null || !value.toString().matches("[0-9]+"))throw new IllegalArgumentException("Integer choice index required");
        int index=value.getAsInt();if(index>=options.size())throw new IllegalArgumentException("Choice index out of range");return index;
    }
    synchronized Object action(JsonObject input) {
        if(closed || human==null)throw new IllegalStateException("Human controller unavailable");
        IGameController controller=decisionController();
        String type=input.get("type").getAsString();
        if(actionQueued && !type.equals("concede"))throw new IllegalArgumentException("Previous human action still running");
        if(!type.equals("concede") && (numberPending!=null || !pending.isEmpty()))
            throw new IllegalArgumentException("Answer the pending Forge decision before taking another action");
        if(!type.equals("concede") && (!input.has("eventId") || buttonEvent==0 || input.get("eventId").getAsLong()!=buttonEvent))
            throw new IllegalArgumentException("Stale human action");
        Runnable action=switch(type) {
            case "undo" -> {
                if(!(controller instanceof forge.player.PlayerControllerHuman hc)
                    || !(hc.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputPassPriority) || !hc.canUndoLastAction())
                    throw new IllegalArgumentException("No reversible mana action available");
                yield hc::undoLastAction;
            }
            case "selectMany" -> {
                if(!(controller instanceof forge.player.PlayerControllerHuman hc) ||
                    !(hc.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputSelectEntitiesFromList<?> current))
                    throw new IllegalArgumentException("No entity selection pending");
                var descriptor=BattleInputs.describe(match.getGame(),hc,seats);
                JsonArray ids=input.getAsJsonArray("instanceIds");
                if(ids==null || ids.size()<(Integer)descriptor.get("min") || ids.size()>(Integer)descriptor.get("max"))
                    throw new IllegalArgumentException("Wrong selection count");
                List<forge.game.GameEntity> selected=new ArrayList<>();Set<Integer> unique=new HashSet<>();
                for(var value:ids) {
                    int id=value.getAsInt();if(!unique.add(id) || !((List<?>)descriptor.get("choices")).contains(id))throw new IllegalArgumentException("Illegal entity selection");
                    selected.add(entity(id));
                }
                yield ()->{for(var target:selected) {
                    if(target instanceof forge.game.player.Player p)controller.selectPlayer(PlayerView.get(p),null);
                    else controller.selectCard(CardView.get((forge.game.card.Card)target),null,null);
                } if(hc.getInputProxy().getInput()==current)controller.selectButtonOk();};
            }
            case "playAbility" -> {
                if(!(controller instanceof forge.player.PlayerControllerHuman hc) ||
                    !(hc.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputPassPriority ||
                      hc.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputPayMana))
                    throw new IllegalArgumentException("No ability action pending");
                var ability=offeredAbilities.get(input.get("abilityId").getAsInt());
                if(ability==null || !ability.canPlay(true))throw new IllegalArgumentException("Ability no longer available");
                yield ()->{castingAbility=ability;if(playback!=null)playback.initiating(ability);hc.getInputProxy().selectAbility(ability);};
            }
            case "targets" -> {
                if(!(controller instanceof forge.player.PlayerControllerHuman hc) ||
                    !(hc.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputSelectTargets))
                    throw new IllegalArgumentException("No target selection pending");
                var descriptor=BattleInputs.describe(match.getGame(),hc,seats);
                if(Boolean.TRUE.equals(descriptor.get("divided")))throw new IllegalArgumentException("Divided targeting adapter unavailable");
                JsonArray ids=input.getAsJsonArray("instanceIds");
                if(ids==null || ids.size()<(Integer)descriptor.get("min") || ids.size()>(Integer)descriptor.get("max"))
                    throw new IllegalArgumentException("Wrong target count");
                Set<Integer> unique=new HashSet<>();List<forge.game.GameEntity> selected=new ArrayList<>();
                for(JsonElement item:ids) {
                    int id=item.getAsInt();
                    if(!unique.add(id) || !((List<?>)descriptor.get("targets")).contains(id))throw new IllegalArgumentException("Illegal target");
                    selected.add(entity(id));
                }
                yield ()->{for(var target:selected) {
                    if(target instanceof forge.game.player.Player p)controller.selectPlayer(PlayerView.get(p),null);
                    else controller.selectCard(CardView.get((forge.game.card.Card)target),null,null);
                } if(hc.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputSelectTargets)controller.selectButtonOk();};
            }
            case "blockers" -> {
                if(!(controller instanceof forge.player.PlayerControllerHuman hc))throw new IllegalArgumentException("Human controller unavailable");
                var descriptor=BattleInputs.describe(match.getGame(),hc,seats);
                if(!descriptor.get("type").equals("InputBlock"))throw new IllegalArgumentException("Wrong combat input");
                JsonArray entries=input.getAsJsonArray("assignments");
                if(entries==null)throw new IllegalArgumentException("Combat assignments required");
                List<Runnable> remove=new ArrayList<>(),add=new ArrayList<>();Set<Integer> used=new HashSet<>();
                for(JsonElement value:entries) {
                    var entry=value.getAsJsonObject();int cardId=entry.get("instanceId").getAsInt();
                    if(!used.add(cardId))throw new IllegalArgumentException("Duplicate blocker");
                    var legal=((List<Map<String,Object>>)descriptor.get("blockers")).stream().filter(c->(Integer)c.get("instanceId")==cardId)
                        .findFirst().orElseThrow(()->new IllegalArgumentException("Illegal blocker"));
                    List<Integer> requested=new ArrayList<>();
                    if(entry.has("recipients"))for(var target:entry.getAsJsonArray("recipients"))requested.add(target.getAsInt());
                    else if(entry.get("recipient").getAsInt()!=0)requested.add(entry.get("recipient").getAsInt());
                    if(requested.size()>(Integer)legal.get("maxAttackers") || new HashSet<>(requested).size()!=requested.size()
                        || !((List<?>)legal.get("attackers")).containsAll(requested))throw new IllegalArgumentException("Illegal blocker recipients/capacity");
                    var previous=(List<Integer>)legal.get("selected");var blocker=CardView.get((forge.game.card.Card)entity(cardId));
                    for(int target:previous)if(!requested.contains(target)) {
                        var attacker=CardView.get((forge.game.card.Card)entity(target));
                        remove.add(()->{controller.selectCard(attacker,null,null);controller.selectCard(blocker,null,null);});
                    }
                    for(int target:requested)if(!previous.contains(target)) {
                        var attacker=CardView.get((forge.game.card.Card)entity(target));
                        add.add(()->{controller.selectCard(attacker,null,null);controller.selectCard(blocker,null,null);});
                    }
                }
                boolean submit=!input.has("submit") || input.get("submit").getAsBoolean();
                yield ()->{for(var change:remove)change.run();for(var change:add)change.run();if(submit)controller.selectButtonOk();};
            }
            case "attackers" -> {
                if(!(controller instanceof forge.player.PlayerControllerHuman hc))throw new IllegalArgumentException("Human controller unavailable");
                var descriptor=BattleInputs.describe(match.getGame(),hc,seats);
                boolean attack=type.equals("attackers");
                if(!descriptor.get("type").equals(attack?"InputAttack":"InputBlock"))throw new IllegalArgumentException("Wrong combat input");
                JsonArray entries=input.getAsJsonArray("assignments");
                if(entries==null)throw new IllegalArgumentException("Combat assignments required");
                List<Runnable> selections=new ArrayList<>();Set<Integer> used=new HashSet<>();
                for(JsonElement element:entries) {
                    var entry=element.getAsJsonObject();int cardId=entry.get("instanceId").getAsInt(), recipient=entry.get("recipient").getAsInt();
                    if(!used.add(cardId))throw new IllegalArgumentException("Duplicate combatant");
                    var candidates=(List<Map<String,Object>>)descriptor.get(attack?"attackers":"blockers");
                    var allowed=candidates.stream().filter(c->(Integer)c.get("instanceId")==cardId).findFirst()
                        .orElseThrow(()->new IllegalArgumentException("Illegal combatant"));
                    if(recipient!=0 && !((List<?>)allowed.get(attack?"defenders":"attackers")).contains(recipient))throw new IllegalArgumentException("Illegal combat recipient");
                    int previous=attack?(Integer)allowed.get("selected"):((List<Integer>)allowed.get("selected")).stream().findFirst().orElse(0);
                    if(previous==recipient)continue;
                    var card=(forge.game.card.Card)entity(cardId);
                    if(previous!=0) {
                        var oldTarget=entity(previous);
                        selections.add(()->{if(oldTarget instanceof forge.game.player.Player p)controller.selectPlayer(PlayerView.get(p),null);
                            else controller.selectCard(CardView.get((forge.game.card.Card)oldTarget),null,null);
                            controller.selectCard(CardView.get(card),null,null);});
                    }
                    if(recipient==0)continue;
                    var target=entity(recipient);
                    selections.add(()->{
                        if(target instanceof forge.game.player.Player p)controller.selectPlayer(PlayerView.get(p),null);
                        else controller.selectCard(CardView.get((forge.game.card.Card)target),null,null);
                        controller.selectCard(CardView.get(card),null,null);
                    });
                }
                boolean submit=!input.has("submit") || input.get("submit").getAsBoolean();
                yield ()->{for(Runnable selection:selections)selection.run();if(submit)controller.selectButtonOk();};
            }
            case "bottomCards" -> {
                if(!(controller instanceof forge.player.PlayerControllerHuman hc)
                    || !(hc.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputLondonMulligan))
                    throw new IllegalArgumentException("No London bottom-card selection pending");
                JsonArray ids=input.getAsJsonArray("instanceIds");
                if(ids==null || ids.size()!=(Integer)BattleInputs.field(hc.getInputProxy().getInput(),"toReturn"))throw new IllegalArgumentException("Wrong bottom-card count");
                List<CardView> cards=new ArrayList<>();Set<Integer> unique=new HashSet<>();
                for(JsonElement entry:ids) {
                    int id=forge.game.ArenaCardIds.forgeId(entry.getAsInt());
                    if(!unique.add(id))throw new IllegalArgumentException("Duplicate bottom card");
                    CardView found=null;for(CardView card:human.getHand())if(card.getId()==id)found=card;
                    if(found==null)throw new IllegalArgumentException("Bottom card must be in own hand");
                    cards.add(found);
                }
                yield ()->{for(CardView card:cards)controller.selectCard(card,null,null);controller.selectButtonOk();};
            }
            case "ok" -> controller::selectButtonOk;
            case "cancel" -> controller::selectButtonCancel;
            case "concede" -> controller::concede;
            case "alphaStrike" -> controller::alphaStrike;
            case "selectPlayer" -> {
                int seat=input.get("seat").getAsInt();
                forge.game.player.Player player=seats.entrySet().stream().filter(e->e.getValue()==seat)
                    .map(Map.Entry::getKey).findFirst().orElseThrow(()->new IllegalArgumentException("Invalid player seat"));
                yield ()->controller.selectPlayer(PlayerView.get(player),null);
            }
            case "selectTarget" -> {
                if(!(controller instanceof forge.player.PlayerControllerHuman hc) ||
                    !(hc.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputSelectTargets))
                    throw new IllegalArgumentException("No target selection pending");
                var descriptor=BattleInputs.describe(match.getGame(),hc,seats);
                int id=input.get("instanceId").getAsInt();
                if(!((List<?>)descriptor.get("targets")).contains(id) && !((List<?>)descriptor.get("selected")).contains(id))
                    throw new IllegalArgumentException("Illegal target");
                var target=entity(id);
                yield ()->{if(target instanceof forge.game.player.Player p)controller.selectPlayer(PlayerView.get(p),null);
                    else controller.selectCard(CardView.get((forge.game.card.Card)target),null,null);};
            }
            case "selectCard" -> {
                int id=forge.game.ArenaCardIds.forgeId(input.get("instanceId").getAsInt());
                CardView view=null;
                if(human.getHand()!=null)for(CardView card:human.getHand())if(card.getId()==id)view=card;
                for(PlayerView player:gui.getGameView().getPlayers())
                    if(player.getBattlefield()!=null)for(CardView card:player.getBattlefield())if(card.getId()==id)view=card;
                if(view==null)throw new IllegalArgumentException("Card is not selectable");
                CardView selected=view;
                yield ()->controller.selectCard(selected,null,null);
            }
            default -> throw new IllegalArgumentException("Unknown human action");
        };
        if(type.equals("cancel") && controller instanceof forge.player.PlayerControllerHuman hc
            && hc.getInputProxy().getInput() instanceof forge.gamemodes.match.input.InputConfirmMulligan)mulliganCount++;
        buttonEvent=0;
        actionQueued=true;
        FThreads.invokeInEdtLater(()->{try {action.run();}finally {actionQueued=false;}});
        return obj("accepted",true);
    }
    void close() {
        if(closed)return;
        for(var peer:peers) {
            peer.closed=true;
            for(Pending p:peer.pending.values())p.answer.completeExceptionally(new IllegalStateException("Session closed"));
            if(peer.numberPending!=null)peer.numberPending.answer().completeExceptionally(new IllegalStateException("Session closed"));
        }
        if(human!=null)FThreads.invokeInEdtLater(()->gui.getGameController(human).concede());
    }
    private forge.game.GameEntity entity(int id) {
        for(var entry:seats.entrySet()) {
            if(entry.getValue()==id)return entry.getKey();
            for(var card:entry.getKey().getAllCards())if(forge.game.ArenaCardIds.id(card.getId())==id)return card;
        }
        throw new IllegalArgumentException("Unknown battle entity");
    }
}
