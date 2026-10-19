/* SPDX-License-Identifier: GPL-3.0-or-later */
/** Human priority must include fixed-output filters that Forge's AI planner excludes. */
final class BattleManaAffordability {
    static boolean canPay(forge.game.spellability.SpellAbility spell, forge.game.player.Player player) {
        if (forge.ai.ComputerUtilMana.canPayManaCost(spell, player, 0, false)) return true;
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
    private static void combinations(String[] colors,int count,int start,java.util.List<String> prefix,
            java.util.List<java.util.List<String>> result) {
        if(count==0){result.add(new java.util.ArrayList<>(prefix));return;}
        for(int i=start;i<colors.length;i++) {
            prefix.add(colors[i]);combinations(colors,count-1,i,prefix,result);prefix.remove(prefix.size()-1);
        }
    }
}
