package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.nio.file.*;
import java.util.*;

/** Only initial positions; casts, targets, separation and pile choice stay native. */
public final class PileFixture {
    static void observe() {
        Thread observer=new Thread(()->{
            Set<Long> written=new HashSet<>();
            while(!ForgeBridge.finished) {
                try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&r.kind.equals("InputPassPriority")&&written.add(r.requestId)) {
                            List<Object> players=new ArrayList<>();
                            for(Player player:ForgeBridge.game.getPlayers()) {
                                List<Integer> library=new ArrayList<>();for(Card card:player.getCardsIn(ZoneType.Library))library.add(card.getId());
                                players.add(Map.of("library",library));
                            }
                            Path target=Path.of(ForgeBridge.config.get("auditDir").getAsString(),r.requestId+".json"),temp=target.resolveSibling(target.getFileName()+".tmp");
                            Files.writeString(temp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players)));
                            Files.move(temp,target,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(10);
                }catch(Exception error){throw new RuntimeException(error);}
            }
        },"pile-read-only-proof");observer.setDaemon(true);observer.start();
    }
    static Card place(String name,Player player,ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException(name);
        Card card=Card.fromPaperCard(paper,player);player.getZone(zone).add(card);
        card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());card.setSickness(false);return card;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            String test=ForgeBridge.config.get("fixture").getAsString();
            int actor=ForgeBridge.config.get("firstPlayerSeat").getAsInt()-1;
            Player player=ForgeBridge.game.getPlayers().get(actor),other=ForgeBridge.game.getPlayers().get(1-actor);
            for(int i=0;i<8;i++){place("Island",player,ZoneType.Battlefield);place("Swamp",player,ZoneType.Battlefield);}
            if(test.startsWith("parley")) {
                place("Boneyard Parley",player,ZoneType.Hand);
                for(String name:new String[]{"Grizzly Bears","Runeclaw Bear","Llanowar Elves"})place(name,player,ZoneType.Graveyard);
                for(String name:new String[]{"Shivan Dragon","Dragon Whelp"})place(name,other,ZoneType.Graveyard);
            }else {
                if(test.startsWith("portal"))place("Phyrexian Portal",player,ZoneType.Battlefield);
                else place(test.startsWith("favor")?"Fortune's Favor":"Fact or Fiction",player,ZoneType.Hand);
                for(String name:new String[]{"Shock","Opt","Unsummon","Grizzly Bears","Runeclaw Bear","Llanowar Elves"}) {
                    Card card=place(name,player,ZoneType.Library);player.getZone(ZoneType.Library).remove(card);player.getZone(ZoneType.Library).add(card,0);
                }
            }
            ForgeBridge.game.getAction().checkStaticAbilities();observe();
        });
    }
}
