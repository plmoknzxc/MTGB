package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.util.ArrayList;

/** Initial zones/order only, followed by read-only native library observations. */
public final class LibraryOrderFixture {
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
                if(i==0) {
                    if(ForgeBridge.config.get("replaceLibrary").getAsBoolean())
                        for(Card c:new ArrayList<>(p.getCardsIn(ZoneType.Library))) {
                            p.getZone(ZoneType.Library).remove(c);p.getZone(ZoneType.Exile).add(c);
                        }
                    var top=new ArrayList<Card>();
                    for(var name:ForgeBridge.config.getAsJsonArray("top"))
                        top.add(LibraryCostFixture.place(name.getAsString(),p,ZoneType.Library));
                    var existing=new ArrayList<>(p.getCardsIn(ZoneType.Library));existing.removeAll(top);
                    top.addAll(existing);p.getZone(ZoneType.Library).setCards(top);
                }
            }
            ForgeBridge.game.getAction().checkStaticAbilities();LibraryCostFixture.observe();
        });
    }
}
