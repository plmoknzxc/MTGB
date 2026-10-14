package mtgb.bridge;

import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Arrange initial cards only; observe native day/night and speed read-only. */
public final class PlayerStatusFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            int actor=ForgeBridge.config.get("actor").getAsInt();
            Player own=ForgeBridge.game.getPlayers().get(actor-1),other=ForgeBridge.game.getPlayers().get(2-actor);
            for(String land:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int i=0;i<6;i++)StateBranchFixture.place(land,own,ZoneType.Battlefield);
            StateBranchFixture.setup(own,ForgeBridge.config.getAsJsonObject("own"));StateBranchFixture.setup(other,ForgeBridge.config.getAsJsonObject("other"));
            ForgeBridge.game.getAction().checkStaticAbilities();
            Thread observer=new Thread(()->{
                Set<Long> saved=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&saved.add(r.requestId)){
                        Map<String,Object> data=new LinkedHashMap<>();data.put("requestId",r.requestId);data.put("dayTime",ForgeBridge.game.getDayTime());
                        data.put("speeds",ForgeBridge.game.getPlayers().stream().map(Player::getSpeed).toList());
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(data));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                    }}Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"player-status-read-only-audit");observer.setDaemon(true);observer.start();
        });
    }
}
