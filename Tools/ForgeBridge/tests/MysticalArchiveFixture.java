package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.util.ArrayList;

/** Arrange the opening position only. Preserve registered physical card IDs. */
public final class MysticalArchiveFixture {
    public static void main(String[] args) throws Exception {
        ForgeBridge.run(args, () -> {
            Player player = ForgeBridge.game.getPlayers().get(0);
            for (Card card : new ArrayList<>(player.getCardsIn(ZoneType.Hand))) {
                player.getZone(ZoneType.Hand).remove(card);
                player.getZone(ZoneType.Library).add(card);
            }
            String scenario = ForgeBridge.config.get("fixture").getAsString();
            boolean playLand = scenario.equals("draft") || scenario.equals("other-archive");
            Card archive = registeredCard(player, "The Mystical Archive",
                playLand ? ZoneType.Hand : ZoneType.Battlefield);
            Card original = registeredCard(player, "Mind Stone", ZoneType.Hand);
            Card copy = RoyalStateFixture.place("Mind Stone", player, ZoneType.Hand);
            if (!player.startedWith(archive) || !player.startedWith(original) || player.startedWith(copy))
                throw new AssertionError("Opening-deck physical identities");
            RoyalStateFixture.place("Wastes", player, ZoneType.Battlefield);
            if (playLand) {
                RoyalStateFixture.place("Wastes", player, ZoneType.Battlefield);
                if (scenario.equals("other-archive"))
                    RoyalStateFixture.place("The Mystical Archive", player, ZoneType.Battlefield);
            } else {
                RoyalStateFixture.place("Jalum Tome", player, ZoneType.Battlefield);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }

    private static Card registeredCard(Player player, String name, ZoneType destination) {
        Card card = player.getCardsIn(ZoneType.Library).stream()
            .filter(c -> c.getName().equals(name)).findFirst().orElseThrow();
        player.getZone(ZoneType.Library).remove(card);
        player.getZone(destination).add(card);
        card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());
        return card;
    }
}
