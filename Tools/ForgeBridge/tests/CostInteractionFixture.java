package mtgb.bridge;

import forge.game.card.Card;
import forge.game.card.CounterType;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.nio.file.*;
import java.util.*;

/** Setup only: every cost choice, resource payment, cast and resolution is native. */
public final class CostInteractionFixture {
    static Card place(String name, Player p, ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException("Unknown fixture card: "+name);
        Card c=Card.fromPaperCard(paper,p);
        p.getZone(zone).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player own=ForgeBridge.game.getPlayers().get(ForgeBridge.config.get("firstPlayerSeat").getAsInt()-1);
            Player other=ForgeBridge.game.getPlayers().get(own==ForgeBridge.game.getPlayers().get(0)?1:0);
            var spec=ForgeBridge.config.getAsJsonObject("spec");
            if(spec.has("chooseCostOrder") && spec.get("chooseCostOrder").getAsBoolean())
                own.getController().getFullControl().add(forge.game.player.PlayerController.FullControlFlag.ChooseCostOrder);
            if(spec.has("manualManaPayment") && spec.get("manualManaPayment").getAsBoolean())
                own.getController().getFullControl().add(forge.game.player.PlayerController.FullControlFlag.NoPaymentFromManaAbility);
            if(spec.has("initialLife"))own.setLife(spec.get("initialLife").getAsInt(),null);
            if(spec.has("energy"))own.setCounters(CounterType.getType("ENERGY"),spec.get("energy").getAsInt());
            for(String key:new String[]{"hand","field","enemy","grave","library"})if(spec.has(key))for(var n:spec.getAsJsonArray(key)){
                Card c=place(n.getAsString(),key.equals("enemy")?other:own,key.equals("hand")?ZoneType.Hand:key.equals("grave")?ZoneType.Graveyard:key.equals("library")?ZoneType.Library:ZoneType.Battlefield);
                if(key.equals("library")) {own.getZone(ZoneType.Library).remove(c);own.getZone(ZoneType.Library).add(c,0);}
                if(spec.has("tapped")&&c.getName().equals(spec.get("tapped").getAsString()))c.setTapped(true);
                if(spec.has("initialLoyalty") && c.isPlaneswalker())c.setCounters(CounterType.getType("LOYALTY"),spec.get("initialLoyalty").getAsInt());
                if(spec.has("counterCard") && c.getName().equals(spec.get("counterCard").getAsString()))
                    c.setCounters(CounterType.getType(spec.get("counterType").getAsString()),spec.get("counterAmount").getAsInt());
            }
            if(spec.has("commander")) {
                Card c=place(spec.get("commander").getAsString(),own,ZoneType.Command);own.addCommander(c);
                for(int i=0;i<spec.get("priorCasts").getAsInt();i++)own.incCommanderCast(c);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            // Read-only native payment evidence, including lands sacrificed after
            // producing mana. Surviving tapped lands alone cannot prove this cost.
            Thread observer=new Thread(()->{
                Set<Long> saved=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&r.kind.equals("InputPassPriority")&&saved.add(r.requestId)) {
                            List<Object> entries=new ArrayList<>();
                            for(var item:ForgeBridge.game.getStack()) {
                                var sa=item.getSpellAbility();
                                entries.add(Map.of("card",sa.getHostCard().getName(),"id",sa.getHostCard().getId(),"kind",sa.isSpell()?"spell":"ability","mana",sa.getPayingMana().size()));
                            }
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString());
                            Path dst=dir.resolve(r.requestId+".json"),tmp=dir.resolve(r.requestId+".tmp");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(entries));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"native-payment-audit");observer.setDaemon(true);observer.start();
        });
    }
}
