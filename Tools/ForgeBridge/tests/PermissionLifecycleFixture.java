package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Opening positions and read-only permission audit. No result or phase mutation. */
public final class PermissionLifecycleFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++) {
                Player player=ForgeBridge.game.getPlayers().get(seat-1);
                for(String land:List.of("Plains","Island","Swamp","Mountain","Forest"))
                    for(int i=0;i<6;i++)StateBranchFixture.place(land,player,ZoneType.Battlefield);
                StateBranchFixture.setup(player,ForgeBridge.config.getAsJsonObject(seat==1?"own":"other"));
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            Thread observer=new Thread(()->{
                Set<Long> saved=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&saved.add(r.requestId)) {
                            List<Object> cards=new ArrayList<>();
                            for(Card c:ForgeBridge.game.getCardsInGame()) {
                                List<Integer> allowed=new ArrayList<>(),known=new ArrayList<>();
                                for(Player p:ForgeBridge.game.getPlayers()) {
                                    int seat=ForgeBridge.game.getPlayers().indexOf(p)+1;
                                    if(c.mayPlay(p).stream().anyMatch(o->o.grantsZonePermissions()))allowed.add(seat);
                                    if(c.getView().canBeShownTo(p.getView()) || c.isFaceDown()&&c.getView().canFaceDownBeShownTo(p.getView()))known.add(seat);
                                }
                                cards.add(Map.of("id",c.getId(),"name",c.getName(),"zone",c.getZone().getZoneType().name(),"owner",ForgeBridge.game.getPlayers().indexOf(c.getOwner())+1,"controller",ForgeBridge.game.getPlayers().indexOf(c.getController())+1,"faceDown",c.isFaceDown(),"permissionSeats",allowed,"knownSeats",known));
                            }
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"cards",cards)));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"permission-read-only-audit");observer.setDaemon(true);observer.start();
        });
    }
}
