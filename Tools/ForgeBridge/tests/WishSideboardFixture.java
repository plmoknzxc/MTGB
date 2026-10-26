package mtgb.bridge;

import forge.game.player.Player;
import forge.game.zone.ZoneType;

/** Initial position only: Wish, opposing activation, and sideboard spells are played natively. */
public final class WishSideboardFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player own=ForgeBridge.game.getPlayers().get(0),other=ForgeBridge.game.getPlayers().get(1);
            StateBranchFixture.setup(own,ForgeBridge.config.getAsJsonObject("own"));
            StateBranchFixture.setup(other,ForgeBridge.config.getAsJsonObject("other"));
            for(int i=0;i<8;i++) {
                if(!ForgeBridge.config.has("noBlue"))StateBranchFixture.place("Island",own,ZoneType.Battlefield);
                if(!ForgeBridge.config.has("noBlue")||i<3)StateBranchFixture.place("Mountain",own,ZoneType.Battlefield);
                StateBranchFixture.place("Island",other,ZoneType.Battlefield);
            }
            StateBranchFixture.place("Jayemdae Tome",other,ZoneType.Battlefield);
            for(String name:new String[]{"Stifle","Counterspell","Divination","Mountain"})
                StateBranchFixture.place(name,own,ZoneType.Sideboard);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
