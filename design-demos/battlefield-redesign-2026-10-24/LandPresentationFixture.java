package mtgb.bridge;

import forge.game.player.Player;
import forge.game.zone.ZoneType;

/** Initial positions only. Mana activations and Blood Moon resolve natively. */
public final class LandPresentationFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player p=ForgeBridge.game.getPlayers().get(0),other=ForgeBridge.game.getPlayers().get(1);
            StateBranchFixture.setup(p,ForgeBridge.config.getAsJsonObject("own"));
            StateBranchFixture.setup(other,ForgeBridge.config.getAsJsonObject("other"));
            for(String land:new String[]{"Hallowed Fountain","Hallowed Fountain","Hallowed Fountain","Island","Island","Mountain","Plains","Wastes","Shivan Reef","Raffine's Tower","City of Brass","Flooded Strand"})
                StateBranchFixture.place(land,p,ZoneType.Battlefield);
            for(int i=0;i<2;i++)StateBranchFixture.place("Steam Vents",p,ZoneType.Battlefield).setTapped(true);
            for(String name:new String[]{"Meticulous Archive","Meticulous Archive","Hedge Maze","Commercial District","Breeding Pool","Thundering Falls"})
                StateBranchFixture.place(name,p,ZoneType.Battlefield).setTapped(!name.equals("Breeding Pool"));
            var linked=StateBranchFixture.place("Hallowed Fountain",p,ZoneType.Battlefield);
            linked.addRemembered(p.getCardsIn(ZoneType.Hand).getFirst());
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
