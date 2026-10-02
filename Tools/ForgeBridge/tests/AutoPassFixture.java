package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;

/** Isolated setup only; normal Forge priority and ability input decide the outcome. */
public final class AutoPassFixture {
    static Card place(String name,Player owner,ZoneType zone) {
        Card card=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),owner);
        owner.getZone(zone).add(card);card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());
        card.setSickness(false);return card;
    }
    public static void main(String[] args) throws Exception {
        ForgeBridge.run(args,() -> {
            Player player=ForgeBridge.game.getPlayers().get(1);
            switch(ForgeBridge.config.get("fixture").getAsString()) {
                case "none": break;
                case "tapped": place("Mountain",player,ZoneType.Battlefield).setTapped(true); break;
                case "land": place("Mountain",player,ZoneType.Battlefield); break;
                case "mana-creature": place("Llanowar Elves",player,ZoneType.Battlefield); break;
                case "instant": place("Summoner's Pact",player,ZoneType.Hand); break;
                case "spell-with-mana":
                    place("Shock",player,ZoneType.Hand);place("Mountain",player,ZoneType.Battlefield); break;
                case "activated": place("Prodigal Pyromancer",player,ZoneType.Battlefield); break;
                case "flashback":
                    place("Lava Dart",player,ZoneType.Graveyard);
                    place("Mountain",player,ZoneType.Battlefield).setTapped(true); break;
                case "unaffordable-ability": place("Shivan Dragon",player,ZoneType.Battlefield); break;
                case "restricted-land": place("Temple of the False God",player,ZoneType.Battlefield); break;
                default: throw new IllegalArgumentException("Unknown test fixture");
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
