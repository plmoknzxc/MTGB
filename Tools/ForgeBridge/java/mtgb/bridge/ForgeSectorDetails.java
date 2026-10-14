package mtgb.bridge;

import forge.game.card.Card;
import forge.game.zone.ZoneType;
import java.util.Map;

/** Public sector membership is independent of a card's face or printed identity. */
final class ForgeSectorDetails {
    private static boolean known(String value) {
        return "Alpha".equals(value) || "Beta".equals(value) || "Gamma".equals(value);
    }

    static void add(Card card, Map<String, Object> result) {
        if (card.isInZone(ZoneType.Battlefield) && known(card.getSector()))
            result.put("sector", card.getSector());
        // A resolving ability can retain its publicly known source LKI after it leaves the battlefield.
        if (!card.isFaceDown() && known(card.getChosenSector()))
            result.put("chosenSector", card.getChosenSector());
    }
}
