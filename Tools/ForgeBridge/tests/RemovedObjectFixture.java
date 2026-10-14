package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Initial positions only. The observer never changes native actions/results. */
public final class RemovedObjectFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++)RoyalStateFixture.setup(ForgeBridge.game.getPlayers().get(seat-1),ForgeBridge.config.getAsJsonArray("positions").get(seat-1).getAsJsonObject());
            ForgeBridge.game.getAction().checkStaticAbilities();
            var original=new ArrayList<Card>();
            for(Player p:ForgeBridge.game.getPlayers())for(Card c:p.getCardsIn(ZoneType.Battlefield))if(c.isCreature())original.add(c);
            var events=new NamePredicateFixture.Events();ForgeBridge.game.subscribeToEvents(events);
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&!seen.contains(r.requestId)){
                            List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()){
                                Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:ZoneType.values())if(z!=ZoneType.None)zones.put(z.name(),p.getCardsIn(z).stream().map(MeldPayloadFixture::card).toList());
                                players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"zones",zones,"mana",NamePredicateFixture.pool(p)));
                            }
                            List<Object> tracked=new ArrayList<>();for(Card c:original){Card current=ForgeBridge.game.getCardState(c,null);var item=MeldPayloadFixture.card(current==null?c:current);item.put("present",current!=null);tracked.add(item);}
                            var state=Map.of("requestId",r.requestId,"players",players,"tracked",tracked,"stack",NamePredicateFixture.stack(),"events",events.snapshot(),"turn",ForgeBridge.game.getPhaseHandler().getTurn(),"phase",ForgeBridge.game.getPhaseHandler().getPhase().name());
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(state));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                        }
                    }Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"removed-object-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
