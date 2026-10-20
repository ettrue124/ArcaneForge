/* SPDX-License-Identifier: GPL-3.0-or-later
 * This Forge integration bridge is distributed under GNU GPL version 3 or later.
 * See vendor/forge/LICENSE for the complete license text. No warranty is provided.
 */
import com.google.gson.*;
import com.google.common.eventbus.Subscribe;
import forge.GuiDesktop;
import forge.StaticData;
import forge.ai.LobbyPlayerAi;
import forge.deck.Deck;
import forge.deck.DeckSection;
import forge.game.*;
import forge.game.card.Card;
import forge.game.event.GameEventTurnPhase;
import forge.game.player.Player;
import forge.game.player.RegisteredPlayer;
import forge.game.zone.ZoneType;
import forge.gui.GuiBase;
import forge.item.PaperCard;
import forge.model.FModel;
import forge.util.MyRandom;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import java.util.concurrent.*;

/** Local JSON-lines boundary around Forge's actual rules and AI controllers. */
public final class ForgeEngineMain {
    private static final Gson JSON = new GsonBuilder().serializeNulls().create();
    private static boolean initialized;
    private static boolean poisoned;
    private static String assets;
    private static ForgeInteractiveSession interactive;

    private static Object startInteractive(JsonObject input) {
        if (interactive!=null) throw new IllegalStateException("Close the existing interactive game first");
        JsonArray specifications=input.getAsJsonArray("decks");
        if (specifications==null || (specifications.size()!=2 && specifications.size()!=4)) throw new IllegalArgumentException("Exactly two or four decks required");
        String format=specifications.get(0).getAsJsonObject().has("format")?string(specifications.get(0).getAsJsonObject(),"format"):"Historic";
        boolean unbanned=format.equals("CommanderUnbanned");
        boolean commander=format.equals("Commander") || format.equals("Commander4") || unbanned;
        if(specifications.size()==4 && !commander)throw new IllegalArgumentException("Four-player matches require Commander decks");
        JsonObject commanderPolicy=null;
        if(commander) {
            try { commanderPolicy=JsonParser.parseString(Files.readString(Path.of(assets).getParent().getParent().getParent().resolve("data/commander-bans.json"))).getAsJsonObject(); }
            catch(IOException error){throw new IllegalStateException("Verified Commander bans are missing",error);}
            Set<String> banned=new HashSet<>();if(!unbanned)for(var name:commanderPolicy.getAsJsonArray("bannedNames"))banned.add(name.getAsString());
            StaticData.instance().setCommanderPredicate(card->!banned.contains(card.getName()) && !card.getRules().isCustom() && !card.getRules().isVariant());
        }
        List<Deck> decks=new ArrayList<>();
        List<Map<String,Integer>> groupIds=new ArrayList<>();
        for(JsonElement specification:specifications) {
            JsonObject spec=specification.getAsJsonObject();
            if(commander && (!spec.has("format") || !(string(spec,"format").equals("Commander") || string(spec,"format").equals("Commander4") || string(spec,"format").equals("CommanderUnbanned"))))
                throw new IllegalArgumentException("All players must use Commander decks");
            Deck deck=new Deck(string(spec,"name"));
            Map<String,Integer> map=new HashMap<>();
            JsonArray entries=spec.getAsJsonArray("cards");
            if(entries==null || entries.size()>10000)throw new IllegalArgumentException("Deck cards required, maximum 10000 entries");
            int count=0;
            for(JsonElement entry:entries) {
                JsonObject cardSpec=entry.getAsJsonObject();
                int quantity=integer(cardSpec,"quantity",0);
                if(quantity<1 || quantity>10000 || (count+=quantity)>10000)throw new IllegalArgumentException("Invalid deck quantity");
                String name=string(cardSpec,"name");PaperCard card=findCard(name);
                if(card==null)throw new IllegalArgumentException("Unsupported Forge card: "+name);
                deck.getMain().add(card,quantity);
                if(cardSpec.has("grpId")) {
                    map.put(card.getName(), cardSpec.get("grpId").getAsInt());
                }
            }
            if(count<1)throw new IllegalArgumentException("Deck cannot be empty");
            if(!commander && spec.has("sideboard"))for(var value:spec.getAsJsonArray("sideboard")) {
                var entry=value.getAsJsonObject();String name=string(entry,"name");PaperCard card=findCard(name);
                int quantity=integer(entry,"quantity",0);
                if(card==null || quantity<1 || quantity>15)throw new IllegalArgumentException("Invalid sideboard card: "+name);
                deck.getOrCreate(DeckSection.Sideboard).add(card,quantity);
                if(entry.has("grpId"))map.put(card.getName(),entry.get("grpId").getAsInt());
            }
            if(commander) {
                for(String pile:new String[]{"commanders","companions"}) {
                    JsonArray extra=spec.getAsJsonArray(pile);
                    if(extra!=null)for(var value:extra) {
                        var cardSpec=value.getAsJsonObject();String name=string(cardSpec,"name");PaperCard card=findCard(name);
                        if(card==null || integer(cardSpec,"quantity",0)!=1)throw new IllegalArgumentException("Invalid "+pile+" card: "+name);
                        if(!unbanned && pile.equals("companions"))for(var forbidden:commanderPolicy.getAsJsonArray("bannedAsCompanion"))
                            if(name.equals(forbidden.getAsString()))throw new IllegalArgumentException(name+" is banned as a companion");
                        deck.getOrCreate(pile.equals("commanders")?DeckSection.Commander:DeckSection.Sideboard).add(card,1);
                        map.put(card.getName(),cardSpec.get("grpId").getAsInt());
                    }
                }
                applyRulebreakerColors(deck);
                String problem=forge.deck.DeckFormat.Commander.getDeckConformanceProblem(deck);
                if(problem!=null)throw new IllegalArgumentException("Illegal Commander deck: "+problem);
            }
            decks.add(deck);
            groupIds.add(map);
        }
        interactive=new ForgeInteractiveSession(decks,groupIds,commander,input.has("featureScenario")?input.getAsJsonObject("featureScenario"):null);
        return object("started",true,"mode","human-vs-ai","playerCount",decks.size(),"format",format,"arenaPlayable",false);
    }

