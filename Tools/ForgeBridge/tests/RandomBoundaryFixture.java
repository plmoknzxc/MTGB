package mtgb.bridge;

import forge.game.card.Card;
import forge.game.card.CounterType;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Initial positions only. Roll records and modification limits are read-only proof. */
public final class RandomBoundaryFixture {
    static Card place(String name,Player p,ZoneType zone){return RandomMechanicFixture.place(name,p,zone);}
    static void observeLimits() {
        Thread t=new Thread(()->{
            Set<Long> done=new HashSet<>();
            while(!ForgeBridge.finished) {
                try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&r.kind.equals("InputPassPriority")&&done.add(r.requestId)) {
                            List<Object> cards=new ArrayList<>();
                            for(Player p:ForgeBridge.game.getPlayers())for(Card c:p.getCardsIn(ZoneType.Battlefield))
                                if(c.hasSVar("ModsThisTurn"))cards.add(Map.of("id",c.getId(),"name",c.getName(),"mods",c.getSVar("ModsThisTurn")));
                            Path target=Path.of(ForgeBridge.config.get("auditDir").getAsString(),"limits-"+r.requestId+".json");
                            Path tmp=target.resolveSibling(target.getFileName()+".tmp");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"cards",cards)));
                            Files.move(tmp,target,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(10);
                }catch(Exception e){throw new RuntimeException(e);}
            }
        },"random-limit-read-only-proof");t.setDaemon(true);t.start();
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0);
            String test=ForgeBridge.config.get("fixture").getAsString();
            if(test.equals("monitor-no-mana"))for(int i=0;i<4;i++)place("Island",one,ZoneType.Battlefield);
            else for(String land:new String[]{"Island","Mountain","Swamp","Plains","Forest"})for(int i=0;i<4;i++)place(land,one,ZoneType.Battlefield);
            if(test.equals("swap-duration")) {
                Card c=place("Vedalken Squirrel-Whacker",one,ZoneType.Battlefield);
                c.addNewPT(4,5,ForgeBridge.game.getNextTimestamp(),0);
                place("Ebony Fly",one,ZoneType.Battlefield);place("Unsummon",one,ZoneType.Hand);
            } else {
                int spells=test.endsWith("limit")?3:1;
                for(int i=0;i<spells;i++)place("Contact Other Plane",one,ZoneType.Hand);
                if(test.startsWith("monitor"))place("Monitor Monitor",one,ZoneType.Battlefield);
                if(test.startsWith("night"))place("Night Shift of the Living Dead",one,ZoneType.Battlefield);
                if(test.equals("night-no-pay"))place("Platinum Emperion",one,ZoneType.Battlefield);
                if(test.equals("xeno-last"))place("Xenosquirrels",one,ZoneType.Battlefield).setCounters(CounterType.getType("P1P1"),1);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();RandomMechanicFixture.observe();observeLimits();
        });
    }
}
