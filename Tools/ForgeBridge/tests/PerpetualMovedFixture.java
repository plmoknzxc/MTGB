package mtgb.bridge;
import forge.game.card.Card;
import java.nio.file.*;
import java.util.*;
/** Read-only live cards and the engine's actual zone-change LKI table. */
public final class PerpetualMovedFixture {
    static Map<String,Object> details(Card c)throws Exception {
        var m=PerpetualRetentionFixture.details(c);m.put("tapped",c.isTapped());
        var replacements=new ArrayList<Object>();
        for(var re:c.getReplacementEffects())replacements.add(Map.of("params",re.getMapParams(),"hostId",re.getHostCard().getId(),"intrinsic",re.isIntrinsic()));
        m.put("replacements",replacements);return m;
    }
    public static void main(String[] args)throws Exception {
        Thread observer=new Thread(()->{
            Set<Long> seen=new HashSet<>();var references=new ArrayList<Card>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    var r=ForgeBridge.pending;
                    if(r!=null&&!r.consumed&&ForgeBridge.game!=null&&seen.add(r.requestId)) {
                        var cards=new ArrayList<Object>();for(Card c:ForgeBridge.game.getCardsInGame())if(!c.isLand()){
                            cards.add(details(c));if(references.stream().noneMatch(old->old==c))references.add(c);
                        }
                        var lkis=new ArrayList<Object>();for(Card old:references){var lki=ForgeBridge.game.getChangeZoneLKIInfo(old);if(lki!=old)lkis.add(Map.of("query",details(old),"engineLKI",details(lki)));}
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+"-moved.tmp"),dst=dir.resolve(r.requestId+"-moved.json");Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("cards",cards,"lkis",lkis)));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                    }
                }
                Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}
        },"perpetual-moved-read-only");observer.setDaemon(true);observer.start();PerpetualRetentionFixture.main(args);
    }
}
