/* SPDX-License-Identifier: GPL-3.0-or-later */
import forge.LobbyPlayer;
import forge.game.Game;
import forge.game.card.CardCollection;
import forge.game.card.Card;
import forge.game.card.CardCollectionView;
import forge.game.player.Player;
import forge.game.player.actions.ModeChoiceAction;
import forge.game.player.actions.ScryAction;
import forge.game.replacement.ReplacementEffect;
import forge.game.spellability.AbilitySub;
import forge.game.spellability.SpellAbility;
import forge.game.spellability.SpellAbilityView;
import forge.game.zone.ZoneType;
import forge.player.PlayerControllerHuman;
import forge.util.Localizer;
import forge.card.mana.ManaAtom;
import forge.card.mana.ManaCost;
import forge.card.mana.ManaCostShard;
import forge.game.mana.ManaConversionMatrix;
import forge.game.mana.ManaCostBeingPaid;
import forge.game.player.actions.ConfirmAction;
import forge.game.trigger.Trigger;
import forge.game.trigger.WrappedAbility;
import forge.player.AutoYieldStore;
import org.apache.commons.lang3.tuple.ImmutablePair;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public class BattlePlayerControllerHuman extends PlayerControllerHuman {
    private int declinePactResolution = -1;
    // Preview calls run on the bridge thread; real payment runs on the game thread.
    static final ThreadLocal<Boolean> PREVIEW_PAYMENT=ThreadLocal.withInitial(()->false);
    static final ThreadLocal<Boolean> DISPLAY_PAYMENT=ThreadLocal.withInitial(()->false);
    @Override
    public List<forge.game.spellability.OptionalCostValue> chooseOptionalCosts(
            SpellAbility spell, List<forge.game.spellability.OptionalCostValue> costs) {
        if (costs.isEmpty()) return new ArrayList<>();
        List<forge.game.spellability.OptionalCostValue> selected = new ArrayList<>();
        if(costs.stream().allMatch(c->c.getType().name().equals("Bargain"))) {
            for(var cost:costs) {
                var answer=BattlePromptContext.with(Map.of("sourceId",forge.game.ArenaCardIds.id(spell.getHostCard().getId())),
                    ()->getGui().getChoices("Sacrifice an artifact, enchantment, or token to bargain?",
                        1,1,List.of("Bargain","Cast without bargain")));
                if(answer.contains("Bargain"))selected.add(cost);
            }
            return selected;
        }
        for (var cost : costs) {
            var type = cost.getType();
            if (type != forge.game.spellability.OptionalCost.Kicker1
                    && type != forge.game.spellability.OptionalCost.Kicker2) {
                return super.chooseOptionalCosts(spell, costs);
            }
        }
        // Require an explicit answer: an empty optional SelectN response must
        // not silently turn a requested kicked cast into an ordinary cast.
        for (var cost : costs) {
            String pay = "Pay kicker " + cost.getCost().toSimpleString();
            Map<String,Object> context=new LinkedHashMap<>();
            context.put("sourceId",forge.game.ArenaCardIds.id(spell.getHostCard().getId()));
            if(costs.size()==1 && !spell.costHasManaX() && cost.getCost().isOnlyManaCost()) {
                var kicked=forge.game.GameActionUtil.addOptionalCosts(spell,List.of(cost));
                context.put("nativeKind","kicker");
                context.put("baseManaCost",displayManaCost(spell,getPlayer()));
                context.put("kickedManaCost",displayManaCost(kicked,getPlayer()));
                boolean previous=PREVIEW_PAYMENT.get();
                PREVIEW_PAYMENT.set(true);
                try {
                    describeKickerPayment(context,"base",spell);
                    describeKickerPayment(context,"kicked",kicked);
                } finally {PREVIEW_PAYMENT.set(previous);}
                // Retain the explicit choice if the preview cannot find any
                // solution; a native browser with two cold cards has no exit.
                if(!Boolean.TRUE.equals(context.get("basePayable"))
                        && !Boolean.TRUE.equals(context.get("kickedPayable")))context.remove("nativeKind");
            }
            var answer = BattlePromptContext.with(context,
                () -> getGui().getChoices("Kicker — " + spell.getHostCard().getName(),
                    1, 1, List.of(pay, "Cast without this kicker")));
            if (answer.contains(pay)) selected.add(cost);
        }
        return selected;
    }
    private void describeKickerPayment(Map<String,Object> context,String prefix,SpellAbility spell) {
        var player=getPlayer();
        var cost=forge.ai.ComputerUtilMana.calculateManaCost(spell.getPayCosts(),spell,player,true,0,false);
        var sources=forge.ai.ComputerUtilMana.getManaSourcesToPayCost(cost,spell,player,false);
        context.put(prefix+"Payable",sources!=null);
        if(sources==null)return;
        context.put(prefix+"AutoTapSources",sources.stream()
            .map(card->forge.game.ArenaCardIds.id(card.getId())).distinct().toList());
        // The source planner spends floating mana first. Reconstruct that part
        // of the preview on a disposable cost, then immediately refund it.
        var poolCost=forge.ai.ComputerUtilMana.calculateManaCost(spell.getPayCosts(),spell,player,true,0,false);
        List<forge.game.mana.Mana> spent=new ArrayList<>();
        try {
            player.getManaPool().payManaCostFromPool(poolCost,spell,false,spent);
            Map<Integer,Map<String,Object>> pool=new LinkedHashMap<>();
            for(var mana:spent) {
                int color=switch(forge.card.MagicColor.toShortString(mana.getColor())) {
                    case "W"->1;case "U"->2;case "B"->3;
                    case "R"->4;case "G"->5;default->12;
                };
                int manaId=ForgeEngineMain.manaWireId(mana,color);
                var info=pool.computeIfAbsent(manaId,id->{
                    Map<String,Object> item=new LinkedHashMap<>();
                    item.put("manaId",id);item.put("color",color);
                    item.put("srcInstanceId",forge.game.ArenaCardIds.id(mana.getSourceCard().getId()));
                    item.put("count",0);item.put("restricted",mana.isRestricted());
                    return item;
                });
                info.put("count",(Integer)info.get("count")+1);
            }
            context.put(prefix+"AutoTapPool",new ArrayList<>(pool.values()));
        } finally {player.getManaPool().refundMana(spent);}
    }
    static Integer displayLoyaltyCost(SpellAbility sa) {
        if(!sa.isPwAbility() || sa.getPayCosts()==null || sa.getPayCosts().toString().contains("X"))return null;
        var adjusted=forge.game.cost.CostAdjustment.adjust(sa.getPayCosts(),sa,false);
        int loyalty=0;
        for(var part:adjusted.getCostParts()) {
            if(part instanceof forge.game.cost.CostPutCounter put && put.getCounter().getName().equalsIgnoreCase("Loyalty"))loyalty+=put.getAbilityAmount(sa);
            if(part instanceof forge.game.cost.CostRemoveCounter remove && remove.counter.getName().equalsIgnoreCase("Loyalty"))loyalty-=remove.getAbilityAmount(sa);
        }
        return loyalty;
    }
    static String displayManaCost(SpellAbility sa,Player player) {
        boolean old=DISPLAY_PAYMENT.get();
        Integer previousX=sa.getXManaCostPaid();
        int xSymbols=sa.getPayCosts()!=null && sa.getPayCosts().hasManaCost()
            ?sa.getPayCosts().getCostMana().getManaCostFor(sa).countX():0;
        DISPLAY_PAYMENT.set(true);
        try {
            if(xSymbols>0)sa.setXManaCostPaid(0);
            String display=forge.ai.ComputerUtilMana.calculateManaCost(sa.getPayCosts(),sa,player,true,0,false).toString();
            if(xSymbols>0)display="{X}".repeat(xSymbols)+(display.equals("0")?"":display);
            return display;
        }
        finally {sa.setXManaCostPaid(previousX);DISPLAY_PAYMENT.set(old);}
    }
    @Override
    protected void reveal(final CardCollectionView cards, final ZoneType zone, final forge.game.player.PlayerView owner,
                          String message, boolean addSuffix) {
        if(zone==ZoneType.Library) {
            tempShowCards(cards);
            try {
                var views=forge.game.card.CardView.getCollection(cards);
                BattlePromptContext.with(Map.<String,Object>of("privateLook",true),
                    ()->getGui().getChoices("Look at the top card of your library",0,0,views));
            } finally {endTempShowCards();}
            return;
        }
        if (zone == ZoneType.Hand && owner != null && !owner.equals(getPlayer().getView())) {
            getGame().fireEvent(new forge.game.event.GameEventCardsRevealed(cards.stream().map(forge.game.card.CardView::get).toList(),
                zone, owner, getPlayer().getView()));
        }
        // The reveal journal supplies Arena's revealed-card display. Forge's
        // desktop reveal prompt offers OK/End Turn and blocks before the actual
        // discard choice, which is not an Arena hand-selection workflow.
        getYieldController().maybeInterruptOnReveal();
    }
    @Override
    public boolean confirmAction(final SpellAbility sa, final forge.game.player.PlayerActionConfirmMode mode,
            final String message, final List<String> options, final Card cardToShow, final Map<String,Object> params) {
        if(sa!=null && sa.getHostCard().getName().equals("Tainted Pact")
            && sa.getApi()==forge.game.ability.ApiType.ChangeZone && sa.hasParam("Optional")
            && "Exile".equals(sa.getParamOrDefault("Origin","")) && "Hand".equals(sa.getParamOrDefault("Destination",""))
            && getGame().getStack().peek()!=null) {
            int resolution=getGame().getStack().peek().getId();
            if(declinePactResolution==resolution)return false;
            var choices=List.of("Put this card into hand","No — continue","No to all remaining");
            var selected=BattlePromptContext.with(Map.of("sourceId",forge.game.ArenaCardIds.id(sa.getHostCard().getId())),
                ()->getGui().getChoices("Tainted Pact: "+message,1,1,choices));
            if(selected.get(0).equals(choices.get(2)))declinePactResolution=resolution;
            return selected.get(0).equals(choices.get(0));
        }
        if(sa!=null && sa.getApi()==forge.game.ability.ApiType.PeekAndReveal && sa.hasParam("RevealOptional")
                && sa.getSubAbility()!=null && sa.getSubAbility().getApi()==forge.game.ability.ApiType.ChangeZone
                && "Hand".equals(sa.getSubAbility().getParamOrDefault("Destination",""))) {
            return forge.gamemodes.match.input.InputConfirm.confirm(this,sa.getHostCard().getView(),sa,
                sa.getHostCard().getName()+": Reveal the matching top card and put it into your hand?",true,
                List.of("Reveal and put into hand","Leave on top"));
        }
        return super.confirmAction(sa,mode,message,options,cardToShow,params);
    }
    @Override
    public CardCollectionView chooseCardsToDiscardToMaximumHandSize(final int nDiscard) {
        // Keep cleanup on the same blocking card-choice path as other Arena
        // decisions, instead of the desktop input's await-next-input shortcut.
        var cards = forge.game.GameEntityView.getMap(getPlayer().getCardsIn(ZoneType.Hand));
        tempShowCards(getPlayer().getCardsIn(ZoneType.Hand));
        try {
            var selected = getGui().getChoices("Discard " + nDiscard + " card(s) to your maximum hand size",
                nDiscard, nDiscard, cards.getTrackableKeys());
            CardCollection result = new CardCollection();
            cards.addToList(selected, result);
            if (result.size() != nDiscard) throw new IllegalStateException("Incomplete cleanup discard");
            return result;
        } finally { endTempShowCards(); }
    }

    @Override
    public <T extends forge.game.GameEntity> List<T> chooseEntitiesForEffect(
            final forge.util.collect.FCollectionView<T> options, final int min, final int max,
            final forge.game.player.DelayedReveal reveal, final SpellAbility sa, final String title,
            final Player targetedPlayer, Map<String, Object> params) {
        if (reveal != null) {
            if (reveal.getZone() != null && reveal.getZone().contains(ZoneType.Hand) && !getPlayer().getView().equals(reveal.getOwner())) {
                reveal(reveal);
            }
            tempShow(reveal.getCards());
        }
        if (options.isEmpty()) return new ArrayList<>();
        var views = forge.game.GameEntityView.getMap(options);
        tempShow(options);
        try {
            var context=new LinkedHashMap<String,Object>();
            context.put("sourceId",sa==null?0:forge.game.ArenaCardIds.id(sa.getHostCard().getId()));
            if(sa!=null && sa.getApi()==forge.game.ability.ApiType.Proliferate)context.put("proliferateSelection",true);
            var selected = BattlePromptContext.with(context,
                () -> getGui().getChoices(title, Math.min(min, options.size()), max < 0 ? options.size() : Math.min(max, options.size()), views.getTrackableKeys()));
            List<T> result = new ArrayList<>(); views.addToList(selected, result); return result;
        } finally { endTempShowCards(); }
    }

    public BattlePlayerControllerHuman(final Game game0, final Player p, final LobbyPlayer lp) {
        super(game0, p, lp);
    }

    public BattlePlayerControllerHuman(final Player p, final LobbyPlayer lp, final PlayerControllerHuman owner) {
        super(p, lp, owner);
    }

    @Override
    public List<SpellAbility> orderSimultaneousSa(List<SpellAbility> activePlayerSAs) {
        return activePlayerSAs;
    }

    @Override
    public ReplacementEffect chooseSingleReplacementEffect(final List<ReplacementEffect> possibleReplacers) {
        if (possibleReplacers == null || possibleReplacers.isEmpty()) {
            return null;
        }
        if(possibleReplacers.size()==1)return possibleReplacers.get(0);
        return getGui().getChoices("Choose the replacement effect to apply",1,1,possibleReplacers).get(0);
    }

    @Override
    public CardCollection chooseCardsForEffectMultiple(Map<String, CardCollection> validMap,
            SpellAbility sa, String title, boolean isOptional) {
        // Ask directly for a card of each offered type. A type dropdown hides
        // the revealed cards and adds a decision that the card never requires.
        CardCollection selected=new CardCollection();
        for(var entry:validMap.entrySet()) {
            CardCollection available=new CardCollection(entry.getValue());
            available.removeAll(selected);
            if(available.isEmpty())continue;
            selected.addAll(chooseCardsForEffect(available,sa,
                title+" ("+entry.getKey()+")",0,1,true,null));
        }
        return selected;
    }

    @Override
    public int chooseNumber(SpellAbility sa,String title,int min,int max) {
        if(min>=max)return min;
        return BattlePromptContext.with(Map.of("numberSourceId",sa==null?0:forge.game.ArenaCardIds.id(sa.getHostCard().getId())),
            ()->getGui().getInteger(title,min,max,9));
    }

    @Override
    public int chooseNumber(SpellAbility sa,String title,List<Integer> choices,Player relatedPlayer) {
        return BattlePromptContext.with(Map.of("numberSourceId",sa==null?0:forge.game.ArenaCardIds.id(sa.getHostCard().getId())),
            ()->getGui().getChoices(title,1,1,choices).get(0));
    }

    @Override
    public forge.card.ICardFace chooseSingleCardFace(SpellAbility sa,String message,
            java.util.function.Predicate<forge.card.ICardFace> allowed,String initialName) {
        forge.model.FModel.getMagicDb().ensureAllCardsLoaded();
        String question=message;
        while(true) {
            String name=getGui().showInputDialog(question,"Choose a card name",null,"",null,false);
            if(name==null)continue;
            var face=forge.StaticData.instance().getCommonCards().getFaceByName(name.trim());
            if(face!=null && allowed.test(face))return face;
            question="Enter a valid card name. "+message;
        }
    }

    private static boolean librarySearch(SpellAbility sa) {
        return sa!=null && sa.getApi()==forge.game.ability.ApiType.ChangeZone
            && java.util.Arrays.asList(sa.getParamOrDefault("Origin","").split(",")).contains("Library");
    }

    private CardCollection searchCards(CardCollectionView cards, SpellAbility sa, String title, int min, int max) {
        if(cards.isEmpty())return new CardCollection();
        tempShowCards(cards);
        try {
            var map=forge.game.GameEntityView.getMap(cards);
            var chosen=BattlePromptContext.with(Map.of("nativeKind","search","sourceId",forge.game.ArenaCardIds.id(sa.getHostCard().getId())),
                ()->getGui().getChoices(title,min,Math.min(max,cards.size()),map.getTrackableKeys()));
            CardCollection result=new CardCollection();map.addToList(chosen,result);return result;
        } finally {endTempShowCards();}
    }

    @Override
    public CardCollectionView chooseCardsForEffect(final CardCollectionView cards, final SpellAbility sa,
            final String title, final int min, final int max, final boolean optional, Map<String,Object> params) {
        if(librarySearch(sa))return searchCards(cards,sa,title,optional?0:min,max);
        if(cards.isEmpty())return new CardCollection();
        var views=forge.game.GameEntityView.getMap(cards);
        tempShowCards(cards);
        try {
            var context=new LinkedHashMap<String,Object>();
            context.put("sourceId",sa==null?0:forge.game.ArenaCardIds.id(sa.getHostCard().getId()));
            if(sa!=null && sa.getApi()==forge.game.ability.ApiType.ChangeZone
                && "Exile".equals(sa.getParamOrDefault("Destination","")) && "Hand".equals(sa.getParamOrDefault("Origin","")))
                context.put("promptId",1117);
            var selected=BattlePromptContext.with(context,
                ()->getGui().getChoices(title,optional?0:Math.min(min,cards.size()),max<0?cards.size():Math.min(max,cards.size()),views.getTrackableKeys()));
            CardCollection result=new CardCollection();views.addToList(selected,result);
            if(sa!=null && context.containsKey("promptId"))for(var chosen:result)
                forge.game.ArenaCardIds.selectedForExile(chosen.getId(),sa.getHostCard().getId());
            return result;
        } finally {endTempShowCards();}
    }

    @Override
    public <T extends forge.game.GameEntity> T chooseSingleEntityForEffect(final forge.util.collect.FCollectionView<T> options,
            final forge.game.player.DelayedReveal delayed, final SpellAbility sa, final String title,
            final boolean optional, final Player target, Map<String,Object> params) {
        if (delayed != null) {
            if (delayed.getZone() != null && delayed.getZone().contains(ZoneType.Hand) && !getPlayer().getView().equals(delayed.getOwner())) {
                reveal(delayed);
            }
            tempShow(delayed.getCards());
        }
        if (librarySearch(sa) && options.stream().allMatch(c -> c instanceof forge.game.card.Card)) {
            CardCollection cards = new CardCollection();
            for (T c : options) cards.add((forge.game.card.Card) c);
            CardCollection selected = searchCards(cards, sa, title, optional ? 0 : 1, 1);
            return selected.isEmpty() ? null : (T) selected.get(0);
        }
        if (options.isEmpty()) return null;
        var views = forge.game.GameEntityView.getMap(options);
        tempShow(options);
        try {
            var context=new LinkedHashMap<String,Object>();
            context.put("sourceId",sa==null?0:forge.game.ArenaCardIds.id(sa.getHostCard().getId()));
            boolean exileHand=sa!=null && sa.getApi()==forge.game.ability.ApiType.ChangeZone
                && "Exile".equals(sa.getParamOrDefault("Destination","")) && "Hand".equals(sa.getParamOrDefault("Origin",""));
            if(exileHand)context.put("promptId",1117);
            var selected = BattlePromptContext.with(context,
                () -> getGui().getChoices(title, optional ? 0 : 1, 1, views.getTrackableKeys()));
            List<T> result = new ArrayList<>(); views.addToList(selected, result);
            if(exileHand)for(var chosen:result)if(chosen instanceof forge.game.card.Card card)
                forge.game.ArenaCardIds.selectedForExile(card.getId(),sa.getHostCard().getId());
            return result.isEmpty() ? null : result.get(0);
        } finally { endTempShowCards(); }
    }

    @Override
    protected boolean orderedMoveToTopOfLibrary(final forge.game.zone.ZoneType destinationZone, final SpellAbility source) {
        if (source != null && source.hasParam("LibraryPosition2")) {
            return forge.game.ability.AbilityUtils.calculateAmount(source.getHostCard(), source.getParam("LibraryPosition2"), source) >= 0;
        }
        if (source != null && source.getApi() == forge.game.ability.ApiType.Dig) {
            return false;
        }
        return super.orderedMoveToTopOfLibrary(destinationZone, source);
    }

    @Override
    public boolean payCostToPreventEffect(final forge.game.cost.Cost cost, final SpellAbility sa,
            final boolean alreadyPaid, final forge.util.collect.FCollectionView<forge.game.player.Player> allPayers) {
        boolean cumulativeUpkeep=sa.hasParam("CumulativeUpkeep")
            || sa.isKeyword(forge.game.keyword.Keyword.CUMULATIVE_UPKEEP);
        boolean optionalManaUnless=sa.hasParam("UnlessCost") && cost.isOnlyManaCost() && !cost.isMandatory();
        if (!cumulativeUpkeep && !optionalManaUnless) {
            return super.payCostToPreventEffect(cost, sa, alreadyPaid, allPayers);
        }
        try {
            // Mana payment can immediately consume floating mana. Give the
            // actual unless-payer this decision before entering that workflow.
            // Other cost types already have their own confirmation/selection.
            String payment=cost.isOnlyManaCost()?displayEffectManaCost(cost,sa):cost.toSimpleString();
            String sourceName=sa.getHostCard().getName();
            String question=cumulativeUpkeep?"Pay cumulative upkeep for " + sourceName + "?"
                :"Pay " + payment + " for " + sourceName + "?";
            String decline=cumulativeUpkeep?"Sacrifice " + sourceName:"Decline";
            boolean accepted=forge.gamemodes.match.input.InputConfirm.confirm(this, sa,
                question, false, java.util.List.of("Pay " + payment, decline));
            if (!accepted) return false;
            // Forge normally asks a separate question for a literal zero cost.
            // It is already answered here; symbolic X still uses real payment
            // so Forge records its evaluated value (including zero).
            if (cost.isFree()) return true;
            return super.payCostToPreventEffect(cost, sa, alreadyPaid, allPayers);
        } finally {
            // The resolved payment decision cannot be reversed by undoing a
            // mana ability that supplied its payment.
            getPlayer().getGame().getStack().clearUndoStack();
        }
    }

    private static String displayEffectManaCost(forge.game.cost.Cost cost, SpellAbility sa) {
        var mana=cost.getCostMana();
        if (mana==null) return ManaCost.ZERO.toString();
        var toPay=new ManaCostBeingPaid(mana.getManaCostFor(sa));
        String xDefinition=sa.getParamOrDefault("XAlternative",sa.getSVar("X"));
        String xColor=sa.getXColor();
        if (sa.getHostCard().hasKeyword("Spend only colored mana on X. No more than one mana of each color may be spent this way.")) {
            xColor="WUBRGX";
        }
        // Match PlaySpellAbility.payManaCost's X calculation without changing
        // the ability, spending mana, or running interactive cost adjustments.
        if (mana.getAmountOfX()>0 && !"Count$xPaid".equals(xDefinition)) {
            int x=forge.game.ability.AbilityUtils.calculateAmount(sa.getHostCard(),xDefinition,sa);
            toPay.setXManaCostPaid(x,xColor);
        } else if (sa.getXManaCostPaid()!=null) {
            toPay.setXManaCostPaid(sa.getXManaCostPaid(),xColor);
        }
        return toPay.toManaCost().toString();
    }

    @Override
    public boolean confirmPayment(final forge.game.cost.CostPart costPart, final String question, final SpellAbility sa) {
        // Activating the ability already commits to its printed life cost.
        // HumanCostDecision still checks affordability and actually pays it.
        if (costPart instanceof forge.game.cost.CostPayLife && sa != null && sa.isActivatedAbility()) {
            return true;
        }
        if (costPart instanceof forge.game.cost.CostSacrifice cs && cs.payCostFromSource()) {
            return true;
        }
        return super.confirmPayment(costPart, question, sa);
    }

    @Override
    public List<SpellAbility> chooseSaToActivateFromOpeningHand(final List<SpellAbility> abilities) {
        if(abilities.isEmpty())return List.of();
        var cards=new CardCollection();
        for(var ability:abilities)cards.add(ability.getHostCard());
        var views=forge.game.GameEntityView.getMap(cards);
        var chosen=BattlePromptContext.with(Map.of("promptId",114),
            ()->getGui().getChoices("Perform opening hand actions",0,cards.size(),views.getTrackableKeys()));
        var result=new ArrayList<SpellAbility>();
        for(var view:chosen)
            for(var ability:abilities)if(ability.getHostCard().getId()==view.getId()) {result.add(ability);break;}
        return result;
    }

    @Override
    public String chooseSomeType(final String kindOfType, final SpellAbility sa,
            final java.util.Collection<String> validTypes, final boolean optional) {
        java.util.Collection<String> offeredTypes=validTypes;
        // User preference: Cavern offers the creature types in this deck.
        if("Creature".equals(kindOfType) && sa!=null && "Cavern of Souls".equals(sa.getHostCard().getName())) {
            var deckTypes=new java.util.HashSet<String>();
            var deck=getPlayer().getRegisteredPlayer().getDeck();
            var cards=new ArrayList<forge.item.PaperCard>(deck.getMain().toFlatList());
            cards.addAll(deck.getCommanders());
            for(var card:cards)for(var face:card.getRules().getAllFaces())
                if(face.getType().isCreature())deckTypes.addAll(face.getType().getCreatureTypes());
            var matching=validTypes.stream().filter(deckTypes::contains).toList();
            // Preserve a usable choice for decks without creature types.
            if(!matching.isEmpty())offeredTypes=matching;
        }
        final var choices=offeredTypes;
        return BattlePromptContext.with(Map.of("nativeKind","type", "typeKind",kindOfType,
            "sourceId",sa==null?0:forge.game.ArenaCardIds.id(sa.getHostCard().getId())),
            ()->super.chooseSomeType(kindOfType,sa,choices,optional));
    }

    @Override
    public Map<Byte,Integer> specifyManaCombo(final SpellAbility sa, final forge.card.ColorSet colorSet,
            final int manaAmount, final boolean different) {
        var colors=new ArrayList<forge.card.MagicColor.Color>(colorSet.getOrderedColors());
        if(colorSet.isColorless() && (!different || manaAmount<=1))
            return Map.of(forge.card.MagicColor.COLORLESS,manaAmount);
        if(colors.size()==1 && (!different || manaAmount<=1))
            return Map.of(colors.get(0).getColorMask(),manaAmount);
        if(manaAmount<0 || manaAmount>4 || colors.isEmpty())
            return super.specifyManaCombo(sa,colorSet,manaAmount,different);
        var allocations=new ArrayList<Map<Byte,Integer>>();
        manaCombinations(colors,0,manaAmount,different,new LinkedHashMap<>(),allocations);
        if(allocations.isEmpty())throw new IllegalStateException("No legal mana combination");
        if(allocations.size()==1)return allocations.get(0);
        var labels=new ArrayList<String>();
        for(var allocation:allocations) {
            var label=new StringBuilder("Add ");
            for(var color:colors)label.append(("{"+forge.card.MagicColor.toShortString(color.getColorMask())+"}")
                .repeat(allocation.getOrDefault(color.getColorMask(),0)));
            labels.add(label.toString());
        }
        var context=Map.<String,Object>of("sourceId",forge.game.ArenaCardIds.id(sa.getHostCard().getId()));
        var selected=BattlePromptContext.with(context,
            ()->getGui().getChoices("Choose mana to add",1,1,labels));
        if(selected==null || selected.size()!=1 || !labels.contains(selected.get(0)))
            throw new IllegalStateException("Incomplete mana combination choice");
        return allocations.get(labels.indexOf(selected.get(0)));
    }
    private static void manaCombinations(final List<forge.card.MagicColor.Color> colors, final int index,
            final int remaining, final boolean different, final LinkedHashMap<Byte,Integer> current,
            final List<Map<Byte,Integer>> result) {
        if(index==colors.size()) {
            if(remaining==0)result.add(new LinkedHashMap<>(current));
            return;
        }
        byte color=colors.get(index).getColorMask();
        for(int count=Math.min(remaining,different?1:remaining);count>=0;count--) {
            if(count>0)current.put(color,count);else current.remove(color);
            manaCombinations(colors,index+1,remaining-count,different,current,result);
        }
        current.remove(color);
    }
    @Override
    public byte chooseColor(final String message, final SpellAbility sa, final forge.card.ColorSet colors) {
        if(colors.countColors()<2)return colors.getColor();
        var context=new LinkedHashMap<String,Object>();
        context.put("sourceId",sa==null?0:forge.game.ArenaCardIds.id(sa.getHostCard().getId()));
        if(sa!=null && sa.getManaPart()!=null) {
            context.put("nativeKind","manaProduction");
            context.put("manaColors",colors.getOrderedColors().stream().map(c->c.toString()).toList());
        }
        return BattlePromptContext.with(context,
            ()->getGui().getChoices(message,1,1,com.google.common.collect.Lists.newArrayList(colors.getOrderedColors())).get(0).getColorMask());
    }

    @Override
    public byte chooseColorAllowColorless(final String message, final forge.game.card.Card card, final forge.card.ColorSet colors) {
        var choices=com.google.common.collect.Lists.newArrayList(colors.getOrderedColors());
        choices.add(forge.card.MagicColor.Color.COLORLESS);
        return BattlePromptContext.with(Map.of("sourceId",card==null?0:forge.game.ArenaCardIds.id(card.getId())),
            ()->getGui().getChoices(message,1,1,choices).get(0).getColorMask());
    }

    @Override
    public boolean chooseCardsPile(final SpellAbility sa, final CardCollectionView first,
            final CardCollectionView second, final String faceUp) {
        boolean firstVisible=faceUp.equals("False"),secondVisible=!faceUp.equals("True");
        if(firstVisible)tempShowCards(first);
        if(secondVisible)tempShowCards(second);
        try {
            var piles=List.of(Map.of("id",1,"ids",first.stream().map(c->forge.game.ArenaCardIds.id(c.getId())).toList(),"faceDown",!firstVisible),
                Map.of("id",2,"ids",second.stream().map(c->forge.game.ArenaCardIds.id(c.getId())).toList(),"faceDown",!secondVisible));
            return BattlePromptContext.with(Map.of("nativeKind","piles","groups",piles,"sourceId",forge.game.ArenaCardIds.id(sa.getHostCard().getId())),
                ()->getGui().getChoices("Choose a pile",1,1,List.of("Pile 1","Pile 2")).get(0).equals("Pile 1"));
        } finally {endTempShowCards();}
    }

    private CardCollection orderLibraryCards(CardCollection cards, boolean bottom) {
        var map=forge.game.GameEntityView.getMap(cards);
        var ordered=getGui().order(bottom?"Order cards on bottom of your library":"Order cards on top of your library",
            bottom?"Bottom of library":"Top of library",0,0,map.getTrackableKeys(),null,null,false);
        CardCollection result=new CardCollection();map.addToList(ordered,result);return result;
    }

    @Override
    public ImmutablePair<CardCollection, CardCollection> arrangeForScry(final CardCollection topN) {
        final ImmutablePair<CardCollection, CardCollection> remembered = macros().consumeRememberedScry(topN);
        if (remembered != null) {
            return remembered;
        }

        tempShowCards(topN);
        try {
            var cardList = player.getCardsIn(forge.game.zone.ZoneType.Library);
            var result = arrangeForMove("Scry", cardList, topN, true, true);
            var toTop = result.getLeft();
            var toBottom = result.getRight();
            macros().addRememberedAction(new ScryAction(toTop, toBottom));
            return ImmutablePair.of(toTop, toBottom);
        } finally {
            endTempShowCards();
        }
    }


    @Override
    public ImmutablePair<CardCollection, CardCollection> arrangeForSurveil(final CardCollection topN) {
        tempShowCards(topN);
        try {
            var cards=forge.game.GameEntityView.getMap(topN);
            var views=getGui().order("Surveil", "Top of library", 0, topN.size(),
                cards.getTrackableKeys(), null, null, false);
            CardCollection toTop=new CardCollection();
            cards.addToList(views,toTop);
            CardCollection toGrave=new CardCollection(topN);
            toGrave.removeAll(toTop);
            return ImmutablePair.of(toTop,toGrave);
        } finally {endTempShowCards();}
    }

    @Override
    public List<AbilitySub> chooseModeForAbility(final SpellAbility sa, List<AbilitySub> possible, final int min, final int num,
                                                 boolean allowRepeat) {
        if (!allowRepeat && min == num && num == possible.size()) {
            return possible;
        }
        if (allowRepeat || sa.hasParam("Pawprint")) {
            return super.chooseModeForAbility(sa, possible, min, num, allowRepeat);
        }
        boolean trackerFrozen = getGame().getTracker().isFrozen();
        if (trackerFrozen) {
            getGame().getTracker().unfreeze();
        }
        Map<SpellAbilityView, AbilitySub> spellViewCache = SpellAbilityView.getMap(possible);
        if (trackerFrozen) {
            getGame().getTracker().freeze();
        }
        final String modeTitle = Localizer.getInstance().getMessage("lblPlayerActivatedCardChooseMode", sa.getActivatingPlayer().toString(), sa.getHostCard().getTranslatedName());
        final List<String> choiceDescriptions = new ArrayList<>();
        final Map<String, AbilitySub> modesByDescription = new LinkedHashMap<>();
        for (final AbilitySub sub : spellViewCache.values()) {
            final String description = sub.toUnsuppressedString();
            choiceDescriptions.add(description);
            modesByDescription.put(description, sub);
        }
        final List<String> remembered = macros().consumeRememberedModeChoice(choiceDescriptions, min, num, allowRepeat);
        if (remembered != null) {
            final List<AbilitySub> chosen = new ArrayList<>(remembered.size());
            for (final String description : remembered) {
                chosen.add(modesByDescription.get(description));
            }
            return chosen;
        }
        // Arena's small modal browser selects one item even with max > 1.
        // Present each legal combination as one labeled choice instead.
        final List<AbilitySub> modes = new ArrayList<>(spellViewCache.values());
        final Map<String,List<AbilitySub>> combinations = new LinkedHashMap<>();
        for(var indices:modeSelections(modes.size(),min,Math.min(num,modes.size()))) {
            List<AbilitySub> group=indices.stream().map(modes::get).toList();
            String label=group.isEmpty()?"Choose no modes":group.stream()
                .map(m->m.hasParam("SpellDescription")?m.getParam("SpellDescription").trim():m.toUnsuppressedString().trim())
                .reduce((a,b)->a+" / "+b).orElse("");
            if(group.size()>1)label=(group.size()==2 && modes.size()==2?"Both modes: ":"Choose "+group.size()+" modes: ")+label;
            if(combinations.containsKey(label))label+=" ("+(combinations.size()+1)+")";
            combinations.put(label,group);
        }
        final List<String> choices = new ArrayList<>(combinations.keySet());
        choices.sort(java.util.Comparator.comparingInt(label -> combinations.get(label).size()));
        var modeDefinitions=new ArrayList<Map<String,Object>>();
        for(int i=0;i<modes.size();i++)modeDefinitions.add(Map.of("index",i,"description",
            modes.get(i).hasParam("SpellDescription")?modes.get(i).getParam("SpellDescription"):modes.get(i).toUnsuppressedString()));
        var modeCombinations=new ArrayList<Map<String,Object>>();
        for(int i=0;i<choices.size();i++)modeCombinations.add(Map.of("id",i+1,"modes",combinations.get(choices.get(i)).stream().map(modes::indexOf).toList()));
        List<String> selected = BattlePromptContext.with(Map.of("nativeKind","modes","sourceId",forge.game.ArenaCardIds.id(sa.getHostCard().getId()),
            "modeMin",min,"modeMax",num,"modeDefinitions",modeDefinitions,"modeCombinations",modeCombinations,
            "modeDescription",ForgePlayback.abilityDescription(sa)),
            ()->getGui().getChoices(modeTitle,1,1,choices));
        final List<AbilitySub> chosen = new ArrayList<>();
        if (selected != null && !selected.isEmpty())chosen.addAll(combinations.get(selected.get(0)));
        final List<String> chosenDescriptions = new ArrayList<>(chosen.size());
        for (final AbilitySub sub : chosen) {
            chosenDescriptions.add(sub.toUnsuppressedString());
        }
        macros().addRememberedAction(new ModeChoiceAction(chosenDescriptions));
        return chosen;
    }

    static List<List<Integer>> modeSelections(int count,int min,int max) {
        List<List<Integer>> result=new ArrayList<>();
        collectModes(count,0,new ArrayList<>(),min,max,result);
        result.sort(java.util.Comparator.comparingInt(List::size));return result;
    }
    private static void collectModes(int count,int index,List<Integer> selected,int min,int max,List<List<Integer>> result) {
        if(index==count) {
            if(selected.size()>=min && selected.size()<=max)result.add(new ArrayList<>(selected));return;
        }
        if(selected.size()<max) {
            selected.add(index);collectModes(count,index+1,selected,min,max,result);selected.remove(selected.size()-1);
        }
        collectModes(count,index+1,selected,min,max,result);
    }

    @Override
    public boolean confirmTrigger(final WrappedAbility wrapper) {
        final SpellAbility sa = wrapper.getWrappedAbility();
        final Trigger regtrig = wrapper.getTrigger();
        if (!isMacroActive()) {
            AutoYieldStore.TriggerDecision decision = getTriggerDecision(wrapper.yieldKey());
            if (decision == AutoYieldStore.TriggerDecision.ACCEPT) return true;
            if (decision == AutoYieldStore.TriggerDecision.DECLINE) return false;
        }

        // Ask before payment: floating mana can otherwise satisfy the cost
        // before the player ever gets a chance to decline the trigger.
        if (sa.hasParam("Cost") && !sa.getParam("Cost").equals("0")) {
            boolean sacrificeSource=sa.getPayCosts().getCostParts().stream().anyMatch(part ->
                part instanceof forge.game.cost.CostSacrifice sacrifice && sacrifice.payCostFromSource());
            String payment=sacrificeSource?"Sacrifice " + regtrig.getHostCard().getName():"Pay " + sa.getPayCosts().toSimpleString();
            if(!sacrificeSource && sa.getParam("Cost").trim().matches("C(?:\\s+C)*"))
                payment="Pay " + sa.getParam("Cost").trim().split("\\s+").length + " colorless mana";
            return forge.gamemodes.match.input.InputConfirm.confirm(this, wrapper,
                payment + "?", false, java.util.List.of(payment, "Decline"));
        }

        final StringBuilder buildQuestion = new StringBuilder(Localizer.getInstance().getMessage("lblUseTriggeredAbilityOf") + " ");
        String hostName = regtrig.getHostCard().getName();
        buildQuestion.append(hostName).append("?");
        final boolean result = forge.gamemodes.match.input.InputConfirm.confirm(this, wrapper, buildQuestion.toString());
        macros().addRememberedAction(new ConfirmAction(wrapper.getCardView(), result));
        return result;
    }

    @Override
    public boolean applyManaToCost(ManaCostBeingPaid toPay, SpellAbility ability, String prompt, ManaConversionMatrix matrix, boolean effect) {
        int phyLifeToLose = 0;
        while (toPay.containsPhyrexianMana() && getPlayer().canPayLife(phyLifeToLose + 2, effect, ability)) {
            ManaCostShard chosen = null;
            for (var shard : toPay.getUnpaidShards()) {
                if (shard.isPhyrexian()) {
                    chosen = shard;
                    break;
                }
            }
            if (chosen == null) break;
            final ManaCostShard phyShard = chosen;

            List<String> colors = new ArrayList<>();
            if ((phyShard.getColorMask() & ManaAtom.WHITE) != 0) colors.add("White");
            if ((phyShard.getColorMask() & ManaAtom.BLUE) != 0) colors.add("Blue");
            if ((phyShard.getColorMask() & ManaAtom.BLACK) != 0) colors.add("Black");
            if ((phyShard.getColorMask() & ManaAtom.RED) != 0) colors.add("Red");
            if ((phyShard.getColorMask() & ManaAtom.GREEN) != 0) colors.add("Green");
            colors.add("Phyrexian");

            var context = new LinkedHashMap<String, Object>();
            context.put("nativeKind", "manaType");
            context.put("sourceId", ability != null && ability.getHostCard() != null ? forge.game.ArenaCardIds.id(ability.getHostCard().getId()) : 0);
            if (ability != null && ability.getHostCard() != null && ability.getHostCard().getManaCost() != null) {
                context.put("manaCost", ability.getHostCard().getManaCost().toString());
            } else if (ability != null && ability.getPayCosts() != null && ability.getPayCosts().getTotalMana() != null) {
                context.put("manaCost", ability.getPayCosts().getTotalMana().toString());
            }
            context.put("manaColors", colors);

            List<String> choices = List.of("Pay mana", "Pay 2 life");
            List<String> selected = BattlePromptContext.with(context,
                () -> getGui().getChoices("Select how to pay " + phyShard, 1, 1, choices));

            if (selected == null || selected.isEmpty()) {
                return false;
            }
            if (selected.get(0).equals("Pay 2 life")) {
                toPay.payPhyrexian();
                if (ability != null) {
                    ability.setSpendPhyrexianMana(true);
                }
                phyLifeToLose += 2;
            } else {
                break;
            }
        }

        boolean paid;
        if (toPay.isPaid()) {
            paid = true;
        } else {
            paid = super.applyManaToCost(toPay, ability, prompt, matrix, effect);
        }

        if (paid && phyLifeToLose > 0) {
            getPlayer().payLife(phyLifeToLose, ability, effect);
        }
        return paid;
    }

    @Override
    public Map<forge.game.card.Card, ManaCostShard> chooseCardsForConvokeOrImprovise(
            final SpellAbility sa, final ManaCost manaCost,
            final CardCollectionView untappedCards, boolean artifacts, boolean creatures, Integer maxReduction) {
        var list = new CardCollection(untappedCards);
        if(DISPLAY_PAYMENT.get())return java.util.Collections.emptyMap();
        if(!PREVIEW_PAYMENT.get() && !forge.game.cost.CostAdjustment.ARENA_PAYMENT_PREVIEW.get()) {
            int maximum=Math.min(manaCost.getCMC(),list.size());
            if(maxReduction!=null)maximum=Math.min(maximum,maxReduction);
            if(maximum<=0)return java.util.Collections.emptyMap();
            var views=forge.game.GameEntityView.getMap(list);
            String mechanic=artifacts?(creatures?"waterbend":"improvise"):"convoke";
            var selected=getGui().getChoices("Choose permanents to tap for "+mechanic+" (remaining cost "+manaCost+")",
                0,maximum,views.getTrackableKeys());
            CardCollection chosen=new CardCollection();
            views.addToList(selected,chosen);
            list=chosen;
        }
        var map = forge.ai.ComputerUtilMana.getConvokeOrImproviseFromList(manaCost, list, artifacts, creatures);
        if (maxReduction != null && map.size() > maxReduction) {
            Map<forge.game.card.Card, ManaCostShard> limited = new java.util.HashMap<>();
            int count = 0;
            for (var entry : map.entrySet()) {
                if (count++ >= maxReduction) break;
                limited.put(entry.getKey(), entry.getValue());
            }
            return limited;
        }
        return map;
    }

    @Override
    public CardCollectionView chooseCardsToDelve(final int genericAmount, final CardCollection grave) {
        int count = Math.min(genericAmount, grave.size());
        if (count == 0) return CardCollection.EMPTY;
        CardCollection toExile = new CardCollection();
        for (int i = 0; i < count; i++) {
            toExile.add(grave.get(i));
        }
        return toExile;
    }

    @Override
    public Player choosePlayerToAssistPayment(forge.util.collect.FCollectionView<Player> optionList, SpellAbility sa, String title, int max) {
        return null;
    }
}
