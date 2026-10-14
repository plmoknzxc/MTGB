package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.nio.file.*;
import java.util.*;

/** Setup and read-only audit; discover, casting, choices and zones run natively. */
public final class DiscoverFixture {
    static Card place(String name,Player p,ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException(name);
        Card c=Card.fromPaperCard(paper,p);p.getZone(zone).add(c);
        c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            var spec=ForgeBridge.config.getAsJsonObject("spec");
            Player own=ForgeBridge.game.getPlayers().get(ForgeBridge.config.get("firstPlayerSeat").getAsInt()-1);
            for(int i=0;i<12;i++)place("Mountain",own,ZoneType.Battlefield);
            for(String key:new String[]{"hand","field"})for(var n:spec.getAsJsonArray(key))
                place(n.getAsString(),own,key.equals("hand")?ZoneType.Hand:ZoneType.Battlefield);
            var top=spec.getAsJsonArray("top");
            for(int i=top.size()-1;i>=0;i--) {
                Card c=place(top.get(i).getAsString(),own,ZoneType.Library);
                own.getZone(ZoneType.Library).remove(c);own.getZone(ZoneType.Library).add(c,0);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            Thread observer=new Thread(()->{
                Set<Long> saved=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&saved.add(r.requestId)) {
                            List<Object> players=new ArrayList<>(),stack=new ArrayList<>();
                            for(Player p:ForgeBridge.game.getPlayers()) {
                                List<Object> library=new ArrayList<>();
                                for(Card c:p.getCardsIn(ZoneType.Library))library.add(Map.of("id",c.getId(),"name",c.getName(),"land",c.isLand(),"mv",c.getCMC()));
                                players.add(Map.of("library",library));
                            }
                            for(var item:ForgeBridge.game.getStack()) {
                                var sa=item.getSpellAbility();
                                stack.add(Map.of("id",sa.getHostCard().getId(),"name",sa.getHostCard().getName(),"spell",sa.isSpell(),"mana",sa.getPayingMana().size(),"x",sa.getHostCard().getXManaCostPaid()));
                            }
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString());
                            Path tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",stack)));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception error){throw new RuntimeException(error);}
            },"discover-read-only-audit");observer.setDaemon(true);observer.start();
        });
    }
}
