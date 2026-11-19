/* SPDX-License-Identifier: GPL-3.0-or-later */
import forge.ai.AIOption;
import forge.ai.LobbyPlayerAi;
import java.nio.file.*;
import java.util.Set;

/** Competitive heuristic profile plus simulation safety checks. */
final class BattleBotAi {
    static void initialize(String assets) {
        try {
            Path directory=Path.of(assets,"res","ai");
            Files.writeString(directory.resolve("ArcaneCompetitive.ai"),Files.readString(Path.of("engine-bridge/ArcaneCompetitive.ai")));
            forge.ai.AiProfileUtil.loadAllProfiles(directory.toString());
        } catch(java.io.IOException error) {throw new IllegalStateException("Competitive AI profile unavailable",error);}
    }
    static LobbyPlayerAi create(String name) {
        var player=new LobbyPlayerAi(name,Set.of(AIOption.USE_HYBRID_SIMULATION));
        player.setAiProfile("ArcaneCompetitive");
        return player;
    }
}
