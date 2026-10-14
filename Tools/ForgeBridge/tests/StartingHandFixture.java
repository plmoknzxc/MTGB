package mtgb.bridge;

import forge.deck.DeckSection;
import forge.model.FModel;

/** Official Backup Plan initialization; no direct hand or library manipulation. */
public final class StartingHandFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,null,players->{
            int copies=ForgeBridge.config.get("fixture").getAsString().equals("rotate-two")?2:1;
            var player=players.get(0);
            var paper=FModel.getMagicDb().getVariantCards().getCard("Backup Plan");
            if(paper==null)throw new IllegalStateException("Missing official Backup Plan variant card");
            player.getDeck().getOrCreate(DeckSection.Conspiracy).add(paper,copies);
            player.assignConspiracies();
        });
    }
}
