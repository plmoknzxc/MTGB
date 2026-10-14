package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Setup only: real reduction decisions, costs and resolution use the production bridge. */
public final class TapPaymentFixture {
    static Card add(String name,Player player,ZoneType zone) {
        Card card=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),player);
        player.getZone(zone).add(card);card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());card.setSickness(false);return card;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()-> {
            Player player=ForgeBridge.game.getPlayers().get(0);
            String test=ForgeBridge.config.get("fixture").getAsString();
            if(test.startsWith("waterbend")) {
                add("Ruthless Waterbender",player,ZoneType.Battlefield);
                add("Tormod's Crypt",player,ZoneType.Battlefield);
                add("Grizzly Bears",player,ZoneType.Battlefield);
            } else {
                boolean improvise=test.startsWith("improvise");
                add(improvise?"Herald of Anguish":"Venerated Loxodon",player,ZoneType.Hand);
                for(int i=0;i<5;i++)add(improvise?"Tormod's Crypt":"Soul Warden",player,ZoneType.Battlefield);
            }
            // Enough ordinary mana also permits the zero-reduction and cancelled branches.
            int lands=test.endsWith("only")?(test.startsWith("improvise")?2:0):8;
            for(int i=0;i<lands;i++)add(test.startsWith("improvise")?"Swamp":"Plains",player,ZoneType.Battlefield);
        });
    }
}
