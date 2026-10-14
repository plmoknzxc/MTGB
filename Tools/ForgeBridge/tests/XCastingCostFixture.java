package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Arrange only; X, payment, counterspell tax and resolution use native actions. */
public final class XCastingCostFixture {
    static void place(String name,Player player,ZoneType zone) {
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),player);
        player.getZone(zone).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
            String test=ForgeBridge.config.get("fixture").getAsString();
            for(int i=0;i<12;i++)place("Mountain",one,ZoneType.Battlefield);
            place(test.startsWith("xx")?"Walking Ballista":"Song of Totentanz",one,ZoneType.Hand);
            if(test.endsWith("reduce"))place("Helm of Awakening",one,ZoneType.Battlefield);
            if(test.endsWith("increase"))place("Sphere of Resistance",two,ZoneType.Battlefield);
            if(test.equals("counter-tax")) {
                place("Mana Leak",two,ZoneType.Hand);
                for(int i=0;i<4;i++)place("Island",two,ZoneType.Battlefield);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