    private static Map<String,Object> object(Object... values) {
        Map<String,Object> result = new LinkedHashMap<>();
        for (int i=0; i<values.length; i+=2) result.put((String)values[i], values[i+1]);
        return result;
    }
    private static void initialize() {
        if (initialized) return;
        System.setProperty("java.awt.headless", "true");
        GuiBase.setInterface(new GuiDesktop() {
            @Override public String getAssetsDir() { return assets; }
        });
        FModel.initialize(null, null);
        BattleAdditionalCards.load();
        FModel.getPreferences().setPref(forge.localinstance.properties.ForgePreferences.FPref.UI_SELECT_FROM_CARD_DISPLAYS, "true");
        FModel.getPreferences().setPref(forge.localinstance.properties.ForgePreferences.FPref.YIELD_SKIP_PHASE_DELAY, "true");
        FModel.getPreferences().setPref(forge.localinstance.properties.ForgePreferences.FPref.YIELD_SKIP_RESOLVE_DELAY, "true");
        initialized = true;
    }
    private static String string(JsonObject input, String key) {
        JsonElement v=input.get(key);
        if (v==null || !v.isJsonPrimitive() || !v.getAsJsonPrimitive().isString())
            throw new IllegalArgumentException(key+" must be a string");
        return v.getAsString();
    }
    private static int integer(JsonObject input, String key, int fallback) {
        JsonElement v=input.get(key);
        if (v==null) return fallback;
        if (!v.isJsonPrimitive() || !v.getAsJsonPrimitive().isNumber() || !v.toString().matches("-?[0-9]+"))
            throw new IllegalArgumentException(key+" must be an integer");
        return v.getAsInt();
    }
    private static PaperCard findCard(String name) {
        PaperCard card=StaticData.instance().getCommonCards().getCard(name);
        if(card==null && name.contains("///"))
            card=StaticData.instance().getCommonCards().getCard(name.replaceAll("\\s*/{3}\\s*"," // "));
        // No automatic stripping of A- prefixes or other substitution for Arena variants.
        return card;
    }
    private static Object cards(JsonObject input) {
        JsonArray names=input.getAsJsonArray("names");
        if (names==null || names.size()>40000) throw new IllegalArgumentException("names array required, maximum 40000");
        List<Object> cards=new ArrayList<>();
        for (JsonElement element:names) {
            if (!element.isJsonPrimitive() || !element.getAsJsonPrimitive().isString()) throw new IllegalArgumentException("Card names must be strings");
            String name=element.getAsString();
            PaperCard card=findCard(name);
            cards.add(object("name",name,"supported",card!=null,
                    "forgeName",card==null?null:card.getName(),"edition",card==null?null:card.getEdition(),
                    "canBeCommander",card!=null && card.getRules().canBeCommander()));
        }
        return object("cards",cards);
    }
    private static int seat(Map<Player,Integer> seats, Player player) {
        return player==null?0:seats.getOrDefault(player,0);
    }
    static Object snapshot(Game game, Map<Integer,Integer> identities, Map<Player,Integer> seats, JsonObject input) {
        List<Object> players=new ArrayList<>(), cards=new ArrayList<>();
        Set<Integer> seen=new HashSet<>();
        var displayUnder=BattleExilePresentation.underPermanents(game);
        int omitted=0;
        for (Player p:seats.keySet()) {
            Map<String,Map<String,Object>> pool=new LinkedHashMap<>();
            for(var mana:p.getManaPool()) {
                int color=switch(forge.card.MagicColor.toShortString(mana.getColor())) {
                    case "W" -> 1; case "U" -> 2; case "B" -> 3;
                    case "R" -> 4; case "G" -> 5; default -> 12;
                };
                int source=forge.game.ArenaCardIds.id(mana.getSourceCard().getId());
                int manaId=manaWireId(mana,color);
                String key=Integer.toString(manaId);
                var entry=pool.computeIfAbsent(key,k->object("manaId",manaId,
                    "color",color,"srcInstanceId",source,"count",0,
                    "specs",mana.isRestricted()?List.of(object("type",3)):List.of()));
                entry.put("count",(Integer)entry.get("count")+1);
            }
            players.add(object("seat",seat(seats,p),"life",p.getLife(),"lifeLostThisTurn",p.getLifeLostThisTurn(),"manaPool",pool.values(),"lost",p.hasLost(),
                "protection",p.getKeywords().getValues().stream().filter(k->k.getKeyword()==forge.game.keyword.Keyword.PROTECTION)
                    .map(k->k.getOriginal()).distinct().toList(),
                "maxHandSize",p.getMaxHandSize(),"unlimitedHandSize",p.isUnlimitedHandSize(),
                "monarch",p.isMonarch(),"citysBlessing",p.hasBlessing(),
                "counters",p.getCounters().entrySet().stream().map(e->object("type",e.getElement().toString(),"count",e.getCount())).toList(),
                "lossReason",p.hasLost()?String.valueOf(p.getStats().getOutcome().lossState):""));
            for (ZoneType zone:ZoneType.values()) {
                if (zone==ZoneType.None) continue;
                // Rules queries hide phased-out permanents by default. The
                // presentation snapshot must retain their identity and state
                // so Arena can phase them out and back in instead of deleting.
                for (Card c:p.getCardsIn(zone,false)) {
                    if (!seen.add(c.getId())) continue;
                    Integer grpId=identities.get(c.getId());
                    boolean manaEffect=c.isImmutable() && c.isInZone(ZoneType.Command) && !c.getManaAbilities().isEmpty() && c.getEffectSource()!=null;
                    if(grpId==null && manaEffect) {
                        grpId=identities.get(c.getEffectSource().getId());
                        if(grpId!=null)identities.put(c.getId(),grpId);
                    }
                    if(grpId==null && c.isToken()) {grpId=BattleGeneratedCards.printing(c,identities);if(grpId!=null)identities.put(c.getId(),grpId);}
                    if (grpId==null) { omitted++; continue; }
                    int currentPrinting=BattleFaces.printing(grpId,c.getName());
                    Integer copyPrinting=BattleFaces.copiedPrinting(c,identities);
                    if(copyPrinting!=null)currentPrinting=copyPrinting;
                    Map<String,Object> encoded=object("instanceId",forge.game.ArenaCardIds.id(c.getId()),"grpId",currentPrinting,
                            "originalGrpId",grpId,"name",c.getName(),"cardState",c.getCurrentStateName().name(),
                            "isToken",c.isToken(),
                            "ownerSeat",seat(seats,c.getOwner()),"controllerSeat",seat(seats,c.getController()),
                            // Flashback is a virtual list of playable cards in
                            // other zones, including commanders. Use the card's
                            // real zone so the adapter retains command/library UI.
                            "zone",c.isPhasedOut()?"PhasedOut":c.getZone().getZoneType().name(),"tapped",c.isTapped(),"isLand",c.isLand(),
                            "manaCost",c.getManaCost().toString(),
                            "power",c.getNetPower(),"toughness",c.getNetToughness(),
                            "summoningSick",c.isSick(),"damage",c.getDamage());
                    BattleTurnHistory.add(c,encoded);
                    encoded.put("chosenType",c.getChosenType());
                    encoded.put("onAdventure",c.isInZone(ZoneType.Exile) && c.isOnAdventure() && !c.isFaceDown());
                    if(c.isInZone(forge.game.zone.ZoneType.Exile)) {
                        if(displayUnder.containsKey(c.getId()))encoded.put("displayUnder",displayUnder.get(c.getId()));
                        Card exiler=c.getExiledWith();
                        Card currentExiler=exiler==null?null:game.getCardState(exiler,null);
                        if(currentExiler!=null && exiler.equalsWithGameTimestamp(currentExiler))
                            encoded.put("exiledWith",forge.game.ArenaCardIds.id(exiler.getId()));
                        else if(exiler==null && forge.game.ArenaCardIds.exileSource(c.getId())!=null)
                            encoded.put("exiledWith",forge.game.ArenaCardIds.exileSource(c.getId()));
                    }
                    List<Object> blockingRestrictions=new ArrayList<>();
                    if(c.isInPlay() && !c.isPhasedOut())
                        for(var restrictionSource:game.getCardsIn(ZoneType.STATIC_ABILITIES_SOURCE_ZONES))
                        for(var restriction:restrictionSource.getStaticAbilities()) {
                            if(restriction.getKeyword()!=null || !restriction.checkConditions(forge.game.staticability.StaticAbilityMode.CantBlockBy)
                                || !restriction.matchesValidParam("ValidAttacker",c))continue;
                            Integer restrictionPrinting=identities.get(restrictionSource.getId());
                            if(restrictionPrinting==null)continue;
                            blockingRestrictions.add(object("sourceGrpId",restrictionPrinting,"cardName",restrictionSource.getName(),
                                "sourceId",forge.game.ArenaCardIds.id(restrictionSource.getId()),
                                "description",restriction.getParamOrDefault("Description","")));
                        }
                    encoded.put("blockingRestrictions",blockingRestrictions);
                    if(c.isInPlay() && c.isCreature()) {
                        var powerStats=c.getNetPowerBreakdown();
                        var toughnessStats=c.getNetToughnessBreakdown();
                        encoded.put("powerModified",powerStats.tempBoost!=0 || powerStats.currentValue!=c.getBasePower());
                        encoded.put("toughnessModified",toughnessStats.tempBoost!=0 || toughnessStats.currentValue!=c.getBaseToughness());
                    }
                    List<String> chosenColors=new ArrayList<>();
                    c.getChosenColors().forEach(chosenColors::add);
                    encoded.put("chosenColors",chosenColors);
                    if(c.hasChosenNumber())encoded.put("chosenNumber",c.getChosenNumber());
                    encoded.put("enteredZoneThisTurn",c.enteredThisTurn());
                    encoded.put("hasNoAbilities",c.isInPlay() && !c.isLand() && c.hasNoAbilities()
                        && c.getSpellAbilities().stream().noneMatch(a->a.isActivatedAbility()));
                    encoded.put("monstrous",c.isMonstrous());
                    encoded.put("renowned",c.isRenowned());
                    var activeAbilities=c.getSpellAbilities();
                    var activeKeywords=c.getUnhiddenKeywords().getValues();
                    encoded.put("decayed",c.isCreature() && activeKeywords.stream().anyMatch(k->k.getKeyword()==forge.game.keyword.Keyword.DECAYED));
                    boolean intrinsicRemains=activeAbilities.stream().anyMatch(a->a.isIntrinsic() && !a.isBasicSpell() && !a.isLandAbility())
                        || c.getTriggers().stream().anyMatch(a->a.isIntrinsic())
                        || c.getStaticAbilities().stream().anyMatch(a->a.isIntrinsic())
                        || c.getCurrentState().getReplacementEffects(false).stream().anyMatch(a->a.isIntrinsic())
                        || activeKeywords.stream().anyMatch(a->a.isIntrinsic());
                    // Generated token abilities are not necessarily intrinsic. Only
                    // infer printed suppression when an actual trait change exists.
                    encoded.put("suppressPrintedAbilities",c.isInPlay() && !c.isLand()
                        && !c.getChangedCardTraits().isEmpty() && !intrinsicRemains);
                    encoded.put("suppressedKeywords",c.getCurrentState().getIntrinsicKeywords().stream()
                        .filter(k->activeKeywords.stream().noneMatch(a->a.isIntrinsic() && a.getKeyword()==k.getKeyword()))
                        .map(k->k.getOriginal()).distinct().toList());
                    encoded.put("grantedKeywords",activeKeywords.stream().filter(k->!k.isIntrinsic()).map(k->k.getOriginal()).distinct().toList());
                    if(c.isClassCard())encoded.put("classLevel",c.getClassLevel());
                    if(copyPrinting!=null) {
                        encoded.put("isCopy",true);
                        encoded.put("copyFromGrpId",copyPrinting);
                    }
                    if(c.isEmblem() || manaEffect) {
                        encoded.put("isEmblem",true);
                        if(manaEffect)encoded.put("manaEffect",true);
                        encoded.put("emblemText",c.getAbilityText());
                        if(c.getEffectSourceAbility()!=null)encoded.put("emblemGrantText",ForgePlayback.abilityDescription(c.getEffectSourceAbility()));
                    }
                    if(c.isPlaneswalker()) {
                        int loyalty=c.getCurrentLoyalty();
                        try {
                            int printed=Integer.parseInt(c.getCurrentState().getBaseLoyalty());
                            encoded.put("baseLoyalty",printed);
                            if(!c.isInZone(ZoneType.Battlefield))loyalty=printed;
                        } catch(NumberFormatException ignored) { }
                        encoded.put("loyalty",loyalty);
                        encoded.put("loyaltyActivationsRemaining",c.planeswalkerActivationLimitUsed()?0:1);
                    }
                    String devotionRule=c.getSVar("X");
                    if(activeAbilities.stream().anyMatch(a->"Metalcraft".equals(a.getParamOrDefault("Activation","")))) {
                        encoded.put("metalcraftActive",c.getController().hasMetalcraft());
                        encoded.put("metalcraftCount",c.getController().getCardsIn(ZoneType.Battlefield).stream().filter(Card::isArtifact).count());
                    }
                    if(devotionRule.startsWith("Count$Devotion.")) {
                        String[] devotionColors=devotionRule.substring("Count$Devotion.".length()).split("\\.");
                        byte devotionMask=0;
                        for(String devotionColor:devotionColors)devotionMask |= forge.card.mana.ManaAtom.fromName(devotionColor);
                        int devotion=c.getController().getDevotionMod();
                        for(var permanent:c.getController().getCardsIn(ZoneType.Battlefield))
                            for(var shard:permanent.getManaCost())if(shard.isColor(devotionMask))devotion++;
                        encoded.put("devotion",devotion);
                    }
                    encoded.put("cardTypes",c.getType().getCoreTypes().stream().map(Enum::name).toList());
                    encoded.put("subtypes",c.getType().getSubtypes().stream().toList());
                    encoded.put("supertypes",c.getType().getSupertypes().stream().map(Enum::name).toList());
                    encoded.put("faceDown",c.isFaceDown());
                    if(c.isFaceDown())encoded.put("faceDownReason",c.isForetold()?"Foretell":c.isCloaked()?"Cloak":c.isManifested()?"Manifest":
                        c.getCastSA()!=null && c.getCastSA().isCastFaceDown()?
                            c.getCastSA().getStackDescription().startsWith("Disguise")?"Disguise":"Morph":"TurnedFaceDown");
                    List<Object> granted = new ArrayList<>();
                    for (var ability : activeAbilities) {
                        if (ability.isIntrinsic() || ability.isLandAbility()) continue;
                        granted.add(abilityIdentity(ability, identities));
                    }
                    // Copy exceptions such as Sakashima/Joo Dee retain static
                    // rules that are absent from the copied printing's text.
                    for(var ability:c.getStaticAbilities()) {
                        if(!ability.checkConditions(forge.game.staticability.StaticAbilityMode.IgnoreLegendRule))continue;
                        granted.add(BattleInputs.obj("abilityId",ability.getId(),"sourceGrpId",identities.getOrDefault(c.getId(),0),
                            "granted",true,"hostCardName",c.getName(),"cardName",c.getName(),
                            "loyaltyCost","","description",ability.getParamOrDefault("Description","")));
                    }
                    encoded.put("grantedAbilities", granted);
                    if(c.isInPlay())encoded.put("activationCosts",activeAbilities.stream()
                        .filter(a->a.isActivatedAbility() && !a.isManaAbility() && a.getPayCosts()!=null && (a.isPwAbility() || a.getPayCosts().hasManaCost() && (interactive==null || interactive.canPreviewActions())))
                        .map(a->{a.setActivatingPlayer(c.getController());var entry=object("actionType",2,
                            "instanceId",forge.game.ArenaCardIds.id(c.getId()),"grpId",encoded.get("grpId"),"cardName",c.getName(),
                            "description",ForgePlayback.abilityDescription(a),"abilityIdentity",abilityIdentity(a,identities),
                            "manaCost",a.getPayCosts().hasManaCost()?BattlePlayerControllerHuman.displayManaCost(a,c.getController()):"{0}");
                            var loyalty=BattlePlayerControllerHuman.displayLoyaltyCost(a);
                            if(loyalty!=null)entry.put("adjustedLoyaltyCost",loyalty);
                            return entry;}).toList());
                    List<Integer> colors=new ArrayList<>();byte[] colorBits=forge.card.MagicColor.WUBRG;
                    for(int i=0;i<colorBits.length;i++)if(c.getColor().hasAnyColor(colorBits[i]))colors.add(i+1);
                    encoded.put("colors",colors);
                    if(c.isCommander()) {
                        encoded.put("commander",true);
                        encoded.put("commanderCastCount",c.getOwner().getCommanderCast(c));
                    }
                    if(c.isBattle() && c.getProtectingPlayer()!=null)
                        encoded.put("protectorSeat",seats.get(c.getProtectingPlayer()));
                    // Static permissions follow the actual current top card after
                    // draws, shuffles and zone changes; never reveal the whole deck.
                    encoded.put("viewers",seats.entrySet().stream()
                            .filter(e->c.getView().canBeShownTo(e.getKey().getView())
                                || c.isInZone(ZoneType.Exile) && c.isOnAdventure() && !c.isFaceDown())
                            .map(Map.Entry::getValue).toList());
                    if(c.isAttachedToEntity())
                        encoded.put("attachedTo",BattleInputs.id(c.getEntityAttachedTo(),seats));
                    List<Object> counters=new ArrayList<>();
                    for(var counter:c.getCounters().entrySet())
                        counters.add(object("type",counter.getElement().toString(),"count",counter.getCount()));
                    encoded.put("counters",counters);
                    encoded.put("regenerationShields",c.getShieldCount());
                    var combat=game.getCombat();
                    if(combat!=null) {
                        if(combat.isAttacking(c)) {
                            encoded.put("attackTarget",BattleInputs.id(combat.getDefenderByAttacker(c),seats));
                            encoded.put("attackState",game.getPhaseHandler().getPhase()==forge.game.phase.PhaseType.COMBAT_DECLARE_ATTACKERS?1:2);
                            encoded.put("blockedBy",combat.getBlockers(c).stream().map(b->forge.game.ArenaCardIds.id(b.getId())).toList());
                            encoded.put("blockState",combat.isBlocked(c)?3:combat.isUnblocked(c)?4:0);
                        }
                        List<Integer> blocking=new ArrayList<>();
                        for(Card attacker:combat.getAttackers())if(combat.isBlocking(c,attacker))blocking.add(forge.game.ArenaCardIds.id(attacker.getId()));
                        if(!blocking.isEmpty()) {
                            encoded.put("blocking",blocking);
                            encoded.put("blockState",game.getPhaseHandler().getPhase()==forge.game.phase.PhaseType.COMBAT_DECLARE_BLOCKERS?1:2);
                        }
                    }
                    cards.add(encoded);
                }
            }
        }
        var phase=game.getPhaseHandler();
        String phaseName=phase.getPhase()==null || phase.getPhase().name().equals("NONE")?"MAIN1":phase.getPhase().name();
        int active=seat(seats,phase.getPlayerTurn());
        if(active==0) active=1;
        int priority=seat(seats,phase.getPriorityPlayer());
        if(priority==0) priority=active;
        return object("matchId",input.has("matchId")?input.get("matchId").getAsString():"forge-local",
                "gameStateId",integer(input,"gameStateId",1),"msgId",integer(input,"msgId",1),"viewerSeat",integer(input,"viewerSeat",1),
                "turn",object("number",Math.max(1,phase.getTurn()),"phase",phaseName,
                        "activeSeat",active,"prioritySeat",priority),
                "players",players,"cards",cards,"omittedUnmappedCards",omitted,
                "extraTurns",forge.game.ArenaExtraTurns.pending(game).stream().map(t->object(
                    "seat",seat(seats,t.player()),"sourceGrpId",t.source()==null?0:identities.getOrDefault(t.source().getId(),0),
                    "sourceId",t.source()==null?0:forge.game.ArenaCardIds.id(t.source().getId()))).toList());
    }
    static int manaWireId(forge.game.mana.Mana mana,int color) {
        return forge.game.ArenaCardIds.id(mana.getSourceCard().getId())*16+color+(mana.isRestricted()?20_000_000:0);
    }
    static Map<String,Object> abilityIdentity(forge.game.spellability.SpellAbility ability, Map<Integer,Integer> identities) {
        var grantor = ability.getGrantorStatic();
        var original = ability.getOriginalAbility();
        Card source = grantor != null ? grantor.getHostCard()
                : original != null ? original.getHostCard() : ability.getOriginalHost();
        if (source == null) source = ability.getHostCard();
        source = BattleExilePresentation.printedSource(source);
        return object("abilityId", ability.getId(), "sourceGrpId", identities.getOrDefault(source.getId(), 0),
                "granted", !ability.isIntrinsic(),
                "hostCardName", ability.getHostCard().getName(),
                "loyaltyCost", ability.isPwAbility() && ability.getPayCosts()!=null ? ability.getPayCosts().toString().replace(":", "").trim() : "",
                "cardName", source.getName(), "description", ForgePlayback.abilityDescription(ability));
    }

