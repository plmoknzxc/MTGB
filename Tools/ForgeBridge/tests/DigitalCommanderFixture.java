package mtgb.bridge;

import forge.game.card.Card;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.nio.file.*;
import java.util.*;

/** Native registered commander plus an opening library position only.
 * Exercises an optional Seek destination replacement, not product Commander mode. */
public final class DigitalCommanderFixture {
    public static void main(String[] args)throws Exception {
        DigitalMemoryFixture.observe();
        Thread observer=new Thread(()->{
            Set<Long> seen=new HashSet<>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    var r=ForgeBridge.pending;
                    if(r!=null&&!r.consumed&&ForgeBridge.game!=null&&seen.add(r.requestId)) {
                        var players=new ArrayList<Object>();
                        for(var p:ForgeBridge.game.getPlayers()) {
                            var zones=new LinkedHashMap<String,Object>();
                            for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(ControlVariantFixture::card).toList());
                            players.add(Map.of("seat",ForgeBridge.game.getPlayers().indexOf(p)+1,"life",p.getLife(),"zones",zones));
                        }
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players)));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                    }
                }
                Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}
        },"digital-commander-read-only");observer.setDaemon(true);observer.start();
        ForgeBridge.run(args,()->{
            for(int i=0;i<2;i++)RoyalStateFixture.setup(ForgeBridge.game.getPlayers().get(i),ForgeBridge.config.getAsJsonArray("positions").get(i).getAsJsonObject());
            var own=ForgeBridge.game.getPlayers().get(0);
            for(String n:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int j=0;j<4;j++)RoyalStateFixture.place(n,own,ZoneType.Battlefield);
            Card cmd=own.getCardsIn(ZoneType.Command).stream().filter(Card::isCommander).findFirst().orElseThrow();
            own.getZone(ZoneType.Command).remove(cmd);own.getZone(ZoneType.Library).add(cmd,2);
            ForgeBridge.game.getAction().checkStaticAbilities();
        },registered->{registered.get(0).getCommanders().add(FModel.getMagicDb().getCommonCards().getCard("Isamaru, Hound of Konda"));});
    }
}
