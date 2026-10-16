/* SPDX-License-Identifier: GPL-3.0-or-later */
import forge.LobbyPlayer;
import forge.game.Game;
import forge.game.player.IGameEntitiesFactory;
import forge.game.player.Player;
import forge.game.player.PlayerController;
import forge.player.LobbyPlayerHuman;
import forge.player.PlayerControllerHuman;
import forge.util.GuiDisplayUtil;

public class BattleLobbyPlayerHuman extends LobbyPlayerHuman implements IGameEntitiesFactory {
    public BattleLobbyPlayerHuman(final String name) {
        super(name);
    }

    public BattleLobbyPlayerHuman(final String name, final int avatarIndex, final int sleeveIndex) {
        super(name, avatarIndex, sleeveIndex);
    }

    @Override
    public PlayerController createMindSlaveController(final Player master, final Player slave) {
        return new BattlePlayerControllerHuman(slave, this, (PlayerControllerHuman) master.getController());
    }

    @Override
    public Player createIngamePlayer(final Game game, final int id) {
        final Player player = new Player(GuiDisplayUtil.personalizeHuman(getName()), game, id);
        final PlayerControllerHuman controller = new BattlePlayerControllerHuman(game, player, this);
        player.setFirstController(controller);
        return player;
    }
}
