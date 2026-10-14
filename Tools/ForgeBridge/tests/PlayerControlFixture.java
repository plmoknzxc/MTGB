package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Setup only; actual spells cause and release control through Forge's durations. */
public final class PlayerControlFixture {
    static void place(String name,Player player,ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException(name);
        Card card=Card.fromPaperCard(paper,player);player.getZone(zone).add(card);
        card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());card.setSickness(false);
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            int seat=ForgeBridge.config.get("firstPlayerSeat").getAsInt();
            Player master=ForgeBridge.game.getPlayers().get(seat-1),slave=ForgeBridge.game.getPlayers().get(2-seat);
            boolean combat=ForgeBridge.config.get("fixture").getAsString().equals("combat");
            for(int i=0;i<(combat?4:8);i++)place(combat?"Island":"Swamp",master,ZoneType.Battlefield);
            place(combat?"Secret of Bloodbending":"Worst Fears",master,ZoneType.Hand);
            for(int i=0;i<4;i++)place("Mountain",slave,ZoneType.Battlefield);
            for(int i=0;i<2;i++)place("Island",slave,ZoneType.Battlefield);
            place("Shock",slave,ZoneType.Hand);place("Opt",slave,ZoneType.Hand);
            place("Grizzly Bears",slave,ZoneType.Battlefield);
            place("Grizzly Bears",master,ZoneType.Battlefield);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
