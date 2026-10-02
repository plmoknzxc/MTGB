package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
public final class InteractionFixture {
    static Card add(String name,Player p,ZoneType zone) {
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);
        p.getZone(zone).add(c,0);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player p=ForgeBridge.game.getPlayers().get(0);
            String scenario=ForgeBridge.config.get("session").getAsString();
            add("Ornithopter",p,ZoneType.Battlefield);add("Welding Jar",p,ZoneType.Battlefield);
            if(scenario.equals("mana"))add("Mox Opal",p,ZoneType.Battlefield);
            if(scenario.startsWith("evoke")){add("Endurance",p,ZoneType.Hand);add("Grizzly Bears",p,ZoneType.Hand);}
            if(scenario.equals("evoke-menu"))for(int i=0;i<3;i++)add("Forest",p,ZoneType.Battlefield);
            if(scenario.startsWith("trigger")){add("Skateboard",p,ZoneType.Hand);add("Mountain",p,ZoneType.Battlefield);}
        });
    }
}
