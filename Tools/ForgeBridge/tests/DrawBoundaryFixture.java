package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.util.ArrayList;

/** Opening positions only; native turn/draw/spells/damage determine all results. */
public final class DrawBoundaryFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int i=0;i<2;i++) {
                Player p=ForgeBridge.game.getPlayers().get(i);
                for(Card c:new ArrayList<>(p.getCardsIn(ZoneType.Hand))) {
                    p.getZone(ZoneType.Hand).remove(c);p.getZone(ZoneType.Library).add(c);
                }
                for(var name:ForgeBridge.config.getAsJsonArray("hand"+(i+1)))LibraryCostFixture.place(name.getAsString(),p,ZoneType.Hand);
                for(var name:ForgeBridge.config.getAsJsonArray("board"+(i+1)))LibraryCostFixture.place(name.getAsString(),p,ZoneType.Battlefield);
                int limit=ForgeBridge.config.getAsJsonArray("librarySizes").get(i).getAsInt();
                var library=new ArrayList<>(p.getCardsIn(ZoneType.Library));
                for(int j=limit;j<library.size();j++) {
                    Card c=library.get(j);p.getZone(ZoneType.Library).remove(c);p.getZone(ZoneType.Exile).add(c);
                }
            }
            ForgeBridge.game.getAction().checkStaticAbilities();LibraryCostFixture.observe();
        });
    }
}
