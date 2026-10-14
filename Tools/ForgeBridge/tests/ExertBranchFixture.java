package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Initial positions and read-only native exert observation; native inputs make all changes. */
public final class ExertBranchFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++) {
                Player player=ForgeBridge.game.getPlayers().get(seat-1);
                StateBranchFixture.setup(player,ForgeBridge.config.getAsJsonObject(seat==1?"own":"other"));
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            Thread observer=new Thread(()->{
                Set<Long> saved=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var request=ForgeBridge.pending;
                        if(request!=null&&!request.consumed&&saved.add(request.requestId)) {
                            List<Object> cards=new ArrayList<>();
                            for(Card card:ForgeBridge.game.getCardsIn(ZoneType.Battlefield))if(!card.isLand()) {
                                List<Integer> waiting=new ArrayList<>();
                                for(Player p:ForgeBridge.game.getPlayers())if(card.isExertedBy(p))waiting.add(ForgeBridge.game.getPlayers().indexOf(p)+1);
                                cards.add(Map.of("id",card.getId(),"name",card.getName(),"tapped",card.isTapped(),"exertedThisTurn",card.getExertedThisTurn(),"waitingUntapSeats",waiting));
                            }
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(request.requestId+".tmp"),dst=dir.resolve(request.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",request.requestId,"cards",cards)));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception error){throw new RuntimeException(error);}
            },"exert-read-only-audit");observer.setDaemon(true);observer.start();
        });
    }
}
