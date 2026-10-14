package mtgb.bridge;
import forge.game.card.Card;
import java.nio.file.*;
import java.util.*;
/** Extra read-only characteristic/record payloads; all modifications from real actions. */
public final class PerpetualFieldFixture {
    static Map<String,Object> field(Card c)throws Exception {
        var m=PerpetualRemovalFixture.snapshot(c);m.put("manaCost",c.getManaCost().toString());m.put("colors",c.getColor().getColor());m.put("basePower",c.getCurrentPower());m.put("baseToughness",c.getCurrentToughness());
        var records=new ArrayList<Object>();
        for(var p:c.getPerpetual()) {
            var data=new LinkedHashMap<String,Object>();data.put("kind",p.getClass().getSimpleName());
            for(var component:p.getClass().getRecordComponents()) {Object value=component.getAccessor().invoke(p);data.put(component.getName(),value==null?null:value instanceof Number||value instanceof Boolean?value:value.toString());}
            records.add(data);
        }
        m.put("payloads",records);return m;
    }
    public static void main(String[] args)throws Exception {
        Thread observer=new Thread(()->{
            Set<Long> seen=new HashSet<>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    var r=ForgeBridge.pending;
                    if(r!=null&&!r.consumed&&ForgeBridge.game!=null&&seen.add(r.requestId)) {
                        var cards=new ArrayList<Object>();for(Card c:ForgeBridge.game.getCardsInGame())if(!c.isLand())cards.add(field(c));
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+"-fields.tmp"),dst=dir.resolve(r.requestId+"-fields.json");Files.writeString(tmp,ForgeBridge.JSON.toJson(cards));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                    }
                }
                Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}
        },"perpetual-fields-read-only");observer.setDaemon(true);observer.start();PerpetualRemovalFixture.main(args);
    }
}
