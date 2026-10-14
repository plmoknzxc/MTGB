package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Arrange a position only. Casting, mana production/payment and resolution are native. */
public final class ManaVariantFixture {
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
            Player player = ForgeBridge.game.getPlayers().get(ForgeBridge.config.get("firstPlayerSeat").getAsInt() - 1);
            String test = ForgeBridge.config.get("fixture").getAsString();
            if (test.startsWith("phyrexian")) {
                place(test.equals("phyrexian-black") ? "Grave Titan" : "Moltensteel Dragon", player, ZoneType.Hand);
                if(test.equals("phyrexian-black")) place("K'rrik, Son of Yawgmoth", player, ZoneType.Battlefield);
                if (test.contains("prohibited")) place("Angel of Jubilation", player, ZoneType.Battlefield);
                int red = test.equals("phyrexian-mana") || test.equals("phyrexian-prohibited-mana") ? 6 : test.equals("phyrexian-mixed") ? 1 : 0;
                for (int i=0; i<red; i++) place("Mountain", player, ZoneType.Battlefield);
                if (red<6) for (int i=0; i<4; i++) place("Island", player, ZoneType.Battlefield);
            } else if (test.startsWith("snow")) {
                place("Arcum's Astrolabe", player, ZoneType.Hand);
                place("Forest", player, ZoneType.Battlefield);
                if (test.equals("snow-paid")) place("Snow-Covered Forest", player, ZoneType.Battlefield);
            } else if (test.startsWith("hybrid")) {
                place("Burning-Tree Emissary", player, ZoneType.Hand);
                String[] lands = test.equals("hybrid-green") ? new String[]{"Forest", "Forest"}
                    : test.equals("hybrid-red") ? new String[]{"Mountain", "Mountain"}
                    : test.equals("hybrid-insufficient") ? new String[]{"Forest"}
                    : test.equals("hybrid-retry") ? new String[]{"Island", "Forest", "Mountain"}
                    : new String[]{"Forest", "Mountain"};
                for (String land : lands) place(land, player, ZoneType.Battlefield);
            } else {
                place("Spectral Procession", player, ZoneType.Hand);
                int white = test.equals("twobrid-white") ? 3 : test.equals("twobrid-generic") ? 0 : 1;
                int generic = test.equals("twobrid-white") ? 0 : test.equals("twobrid-generic") ? 6
                    : test.equals("twobrid-insufficient") ? 3 : 4;
                for (int i = 0; i < white; i++) place("Plains", player, ZoneType.Battlefield);
                for (int i = 0; i < generic; i++) place("Island", player, ZoneType.Battlefield);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
