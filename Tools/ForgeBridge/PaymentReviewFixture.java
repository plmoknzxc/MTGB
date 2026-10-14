package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Initial positions only. Land choices, costs and countering resolve in Forge. */
public final class PaymentReviewFixture {
    static void place(String name, Player owner, ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException(name);
        Card card=Card.fromPaperCard(paper,owner);owner.getZone(zone).add(card);
        card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());card.setSickness(false);
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
            String test=ForgeBridge.config.get("fixture").getAsString();
            if(test.startsWith("shock"))place("Hallowed Fountain",one,ZoneType.Hand);
            else {
                for(int i=0;i<(test.equals("tax-insufficient")?1:6);i++)place("Island",one,ZoneType.Battlefield);
                for(int i=0;i<3;i++)place("Island",two,ZoneType.Battlefield);
                place("Opt",one,ZoneType.Hand);place("Mana Leak",two,ZoneType.Hand);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
