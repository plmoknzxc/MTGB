package mtgb.bridge;
import forge.game.card.*;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.util.*;
/** Opening only. Existing observer reads actual rolls/libraries, never sets results. */
public final class DiceCombinationFixture {
 public static void main(String[] args)throws Exception {
  ForgeBridge.run(args,()->{
   Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
   for(Player p:List.of(one,two))for(Card c:new ArrayList<Card>(p.getCardsIn(ZoneType.Hand))){p.getZone(ZoneType.Hand).remove(c);p.getZone(ZoneType.Library).add(c);}
   for(String land:List.of("Island","Swamp"))for(int i=0;i<8;i++)LibraryCostFixture.place(land,one,ZoneType.Battlefield);
   String test=ForgeBridge.config.get("fixture").getAsString();
   if(test.equals("fixed-negative")) {
    LibraryCostFixture.place("The Deck of Many Things",one,ZoneType.Battlefield);
    LibraryCostFixture.place("Shock",one,ZoneType.Hand);LibraryCostFixture.place("Opt",one,ZoneType.Hand);
   }else if(test.equals("multiple-modifiers")) {
    LibraryCostFixture.place("Contact Other Plane",one,ZoneType.Hand);
    Card x=LibraryCostFixture.place("Xenosquirrels",one,ZoneType.Battlefield);x.setCounters(CounterType.getType("P1P1"),2);
    LibraryCostFixture.place("Night Shift of the Living Dead",one,ZoneType.Battlefield);
   }else {
    LibraryCostFixture.place("Grave Endeavor",one,ZoneType.Hand);
    LibraryCostFixture.place("Grizzly Bears",one,ZoneType.Graveyard);
    if(test.equals("reroll-both"))LibraryCostFixture.place("Monitor Monitor",one,ZoneType.Battlefield);
   }
   ForgeBridge.game.getAction().checkStaticAbilities();RandomMechanicFixture.observe();
  });
 }
}
