/* SPDX-License-Identifier: GPL-3.0-or-later */
import forge.LobbyPlayer;
import forge.game.Game;
import forge.game.card.CardCollection;
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
    @Override
    protected void reveal(final CardCollectionView cards, final ZoneType zone, final forge.game.player.PlayerView owner,
                          String message, boolean addSuffix) {
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
        if (sa != null && sa.getApi() == forge.game.ability.ApiType.Proliferate && min == 0) {
            List<T> selected = new ArrayList<>();
            for (T entity : options) {
                if (selected.size() >= max) break;
                if (entity == getPlayer() || entity instanceof forge.game.card.Card card
                        && card.getController() == getPlayer()) selected.add(entity);
            }
            return selected;
        }
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
            var selected = BattlePromptContext.with(Map.of("sourceId", sa == null ? 0 : forge.game.ArenaCardIds.id(sa.getHostCard().getId())),
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
            var selected=BattlePromptContext.with(Map.of("sourceId",sa==null?0:forge.game.ArenaCardIds.id(sa.getHostCard().getId())),
                ()->getGui().getChoices(title,optional?0:Math.min(min,cards.size()),max<0?cards.size():Math.min(max,cards.size()),views.getTrackableKeys()));
            CardCollection result=new CardCollection();views.addToList(selected,result);return result;
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
            var selected = BattlePromptContext.with(Map.of("sourceId", sa == null ? 0 : forge.game.ArenaCardIds.id(sa.getHostCard().getId())),
                () -> getGui().getChoices(title, optional ? 0 : 1, 1, views.getTrackableKeys()));
            List<T> result = new ArrayList<>(); views.addToList(selected, result); return result.isEmpty() ? null : result.get(0);
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
    public boolean confirmPayment(final forge.game.cost.CostPart costPart, final String question, final SpellAbility sa) {
        if (costPart instanceof forge.game.cost.CostSacrifice cs && cs.payCostFromSource()) {
            return true;
        }
        return super.confirmPayment(costPart, question, sa);
    }

    @Override
    public String chooseSomeType(final String kindOfType, final SpellAbility sa,
            final java.util.Collection<String> validTypes, final boolean optional) {
        return BattlePromptContext.with(Map.of("nativeKind","type", "typeKind",kindOfType,
            "sourceId",sa==null?0:forge.game.ArenaCardIds.id(sa.getHostCard().getId())),
            ()->super.chooseSomeType(kindOfType,sa,validTypes,optional));
    }

    @Override
    public byte chooseColor(final String message, final SpellAbility sa, final forge.card.ColorSet colors) {
        if(colors.countColors()<2)return colors.getColor();
        return BattlePromptContext.with(Map.of("sourceId",sa==null?0:forge.game.ArenaCardIds.id(sa.getHostCard().getId())),
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
            "modeMin",min,"modeMax",num,"modeDefinitions",modeDefinitions,"modeCombinations",modeCombinations),
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

        // triggers with costs can always be declined by not paying the cost
        if (sa.hasParam("Cost") && !sa.getParam("Cost").equals("0")) {
            return true;
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
