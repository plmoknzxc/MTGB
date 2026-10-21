package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Fixed positions only. Priority, targeting, costs and restrictions use the native game loop. */
public final class ActionLegalityFixture {
    static Card place(String name,Player p,ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException(name);
        Card c=Card.fromPaperCard(paper,p);p.getZone(zone).add(c);
        c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
            String test=ForgeBridge.config.get("fixture").getAsString();
            for(Player p:new Player[]{one,two})for(String basic:new String[]{"Swamp","Island","Plains","Mountain","Forest"})
                for(int i=0;i<3;i++)place(basic,p,ZoneType.Battlefield);
            place("Opt",one,ZoneType.Hand);
            place("Relic of Progenitus",one,ZoneType.Battlefield);
            place("Requiting Hex",two,ZoneType.Hand);place("Get Lost",two,ZoneType.Hand);
            place("Annul",two,ZoneType.Hand); // Nothing in the initial stack is an artifact/enchantment.
            place("Street Wraith",two,ZoneType.Hand);place("Boseiju, Who Endures",two,ZoneType.Hand);
            place("Otawara, Soaring City",two,ZoneType.Hand);
            place("Mogg Fanatic",two,ZoneType.Battlefield);place("Llanowar Elves",two,ZoneType.Battlefield);
            if(test.equals("voice"))place("Voice of Victory",one,ZoneType.Battlefield);
            else if(test.equals("totem"))place("Cursed Totem",one,ZoneType.Battlefield);
            else if(test.equals("teferi"))place("Teferi, Time Raveler",one,ZoneType.Battlefield).setCounters(forge.game.card.CounterType.getType("LOYALTY"),4);
            else if(test.equals("law")){
                place("Rule of Law",one,ZoneType.Battlefield);
                place("Brainstorm",one,ZoneType.Hand);place("Street Wraith",one,ZoneType.Hand);
            }else if(test.equals("cage")){
                place("Grafdigger's Cage",one,ZoneType.Battlefield);
                place("Think Twice",one,ZoneType.Graveyard);place("Bolas's Citadel",one,ZoneType.Battlefield);
                Card top=place("Opt",one,ZoneType.Library);one.getZone(ZoneType.Library).remove(top);one.getZone(ZoneType.Library).add(top,0);
            }
            else if(!test.equals("control"))throw new IllegalArgumentException(test);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
