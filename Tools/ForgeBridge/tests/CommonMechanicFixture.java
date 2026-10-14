package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Setup only: qualifications, dungeon progress and layers must be earned by real actions. */
public final class CommonMechanicFixture {
    static void place(String name, Player player, ZoneType zone) {
        var paper = FModel.getMagicDb().getCommonCards().getCard(name);
        if (paper == null) throw new IllegalArgumentException("Missing fixture card: " + name);
        Card card = Card.fromPaperCard(paper, player);
        player.getZone(zone).add(card);
        card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());
        card.setSickness(false);
    }
    public static void main(String[] args) throws Exception {
        ForgeBridge.run(args, () -> {
            Player one = ForgeBridge.game.getPlayers().get(0), two = ForgeBridge.game.getPlayers().get(1);
            String test = ForgeBridge.config.get("fixture").getAsString();
            int lands = test.equals("ascend") ? 8 : test.equals("ascend-source") ? 10 : 25;
            String[] basics = {"Plains", "Swamp", "Mountain", "Island", "Forest"};
            for (int i = 0; i < lands; i++) place(basics[i % basics.length], one, ZoneType.Battlefield);
            if (test.startsWith("story")) {
                place("Mox Opal", one, ZoneType.Battlefield);
                if (test.equals("story")) place("Sol Ring", one, ZoneType.Battlefield);
                else place("Ornithopter", one, ZoneType.Hand);
                place("Óin the Brave", one, ZoneType.Hand);
                place("Murder", one, ZoneType.Hand);
            } else if (test.startsWith("ascend")) {
                place("Ocelot Pride", one, ZoneType.Hand);
                if (test.equals("ascend")) place("Ornithopter", one, ZoneType.Hand);
                place("Murder", one, ZoneType.Hand);
            } else if (test.equals("dungeon")) {
                place("Dungeon Map", one, ZoneType.Hand);
            } else if (test.equals("initiative")) {
                place("White Plume Adventurer", one, ZoneType.Hand);
            } else if (test.equals("ring")) {
                for (int i = 0; i < 5; i++) place("Nazgûl", one, ZoneType.Hand);
            } else if (test.startsWith("moon")) {
                for (String name : new String[]{"Wooded Foothills", "Breeding Pool", "Darksteel Citadel", "Dryad Arbor", "Scrying Sheets", "Minamo, School at Water's Edge"})
                    place(name, one, ZoneType.Battlefield);
                place("Wooded Foothills", two, ZoneType.Battlefield);
                for (String name : new String[]{"Blood Moon", "Harbinger of the Seas", "Murder", "Disenchant"}) place(name, one, ZoneType.Hand);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
