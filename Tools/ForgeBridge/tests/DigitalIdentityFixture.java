package mtgb.bridge;

import forge.game.card.Card;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Read-only identities and remembered bindings. Opening and all native results
 * remain in the unchanged ControlVariantFixture and production engine. */
public final class DigitalIdentityFixture {
    public static void main(String[] args) throws Exception {
        Thread observer=new Thread(()->{
            Set<Long> seen=new HashSet<>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    var r=ForgeBridge.pending;
                    if(r!=null&&!r.consumed&&ForgeBridge.game!=null&&seen.add(r.requestId)) {
                        List<Object> cards=new ArrayList<>();
                        for(Card c:ForgeBridge.game.getCardsInGame()) {
                            List<Object> remembered=new ArrayList<>();
                            for(Object x:c.getRemembered())remembered.add(x instanceof Card?((Card)x).getId():String.valueOf(x));
                            var m=new LinkedHashMap<String,Object>();
                            m.put("id",c.getId());m.put("name",c.getName());m.put("token",c.isToken());m.put("remembered",remembered);
                            m.put("zone",c.getZone()==null?"None":c.getZone().getZoneType().name());
                            m.put("owner",ForgeBridge.game.getPlayers().indexOf(c.getOwner())+1);cards.add(m);
                        }
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+"-identity.tmp"),dst=dir.resolve(r.requestId+"-identity.json");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(cards));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                    }
                }
                Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}
        },"digital-identity-read-only");observer.setDaemon(true);observer.start();
        ControlVariantFixture.main(args);
    }
}
