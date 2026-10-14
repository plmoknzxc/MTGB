package mtgb.bridge;

import forge.game.card.Card;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Initial positions only. Read-only audit; native declarations and damage produce outcomes. */
public final class CombatKeywordFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++)
                StateBranchFixture.setup(ForgeBridge.game.getPlayers().get(seat-1),ForgeBridge.config.getAsJsonObject(seat==1?"own":"other"));
            ForgeBridge.game.getAction().checkStaticAbilities();
            Thread observer=new Thread(()->{
                Set<Long> saved=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&saved.add(r.requestId)) {
                            List<Object> cards=new ArrayList<>(),players=new ArrayList<>(),bands=new ArrayList<>();
                            for(Card c:ForgeBridge.game.getCardsInGame()) {
                                Map<String,Object> item=new LinkedHashMap<>();
                                item.put("id",c.getId());item.put("name",c.getName());item.put("zone",c.getZone().getZoneType().name());
                                item.put("power",c.getNetPower());item.put("toughness",c.getNetToughness());item.put("damage",c.getDamage());
                                Map<String,Integer> counters=new TreeMap<>();
                                for(var e:c.getCounters().entrySet())counters.put(e.getElement().toString(),e.getCount());
                                item.put("counters",counters);cards.add(item);
                            }
                            for(var p:ForgeBridge.game.getPlayers()) {
                                Map<String,Integer> counters=new TreeMap<>();
                                for(var e:p.getCounters().entrySet())counters.put(e.getElement().toString(),e.getCount());
                                players.add(Map.of("life",p.getLife(),"counters",counters,"library",p.getCardsIn(ZoneType.Library).stream().map(Card::getId).toList()));
                            }
                            var combat=ForgeBridge.game.getCombat();
                            if(combat!=null)for(Card c:combat.getAttackers())
                                bands.add(Map.of("attacker",c.getId(),"members",combat.getBandOfAttacker(c).getAttackers().stream().map(Card::getId).toList()));
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"cards",cards,"players",players,"bands",bands)));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"combat-keyword-read-only-audit");observer.setDaemon(true);observer.start();
        });
    }
}
