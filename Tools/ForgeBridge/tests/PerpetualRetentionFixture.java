package mtgb.bridge;
import forge.game.card.Card;
import java.nio.file.*;
import java.util.*;
/** Read-only actual keywords/types and static-trait ownership. No effect injection. */
public final class PerpetualRetentionFixture {
    static Map<String,Object> details(Card c)throws Exception {
        var m=PerpetualFieldFixture.field(c);
        m.put("type",c.getType().toString());
        m.put("keywords",c.getKeywords().stream().map(k->k.getOriginal()).toList());
        var statics=new ArrayList<Object>();
        for(var s:c.getStaticAbilities())statics.add(Map.of("params",s.getMapParams(),"hostId",s.getHostCard().getId(),"intrinsic",s.isIntrinsic()));
        m.put("statics",statics);return m;
    }
    public static void main(String[] args)throws Exception {
        Thread observer=new Thread(()->{
            Set<Long> seen=new HashSet<>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    var r=ForgeBridge.pending;
                    if(r!=null&&!r.consumed&&ForgeBridge.game!=null&&seen.add(r.requestId)) {
                        var cards=new ArrayList<Object>();for(Card c:ForgeBridge.game.getCardsInGame())if(!c.isLand())cards.add(details(c));
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+"-retention.tmp"),dst=dir.resolve(r.requestId+"-retention.json");Files.writeString(tmp,ForgeBridge.JSON.toJson(cards));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                    }
                }
                Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}
        },"perpetual-retention-read-only");observer.setDaemon(true);observer.start();PerpetualFieldFixture.main(args);
    }
}
