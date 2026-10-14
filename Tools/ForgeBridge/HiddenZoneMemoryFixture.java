package mtgb.bridge;

import forge.game.zone.ZoneType;

/** Initial cards only. Reveal, discard, draw and return are native effects. */
public final class HiddenZoneMemoryFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            var one=ForgeBridge.game.getPlayers().get(0);var two=ForgeBridge.game.getPlayers().get(1);
            for(int i=0;i<4;i++) {
                ResolutionReviewFixture.place("Swamp",one,ZoneType.Battlefield);
                ResolutionReviewFixture.place("Island",two,ZoneType.Battlefield);
            }
            ResolutionReviewFixture.place("Thoughtseize",one,ZoneType.Hand);
            for(String name:new String[]{"Brainstorm","Negate","Duress","Grizzly Bears"})ResolutionReviewFixture.place(name,two,ZoneType.Hand);
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
