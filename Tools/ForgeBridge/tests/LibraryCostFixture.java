package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.nio.file.*;
import java.util.*;

/** Initial setup and read-only library identities; no counter/turn/payment mutations. */
public final class LibraryCostFixture {
    static Card place(String name,Player p,ZoneType zone) {
        Card c=Card.fromPaperCard(Objects.requireNonNull(FModel.getMagicDb().getCommonCards().getCard(name)),p);
        p.getZone(zone).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    static void observe() {
        Thread t=new Thread(()->{
            Set<Long> done=new HashSet<>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    var r=ForgeBridge.pending;
                    if(r!=null&&!r.consumed&&r.kind.equals("InputPassPriority")&&done.add(r.requestId)) {
                        List<Object> players=new ArrayList<>();
                        for(Player p:ForgeBridge.game.getPlayers()) {
                            List<Integer> library=new ArrayList<>();for(Card c:p.getCardsIn(ZoneType.Library))library.add(c.getId());
                            players.add(Map.of("library",library));
                        }
                        Path target=Path.of(ForgeBridge.config.get("auditDir").getAsString(),r.requestId+".json"),tmp=target.resolveSibling(target.getFileName()+".tmp");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players)));
                        Files.move(tmp,target,StandardCopyOption.REPLACE_EXISTING);
                    }
                }
                Thread.sleep(10);
            }catch(Exception e){throw new RuntimeException(e);}
        },"library-cost-read-only-proof");t.setDaemon(true);t.start();
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            int seat=ForgeBridge.config.get("firstPlayerSeat").getAsInt();Player one=ForgeBridge.game.getPlayers().get(seat-1),two=ForgeBridge.game.getPlayers().get(2-seat);
            String test=ForgeBridge.config.get("fixture").getAsString();
            for(int i=0;i<4;i++)place("Plains",one,ZoneType.Battlefield);
            place("Jötun Grunt",one,ZoneType.Hand);
            int count=test.equals("insufficient")?1:test.equals("second-age")?6:2;
            for(int i=0;i<count;i++)place("Grizzly Bears",one,ZoneType.Graveyard);
            // Opponent naturally draws then discards at cleanup; zero initial
            // graveyard cards keeps that player's first upkeep payment below two.
            for(int i=0;i<(test.equals("insufficient")?0:2);i++)place("Shock",two,ZoneType.Graveyard);
            ForgeBridge.game.getAction().checkStaticAbilities();observe();
        });
    }
}
