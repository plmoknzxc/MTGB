package mtgb.bridge;

import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Read-only witness of real registered main/side pools after the opening; no setup writes. */
public final class SeriesOpeningObserver {
    public static void main(String[] args)throws Exception {
        Path out=Path.of(args[1]).getParent().resolve("audit");Files.createDirectories(out);
        ForgeBridge.run(args,()->{
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&!seen.contains(r.requestId)){
                        List<Object> players=new ArrayList<>();for(var p:ForgeBridge.game.getPlayers()){
                            Map<String,Object> zones=new LinkedHashMap<>();for(var z:ZoneType.values())if(z!=ZoneType.None)
                                zones.put(z.name(),p.getCardsIn(z).stream().map(c->Map.of("id",c.getId(),"name",c.getName(),"owner",c.getOwner().getId()+1,"zone",z.name(),"printing",ForgeCatalog.key((forge.item.PaperCard)c.getPaperCard()))).toList());
                            players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"zones",zones));
                        }
                        var state=Map.of("requestId",r.requestId,"gameIdentity",System.identityHashCode(ForgeBridge.game),"players",players,"turn",ForgeBridge.game.getPhaseHandler().getTurn(),"turnSeat",ForgeBridge.game.getPhaseHandler().getPlayerTurn().getId()+1,"phase",String.valueOf(ForgeBridge.game.getPhaseHandler().getPhase()));
                        Path tmp=out.resolve(r.requestId+".tmp"),dst=out.resolve(r.requestId+".json");Files.writeString(tmp,ForgeBridge.JSON.toJson(state));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                    }}Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"read-only-series-opening");observer.setDaemon(true);observer.start();
        });
    }
}
