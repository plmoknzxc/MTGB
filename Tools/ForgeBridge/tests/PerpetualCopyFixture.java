package mtgb.bridge;
import forge.game.card.Card;
import forge.card.CardStateName;
import java.nio.file.*;
import java.util.*;
/** Observe actual copied CardState separately from recipient journals. */
public final class PerpetualCopyFixture {
    static Map<String,Object> details(Card c)throws Exception {
        var m=PerpetualRetentionFixture.details(c);var state=c.getCurrentState();var physical=c.getOriginalState(CardStateName.Original);
        m.put("token",c.isToken());m.put("cloned",c.isCloned());m.put("cloneOrigin",c.getCloneOrigin()==null?0:c.getCloneOrigin().getId());
        m.put("copiableName",state.getName());m.put("copiablePower",state.getBasePower());m.put("copiableToughness",state.getBaseToughness());m.put("copiableType",state.getType().toString());
        m.put("physicalName",physical.getName());m.put("physicalPower",physical.getBasePower());m.put("physicalToughness",physical.getBaseToughness());return m;
    }
    public static void main(String[] args)throws Exception {
        Thread observer=new Thread(()->{
            Set<Long> seen=new HashSet<>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    var r=ForgeBridge.pending;
                    if(r!=null&&!r.consumed&&ForgeBridge.game!=null&&seen.add(r.requestId)) {
                        var cards=new ArrayList<Object>();for(Card c:ForgeBridge.game.getCardsInGame())if(!c.isLand())cards.add(details(c));
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+"-copies.tmp"),dst=dir.resolve(r.requestId+"-copies.json");Files.writeString(tmp,ForgeBridge.JSON.toJson(cards));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                    }
                }
                Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}
        },"perpetual-copies-read-only");observer.setDaemon(true);observer.start();PerpetualRetentionFixture.main(args);
    }
}
