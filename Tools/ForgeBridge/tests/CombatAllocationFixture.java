package mtgb.bridge;

import forge.game.card.*;
import forge.game.player.Player;
import forge.game.zone.ZoneType;

/** Setup only. Native actions perform spells, declarations and combat damage. */
public final class CombatAllocationFixture {
    static Card place(String n,Player p,ZoneType z){return LibraryCostFixture.place(n,p,z);}
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            String test=ForgeBridge.config.get("fixture").getAsString();int seat=ForgeBridge.config.get("firstPlayerSeat").getAsInt();
            Player actor=ForgeBridge.game.getPlayers().get(seat-1),other=ForgeBridge.game.getPlayers().get(2-seat);
            if(!test.equals("menace"))for(int i=0;i<4;i++){place("Forest",actor,ZoneType.Battlefield);place("Mountain",actor,ZoneType.Battlefield);}
            String attacker=test.equals("many-blockers")?"Gigantosaurus":test.startsWith("deathtouch")?"Vampire Nighthawk":test.equals("first-strike")?"White Knight":test.startsWith("double-strike")?"Fencing Ace":test.equals("menace")?"Boggart Brute":test.startsWith("as-unblocked")?"Thorn Elemental":test.equals("block-multiple")?"Grizzly Bears":"Colossal Dreadmaw";
            place(attacker,actor,ZoneType.Battlefield);
            if(test.equals("block-multiple")){
                place("Grizzly Bears",actor,ZoneType.Battlefield);place("Two-Headed Giant of Foriys",other,ZoneType.Battlefield);
            }else if(test.equals("many-blockers")){
                for(int i=0;i<10;i++)place("Grizzly Bears",other,ZoneType.Battlefield);
            }else if(!test.equals("double-strike-open")){
                place(test.startsWith("deathtouch")||test.equals("damaged-blocker")?"Wall of Air":"Grizzly Bears",other,ZoneType.Battlefield);
                if(!test.equals("planeswalker"))place(test.startsWith("deathtouch")?"Wall of Air":"Grizzly Bears",other,ZoneType.Battlefield);
            }
            if(test.equals("planeswalker")){Card pw=place("Gideon Jura",other,ZoneType.Battlefield);pw.setCounters(CounterType.getType("LOYALTY"),4);}
            if(test.equals("many-blockers")||test.equals("deathtouch-trample")||test.equals("first-strike")||test.startsWith("double-strike"))place("Rancor",actor,ZoneType.Hand);
            if(test.equals("damaged-blocker"))place("Shock",actor,ZoneType.Hand);
            ForgeBridge.game.getAction().checkStaticAbilities();LibraryCostFixture.observe();
        });
    }
}
