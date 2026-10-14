package mtgb.bridge;

import forge.game.card.Card;
import forge.game.card.CounterType;
import forge.game.keyword.Keyword;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Opening positions only; abilities and spells execute normally. Observer is read-only. */
public final class LevelThresholdFixture {
    static Map<String,Object> card(Card c) {
        Map<String,Object> m=new LinkedHashMap<>();
        m.put("id",c.getId());m.put("name",c.getName());m.put("zone",c.getZone().getZoneType().name());
        m.put("timestamp",c.getGameTimestamp());m.put("tapped",c.isTapped());
        m.put("power",c.getNetPower());m.put("toughness",c.getNetToughness());
        m.put("basePower",c.getBasePower());m.put("baseToughness",c.getBaseToughness());
        m.put("currentPower",c.getCurrentPower());m.put("currentToughness",c.getCurrentToughness());
        m.put("level",c.getCounters(CounterType.getType("LEVEL")));m.put("flying",c.hasKeyword(Keyword.FLYING));
        m.put("abilities",c.getSpellAbilities().stream().map(sa->Map.of("description",sa.getDescription(),"levelUp",sa.hasParam("SorcerySpeed")&&"LEVEL".equals(sa.getParam("CounterType")),"canPlay",sa.canPlay())).toList());
        return m;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++) {
                Player p=ForgeBridge.game.getPlayers().get(seat-1);
                for(int n=0;n<10;n++)StateBranchFixture.place("Island",p,ZoneType.Battlefield);
                for(int n=0;n<8;n++)StateBranchFixture.place("Forest",p,ZoneType.Battlefield);
                StateBranchFixture.setup(p,ForgeBridge.config.getAsJsonObject(seat==1?"own":"other"));
                if(seat==1&&ForgeBridge.config.has("initialLevel"))for(Card c:p.getCardsIn(ZoneType.Battlefield))if(c.getName().equals("Coralhelm Commander"))c.setCounters(CounterType.getType("LEVEL"),ForgeBridge.config.get("initialLevel").getAsInt());
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            Thread observer=new Thread(()->{Set<Long> saved=new HashSet<>();while(!ForgeBridge.finished)try{
                synchronized(ForgeBridge.class){var req=ForgeBridge.pending;if(req!=null&&!req.consumed&&saved.add(req.requestId)){
                    List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()){
                        Map<String,Object> m=new LinkedHashMap<>();m.put("life",p.getLife());m.put("cards",p.getAllCards().stream().map(LevelThresholdFixture::card).toList());m.put("library",p.getCardsIn(ZoneType.Library).stream().map(Card::getId).toList());players.add(m);
                    }
                    Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(req.requestId+".tmp"),dst=d.resolve(req.requestId+".json");
                    Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",req.requestId,"players",players)));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                }}Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}},"level-threshold-read-only");observer.setDaemon(true);observer.start();
        });
    }
}
