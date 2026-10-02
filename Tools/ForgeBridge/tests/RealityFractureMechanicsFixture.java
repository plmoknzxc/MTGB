package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Only sets up positions. All tested actions run through the production bridge. */
public final class RealityFractureMechanicsFixture {
    static Card place(String name, Player player, ZoneType zone) {
        Card card = Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name), player);
        player.getZone(zone).add(card);
        card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());
        card.setSickness(false);
        return card;
    }
    public static void main(String[] args) throws Exception {
        ForgeBridge.run(args, () -> {
            Player one = ForgeBridge.game.getPlayers().get(0);
            for (String land : new String[]{"Plains","Island","Swamp","Mountain","Forest"})
                for (int i=0;i<10;i++) place(land,one,ZoneType.Battlefield);
            String test=ForgeBridge.config.get("fixture").getAsString();
            String card=ForgeBridge.config.get("testCard").getAsString();
            if(test.equals("comparison")) {
                place("Sol Ring",one,ZoneType.Battlefield);
                place("Sol Ring",one,ZoneType.Battlefield);
                place("Sol Ring",one,ZoneType.Battlefield);
                place("Glorious Anthem",one,ZoneType.Battlefield);
                place("Glorious Anthem",one,ZoneType.Battlefield);
                place("Xenagos, God of Revels",one,ZoneType.Battlefield);
                place("Grizzly Bears",one,ZoneType.Battlefield);
                place("Thassa, God of the Sea",one,ZoneType.Battlefield);
                place("Garruk Wildspeaker",one,ZoneType.Battlefield).setCounters(forge.game.card.CounterType.getType("LOYALTY"),3);
                Card foodSource=place("Gilded Goose",one,ZoneType.Graveyard);
                Card jaceSource=place("Way of the Pyromancer",one,ZoneType.Graveyard);
                for(String script:new String[]{"c_a_food_sac","u_empower"}) {
                    Card source=script.equals("u_empower")?jaceSource:foodSource;
                    Card token=forge.game.card.token.TokenInfo.getProtoType(script,source.getFirstSpellAbility(),one,false);
                    token.setTokenSpawningAbility(source.getFirstSpellAbility());
                    if(script.equals("u_empower")){token.addType("Jace");token.setName("Jace Token");token.setCounters(forge.game.card.CounterType.getType("LOYALTY"),2);}
                    one.getZone(ZoneType.Battlefield).add(token);token.setGameTimestamp(ForgeBridge.game.getNextTimestamp());
                }
            }
            place(card,one,test.equals("flashback") || test.equals("grave-empower") ? ZoneType.Graveyard : ZoneType.Hand);
            if(test.equals("behold-hand"))place("Jace Beleren",one,ZoneType.Hand);
            if(test.equals("behold-board"))place("Jace Beleren",one,ZoneType.Battlefield).setCounters(forge.game.card.CounterType.getType("LOYALTY"),3);
            if(test.equals("flashback") || test.equals("threshold"))
                for(int i=0;i<(test.equals("threshold")?7:6);i++)place("Forest",one,ZoneType.Graveyard);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
