package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.util.ArrayList;

/** Initial positions only; land entry, choices, production and spells use Forge. */
public final class RestrictedManaFixture {
    static void place(String name, Player player, ZoneType zone) {
        var paper = FModel.getMagicDb().getCommonCards().getCard(name);
        if (paper == null) throw new IllegalArgumentException(name);
        Card card = Card.fromPaperCard(paper, player);
        player.getZone(zone).add(card);
        card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());
        card.setSickness(false);
    }
    public static void main(String[] args) throws Exception {
        ForgeBridge.run(args, () -> {
            Player one = ForgeBridge.game.getPlayers().get(0);
            for (Card card : new ArrayList<>(one.getCardsIn(ZoneType.Hand))) {
                one.getZone(ZoneType.Hand).remove(card);
                one.getZone(ZoneType.Library).add(card);
            }
            for (var name : ForgeBridge.config.getAsJsonArray("hand"))
                place(name.getAsString(), one, ZoneType.Hand);
            for (var name : ForgeBridge.config.getAsJsonArray("lands"))
                place(name.getAsString(), one, ZoneType.Battlefield);
            if (ForgeBridge.config.has("graveyard"))
                for (var name : ForgeBridge.config.getAsJsonArray("graveyard"))
                    place(name.getAsString(), one, ZoneType.Graveyard);
            if (ForgeBridge.config.has("opponentHand")) {
                Player two = ForgeBridge.game.getPlayers().get(1);
                for (Card card : new ArrayList<>(two.getCardsIn(ZoneType.Hand))) {
                    two.getZone(ZoneType.Hand).remove(card);
                    two.getZone(ZoneType.Library).add(card);
                }
                for (var name : ForgeBridge.config.getAsJsonArray("opponentHand"))
                    place(name.getAsString(), two, ZoneType.Hand);
                for (var name : ForgeBridge.config.getAsJsonArray("opponentBoard"))
                    place(name.getAsString(), two, ZoneType.Battlefield);
            }
            if (ForgeBridge.config.has("counter") && ForgeBridge.config.get("counter").getAsBoolean()) {
                Player two = ForgeBridge.game.getPlayers().get(1);
                place("Counterspell", two, ZoneType.Hand);
                place("Island", two, ZoneType.Battlefield);
                place("Island", two, ZoneType.Battlefield);
            }
            if (ForgeBridge.config.get("fixture").getAsString().equals("replacement-black"))
                place("Contamination", ForgeBridge.game.getPlayers().get(1), ZoneType.Battlefield);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
