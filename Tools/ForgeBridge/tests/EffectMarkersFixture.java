package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
public final class EffectMarkersFixture {
    static Card place(String name,Player p,ZoneType zone) {
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);
        p.getZone(zone).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(Player p:ForgeBridge.game.getPlayers()) {
                place("Grizzly Bears",p,ZoneType.Battlefield);
                place("Plains",p,ZoneType.Battlefield);
                place("Ephemerate",p,ZoneType.Hand);
            }
        });
    }
}
