package mtgb.bridge;

import forge.game.card.Card;
import forge.game.card.CounterEnumType;
import forge.game.card.CounterType;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import com.google.common.collect.HashMultiset;
import java.nio.file.*;
import java.util.*;

/** Only initial positions and read-only proof. Random outcomes are never set by the fixture. */
public final class RandomMechanicFixture {
    static Card place(String name,Player player,ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException(name);
        Card card=Card.fromPaperCard(paper,player);player.getZone(zone).add(card);
        card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());card.setSickness(false);return card;
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
                            for(Player player:ForgeBridge.game.getPlayers()) {
                                List<Integer> library=new ArrayList<>();for(Card card:player.getCardsIn(ZoneType.Library))library.add(card.getId());
                                players.add(Map.of("library",library,"rolls",new ArrayList<>(player.getDiceRollsThisTurn()),
                                    "rolled",player.getNumRollsThisTurn(),"flipped",player.getNumFlipsThisTurn()));
                            }
                            Path target=Path.of(ForgeBridge.config.get("auditDir").getAsString(),r.requestId+".json"),temp=target.resolveSibling(target.getFileName()+".tmp");
                            Files.writeString(temp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players)));
                            Files.move(temp,target,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(10);
                }catch(Exception error){throw new RuntimeException(error);}
            }
        },"random-read-only-proof");observer.setDaemon(true);observer.start();
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
            String test=ForgeBridge.config.get("fixture").getAsString();
            for(String land:new String[]{"Island","Mountain","Swamp","Plains","Forest"})for(int i=0;i<4;i++)place(land,one,ZoneType.Battlefield);
            if(test.startsWith("notice")) {
                Card source=place("Contact Other Plane",one,ZoneType.Hand);
                if(test.equals("notice-private")) {
                    ForgeBridge.game.getAction().notifyOfValue(source.getFirstSpellAbility(),one,"Owner-only value",two);
                    ForgeBridge.game.getAction().notifyOfValue(source.getFirstSpellAbility(),one,"Other recipient value",one);
                }else for(int i=0;i<20;i++)ForgeBridge.game.getAction().notifyOfValue(source.getFirstSpellAbility(),one,"Private value "+i,two);
            }else if(test.startsWith("coin")||test.startsWith("thumb")) {
                place("Krark, the Thumbless",one,ZoneType.Battlefield);place("Shock",one,ZoneType.Hand);
                if(test.startsWith("thumb"))place("Krark's Thumb",one,ZoneType.Battlefield);
            }else if(test.equals("mana-clash"))place("Mana Clash",one,ZoneType.Hand);
            else if(test.startsWith("swap")) {
                Card whacker=place("Vedalken Squirrel-Whacker",one,ZoneType.Battlefield);
                whacker.addNewPT(4,5,ForgeBridge.game.getNextTimestamp(),0);
                place("Ebony Fly",one,ZoneType.Battlefield);
            }else {
                place("Contact Other Plane",one,ZoneType.Hand);
                if(test.startsWith("pixie"))place("Pixie Guide",one,ZoneType.Battlefield);
                if(test.startsWith("monitor"))place("Monitor Monitor",one,ZoneType.Battlefield);
                if(test.startsWith("xeno")) {
                    Card xeno=place("Xenosquirrels",one,ZoneType.Battlefield);
                    xeno.setCounters(CounterType.getType("P1P1"),2);
                }
                if(test.startsWith("night")) {place("Night Shift of the Living Dead",one,ZoneType.Battlefield);place("Contact Other Plane",one,ZoneType.Hand);}
            }
            ForgeBridge.game.getAction().checkStaticAbilities();observe();
        });
    }
}
