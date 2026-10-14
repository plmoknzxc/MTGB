package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.util.*;

/** Initial permanents and spells only; no forced replacements or results. */
public final class ReplacementFixture {
    static Card place(String name, Player owner, ZoneType zone) {
        Card c=Card.fromPaperCard(Objects.requireNonNull(FModel.getMagicDb().getCommonCards().getCard(name)),owner);
        owner.getZone(zone).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            String test=ForgeBridge.config.get("fixture").getAsString();
            int seat=ForgeBridge.config.get("firstPlayerSeat").getAsInt();
            Player actor=ForgeBridge.game.getPlayers().get(seat-1), other=ForgeBridge.game.getPlayers().get(2-seat);
            for(int i=0;i<6;i++)place(test.startsWith("zone-")?"Mountain":test.equals("token-other-player")?"Plains":"Forest",actor,ZoneType.Battlefield);
            if(test.startsWith("counter-")) {
                Player affected=test.equals("counter-other-player")?other:actor;
                place("Grizzly Bears",affected,ZoneType.Battlefield);
                if(!test.equals("counter-none")&&!test.equals("counter-zero-target")) {
                    place("Hardened Scales",affected,ZoneType.Battlefield);place("Doubling Season",affected,ZoneType.Battlefield);
                }
                place("Travel Preparations",actor,ZoneType.Hand);
                if(test.equals("counter-repeat"))place("Travel Preparations",actor,ZoneType.Hand);
            } else if(test.equals("token-double")) {
                place("Doubling Season",actor,ZoneType.Battlefield);place("Parallel Lives",actor,ZoneType.Battlefield);
                place("Raise the Alarm",actor,ZoneType.Hand);
                // A white source is required by the real spell cost.
                place("Plains",actor,ZoneType.Battlefield);
            } else if(test.equals("token-manufactor")) {
                place("Academy Manufactor",actor,ZoneType.Battlefield);place("Doubling Season",actor,ZoneType.Battlefield);
                place("Gilded Goose",actor,ZoneType.Hand);
            } else if(test.equals("token-other-player")) {
                place("Doubling Season",actor,ZoneType.Battlefield);place("Thran Spider",actor,ZoneType.Hand);
            } else if(test.startsWith("zone-")) {
                place("Rest in Peace",actor,ZoneType.Battlefield);place("Leyline of the Void",actor,ZoneType.Battlefield);
                place("Grizzly Bears",other,ZoneType.Battlefield);place("Lightning Bolt",actor,ZoneType.Hand);
            } else throw new IllegalArgumentException(test);
            ForgeBridge.game.getAction().checkStaticAbilities();LibraryCostFixture.observe();
        });
    }
}
