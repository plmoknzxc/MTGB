package mtgb.bridge;

import forge.game.ability.AbilityFactory;
import forge.game.card.Card;
import forge.game.card.CardCollection;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.nio.file.*;
import java.util.*;

/** Bridge adapter only: real reveal/owner decisions and native zone/shuffle calls, no spell cast. */
public final class ClashKnowledgeFixture {
    static volatile String stage="setup";
    static final List<Object> checkpoints=new ArrayList<>();
    static Card place(String name,Player p,ZoneType zone){
        Card c=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),p);p.getZone(zone).add(c);c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());return c;
    }
    static Map<String,Object> snapshot(){
        List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()){
            List<Object> library=new ArrayList<>();for(Card c:p.getCardsIn(ZoneType.Library))library.add(Map.of("id",c.getId(),"name",c.getName(),"zone",c.getZone().getZoneType().name(),"owner",c.getOwner().getId()+1,"timestamp",c.getGameTimestamp()));
            players.add(Map.of("seat",p.getId()+1,"library",library,"hand",p.getCardsIn(ZoneType.Hand).stream().map(Card::getId).toList(),"life",p.getLife(),"pool",p.getManaPool().totalMana()));
        }
        return Map.of("stage",stage,"players",players,"views",ForgeBridge.views());
    }
    static void checkpoint(String name)throws Exception{
        stage=name;checkpoints.add(snapshot());Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString());Files.writeString(d.resolve("checkpoints.json"),ForgeBridge.JSON.toJson(checkpoints));
    }
    static void decide(Player p,SpellAbility cause,String label)throws Exception{
        stage=label;Card c=p.getCardsIn(ZoneType.Library).get(0);boolean top=p.getController().willPutCardOnTop(c);
        if(top)ForgeBridge.game.getAction().moveToLibrary(c,cause);else ForgeBridge.game.getAction().moveToBottomOfLibrary(c,cause);
        checkpoint(label+"-after");
    }
    public static void main(String[] args)throws Exception{
        ForgeBridge.run(args,()->{
            try{
                for(int seat=1;seat<=2;seat++){
                    Player p=ForgeBridge.game.getPlayers().get(seat-1);
                    for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library})for(Card c:new ArrayList<>(p.getCardsIn(z)))p.getZone(z).remove(c);
                    for(String name:seat==1?new String[]{"Air Elemental","Plains","Grizzly Bears"}:new String[]{"Serra Angel","Mountain","Runeclaw Bear"})place(name,p,ZoneType.Library);
                }
                Player own=ForgeBridge.game.getPlayers().get(0),other=ForgeBridge.game.getPlayers().get(1);
                Card source=place("Captivating Glance",own,ZoneType.Hand);
                SpellAbility cause=AbilityFactory.getAbility(source.getSVar("CaptiveClash"),source);cause.setActivatingPlayer(own);
                Thread observer=new Thread(()->{
                    Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try{
                        synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&seen.add(r.requestId)){
                            Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(r.requestId+".tmp"),dst=d.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(snapshot()));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }}Thread.sleep(5);
                    }catch(Exception e){throw new RuntimeException(e);}
                },"clash-knowledge-read-only-observer");observer.setDaemon(true);observer.start();
                checkpoint("initial");
                stage="public-reveal";ForgeBridge.game.getAction().revealTo(new CardCollection(List.of(own.getCardsIn(ZoneType.Library).get(0),other.getCardsIn(ZoneType.Library).get(0))),ForgeBridge.game.getPlayers(),"Clash public top adapter",false);
                decide(own,cause,"public-top-1");decide(other,cause,"public-top-2");decide(own,cause,"public-bottom-1");
                own.shuffle(cause);other.shuffle(cause);checkpoint("public-shuffle-after");
                stage="private-reveal-1";ForgeBridge.game.getAction().revealTo(new CardCollection(own.getCardsIn(ZoneType.Library).get(0)),List.of(own),"Clash private top adapter",false);
                decide(own,cause,"private-top-1");
                stage="private-reveal-2";ForgeBridge.game.getAction().revealTo(new CardCollection(other.getCardsIn(ZoneType.Library).get(0)),List.of(other),"Clash private top adapter",false);
                decide(other,cause,"private-top-2");decide(own,cause,"private-bottom-1");
                other.shuffle(cause);checkpoint("private-shuffle-after");stage="native-continuation";
            }catch(Exception e){throw new RuntimeException(e);}
        });
    }
}
