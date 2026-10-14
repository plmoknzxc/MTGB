package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Opening arrangement only; mana generation, payment/refund and effects are native. */
public final class SnowPoolFixture {
    static void place(String name,Player player,ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException(name);
        Card card=Card.fromPaperCard(paper,player);player.getZone(zone).add(card);
        card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());card.setSickness(false);
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player player=ForgeBridge.game.getPlayers().get(ForgeBridge.config.get("firstPlayerSeat").getAsInt()-1);
            String test=ForgeBridge.config.get("fixture").getAsString();
            place(test.endsWith("colored")?"Ice-Fang Coatl":"Arcum's Astrolabe",player,ZoneType.Hand);
            for(String name:new String[]{"Plains","Island","Swamp","Mountain","Forest","Wastes",
                "Snow-Covered Plains","Snow-Covered Island","Snow-Covered Swamp","Snow-Covered Mountain","Snow-Covered Forest","Scrying Sheets"})
                place(name,player,ZoneType.Battlefield);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
