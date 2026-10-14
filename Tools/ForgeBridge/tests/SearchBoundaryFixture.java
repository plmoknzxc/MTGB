package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.util.ArrayList;

/** Opening setup only. All searches, moves, costs and shuffles are native. */
public final class SearchBoundaryFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int i=0;i<2;i++) {
                Player p=ForgeBridge.game.getPlayers().get(i);
                for(Card c:new ArrayList<>(p.getCardsIn(ZoneType.Hand))) {
                    p.getZone(ZoneType.Hand).remove(c);p.getZone(ZoneType.Library).add(c);
                }
                for(String zone:new String[]{"hand","board","grave"})
                    for(var name:ForgeBridge.config.getAsJsonArray(zone+(i+1)))
                        LibraryCostFixture.place(name.getAsString(),p,zone.equals("hand")?ZoneType.Hand:zone.equals("board")?ZoneType.Battlefield:ZoneType.Graveyard);
                if(ForgeBridge.config.get("emptyLibrary").getAsBoolean()&&i==0)
                    for(Card c:new ArrayList<>(p.getCardsIn(ZoneType.Library))) {
                        p.getZone(ZoneType.Library).remove(c);p.getZone(ZoneType.Exile).add(c);
                    }
            }
            ForgeBridge.game.getAction().checkStaticAbilities();LibraryCostFixture.observe();
        });
    }
}
