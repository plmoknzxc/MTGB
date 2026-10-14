package mtgb.bridge;
import com.google.common.eventbus.Subscribe;
import forge.game.card.Card;
import forge.game.card.CounterType;
import forge.game.event.*;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;
import java.util.concurrent.atomic.AtomicInteger;

/** Initial position once only. Real native actions produce exile, copy and restart results. */
public final class RestartStateFixture {
    static final AtomicInteger setupCalls=new AtomicInteger();
    static final List<Object> events=Collections.synchronizedList(new ArrayList<>());
    public static final class Events {
        @Subscribe public void restarted(GameEventGameRestarted e){events.add(Map.of("event","restarted","actor",e.whoRestarted().getId()+1,"gameIdentity",System.identityHashCode(ForgeBridge.game)));}
        @Subscribe public void started(GameEventGameStarted e){events.add(Map.of("event","started","gameIdentity",System.identityHashCode(ForgeBridge.game)));}
        @Subscribe public void finished(GameEventGameFinished e){events.add(Map.of("event","finished"));}
        @Subscribe public void zone(GameEventZone e){if(e.card()!=null)events.add(Map.of("event","zone","card",e.card().getId(),"zone",e.zoneType().name(),"mode",e.mode().name()));}
    }
    static Map<String,Object> card(Card c){
        var x=NamePredicateFixture.card(c);x.put("exiledWith",c.getExiledWith()==null?0:c.getExiledWith().getId());
        x.put("controller",c.getController().getId()+1);x.put("timestamp",c.getGameTimestamp());return x;
    }
    static Object delayed()throws Exception {
        Map<String,Object> x=new LinkedHashMap<>();var handler=ForgeBridge.game.getTriggerHandler();
        for(var f:handler.getClass().getDeclaredFields())if(f.getName().toLowerCase().contains("delayed")){
            f.setAccessible(true);Object value=f.get(handler);x.put(f.getName(),value instanceof Collection<?> c?c.size():value instanceof Map<?,?> m?m.size():String.valueOf(value));
        }return x;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            if(setupCalls.incrementAndGet()!=1)return; // Karn repeats startFirstTurn hook; never reinstall fixture.
            int actor=ForgeBridge.config.get("actor").getAsInt();
            for(int seat=1;seat<=2;seat++){
                Player p=ForgeBridge.game.getPlayers().get(seat-1);RoyalStateFixture.setup(p,ForgeBridge.config.getAsJsonArray("positions").get(seat-1).getAsJsonObject());
                p.setLife(seat==actor?12:15,null);p.setCounters(CounterType.getType("ENERGY"),3,p,false);
                p.setBlessing(true,null);p.setNumRingTemptedYou(3);p.createTheRing("LTR");p.setRingLevel(2);
            }
            var own=ForgeBridge.game.getPlayers().get(actor-1);
            for(Card c:own.getCardsIn(ZoneType.Battlefield))if(c.getName().equals("Karn Liberated"))c.setCounters(CounterType.getType("LOYALTY"),ForgeBridge.config.get("loyalty").getAsInt());
            ForgeBridge.game.setMonarch(own);ForgeBridge.game.setDayTime(true);
            ForgeBridge.game.getAction().checkStaticAbilities();ForgeBridge.game.subscribeToEvents(new Events());
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&!seen.contains(r.requestId)){
                        List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()){
                            Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:ZoneType.values())if(z!=ZoneType.None)zones.put(z.name(),p.getCardsIn(z).stream().map(RestartStateFixture::card).toList());
                            Map<String,Object> state=new LinkedHashMap<>();state.put("seat",p.getId()+1);state.put("life",p.getLife());state.put("counters",p.getCounters());state.put("zones",zones);state.put("mana",NamePredicateFixture.pool(p));state.put("blessing",p.hasBlessing());state.put("ring",p.getNumRingTemptedYou());state.put("ringBearer",p.getRingBearer()==null?0:p.getRingBearer().getId());players.add(state);
                        }
                        Map<String,Object> state=new LinkedHashMap<>();state.put("requestId",r.requestId);state.put("players",players);state.put("stack",NamePredicateFixture.stack());state.put("gameIdentity",System.identityHashCode(ForgeBridge.game));state.put("age",ForgeBridge.game.getAge().name());state.put("turn",ForgeBridge.game.getPhaseHandler().getTurn());state.put("phase",String.valueOf(ForgeBridge.game.getPhaseHandler().getPhase()));state.put("turnSeat",ForgeBridge.game.getPhaseHandler().getPlayerTurn().getId()+1);state.put("setupCalls",setupCalls.get());state.put("delayed",delayed());state.put("monarch",ForgeBridge.game.getMonarch()==null?0:ForgeBridge.game.getMonarch().getId()+1);state.put("day",String.valueOf(ForgeBridge.game.getDayTime()));synchronized(events){state.put("events",List.copyOf(events));}
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");Files.writeString(tmp,ForgeBridge.JSON.toJson(state));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                    }}Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"restart-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
