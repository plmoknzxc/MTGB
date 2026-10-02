package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

public final class CardSelectionFixture {
    static Card add(String name,Player p,ZoneType zone) {
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);
        p.getZone(zone).add(c,0);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player p=ForgeBridge.game.getPlayers().get(0),other=ForgeBridge.game.getPlayers().get(1);
            String spell=ForgeBridge.config.get("spell").getAsString();
            if(ForgeBridge.config.get("session").getAsString().endsWith("-dialog"))
                FModel.getPreferences().setPref(forge.localinstance.properties.ForgePreferences.FPref.UI_SELECT_FROM_CARD_DISPLAYS,false);
            add(spell,p,ForgeBridge.config.get("session").getAsString().startsWith("fetch")?ZoneType.Battlefield:ZoneType.Hand);
            for(String land:new String[]{"Island","Swamp","Forest","Plains","Mountain"})for(int i=0;i<3;i++)add(land,p,ZoneType.Battlefield);
            add("Forest",p,ZoneType.Library);add("Island",p,ZoneType.Library);add("Lightning Bolt",p,ZoneType.Library);
            if(ForgeBridge.config.get("session").getAsString().equals("explore-land"))add("Forest",p,ZoneType.Library);
            add("Grizzly Bears",p,ZoneType.Graveyard);add("Shock",p,ZoneType.Graveyard);
            add("Goblin Piker",p,ZoneType.Exile);add("Ornithopter",p,ZoneType.Exile);
            add("Duress",other,ZoneType.Hand);add("Negate",other,ZoneType.Hand);
        });
    }
}
