package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

public final class ScryFixture {
    static Card add(String name, Player player, ZoneType zone, boolean top) {
        Card card=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),player);
        if(top)player.getZone(zone).add(card,0); else player.getZone(zone).add(card);
        card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());card.setSickness(false);return card;
    }
    public static void main(String[] args) throws Exception {
        ForgeBridge.run(args,()->{
            if(ForgeBridge.config.has("forceFailure"))throw new IllegalStateException("private diagnostic fixture");
            Player player=ForgeBridge.game.getPlayers().get(0);
            add("Preordain",player,ZoneType.Hand,false);
            add("Island",player,ZoneType.Battlefield,false);
            add("Forest",player,ZoneType.Library,true);
            add("Lightning Bolt",player,ZoneType.Library,true);
            for(Player seat:ForgeBridge.game.getPlayers()) {
                add("Shock",seat,ZoneType.Hand,false);
                add("Mountain",seat,ZoneType.Battlefield,false);
            }
        });
    }
}
