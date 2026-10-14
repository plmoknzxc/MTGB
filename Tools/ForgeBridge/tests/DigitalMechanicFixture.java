package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.nio.file.*;
import java.util.*;

/** Initial positions and read-only private audit; all effects run through native inputs. */
public final class DigitalMechanicFixture {
    static void place(String name,Player p,ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException(name);
        Card c=Card.fromPaperCard(paper,p);p.getZone(zone).add(c);
        c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);
    }
    static void observe() {
        Thread observer=new Thread(()->{
            Set<Long> written=new HashSet<>();
            while(!ForgeBridge.finished) {
                try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&r.kind.equals("InputPassPriority")&&written.add(r.requestId)) {
                            List<Object> players=new ArrayList<>();
                            for(Player p:ForgeBridge.game.getPlayers()) {
                                List<Object> library=new ArrayList<>();
                                for(Card c:p.getCardsIn(ZoneType.Library))library.add(Map.of("id",c.getId(),"name",c.getName()));
                                players.add(Map.of("library",library));
                            }
                            Path target=Path.of(ForgeBridge.config.get("auditDir").getAsString(),r.requestId+".json");
                            Path temp=target.resolveSibling(target.getFileName()+".tmp");
                            Files.writeString(temp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players)));
                            Files.move(temp,target,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(10);
                }catch(Exception e){throw new RuntimeException(e);}
            }
        },"digital-read-only-proof");observer.setDaemon(true);observer.start();
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0);
            String test=ForgeBridge.config.get("fixture").getAsString();
            for(String land:new String[]{"Island","Mountain","Swamp","Plains","Forest"})for(int i=0;i<4;i++)place(land,one,ZoneType.Battlefield);
            if(test.startsWith("conjure")) {
                place("Izzet Polarizer",one,ZoneType.Battlefield);place("Shock",one,ZoneType.Hand);
            }else if(test.startsWith("draft")) {
                place("Xander's Wake",one,ZoneType.Battlefield);
                place("Grizzly Bears",one,ZoneType.Battlefield);place("Runeclaw Bear",one,ZoneType.Battlefield);
                place("Shock",one,ZoneType.Hand);place("Shock",one,ZoneType.Hand);
            }else if(test.startsWith("seek")) {
                place("Ureni's Counsel",one,ZoneType.Hand);
                if(!test.endsWith("empty")) {place("Shivan Dragon",one,ZoneType.Library);place("Dragon Whelp",one,ZoneType.Library);}
                place("Opt",one,ZoneType.Library);
            }else if(test.startsWith("perpetual")) {
                place("Hypnotic Pattern",one,ZoneType.Hand);place("Unsummon",one,ZoneType.Hand);
                place("Grizzly Bears",one,ZoneType.Battlefield);place("Runeclaw Bear",one,ZoneType.Battlefield);
            }else throw new IllegalArgumentException(test);
            ForgeBridge.game.getAction().checkStaticAbilities();observe();
        });
    }
}
