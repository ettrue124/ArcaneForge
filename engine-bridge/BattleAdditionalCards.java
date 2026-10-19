/* SPDX-License-Identifier: GPL-3.0-or-later */
import java.nio.file.*;
import forge.StaticData;
import forge.card.CardRules;

/** Reviewed local rules for Arena cards absent from the bundled Forge release. */
final class BattleAdditionalCards {
    static void load() {
        Path directory=Path.of("engine-bridge","cards");
        if(!Files.isDirectory(directory))return;
        try(var paths=Files.list(directory)) {
            for(Path path:paths.filter(p->p.toString().endsWith(".txt")).sorted().toList()) {
                var rules=CardRules.fromScript(Files.readAllLines(path));
                if(StaticData.instance().getCommonCards().getCard(rules.getName())==null)
                    StaticData.instance().getCommonCards().getEditor().putCard(rules);
            }
        } catch(java.io.IOException error) {throw new IllegalStateException("Additional card rules could not load",error);}
    }
}
