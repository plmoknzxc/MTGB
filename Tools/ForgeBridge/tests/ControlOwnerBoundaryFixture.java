package mtgb.bridge;

import java.nio.file.*;
import java.util.*;
import forge.game.zone.ZoneType;

/** Delegate unchanged opening setup/native controller; extra observer only reads token identity. */
public final class ControlOwnerBoundaryFixture {
    public static void main(String[] args)throws Exception {
        Thread observer=new Thread(()->{
            Set<Long> seen=new HashSet<>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    var r=ForgeBridge.pending;
                    if(ForgeBridge.game!=null&&r!=null&&!r.consumed&&seen.add(r.requestId)) {
                        var tokens=ForgeBridge.game.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.isToken())
                            .map(c->Map.of("id",c.getId(),"owner",ForgeBridge.game.getPlayers().indexOf(c.getOwner())+1,"controller",ForgeBridge.game.getPlayers().indexOf(c.getController())+1)).toList();
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString());
                        Files.writeString(dir.resolve(r.requestId+"-tokens.json"),ForgeBridge.JSON.toJson(tokens));
                    }
                }
                Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}
        },"owner-boundary-token-read-only");observer.setDaemon(true);observer.start();
        ControlVariantFixture.main(args);
    }
}
