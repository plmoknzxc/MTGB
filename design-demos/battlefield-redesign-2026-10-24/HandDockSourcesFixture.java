package mtgb.bridge;

import forge.game.player.Player;
import forge.game.zone.ZoneType;

/** Positions only; eligibility and wish resolution are native. */
public final class HandDockSourcesFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player own=ForgeBridge.game.getPlayers().get(0),other=ForgeBridge.game.getPlayers().get(1);
            if(ForgeBridge.config.has("lands"))for(var land:ForgeBridge.config.getAsJsonArray("lands"))
                StateBranchFixture.place(land.getAsString(),own,ZoneType.Battlefield);
            StateBranchFixture.setup(own,ForgeBridge.config.getAsJsonObject("own"));
            StateBranchFixture.setup(other,ForgeBridge.config.getAsJsonObject("other"));
            own.addCommander(StateBranchFixture.place("Thrasios, Triton Hero",own,ZoneType.Command));
            StateBranchFixture.place("Opt",own,ZoneType.Sideboard);
            StateBranchFixture.place("Negate",own,ZoneType.Sideboard);
            StateBranchFixture.place("Lightning Bolt",other,ZoneType.Sideboard);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
