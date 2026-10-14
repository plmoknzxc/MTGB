package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Arrange only. All casting choices, costs, payments and resolution are native. */
public final class PhyrexianModifierFixture {
    static Card place(String name, Player p, ZoneType zone) {
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);
        p.getZone(zone).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player p=ForgeBridge.game.getPlayers().get(ForgeBridge.config.get("firstPlayerSeat").getAsInt()-1);
            Player enemy=ForgeBridge.game.getPlayers().get(p==ForgeBridge.game.getPlayers().get(0)?1:0);
            place(ForgeBridge.config.get("spell").getAsString(),p,ZoneType.Hand);
            place("Grizzly Bears",enemy,ZoneType.Battlefield);
            if(ForgeBridge.config.has("krrik")&&ForgeBridge.config.get("krrik").getAsBoolean())place("K'rrik, Son of Yawgmoth",p,ZoneType.Battlefield);
            for(var modifier:ForgeBridge.config.getAsJsonArray("modifiers")) {
                Card c=place(modifier.getAsString(),enemy,ZoneType.Battlefield);
                if(c.getName().equals("Trinisphere")&&ForgeBridge.config.get("triTapped").getAsBoolean())c.setTapped(true);
            }
            for(int i=0;i<ForgeBridge.config.get("swamps").getAsInt();i++)place("Swamp",p,ZoneType.Battlefield);
            for(int i=0;i<ForgeBridge.config.get("islands").getAsInt();i++)place("Island",p,ZoneType.Battlefield);
            for(int i=0;i<(ForgeBridge.config.has("mountains")?ForgeBridge.config.get("mountains").getAsInt():0);i++)place("Mountain",p,ZoneType.Battlefield);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
