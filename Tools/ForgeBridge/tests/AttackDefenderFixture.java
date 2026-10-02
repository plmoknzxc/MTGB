package mtgb.bridge;

import forge.game.card.*;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Test-only executable, never packaged: real official cards in a fixed pre-combat board.
 * Setup skips casting/ETBs; declarations, priority and combat damage use the normal game loop. */
public final class AttackDefenderFixture {
    static Card place(String name,Player owner) {
        Card card=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),owner);
        owner.getZone(ZoneType.Battlefield).add(card);
        card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());
        card.setSickness(false);return card;
    }
    public static void main(String[] args) throws Exception {
        ForgeBridge.run(args,() -> {
            Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
            for(int i=0;i<3;i++)place("Goblin Piker",one);
            place("Wall of Wood",one); // Defender: cannot be declared as an attacker.
            Card walker=place("Jace Beleren",two);
            walker.setCounters(CounterType.getType("LOYALTY"),5);
            Card battle=place("Invasion of Zendikar",one);
            battle.setProtectingPlayer(two);battle.setCounters(CounterType.getType("DEFENSE"),5);
            Card friendly=place("Jace Beleren",one);
            friendly.setCounters(CounterType.getType("LOYALTY"),5);
            Card protectedBattle=place("Invasion of Zendikar",two);
            protectedBattle.setProtectingPlayer(one);protectedBattle.setCounters(CounterType.getType("DEFENSE"),5);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
