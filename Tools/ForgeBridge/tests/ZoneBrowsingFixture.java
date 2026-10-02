package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

public final class ZoneBrowsingFixture {
    static Card place(String name,Player p,ZoneType zone) {
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);
        p.getZone(zone).add(c);return c;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(Player p:ForgeBridge.game.getPlayers()) {
                place("Grizzly Bears",p,ZoneType.Graveyard);
                place("Grizzly Bears",p,ZoneType.Graveyard);
                place("Lightning Bolt",p,ZoneType.Graveyard);
                place("Island",p,ZoneType.Exile);
                Card hidden=place("Runeclaw Bear",p,ZoneType.Exile);
                hidden.turnFaceDown(true);
            }
        });
    }
}
