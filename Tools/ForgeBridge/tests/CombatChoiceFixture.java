package mtgb.bridge;

import forge.game.card.Card;
import forge.game.zone.ZoneType;
import forge.util.MyRandom;
import java.nio.file.*;
import java.util.*;

/** Opening positions/RNG only. Everything after start is a legal native action;
 * observer records original IDs and combat relation without changing outcomes. */
public final class CombatChoiceFixture {
    static final List<Object> randomCalls = new ArrayList<>();
    static final class RecordedRandom extends Random {
        RecordedRandom(long seed) { super(seed); }
        @Override public int nextInt(int bound) {
            int value=super.nextInt(bound);
            boolean camouflage=Arrays.stream(Thread.currentThread().getStackTrace())
                .anyMatch(f->f.getClassName().endsWith("CamouflageEffect"));
            if(camouflage) randomCalls.add(Map.of("bound",bound,"value",value));
            return value;
        }
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++) {
                var player=ForgeBridge.game.getPlayers().get(seat-1);
                var spec=ForgeBridge.config.getAsJsonObject(seat==1?"own":"other");
                RoyalStateFixture.setup(player,spec);
                if(spec.has("tapped"))for(var name:spec.getAsJsonArray("tapped"))
                    player.getCardsIn(ZoneType.Battlefield).stream()
                        .filter(c->c.getName().equals(name.getAsString())).forEach(c->c.setTapped(true));
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            MyRandom.setRandom(new RecordedRandom(ForgeBridge.config.get("fixtureSeed").getAsLong()));
            Thread observer=new Thread(()->{
                Set<Long> saved=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&saved.add(r.requestId)) {
                            List<Object> players=new ArrayList<>(),matrix=new ArrayList<>();
                            for(var p:ForgeBridge.game.getPlayers()) {
                                Map<String,Object> zones=new LinkedHashMap<>();
                                for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile}) {
                                    List<Object> cards=new ArrayList<>();
                                    for(Card c:p.getCardsIn(z)) {
                                        Map<String,Object> row=new LinkedHashMap<>();
                                        row.put("id",c.getId());row.put("name",c.getName());row.put("zone",z.name());
                                        row.put("owner",ForgeBridge.game.getPlayers().indexOf(c.getOwner())+1);
                                        row.put("controller",ForgeBridge.game.getPlayers().indexOf(c.getController())+1);
                                        row.put("timestamp",c.getGameTimestamp());row.put("tapped",c.isTapped());
                                        row.put("power",c.getNetPower());row.put("toughness",c.getNetToughness());row.put("damage",c.getDamage());
                                        Map<String,Integer> counters=new TreeMap<>();for(var e:c.getCounters().entrySet())counters.put(e.getElement().toString(),e.getCount());
                                        row.put("counters",counters);cards.add(row);
                                    }
                                    zones.put(z.name(),cards);
                                }
                                players.add(Map.of("seat",ForgeBridge.game.getPlayers().indexOf(p)+1,"life",p.getLife(),"zones",zones));
                            }
                            var combat=ForgeBridge.game.getCombat();
                            if(combat!=null)for(Card a:combat.getAttackers())
                                matrix.add(Map.of("attacker",a.getId(),"blockers",combat.getBlockers(a).stream().map(Card::getId).toList(),"band",combat.getBandOfAttacker(a).getAttackers().stream().map(Card::getId).toList()));
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"phase",ForgeBridge.game.getPhaseHandler().getPhase().name(),"players",players,"matrix",matrix,"randomCalls",new ArrayList<>(randomCalls))));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"combat-choice-read-only-audit");observer.setDaemon(true);observer.start();
        });
    }
}
