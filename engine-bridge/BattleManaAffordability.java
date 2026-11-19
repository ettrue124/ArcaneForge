/* SPDX-License-Identifier: GPL-3.0-or-later */
/** Human priority must include fixed-output filters that Forge's AI planner excludes. */
final class BattleManaAffordability {
    static boolean canPay(forge.game.spellability.SpellAbility spell, forge.game.player.Player player) {
        if (forge.ai.ComputerUtilMana.canPayManaCost(spell, player, 0, false)) return true;
        if(spell.getPayCosts()==null)return false;
        // Urza-style repeatable mana: each untapped artifact can pay for one
        // activation. Check a complete payment without changing any real taps
        // or counting the same artifact as both this cost and a mana source.
        for(var source:player.getCardsIn(forge.game.zone.ZoneType.Battlefield))for(var manaAbility:source.getManaAbilities()) {
            manaAbility.setActivatingPlayer(player);
            var costs=manaAbility.getPayCosts();
            if(costs==null || costs.getCostParts().size()!=1 || manaAbility.getSubAbility()!=null
                || !"1".equals(manaAbility.getParamOrDefault("Amount","1"))
                || !manaAbility.getParamOrDefault("Produced","").matches("[WUBRGC]")
                || !(costs.getCostParts().get(0) instanceof forge.game.cost.CostTapType tap)
                || !"1".equals(tap.getAmount()) || !"Artifact".equals(tap.getType())
                || !tap.canTapSource || !manaAbility.canPlay() || !manaAbility.metConditions())continue;
            String color=manaAbility.getParam("Produced");
            if(manaAbility.getManaPart()==null || !manaAbility.getManaPart().meetsManaRestrictions(spell)
                || !spell.allowsPayingWithShard(source,forge.card.MagicColor.fromName(color)))continue;
            var remaining=forge.ai.ComputerUtilMana.calculateManaCost(spell.getPayCosts(),spell,player,true,0,false);
            var artifacts=new java.util.ArrayList<forge.game.card.Card>();
            for(var artifact:player.getCardsIn(forge.game.zone.ZoneType.Battlefield))
                if(artifact.isArtifact() && forge.game.card.CardPredicates.CAN_TAP.test(artifact))artifacts.add(artifact);
            // Prefer non-mana artifacts, leaving mana rocks for the planner.
            artifacts.sort(java.util.Comparator.comparingInt(c->c.getManaAbilities().isEmpty()?0:1));
            if(payWithArtifactMana(remaining,spell,player,color,artifacts,0,
                    new java.util.HashSet<>(),new int[]{512}))return true;
        }

        // Restricted floating mana needs a separate allocation for the activation.
        // Leave that case to Forge rather than spend the same pool twice.
        if (spell.getPayCosts() == null || player.getManaPool().totalMana() != 0) return false;
        for (var card : player.getCardsIn(forge.game.zone.ZoneType.Battlefield)) {
            for (var filter : card.getManaAbilities()) {
                filter.setActivatingPlayer(player);
                var costs = filter.getPayCosts();
                if (costs == null || !costs.hasManaCost() || !costs.hasTapCost()
                    || filter.getApi() != forge.game.ability.ApiType.Mana
                    || filter.getSubAbility() != null || !filter.canPlay()
                    || !filter.metConditions()
                    || !forge.game.cost.CostPayment.canPayAdditionalCosts(costs, filter, false, player)) continue;
                var activation = costs.getCostMana().getManaCostFor(filter);
                if (activation.getCMC() <= 0) continue;
                var mana = filter.getManaPart();
                String produced = filter.getParamOrDefault("Produced", "");
                if (mana == null || !mana.meetsManaRestrictions(spell)) continue;
                var outputs=new java.util.ArrayList<java.util.List<String>>();
                if(produced.startsWith("Combo ") && produced.substring(6).matches("[WUBRGC]( [WUBRGC])*")) {
                    String amountText=filter.getParamOrDefault("Amount","1");
                    if(!amountText.matches("[1-4]"))continue;
                    combinations(produced.substring(6).split(" "),Integer.parseInt(amountText),0,new java.util.ArrayList<>(),outputs);
                } else if("1".equals(filter.getParamOrDefault("Amount","1")) && produced.matches("[WUBRGC]( [WUBRGC])*")) {
                    outputs.add(java.util.Arrays.asList(produced.split(" ")));
                } else continue;
                for(var output:outputs) {
                var remainder = forge.ai.ComputerUtilMana.calculateManaCost(
                    spell.getPayCosts(), spell, player, true, 0, false);
                boolean useful = false;
                for (String color : output) {
                    byte mask = forge.card.MagicColor.fromName(color);
                    if (spell.allowsPayingWithShard(card, mask))
                        useful |= remainder.ai_payMana(color, player.getManaPool());
                }
                if (!useful) continue;
                // Add the input AFTER applying the output: a Signet cannot fund itself.
                // The AI planner excludes every mana-cost filter, so this also cannot
                // reuse this tapped source or assume a circular activation chain.
                remainder.addManaCost(activation);
                var sources = forge.ai.ComputerUtilMana.getManaSourcesToPayCost(remainder, spell, player, false);
                if (sources != null && sources.stream().noneMatch(source->source==card) && sources.stream().allMatch(source ->
                    source.getManaAbilities().stream().allMatch(a -> a.getManaPart() != null
                        && a.getManaPart().meetsManaRestrictions(filter)))) return true;
                }
            }
        }
        return false;
    }
    private static boolean payWithArtifactMana(forge.game.mana.ManaCostBeingPaid remaining,
            forge.game.spellability.SpellAbility spell,forge.game.player.Player player,String color,
            java.util.List<forge.game.card.Card> artifacts,int next,java.util.Set<forge.game.card.Card> reserved,int[] budget) {
        if(remaining.isPaid())return true;
        if(budget[0]--<=0)return false;
        if(!reserved.isEmpty()) {
            var sources=forge.ai.ComputerUtilMana.getManaSourcesToPayCost(new forge.game.mana.ManaCostBeingPaid(remaining),spell,player,false);
            if(sources!=null && sources.stream().noneMatch(reserved::contains)
                && sources.stream().allMatch(c->c.getManaAbilities().stream().noneMatch(a->
                    a.getPayCosts()!=null && a.getPayCosts().getCostParts().stream().anyMatch(p->p instanceof forge.game.cost.CostTapType))))return true;
        }
        for(int i=next;i<artifacts.size();i++) {
            var reduced=new forge.game.mana.ManaCostBeingPaid(remaining);
            if(!reduced.ai_payMana(color,player.getManaPool()))continue;
            var artifact=artifacts.get(i);reserved.add(artifact);
            boolean paid=payWithArtifactMana(reduced,spell,player,color,artifacts,i+1,reserved,budget);
            reserved.remove(artifact);
            if(paid)return true;
            if(budget[0]<=0)break;
        }
        return false;
    }
    private static void combinations(String[] colors,int count,int start,java.util.List<String> prefix,
            java.util.List<java.util.List<String>> result) {
        if(count==0){result.add(new java.util.ArrayList<>(prefix));return;}
        for(int i=start;i<colors.length;i++) {
            prefix.add(colors[i]);combinations(colors,count-1,i,prefix,result);prefix.remove(prefix.size()-1);
        }
    }
}
