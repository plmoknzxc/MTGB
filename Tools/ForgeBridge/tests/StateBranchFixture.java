package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.nio.file.*;
import java.util.*;

/** Initial positions only; native spells, phase triggers and choices produce results. */
public final class StateBranchFixture {
    static Card place(String name,Player p,ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException(name);
        Card c=Card.fromPaperCard(paper,p);p.getZone(zone).add(c);
        c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    static void setup(Player p,com.google.gson.JsonObject spec) {
        for(Card c:new ArrayList<>(p.getCardsIn(ZoneType.Hand))) {
            p.getZone(ZoneType.Hand).remove(c);p.getZone(ZoneType.Library).add(c);
        }
        for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile})
            if(spec.has(z.name()))for(var n:spec.getAsJsonArray(z.name()))place(n.getAsString(),p,z);
        if(spec.has("Library")) {
            var names=spec.getAsJsonArray("Library");
            for(int i=names.size()-1;i>=0;i--) {
                Card c=place(names.get(i).getAsString(),p,ZoneType.Library);
                p.getZone(ZoneType.Library).remove(c);p.getZone(ZoneType.Library).add(c,0);
            }
        }
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            int actor=ForgeBridge.config.get("actor").getAsInt();
            Player own=ForgeBridge.game.getPlayers().get(actor-1),other=ForgeBridge.game.getPlayers().get(2-actor);
            for(String land:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int i=0;i<6;i++)place(land,own,ZoneType.Battlefield);
            setup(own,ForgeBridge.config.getAsJsonObject("own"));setup(other,ForgeBridge.config.getAsJsonObject("other"));
            ForgeBridge.game.getAction().checkStaticAbilities();
            Thread observer=new Thread(()->{
                Set<Long> saved=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&saved.add(r.requestId)) {
                            List<Object> cards=new ArrayList<>();
                            for(int seat=1;seat<=2;seat++)for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile})
                                for(Card c:ForgeBridge.game.getPlayers().get(seat-1).getCardsIn(z)) {
                                    Map<String,Object> item=new LinkedHashMap<>();item.put("id",c.getId());item.put("name",c.getName());item.put("seat",seat);item.put("zone",z.name());item.put("intensity",c.getIntensity(true));item.put("addedIntensity",c.getIntensity(false));cards.add(item);
                                }
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"cards",cards)));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"state-branch-read-only-audit");observer.setDaemon(true);observer.start();
        });
    }
}
