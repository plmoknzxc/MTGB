package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.nio.file.*;
import java.util.*;

/** Setup plus read-only private-zone proofs. All choices and effects run in Forge. */
public final class ResolutionReviewFixture {
    static Card place(String name,Player owner,ZoneType zone) {
        Card c=Card.fromPaperCard(Objects.requireNonNull(FModel.getMagicDb().getCommonCards().getCard(name),name),owner);
        owner.getZone(zone).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);
        if(c.isPlaneswalker())c.setCounters(forge.game.card.CounterType.getType("LOYALTY"),3);
        return c;
    }
    static void observe() {
        Thread t=new Thread(()->{
            Set<Long> done=new HashSet<>();
            while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class) {
                    var r=ForgeBridge.pending;
                    if(r!=null&&!r.consumed&&r.kind.equals("InputPassPriority")&&done.add(r.requestId)) {
                        List<Object> players=new ArrayList<>();
                        for(Player p:ForgeBridge.game.getPlayers()) {
                            List<Object> library=new ArrayList<>();for(Card c:p.getCardsIn(ZoneType.Library))library.add(Map.of("id",c.getId(),"name",c.getName()));
                            players.add(Map.of("library",library));
                        }
                        Map<String,Object> record=new LinkedHashMap<>();record.put("requestId",r.requestId);record.put("players",players);
                        if(ForgeBridge.config.get("fixture").getAsString().equals("explore-shuffle")&&ForgeBridge.game.getStack().isEmpty()) {
                            Player one=ForgeBridge.game.getPlayers().get(0);var top=ForgeBridge.libraryKnowledge.top(one,1,one);
                            if(top.containsKey("libraryTop")&&one.getCardsIn(ZoneType.Battlefield).stream().anyMatch(c->c.getName().equals("Merfolk Branchwalker"))) {
                                int id=one.getCardsIn(ZoneType.Library).get(0).getId();
                                ForgeBridge.libraryKnowledge.shuffle(new forge.game.event.GameEventShuffle(one.getView()));
                                boolean hidden=ForgeBridge.libraryKnowledge.top(one,1,one).isEmpty();
                                record.put("shuffleAdapter",Map.of("sameTopId",id==one.getCardsIn(ZoneType.Library).get(0).getId(),"cleared",hidden));
                            }
                        }
                        Path target=Path.of(ForgeBridge.config.get("auditDir").getAsString(),r.requestId+".json"),tmp=target.resolveSibling(target.getFileName()+".tmp");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(record));Files.move(tmp,target,StandardCopyOption.REPLACE_EXISTING);
                    }
                }
                Thread.sleep(10);
            }catch(Exception e){throw new RuntimeException(e);}
        },"resolution-read-only-proof");t.setDaemon(true);t.start();
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
            String test=ForgeBridge.config.get("fixture").getAsString();
            for(int i=0;i<4;i++){place("Island",one,ZoneType.Battlefield);place("Forest",one,ZoneType.Battlefield);place("Plains",one,ZoneType.Battlefield);}
            String spell=test.startsWith("growth")?"Growth Spiral":test.startsWith("genesis")?"Planar Genesis":test.startsWith("brain")?"Brainsurge":test.startsWith("wrath")?"Wrath of the Skies":"Merfolk Branchwalker";
            place(spell,one,ZoneType.Hand);
            if(test.equals("explore-draw"))place("Opt",one,ZoneType.Hand);
            if(test.startsWith("growth"))place("Forest",one,ZoneType.Hand);
            if(test.equals("growth-many-yes"))for(int i=0;i<3;i++)place("Forest",one,ZoneType.Hand);
            if(test.startsWith("wrath")) {
                for(String n:new String[]{"Ornithopter","Grizzly Bears","Glorious Anthem","Hill Giant","Jace Beleren"})place(n,two,ZoneType.Battlefield);
                place("Tormod's Crypt",one,ZoneType.Battlefield);
            }
            String[] top=test.startsWith("genesis")?new String[]{"Forest","Grizzly Bears","Lightning Bolt","Island"}:new String[]{"Lightning Bolt","Grizzly Bears","Island","Forest"};
            for(int i=top.length-1;i>=0;i--)ForgeBridge.game.getAction().moveToLibrary(place(top[i],one,ZoneType.Hand),0,null);
            ForgeBridge.game.getAction().checkStaticAbilities();observe();
        });
    }
}
