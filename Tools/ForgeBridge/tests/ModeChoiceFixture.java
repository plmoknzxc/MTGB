package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Setup only: cast, choices, costs, targets and resolution run in Forge. */
public final class ModeChoiceFixture {
    static void add(String name,Player p,ZoneType zone) {
        Card card=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);
        p.getZone(zone).add(card);card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());card.setSickness(false);
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
            String test=ForgeBridge.config.get("session").getAsString();
            if(test.startsWith("offspring")) {
                add("Tender Wildguide",one,ZoneType.Hand);
                for(int i=0;i<4;i++)add("Forest",one,ZoneType.Battlefield);
            } else if(test.startsWith("entwine")) {
                add("One Dozen Eyes",one,ZoneType.Hand);
                for(int i=0;i<9;i++)add("Forest",one,ZoneType.Battlefield);
            } else if(test.startsWith("replicate")) {
                add("Shattering Spree",one,ZoneType.Hand);
                add("Mountain",one,ZoneType.Battlefield);add("Mountain",one,ZoneType.Battlefield);
                add("Tormod's Crypt",two,ZoneType.Battlefield);add("Sol Ring",two,ZoneType.Battlefield);
            } else if(test.startsWith("evoke")) {
                add("Mulldrifter",one,ZoneType.Hand);
                for(int i=0;i<(test.equals("evoke-limited")?3:5);i++)add("Island",one,ZoneType.Battlefield);
            } else if(test.startsWith("kicker")) {
                add("Burst Lightning",one,ZoneType.Hand);
                for(int i=0;i<5;i++)add("Mountain",one,ZoneType.Battlefield);
            } else if(test.startsWith("choose-one")) {
                add("Boros Charm",one,ZoneType.Hand);add("Plains",one,ZoneType.Battlefield);add("Mountain",one,ZoneType.Battlefield);add("Grizzly Bears",one,ZoneType.Battlefield);
            } else {
                add("Requisition Raid",one,ZoneType.Hand);add("Grizzly Bears",one,ZoneType.Battlefield);
                int lands=test.equals("spree-rich")?4:test.equals("spree-unpayable")?1:2;
                for(int i=0;i<lands;i++)add("Plains",one,ZoneType.Battlefield);
                if(!test.equals("spree-targets")){add("Tormod's Crypt",two,ZoneType.Battlefield);add("Glorious Anthem",two,ZoneType.Battlefield);}
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
