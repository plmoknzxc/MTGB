package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Read-only pending-input audit. Ordinary Forge opening, no fixture state changes. */
public final class OpeningOrderFixture {
    public static void main(String[] args) throws Exception {
        Thread watcher=new Thread(()->{
            Set<Long> seen=new HashSet<>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    var q=ForgeBridge.pending;
                    if(q!=null&&!q.consumed&&seen.add(q.requestId)) {
                        List<Object> players=new ArrayList<>();
                        for(Player p:ForgeBridge.game.getPlayers()) {
                            List<Integer> library=new ArrayList<>(),hand=new ArrayList<>();
                            for(Card c:p.getCardsIn(ZoneType.Library))library.add(c.getId());
                            for(Card c:p.getCardsIn(ZoneType.Hand))hand.add(c.getId());
                            players.add(Map.of("library",library,"hand",hand,"life",p.getLife()));
                        }
                        Path target=Path.of(ForgeBridge.config.get("auditDir").getAsString(),q.requestId+".json");
                        Path temp=target.resolveSibling(target.getFileName()+".tmp");
                        Files.writeString(temp,ForgeBridge.JSON.toJson(Map.of("requestId",q.requestId,"kind",q.kind,"players",players)));
                        Files.move(temp,target,StandardCopyOption.REPLACE_EXISTING);
                    }
                }
                Thread.sleep(5);
            }catch(Exception error){throw new RuntimeException(error);}
        },"opening-read-only-audit");
        watcher.setDaemon(true);watcher.start();
        ForgeBridge.run(args,null);
    }
}
