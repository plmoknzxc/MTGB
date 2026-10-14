package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Setup only. Every color/mode selection is requested by a real spell resolution. */
public final class GenericChoiceFixture {
    static void place(String name,Player player,ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException(name);
        Card card=Card.fromPaperCard(paper,player);player.getZone(zone).add(card);
        card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());card.setSickness(false);
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
            for(String name:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int i=0;i<5;i++)place(name,one,ZoneType.Battlefield);
            String test=ForgeBridge.config.get("fixture").getAsString();
            if(test.equals("colors")) {
                place("Tablet of the Guilds",one,ZoneType.Hand);
                place("Shock",one,ZoneType.Hand);place("Giant Growth",one,ZoneType.Hand);
                place("Grizzly Bears",one,ZoneType.Battlefield);
            } else if(test.startsWith("villain")) {
                place("Hunted by The Family",one,ZoneType.Hand);
                place("Grizzly Bears",two,ZoneType.Battlefield);
                if(test.equals("villain-repeat"))place("The Valeyard",one,ZoneType.Battlefield);
            } else throw new IllegalArgumentException(test);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
