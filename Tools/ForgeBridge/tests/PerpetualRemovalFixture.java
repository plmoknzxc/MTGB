package mtgb.bridge;

import forge.game.card.Card;
import java.nio.file.*;
import java.util.*;

/** Read-only perpetual journals and retained native object references.
 * Opening setup is shared; no effect invocation or journal mutation. */
public final class PerpetualRemovalFixture {
    static final List<Card> observed=new ArrayList<>();
    static Map<String,Object> snapshot(Card c) {
        var m=new LinkedHashMap<String,Object>();
        m.put("id",c.getId());m.put("object",System.identityHashCode(c));
        m.put("journalObject",System.identityHashCode(c.getPerpetual()));
        m.put("name",c.getName());m.put("timestamp",c.getGameTimestamp());
        m.put("zone",c.getZone()==null?"None":c.getZone().getZoneType().name());
        m.put("power",c.getNetPower());m.put("toughness",c.getNetToughness());
        m.put("journal",c.getPerpetual().stream().map(p->Map.of("timestamp",p.getTimestamp(),"kind",p.getClass().getSimpleName())).toList());
        var traits=new ArrayList<Object>();
        for(var cell:c.getChangedCardTraits().cellSet()) {
            var triggers=cell.getValue().applyTrigger(new ArrayList<>());
            traits.add(Map.of("timestamp",cell.getRowKey(),"staticId",cell.getColumnKey(),"triggers",triggers.stream().map(t->Map.of("id",t.getId(),"mode",t.getMode().name(),"description",t.toString())).toList()));
        }
        m.put("traits",traits);m.put("triggers",c.getTriggers().stream().map(t->Map.of("id",t.getId(),"mode",t.getMode().name(),"description",t.toString())).toList());
        var remembered=new ArrayList<Object>();
        for(Object x:c.getRemembered())remembered.add(x instanceof Card?((Card)x).getId():String.valueOf(x));
        m.put("remembered",remembered);
        return m;
    }
    static void observe() {
        Thread observer=new Thread(()->{
            Set<Long> seen=new HashSet<>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    var r=ForgeBridge.pending;
                    if(r!=null&&!r.consumed&&ForgeBridge.game!=null&&seen.add(r.requestId)) {
                        var current=new ArrayList<Card>();ForgeBridge.game.getCardsInGame().forEach(current::add);
                        for(Card c:current)if(observed.stream().noneMatch(x->x==c))observed.add(c);
                        var stack=new ArrayList<Object>();
                        for(var item:ForgeBridge.game.getStack()) {
                            var sa=item.getSpellAbility();
                            stack.add(Map.of("sourceId",sa.getHostCard().getId(),"source",sa.getHostCard().getName(),"object",System.identityHashCode(sa.getHostCard()),"triggerId",sa.getTrigger()==null?0:sa.getTrigger().getId(),"description",sa.toString()));
                        }
                        var data=Map.of("current",current.stream().filter(c->!c.isLand()).map(PerpetualRemovalFixture::snapshot).toList(),"retained",observed.stream().filter(c->!c.isLand()).map(PerpetualRemovalFixture::snapshot).toList(),"stack",stack);
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+"-perpetual.tmp"),dst=dir.resolve(r.requestId+"-perpetual.json");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(data));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                    }
                }
                Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}
        },"perpetual-journal-read-only");observer.setDaemon(true);observer.start();
    }
    public static void main(String[] args)throws Exception {observe();DigitalMemoryFixture.main(args);}
}
