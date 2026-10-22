package mtgb.bridge;

import com.google.common.eventbus.Subscribe;
import forge.game.card.Card;
import forge.game.event.GameEventShuffle;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Read-only native audit: no initial positions or game outcomes are changed. */
public final class NoShuffleFixture {
    static List<Object> cards(){
        List<Object> result=new ArrayList<>();
        for(int seat=1;seat<=2;seat++)for(ZoneType zone:List.of(ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard))
            for(Card c:ForgeBridge.game.getPlayers().get(seat-1).getCardsIn(zone))
                result.add(Map.of("seat",seat,"zone",zone.name(),"id",c.getId(),"name",c.getName(),"printing",c.getPaperCard().getEdition()+"/"+c.getPaperCard().getCollectorNumber()));
        return result;
    }
    static void save(String name,Object value)throws Exception{
        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString());
        Files.writeString(dir.resolve(name+".tmp"),ForgeBridge.JSON.toJson(value));
        Files.move(dir.resolve(name+".tmp"),dir.resolve(name+".json"),StandardCopyOption.REPLACE_EXISTING);
    }
    public static final class Events {
        int shuffles;
        @Subscribe public void shuffle(GameEventShuffle event){try{save("shuffle-"+(++shuffles),Map.of("player",event.player().getId(),"cards",cards()));}catch(Exception e){throw new RuntimeException(e);}}
    }
    public static void main(String[] args)throws Exception{
        ForgeBridge.run(args,null,players->{
            Thread audit=new Thread(()->{
                Set<Long> seen=new HashSet<>();
                try{while(ForgeBridge.game==null)Thread.sleep(3);ForgeBridge.game.subscribeToEvents(new Events());}catch(Exception e){throw new RuntimeException(e);}
                while(!ForgeBridge.finished)try{
                    synchronized(ForgeBridge.class){var request=ForgeBridge.pending;if(request!=null&&!request.consumed&&seen.add(request.requestId))save("request-"+request.requestId,Map.of("cards",cards()));}
                    Thread.sleep(3);
                }catch(Exception e){throw new RuntimeException(e);}
            },"no-shuffle-read-only-audit");audit.setDaemon(true);audit.start();
        });
    }
}
