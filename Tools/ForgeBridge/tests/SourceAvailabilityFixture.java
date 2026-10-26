package mtgb.bridge;

import forge.game.player.Player;
import forge.game.zone.ZoneType;

/** Initial positions only. All later timing, costs and choices use the live native game. */
public final class SourceAvailabilityFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player own=ForgeBridge.game.getPlayers().get(0),other=ForgeBridge.game.getPlayers().get(1);
            if(ForgeBridge.config.has("lands"))for(var land:ForgeBridge.config.getAsJsonArray("lands"))
                StateBranchFixture.place(land.getAsString(),own,ZoneType.Battlefield);
            StateBranchFixture.setup(own,ForgeBridge.config.getAsJsonObject("own"));
            StateBranchFixture.setup(other,ForgeBridge.config.getAsJsonObject("other"));
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
