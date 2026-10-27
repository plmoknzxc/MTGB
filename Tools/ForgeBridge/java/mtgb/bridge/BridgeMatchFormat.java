// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import forge.deck.Deck;
import forge.deck.DeckFormat;
import forge.deck.DeckSection;
import forge.game.GameRules;
import forge.game.GameType;
import forge.game.player.RegisteredPlayer;
import java.util.*;

/** Room-selected 1v1 rules. Deck metadata never selects the server's variant. */
final class BridgeMatchFormat {
    static String id() {
        return ForgeBridge.config.has("formatId") ? ForgeBridge.config.get("formatId").getAsString() : "none";
    }
    static Set<GameType> variants() {
        return switch (id()) {
            case "duelcommander" -> EnumSet.of(GameType.Commander);
            case "brawl" -> EnumSet.of(GameType.Brawl);
            default -> EnumSet.noneOf(GameType.class);
        };
    }
    static boolean commanders() { return id().equals("duelcommander") || id().equals("brawl"); }
    static RegisteredPlayer register(Deck deck) {
        if (!commanders()) {
            if (!deck.getCommanders().isEmpty()) throw new IllegalArgumentException("Room format does not use commanders");
            return new RegisteredPlayer(deck);
        }
        String problem=(id().equals("brawl")?DeckFormat.Brawl:DeckFormat.Commander).getDeckConformanceProblem(deck);
        if(problem!=null)throw new IllegalArgumentException("Invalid commander deck: "+problem);
        int side=deck.getOrCreate(DeckSection.Sideboard).countAll();
        if(side>1)throw new IllegalArgumentException("Only a designated companion may be outside the deck");
        return RegisteredPlayer.forVariants(2,variants(),deck,null,false,null,null);
    }
    static void apply(GameRules rules) {
        rules.setAppliedVariants(variants());
        rules.setDuelCommander(id().equals("duelcommander"));
    }
    static int startingLife(int requested) {
        // Local practice explicitly permits a custom life total; online variant rules stay fixed.
        if(ForgeBridge.config.has("localPractice")&&ForgeBridge.config.get("localPractice").getAsBoolean())return requested;
        return commanders() ? (id().equals("brawl")?25:20) : requested;
    }
}
