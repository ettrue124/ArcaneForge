/* SPDX-License-Identifier: GPL-3.0-or-later */
import forge.game.Game;
import forge.game.GameEntity;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import forge.game.combat.CombatUtil;
import forge.gamemodes.match.input.InputSelectTargets;
import forge.player.PlayerControllerHuman;
import java.util.*;

/** Describe Forge's authoritative input; never infer legality from Arena art. */
final class BattleInputs {
    static String type(Object input) {
        if(input==null)return "";
        Class<?> kind=input.getClass();
        while(kind.getSimpleName().isEmpty())kind=kind.getSuperclass();
        return kind.getSimpleName();
    }
    static Map<String,Object> obj(Object... fields) {
        Map<String,Object> result=new LinkedHashMap<>();
        for(int i=0;i<fields.length;i+=2)result.put((String)fields[i],fields[i+1]);
        return result;
    }
    static Object field(Object input, String name) {
        // Pinned Forge InputSelectTargets has no public accessor. Fail loudly if
        // an upstream change removes it, rather than offer guessed targets.
        try {
            for(Class<?> type=input.getClass();type!=null;type=type.getSuperclass()) {
                try {var f=type.getDeclaredField(name);f.setAccessible(true);return f.get(input);}
                catch(NoSuchFieldException ignored) {}
            }
            throw new NoSuchFieldException(name);
        }
        catch(ReflectiveOperationException e){throw new IllegalStateException("Forge target input changed: "+name,e);}
    }
    static int id(GameEntity entity, Map<Player,Integer> seats) {
        return entity instanceof Player p ? seats.get(p) : forge.game.ArenaCardIds.id(entity.getId());
    }
    static Map<String,Object> describe(Game game, PlayerControllerHuman controller, Map<Player,Integer> seats) {
        Object current=controller.getInputProxy().getInput();
        String type=type(current);
        Map<String,Object> result=obj("type",type);
        if(current instanceof forge.gamemodes.match.input.InputConfirm) {
            boolean starting=field(current,"yesButtonText").equals(forge.util.Localizer.getInstance().getMessage("lblPlay"))
                && field(current,"noButtonText").equals(forge.util.Localizer.getInstance().getMessage("lblDraw"));
            result.put("startingPlayer",starting);
            String rawMsg = String.valueOf(field(current,"message"));
            String cleanMsg = rawMsg.replaceAll("\\s*\\(\\d+\\)", "");
            result.put("message", cleanMsg);
            result.put("options",List.of(field(current,"yesButtonText"),field(current,"noButtonText")));
            Object source=field(current,"sa");
            Object card=field(current,"card");
            if(source instanceof forge.game.trigger.WrappedAbility || cleanMsg.startsWith("Use triggered ability of")) {
                result.put("optionalTrigger",true);
                if(source instanceof forge.game.trigger.WrappedAbility wrapper) {
                    var effect=wrapper.getWrappedAbility();
                    if(effect.getApi()==forge.game.ability.ApiType.ChangeZone && effect.hasParam("Imprint")
                        && "Hand".equals(effect.getParam("Origin")) && "Exile".equals(effect.getParam("Destination"))
                        && "Card.nonArtifact+nonLand".equals(effect.getParam("ChangeType")))
                        result.put("optionalTriggerPrompt",7132);
                }
            }
            if(source instanceof SpellAbility sa && sa.hasParam("ETB") && sa.getReplacementEffect()!=null
                && sa.hasParam("UnlessCost") && sa.getParam("UnlessCost").startsWith("PayLife<"))
                result.put("entryReplacementDescription",sa.getReplacementEffect().getDescription());
            if(source instanceof SpellAbility sa && sa.getApi()==forge.game.ability.ApiType.Play
                && sa.hasParam("WithoutManaCost") && !sa.hasParam("CastTransformed")
                && card instanceof forge.game.card.CardView offered)
                result.put("freeCastCardId",forge.game.ArenaCardIds.id(offered.getId()));
            if(source instanceof SpellAbility sa && sa.getApi()==forge.game.ability.ApiType.Explore
                && card instanceof forge.game.card.CardView explored)
                result.put("exploreCardId",forge.game.ArenaCardIds.id(explored.getId()));
            result.put("sourceId",source instanceof SpellAbility sa ? forge.game.ArenaCardIds.id(sa.getHostCard().getId())
                : card instanceof forge.game.card.CardView cv ? forge.game.ArenaCardIds.id(cv.getId()) : 0);
            result.put("cardName",source instanceof SpellAbility sa ? sa.getHostCard().getName()
                : card instanceof forge.game.card.CardView cv ? cv.getName() : "");
        }
        if(current instanceof forge.gamemodes.match.input.InputSelectEntitiesFromList<?>) {
            List<Integer> choices=new ArrayList<>();
            for(Object value:(Iterable<?>)field(current,"validChoices"))choices.add(id((GameEntity)value,seats));
            result.put("choices",choices);result.put("min",field(current,"min"));result.put("max",field(current,"max"));
        }
        if(current instanceof InputSelectTargets) {
            SpellAbility sa=(SpellAbility)field(current,"sa");
            Integer fixed=(Integer)field(current,"numTargets");
            List<Integer> targets=new ArrayList<>();
            for(GameEntity candidate:sa.getTargetRestrictions().getAllCandidates(sa))targets.add(id(candidate,seats));
            result.put("targets",targets);result.put("sourceId",forge.game.ArenaCardIds.id(sa.getHostCard().getId()));
            result.put("description",ForgePlayback.abilityDescription(sa)); result.put("cardName",sa.getHostCard().getName());
            result.put("validTargetTypes",Arrays.asList(sa.getTargetRestrictions().getValidTgts()));
            int targetIndex=0;
            for(var group=sa.getRootAbility();group!=null && group!=sa;group=group.getSubAbility())
                if(group.usesTargeting())targetIndex++;
            result.put("targetIndex",targetIndex);
            result.put("targetingPlayer",seats.getOrDefault(sa.getActivatingPlayer(),1));
            result.put("selected",sa.getTargets().stream().filter(e->e instanceof GameEntity).map(e->id((GameEntity)e,seats)).toList());
            result.put("min",fixed==null?sa.getMinTargets():fixed);
            result.put("max",fixed==null?sa.getMaxTargets():fixed);
            result.put("mandatory",field(current,"mandatory"));
            Collection<?> division=(Collection<?>)field(current,"divisionValues");
            result.put("divided",sa.isDividedAsYouChoose() || division!=null&&!division.isEmpty());
        }
        var combat=game.getCombat();
        if(type.equals("InputAttack") && combat!=null) {
            List<Object> attackers=new ArrayList<>();
            for(Card card:controller.getPlayer().getCreaturesInPlay()) {
                List<Integer> recipients=new ArrayList<>();
                for(GameEntity defender:combat.getDefenders())if(CombatUtil.canAttack(card,defender))recipients.add(id(defender,seats));
                if(!recipients.isEmpty())attackers.add(obj("instanceId",forge.game.ArenaCardIds.id(card.getId()),"defenders",recipients,
                    "selected",combat.isAttacking(card)?id(combat.getDefenderByAttacker(card),seats):0));
            }
            result.put("attackers",attackers);
        }
        if(type.equals("InputBlock") && combat!=null) {
            List<Object> blockers=new ArrayList<>();
            for(Card card:controller.getPlayer().getCreaturesInPlay()) {
                List<Integer> attackers=new ArrayList<>();
                for(Card attacker:combat.getAttackers())if(combat.getDefenderPlayerByAttacker(attacker)==controller.getPlayer()
                    && CombatUtil.canBlock(attacker,card))attackers.add(forge.game.ArenaCardIds.id(attacker.getId()));
                List<Integer> selected=new ArrayList<>();
                for(Card attacker:combat.getAttackers())if(combat.isBlocking(card,attacker))selected.add(forge.game.ArenaCardIds.id(attacker.getId()));
                if(!attackers.isEmpty() || !selected.isEmpty())blockers.add(obj("instanceId",forge.game.ArenaCardIds.id(card.getId()),"attackers",attackers,
                    "selected",selected,"maxAttackers",card.canBlockAny()?attackers.size():Math.max(1,1+card.canBlockAdditional())));
            }
            result.put("blockers",blockers);
        }
        if(current instanceof forge.gamemodes.match.input.InputPayMana payment) {
            Object sa=field(payment,"saPaidFor");
            if(sa instanceof SpellAbility ability) {
                result.put("sourceId",forge.game.ArenaCardIds.id(ability.getHostCard().getId()));
                result.put("cardName",ability.getHostCard().getName());
                result.put("description",ForgePlayback.abilityDescription(ability));
            }
            Object mc=field(payment,"manaCost");
            if(mc!=null) {
                result.put("manaCost",mc.toString());
            }
            result.put("mandatory",field(payment,"mandatory"));
            Object autoSources=field(payment,"autoPayManaSources");
            if(autoSources instanceof Collection<?> col && !col.isEmpty()) {
                List<Integer> ids=new ArrayList<>();
                for(Object c:col)if(c instanceof Card card)ids.add(forge.game.ArenaCardIds.id(card.getId()));
                result.put("autoTapSources",ids);
            }
        }
        return result;
    }
}
