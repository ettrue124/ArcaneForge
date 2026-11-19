import forge.game.card.Card;
import forge.game.zone.ZoneType;
import forge.card.MagicColor;
import forge.card.mana.ManaAtom;

/** Values for Arena's existing devotion badge; never chooses a mana color. */
final class BattleDevotionDisplay {
    static int count(Card card,byte colors) {
        int result=card.getController().getDevotionMod();
        for(var permanent:card.getController().getCardsIn(ZoneType.Battlefield))
            for(var shard:permanent.getManaCost())if(shard.isColor(colors))result++;
        return result;
    }
    static Integer preview(Card card) {
        String rule=card.getSVar("X");
        if(!rule.startsWith("Count$Devotion.") && !rule.startsWith("Count$DevotionDual."))return null;
        String[] parts=rule.split("/",2)[0].substring("Count$".length()).split("\\.");
        if(parts.length<2)return null;
        if(parts[1].equals("Chosen")) {
            if(card.hasChosenColor())return count(card,ManaAtom.fromName(card.getChosenColor()));
            int best=0;
            for(byte color:MagicColor.WUBRG)best=Math.max(best,count(card,color));
            return best;
        }
        byte colors=ManaAtom.fromName(parts[1]);
        if(parts[0].equals("DevotionDual") && parts.length>2)colors|=ManaAtom.fromName(parts[2]);
        return count(card,colors);
    }
}
