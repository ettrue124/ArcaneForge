/* SPDX-License-Identifier: GPL-3.0-or-later */
import com.google.gson.*;
import forge.game.card.Card;
import forge.card.CardStateName;
import java.nio.file.*;
import java.util.*;

/** Resolve generated tokens using real printing links and their printed traits. */
final class BattleGeneratedCards {
    static String tokenKey(Card card) {
        var rules=card.getPaperCard()==null?null:card.getPaperCard().getRules();
        if(rules==null)return "";
        return rules.getName()+"|"+rules.getType()+"|"+rules.getColor()+"|"+rules.getPower()+"|"+rules.getToughness()+"|"+rules.getOracleText().replace('\u2014','-').replace('\u2013','-').replace('\uFFFD','-')
            +(card.getCurrentStateName()==CardStateName.Backside?"|Transformed":"");
    }
    private static final JsonObject catalogue=load();
    private static JsonObject load() {
        try {return JsonParser.parseString(Files.readString(Path.of(System.getProperty("arcane.generatedCatalogue","data/arena-generated-cards.json")))).getAsJsonObject();}
        catch(java.io.IOException error){throw new IllegalStateException("Generated-card catalogue missing",error);}
    }
    private static boolean statMatches(String printed,int actual,String expression) {
        return printed.isEmpty() || (printed.matches("-?[0-9]+") ? Integer.parseInt(printed)==actual : printed.equals(expression));
    }
    private static boolean matches(Card card,JsonObject token) {
        String name=token.get("name").getAsString();
        // Tokens with identical names and stats can have different printed
        // abilities (for example flying and nonflying Spirits).
        var printed=card.getOriginalState(CardStateName.Original);
        if(token.has("types")) {
            Set<String> types=new HashSet<>();
            for(var type:token.getAsJsonArray("types"))types.add(type.getAsString().toLowerCase());
            if(types.contains("artifact")!=printed.getType().isArtifact()
                || types.contains("creature")!=printed.getType().isCreature()
                || types.contains("enchantment")!=printed.getType().isEnchantment())return false;
        }
        if(token.has("colors") && !token.getAsJsonArray("colors").isEmpty()) {
            Set<String> nativeColors=new HashSet<>();
            for(var color:token.getAsJsonArray("colors"))if(!color.getAsString().equalsIgnoreCase("Colorless"))nativeColors.add(color.getAsString().toLowerCase());
            Set<String> forgeColors=new HashSet<>();
            String[] names={"white","blue","black","red","green"};
            for(int i=0;i<5;i++)if(printed.getColor().hasAnyColor(forge.card.MagicColor.WUBRG[i]))forgeColors.add(names[i]);
            if(!nativeColors.equals(forgeColors))return false;
        }
        if(token.has("rules")) {
            Set<String> simple=Set.of("flying","vigilance","trample","haste","menace","deathtouch","lifelink","reach","defender","indestructible","first strike","double strike","decayed");
            Set<String> nativeKeywords=new HashSet<>(),forgeKeywords=new HashSet<>();
            for(var rule:token.getAsJsonArray("rules")) {
                String text=rule.getAsString().replaceAll("<[^>]*>","").trim().toLowerCase();
                for(String keyword:simple)if(text.equals(keyword) || text.startsWith(keyword+" ("))nativeKeywords.add(keyword);
            }
            for(var keyword:printed.getIntrinsicKeywords()) {
                String text=keyword.getOriginal().toLowerCase();
                if(simple.contains(text))forgeKeywords.add(text);
            }
            if(!nativeKeywords.equals(forgeKeywords))return false;
        }
        String[] traits=token.get("signature").getAsString().split("\\|",-1);
        boolean vanilla=printed.getIntrinsicKeywords().isEmpty()
            && printed.getSpellAbilities().stream().noneMatch(a->!(a.isSpell() && a.isBasicSpell()) && !a.isLandAbility()) && printed.getTriggers().isEmpty()
            && printed.getStaticAbilities().isEmpty() && printed.getReplacementEffects().isEmpty();
        if(vanilla && traits.length==7 && !traits[6].isEmpty())return false;
        return (name.equals(card.getName()) || (name+" Token").equals(card.getName()))
            && statMatches(token.get("power").getAsString(),card.getBasePower(),printed.getBasePowerString())
            && statMatches(token.get("toughness").getAsString(),card.getBaseToughness(),printed.getBaseToughnessString());
    }
    static Integer printing(Card card,Map<Integer,Integer> identities) {
        Card copy=card.getCopiedPermanent();
        if(copy!=null && identities.containsKey(copy.getId()))return identities.get(copy.getId());
        var keys=catalogue.getAsJsonObject("keys");
        if(keys!=null && keys.has(tokenKey(card)))return keys.get(tokenKey(card)).getAsInt();
        var tokens=catalogue.getAsJsonObject("tokens");List<Integer> candidates=new ArrayList<>();
        var spawn=card.getTokenSpawningAbility();
        if(spawn!=null) {
            int parent=identities.getOrDefault(spawn.getHostCard().getId(),0);
            var linked=catalogue.getAsJsonObject("links").getAsJsonArray(Integer.toString(parent));
            if(linked!=null)for(var value:linked) {
                int id=value.getAsInt();if(matches(card,tokens.getAsJsonObject(Integer.toString(id))))candidates.add(id);
            }
        }
        // Equivalent printings can have different art. Never substitute a token
        // with different printed abilities just because its name matches.
        if(candidates.isEmpty())for(var entry:tokens.entrySet())if(matches(card,entry.getValue().getAsJsonObject()))
            candidates.add(Integer.parseInt(entry.getKey()));
        Set<String> signatures=new HashSet<>();for(int id:candidates)signatures.add(tokens.getAsJsonObject(Integer.toString(id)).get("signature").getAsString());
        if(candidates.isEmpty() || signatures.size()!=1)return null;
        return Collections.min(candidates);
    }
}
