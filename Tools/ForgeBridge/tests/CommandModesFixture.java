// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Setup only; casting, legal modes, targets and effects use the real engine. */
public final class CommandModesFixture {
    static void add(String name, Player player, ZoneType zone) {
        Card card=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),player);
        player.getZone(zone).add(card);card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());card.setSickness(false);
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
            String test=ForgeBridge.config.get("session").getAsString();
            add("Kolaghan's Command",one,ZoneType.Hand);
            add("Swamp",one,ZoneType.Battlefield);
            add("Mountain",one,ZoneType.Battlefield);add("Mountain",one,ZoneType.Battlefield);
            if(!test.contains("no-creature"))add("Grizzly Bears",one,ZoneType.Graveyard);
            if(!test.contains("no-artifact"))add("Sol Ring",two,ZoneType.Battlefield);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
