package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.util.*;

/** Test-only official board; native static effects and removal, no gameplay simulation. */
public final class LandDetailBoundaryFixture {
    static Card add(String name,Player p) {
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);
        p.getZone(ZoneType.Battlefield).add(c,0);
        c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    static void snapshot(String name,List<Card> lands) {
        ForgeBridge.game.getAction().checkStaticAbilities(false);
        ForgeBridge.send(Map.of("type","land-detail","state",name,"views",ForgeBridge.views(),
            "lands",lands.stream().map(c->Map.of("wire",ForgeBridge.card(c),"mana",c.getManaAbilities().stream().map(a->a.getManaPart().getOrigProduced()).toList())).toList()));
    }
    static void remove(Card c,Player p){p.getZone(ZoneType.Battlefield).remove(c);p.getZone(ZoneType.Graveyard).add(c,0);}
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player p=ForgeBridge.game.getPlayers().get(0);
            var lands=List.of(add("Godless Shrine",p),add("Forest",p),add("Shivan Reef",p),add("Wooded Foothills",p));
            snapshot("unchanged",lands);
            Card moon=add("Blood Moon",p);snapshot("moon",lands);
            remove(moon,p);snapshot("restored",lands);
            Card omen=add("Prismatic Omen",p);snapshot("omen",lands);
            remove(omen,p);snapshot("omen-restored",lands);
        });
    }
}
