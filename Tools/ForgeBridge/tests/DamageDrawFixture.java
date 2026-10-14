package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.util.*;

/** Setup only; all damage, draws, replacement choices and shield use are native. */
public final class DamageDrawFixture {
    static Card place(String name,Player p,ZoneType z){return ReplacementFixture.place(name,p,z);}
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            String test=ForgeBridge.config.get("fixture").getAsString();int seat=ForgeBridge.config.get("firstPlayerSeat").getAsInt();
            Player actor=ForgeBridge.game.getPlayers().get(seat-1),other=ForgeBridge.game.getPlayers().get(2-seat);
            for(int i=0;i<6;i++)place(test.startsWith("draw-")?"Island":"Mountain",actor,ZoneType.Battlefield);
            if(test.startsWith("damage-")) {
                if(test.equals("damage-split-repeat")) {
                    place("Plains",actor,ZoneType.Battlefield);place("Pyrohemia",actor,ZoneType.Battlefield);place("Harm's Way",actor,ZoneType.Hand);
                } else {
                    place("Lightning Bolt",actor,ZoneType.Hand);
                    if(test.contains("optional"))place("Battletide Alchemist",actor,ZoneType.Battlefield);
                    else if(test.equals("damage-only-prevent"))place("Urza's Armor",other,ZoneType.Battlefield);
                    else {
                        place("Furnace of Rath",actor,ZoneType.Battlefield);
                        if(test.contains("prevent"))place("Urza's Armor",other,ZoneType.Battlefield);
                        else place("Torbran, Thane of Red Fell",actor,ZoneType.Battlefield);
                    }
                    if(test.equals("damage-cant-prevent"))place("Leyline of Punishment",actor,ZoneType.Battlefield);
                    if(test.equals("damage-creature-survive"))place("Ancient Brontodon",other,ZoneType.Battlefield);
                }
            } else if(test.startsWith("draw-")) {
                place(test.equals("draw-limit")?"Divination":"Reach Through Mists",actor,ZoneType.Hand);
                if(test.equals("draw-double")){place("Thought Reflection",actor,ZoneType.Battlefield);place("Alhammarret's Archive",actor,ZoneType.Battlefield);}
                else if(test.contains("thief")||test.contains("token")){place("Notion Thief",other,ZoneType.Battlefield);place("Hullbreacher",other,ZoneType.Battlefield);}
                else if(test.contains("underrealm"))place("Underrealm Lich",actor,ZoneType.Battlefield);
                else if(test.contains("dredge"))place("Stinkweed Imp",actor,ZoneType.Graveyard);
                else if(test.contains("optional"))place("Obstinate Familiar",actor,ZoneType.Battlefield);
                else if(test.contains("limit")){
                    place("Spirit of the Labyrinth",actor,ZoneType.Battlefield);
                    if(test.equals("draw-limit-after-first"))place("Reach Through Mists",actor,ZoneType.Hand);
                } else throw new IllegalArgumentException(test);
                int remaining=test.equals("draw-underrealm-short")?2:test.equals("draw-dredge-short")?4:53;
                var library=new ArrayList<Card>();for(Card c:actor.getCardsIn(ZoneType.Library))library.add(c);
                for(int i=remaining;i<library.size();i++){Card c=library.get(i);actor.getZone(ZoneType.Library).remove(c);actor.getZone(ZoneType.Exile).add(c);}
            } else throw new IllegalArgumentException(test);
            ForgeBridge.game.getAction().checkStaticAbilities();LibraryCostFixture.observe();
        });
    }
}
