package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Existing setup fixture plus read-only native attachment observations. */
public final class AttachmentFixture {
    public static void main(String[] args)throws Exception {
        Thread observer=new Thread(()->{
            Set<Long> saved=new HashSet<>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    var r=ForgeBridge.pending;
                    if(r!=null&&!r.consumed&&r.kind.equals("InputPassPriority")&&saved.add(r.requestId)) {
                        List<Object> cards=new ArrayList<>();
                        for(Player p:ForgeBridge.game.getPlayers())for(Card c:p.getCardsIn(ZoneType.Battlefield)) {
                            var target=c.getEntityAttachedTo();Map<String,Object> item=new LinkedHashMap<>();
                            item.put("id",c.getId());item.put("name",c.getName());
                            item.put("controllerSeat",ForgeBridge.game.getPlayers().indexOf(c.getController())+1);
                            item.put("ownerSeat",ForgeBridge.game.getPlayers().indexOf(c.getOwner())+1);
                            item.put("attachedToKind",target instanceof Card?"card":target instanceof Player?"player":"");
                            item.put("attachedToId",target==null?0:target.getId());
                            item.put("attachedByIds",c.getAllAttachedCards().stream().map(Card::getId).toList());
                            cards.add(item);
                        }
                        Path dir=Path.of(ForgeBridge.config.get("relationsDir").getAsString());
                        Path dst=dir.resolve(r.requestId+".json"),tmp=dir.resolve(r.requestId+".tmp");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(cards));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                    }
                }
                Thread.sleep(5);
            }catch(Exception error){throw new RuntimeException(error);}
        },"attachment-read-only-audit");observer.setDaemon(true);observer.start();
        CostInteractionFixture.main(args);
    }
}