    public static final class InitialSnapshotCapture {
        private final Game game;
        private final Map<Integer,Integer> identities;
        private final JsonObject input;
        private final Map<Player,Integer> seats;
        volatile boolean ready;
        Object initialSnapshot;
        InitialSnapshotCapture(Game game, Map<Integer,Integer> identities, Map<Player,Integer> seats, JsonObject input) {
            this.game=game; this.identities=identities; this.seats=seats; this.input=input;
        }
        @Subscribe public void phase(GameEventTurnPhase event) {
            if (ready && initialSnapshot==null && game.getPhaseHandler().getPhase()!=null)
                initialSnapshot=snapshot(game,identities,seats,input);
        }
    }
    private static Object simulate(JsonObject input) throws Exception {
        JsonArray decks=input.getAsJsonArray("decks");
        if (decks==null || decks.size()!=2) throw new IllegalArgumentException("Exactly two decks required");
        int maxSeconds=integer(input,"maxSeconds",30);
        if (maxSeconds<5 || maxSeconds>60) throw new IllegalArgumentException("maxSeconds must be 5..60");
        List<RegisteredPlayer> registered=new ArrayList<>();
        List<Map<String,Integer>> groupIds=new ArrayList<>();
        for (JsonElement element:decks) {
            JsonObject specification=element.getAsJsonObject();
            String name=string(specification,"name");
            Deck deck=new Deck(name);
            Map<String,Integer> map=new HashMap<>();
            JsonArray entries=specification.getAsJsonArray("cards");
            if (entries==null || entries.size()>250) throw new IllegalArgumentException("Deck cards array required, maximum 250 entries");
            int count=0;
            for (JsonElement entry:entries) {
                JsonObject item=entry.getAsJsonObject();
                String cardName=string(item,"name");
                int quantity=integer(item,"quantity",0);
                if (quantity<1 || quantity>250 || (count+=quantity)>250) throw new IllegalArgumentException("Deck quantity must be 1..250, total <=250");
                PaperCard card=findCard(cardName);
                if (card==null) throw new IllegalArgumentException("Unsupported Forge card: "+cardName);
                deck.getOrCreate(DeckSection.Main).add(card,quantity);
                if (item.has("grpId")) {
                    int grpId=integer(item,"grpId",0);
                    if (grpId<=0) throw new IllegalArgumentException("grpId must be positive");
                    Integer previous=map.put(card.getName(),grpId);
                    if (previous!=null && previous!=grpId) throw new IllegalArgumentException("Multiple Arena printings of the same name in one deck need an instance mapping");
                }
            }
            if (count<1) throw new IllegalArgumentException("Deck cannot be empty");
            groupIds.add(map);
            registered.add(new RegisteredPlayer(deck).setPlayer(new LobbyPlayerAi(name,null)));
        }
        if (input.has("seed") && (!input.get("seed").isJsonPrimitive()
                || !input.get("seed").getAsJsonPrimitive().isNumber()
                || !input.get("seed").toString().matches("-?[0-9]+")))
            throw new IllegalArgumentException("seed must be an integer");
        long seed=input.has("seed")?input.get("seed").getAsLong():new Random().nextLong();
        MyRandom.setRandom(new Random(seed));
        GameRules rules=new GameRules(GameType.Constructed);
        rules.setAppliedVariants(EnumSet.of(GameType.Constructed));
        rules.setGamesPerMatch(1);
        Match match=new Match(rules,registered,"Local Forge bridge");
        Game game=match.createGame();
        game.setNoGUIUser();
        Map<Integer,Integer> identities=new HashMap<>();
        Map<Player,Integer> seats=new LinkedHashMap<>();
        for (Player player:game.getPlayers()) seats.put(player,seats.size()+1);
        InitialSnapshotCapture capture=new InitialSnapshotCapture(game,identities,seats,input);
        game.subscribeToEvents(capture);
        ExecutorService worker=Executors.newSingleThreadExecutor(r->{Thread t=new Thread(r,"forge-rules");t.setDaemon(true);return t;});
        Future<?> running=worker.submit(()->match.startGame(game,()->{
            for (Player p:game.getPlayers()) for (Card c:p.getAllCards()) {
                if (c.getPaperCard()!=null) {
                    Integer gid=groupIds.get(seat(seats,p)-1).get(c.getPaperCard().getName());
                    if (gid!=null) identities.put(c.getId(),gid);
                }
            }
            capture.ready=true;
        }));
        boolean timedOut=false;
        try {
            running.get(maxSeconds,TimeUnit.SECONDS);
        } catch (TimeoutException timeout) {
            timedOut=true;
            game.setGameOver(GameEndReason.Draw);
            try { running.get(5,TimeUnit.SECONDS); }
            catch (TimeoutException hung) { poisoned=true; throw new IllegalStateException("Forge did not stop after timeout; process restart required"); }
        } finally { worker.shutdownNow(); }
        GameOutcome outcome=game.getOutcome();
        if (outcome==null) throw new IllegalStateException("Forge returned without a game outcome");
        int winnerSeat=0;
        if (outcome.getWinningPlayer()!=null) winnerSeat=registered.indexOf(outcome.getWinningPlayer())+1;
        List<GameLogEntry> entries=game.getGameLog().getLogEntries(null);
        List<String> logs=new ArrayList<>();
        for (int i=Math.min(entries.size(),200)-1;i>=0;i--) logs.add(entries.get(i).toString());
        return object("engine","Forge","mode","ai-vs-ai","seed",seed,"timedOut",timedOut,
                "winner",outcome.getWinningLobbyPlayer()==null?null:outcome.getWinningLobbyPlayer().getName(),
                "winnerSeat",winnerSeat,
                "turns",outcome.getLastTurnNumber(),"endReason",outcome.getWinCondition().name(),
                "logs",logs,"logsTruncated",entries.size()>200,"initialSnapshot",capture.initialSnapshot,
                "snapshot",snapshot(game,identities,seats,input));
    }
    private static void applyRulebreakerColors(Deck deck) {
        byte identity=0;for(var commander:deck.getCommanders())identity|=commander.getRules().getColorIdentity().getColor();
        var commanders=new ArrayList<PaperCard>(deck.getCommanders());
        for(int index=0;index<commanders.size();index++) {
            var commander=commanders.get(index);
            var rules=forge.deck.DeckRule.parseAll(commander);
            var ci=rules.stream().filter(r->r instanceof forge.deck.DeckRuleColorIdentity && r.isActiveFor(DeckSection.Commander))
                .map(r->(forge.deck.DeckRuleColorIdentity)r).toList();
            for(String raw:commander.getRules().getDeckRules()) {
                var budget=java.util.regex.Pattern.compile("AllowedAdditionalColor\\$\\s*(\\d+)").matcher(raw);
                var branch=java.util.regex.Pattern.compile("AllowedAdditionalColorType\\$\\s*([^|]+)").matcher(raw);
                if(!budget.find() || !branch.find())continue;
                var predicate=forge.card.CardRulesPredicates.restrictionList(branch.group(1).trim());byte chosen=0;
                for(var candidate:deck.getMain())if(predicate.test(candidate.getKey().getRules())
                    && !forge.deck.DeckFormat.allowsOffColorIdentity(ci,candidate.getKey().getRules()))
                    chosen|=(byte)(candidate.getKey().getRules().getColorIdentity().getColor() & ~identity);
                if(forge.card.ColorSet.fromMask(chosen).countColors()<=Integer.parseInt(budget.group(1)))
                    commanders.set(index,commander.copyWithMarkedColors(forge.card.ColorSet.fromMask(chosen)));
            }
        }
        var commandZone=deck.getOrCreate(DeckSection.Commander);commandZone.clear();
        for(var commander:commanders)commandZone.add(commander,1);
    }
    public static void main(String[] arguments) throws Exception {
        // Windows may give System.out a legacy console encoding even with
        // -Dfile.encoding=UTF-8. Card/ability text crosses a UTF-8 JSON boundary.
        PrintStream protocol=new PrintStream(new FileOutputStream(FileDescriptor.out),true,StandardCharsets.UTF_8);
        System.setOut(System.err);
        if (arguments.length!=1) throw new IllegalArgumentException("Provide Forge assets base directory containing res/");
        assets=Path.of(arguments[0]).toAbsolutePath().normalize().toString()+File.separator;
        if (!Files.isDirectory(Path.of(assets,"res","cardsfolder"))) throw new IllegalArgumentException("Forge card scripts directory missing");
        BufferedReader reader=new BufferedReader(new InputStreamReader(System.in,StandardCharsets.UTF_8));
        String line;
        while ((line=reader.readLine())!=null) {
            JsonElement id=JsonNull.INSTANCE;
            Map<String,Object> response;
            try {
                JsonObject input=JsonParser.parseString(line).getAsJsonObject();
                if (input.has("id")) id=input.get("id");
                initialize();
                Object result=switch (string(input,"op")) {
                    case "status" -> object("engine","Forge","ready",true,"mode","ai-vs-ai","humanPrompts",true,"interactiveSession",interactive!=null,"arenaPlayable",false);
                    case "interactiveStart" -> startInteractive(input);
                    case "interactivePoll" -> { if(interactive==null)throw new IllegalStateException("No interactive game");yield interactive.poll(); }
                    case "interactiveAnswer" -> { if(interactive==null)throw new IllegalStateException("No interactive game");yield interactive.answer(input); }
                    case "interactiveAction" -> { if(interactive==null)throw new IllegalStateException("No interactive game");yield interactive.action(input); }
                    case "interactiveSnapshot" -> { if(interactive==null)throw new IllegalStateException("No interactive game");yield interactive.snapshot(input); }
                    case "interactiveClose" -> { if(interactive!=null)interactive.close();interactive=null;yield object("closed",true); }
                    case "cards" -> cards(input);
                    case "simulate" -> { if(interactive!=null)throw new IllegalStateException("Interactive game owns Forge state");yield simulate(input); }
                    default -> throw new IllegalArgumentException("Unknown operation");
                };
                response=object("id",id,"ok",true,"result",result);
            } catch (Throwable error) {
                error.printStackTrace(System.err);
                response=object("id",id,"ok",false,"error",error.toString());
            }
            protocol.println(JSON.toJson(response));
            protocol.flush();
            if (poisoned) System.exit(3);
        }
        System.exit(0);
    }
}
