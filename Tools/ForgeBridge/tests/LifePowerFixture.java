package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;
/** Initial life/positions only. All outcome and payment produced by real native operations. */
public final class LifePowerFixture {
    static Map<String,Object> card(Card c) {
        Map<String,Object> m=CombatRelationFixture.card(c);
        m.put("currentPower",c.getCurrentPower());m.put("currentToughness",c.getCurrentToughness());
        m.put("plusCounters",c.getCounters(forge.game.card.CounterType.getType("P1P1")));
        List<Integer> values=new ArrayList<>();for(Object x:c.getRemembered())if(x instanceof Integer)values.add((Integer)x);
        m.put("rememberedNumbers",values);return m;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++) {
                Player p=ForgeBridge.game.getPlayers().get(seat-1);
                p.setLife(ForgeBridge.config.getAsJsonArray("initialLives").get(seat-1).getAsInt(),null);p.setLifeLostThisTurn(0);
                for(String land:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int j=0;j<8;j++)StateBranchFixture.place(land,p,ZoneType.Battlefield);
                StateBranchFixture.setup(p,ForgeBridge.config.getAsJsonObject(seat==1?"own":"other"));
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&seen.add(r.requestId)) {
                            List<Object> players=new ArrayList<>();
                            for(Player p:ForgeBridge.game.getPlayers())players.add(Map.of("life",p.getLife(),"lifeLost",p.getLifeLostThisTurn(),"canGain",p.canGainLife(),"canLose",p.canLoseLife(),"cards",p.getAllCards().stream().map(LifePowerFixture::card).toList(),"library",p.getCardsIn(ZoneType.Library).stream().map(Card::getId).toList()));
                            Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(r.requestId+".tmp"),dst=d.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("players",players,"requestId",r.requestId,"stack",CombatRelationFixture.stack(),"blockError","")));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"life-power-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
