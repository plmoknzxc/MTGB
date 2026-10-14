package mtgb.bridge;

import com.google.common.eventbus.Subscribe;
import forge.game.ability.AbilityKey;
import forge.game.card.Card;
import forge.game.event.GameEventCardChangeZone;
import forge.game.event.GameEventCardStatsChanged;
import java.nio.file.*;
import java.util.*;

/** Read-only event snapshots of native transient bindings; no effect injection. */
public final class DigitalMemoryFixture {
    static Path audit;
    static List<Object> cards() {
        List<Object> out=new ArrayList<>();
        for(Card c:ForgeBridge.game.getCardsInGame()) {
            var m=new LinkedHashMap<String,Object>();
            m.put("id",c.getId());m.put("name",c.getName());
            m.put("zone",c.getZone()==null?"None":c.getZone().getZoneType().name());
            m.put("owner",ForgeBridge.game.getPlayers().indexOf(c.getOwner())+1);
            m.put("token",c.isToken());m.put("type",c.getType().toString());
            m.put("power",c.getNetPower());m.put("toughness",c.getNetToughness());
            var remembered=new ArrayList<Object>();
            for(Object x:c.getRemembered())remembered.add(x instanceof Card?((Card)x).getId():String.valueOf(x));
            m.put("remembered",remembered);
            m.put("imprinted",c.getImprintedCards().stream().map(Card::getId).toList());
            m.put("keywords",c.getKeywords().stream().map(k->k.getOriginal()).toList());
            m.put("perpetualCount",c.getPerpetual().size());out.add(m);
        }
        return out;
    }
    static List<Object> stack() {
        List<Object> out=new ArrayList<>();
        for(var item:ForgeBridge.game.getStack()) {
            var sa=item.getSpellAbility();var ids=new ArrayList<Object>();
            Object found=sa.getTriggeringObject(AbilityKey.Cards);
            if(found instanceof Iterable<?>)for(Object x:(Iterable<?>)found)if(x instanceof Card)ids.add(((Card)x).getId());
            out.add(Map.of("sourceId",sa.getHostCard().getId(),"source",sa.getHostCard().getName(),"triggerCards",ids));
        }
        return out;
    }
    public static final class Events {
        int sequence;
        synchronized void record(String type,Object ids) {
            try {
                var row=new LinkedHashMap<String,Object>();
                row.put("sequence",++sequence);row.put("event",type);row.put("affected",ids);
                row.put("turn",ForgeBridge.game.getPhaseHandler().getTurn());row.put("cards",cards());
                Files.writeString(audit.resolve("events.jsonl"),ForgeBridge.JSON.toJson(row)+"\n",StandardOpenOption.CREATE,StandardOpenOption.APPEND);
            } catch(Exception e){throw new RuntimeException(e);}
        }
        @Subscribe public void stats(GameEventCardStatsChanged e){record("stats",e.cards().stream().map(c->c.getId()).toList());}
        @Subscribe public void move(GameEventCardChangeZone e){record("move",Map.of("id",e.card().getId(),"from",e.from()==null?"None":e.from().zoneType().name(),"to",e.to()==null?"None":e.to().zoneType().name()));}
    }
    static void observe() {
        Thread observer=new Thread(()->{
            boolean installed=false;Set<Long> seen=new HashSet<>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    if(ForgeBridge.game!=null&&ForgeBridge.config!=null) {
                        if(!installed){audit=Path.of(ForgeBridge.config.get("auditDir").getAsString());ForgeBridge.game.subscribeToEvents(new Events());installed=true;}
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&seen.add(r.requestId)) {
                            Path tmp=audit.resolve(r.requestId+"-memory.tmp"),dst=audit.resolve(r.requestId+"-memory.json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("cards",cards(),"stack",stack())));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                }
                Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}
        },"digital-memory-read-only");observer.setDaemon(true);observer.start();
    }
    public static void main(String[] args)throws Exception {
        observe();DigitalIdentityFixture.main(args);
    }
}
