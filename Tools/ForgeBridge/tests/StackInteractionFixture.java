package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Official cards, setup only. All casts, targets, triggers and resolutions use Forge input. */
public final class StackInteractionFixture {
    static Card place(String name,Player player,ZoneType zone) {
        Card card=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),player);
        player.getZone(zone).add(card);card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());
        card.setSickness(false);return card;
    }
    public static void main(String[] args) throws Exception {
        ForgeBridge.run(args,()->{
            for(Player player:ForgeBridge.game.getPlayers()) {
                for(String name:new String[]{"Lightning Bolt","Counterspell","Negate","Stifle","Goblin Piker"})place(name,player,ZoneType.Hand);
                for(int i=0;i<8;i++)place("Island",player,ZoneType.Battlefield);
                for(int i=0;i<4;i++)place("Mountain",player,ZoneType.Battlefield);
            }
            if(ForgeBridge.config.get("fixture").getAsString().equals("triggers")) {
                Player player=ForgeBridge.game.getPlayers().get(0);
                place("Soul Warden",player,ZoneType.Battlefield);
                place("Soul's Attendant",player,ZoneType.Battlefield);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
