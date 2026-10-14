package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Initial positions only; native naming effects, permissions and discards do the work. */
public final class CardNameFixture {
    static void place(String name,Player p,ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException(name);
        Card c=Card.fromPaperCard(paper,p);p.getZone(zone).add(c);
        c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
            String test=ForgeBridge.config.get("fixture").getAsString();
            for(String basic:new String[]{"Swamp","Island","Plains","Mountain"})for(int i=0;i<4;i++)place(basic,one,ZoneType.Battlefield);
            place("Island",two,ZoneType.Battlefield);place("Mountain",two,ZoneType.Battlefield);
            if(test.startsWith("therapy")) {
                place("Cabal Therapy",one,ZoneType.Hand);
                if(test.equals("therapy-split")) {place("Fire // Ice",two,ZoneType.Hand);place("Fire // Ice",two,ZoneType.Hand);}
                else if(test.equals("therapy-cancel"))place("Cancel",two,ZoneType.Hand);
                else {place("Shock",two,ZoneType.Hand);place("Shock",two,ZoneType.Hand);}
                place("Opt",two,ZoneType.Hand);
            }else if(test.startsWith("needle")) {
                place("Pithing Needle",one,ZoneType.Hand);place("Mogg Fanatic",test.endsWith("-only")||test.endsWith("-other")?two:one,ZoneType.Battlefield);
                if(test.endsWith("-other"))place("Opt",two,ZoneType.Hand);
            }else if(test.startsWith("mage")||test.startsWith("arbiter")) {
                place(test.startsWith("mage")?"Meddling Mage":"Alhammarret, High Arbiter",one,ZoneType.Hand);
                place("Unsummon",one,ZoneType.Hand);place("Shock",one,ZoneType.Hand);place("Opt",one,ZoneType.Hand);
                if(!test.equals("arbiter-empty")){place("Shock",two,ZoneType.Hand);place("Shock",two,ZoneType.Hand);if(!test.endsWith("-only"))place("Opt",two,ZoneType.Hand);}
            }else throw new IllegalArgumentException(test);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
