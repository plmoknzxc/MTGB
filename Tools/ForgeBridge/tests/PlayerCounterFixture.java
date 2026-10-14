package mtgb.bridge;
import forge.game.GameEntity;
import forge.game.card.CounterType;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;
/** Initial positions and read-only observations only. Outcomes use real native operations. */
public final class PlayerCounterFixture {
 static Map<String,Integer> counters(GameEntity e){Map<String,Integer> result=new TreeMap<>();for(var c:e.getCounters().entrySet())result.put(c.getElement().toString(),c.getCount());return result;}
 public static void main(String[] args)throws Exception{
  ForgeBridge.run(args,()->{
   Player own=ForgeBridge.game.getRegisteredPlayers().get(0),other=ForgeBridge.game.getRegisteredPlayers().get(1);
   for(String land:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int i=0;i<10;i++)RoyalStateFixture.place(land,own,ZoneType.Battlefield);
   RoyalStateFixture.setup(own,ForgeBridge.config.getAsJsonObject("own"));RoyalStateFixture.setup(other,ForgeBridge.config.getAsJsonObject("other"));
   if(ForgeBridge.config.has("bearCounters"))for(var c:own.getCardsIn(ZoneType.Battlefield))if(c.getName().equals("Grizzly Bears")){c.setCounters(CounterType.getType("P1P1"),1);c.setCounters(CounterType.getType("CHARGE"),2);}
   if(ForgeBridge.config.has("playerMultitype")){own.setCounters(CounterType.getType("ENERGY"),2);own.setCounters(CounterType.getType("POISON"),1);other.setCounters(CounterType.getType("ENERGY"),1);other.setCounters(CounterType.getType("POISON"),1);}
   ForgeBridge.game.getAction().checkStaticAbilities();Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString());
   Thread observer=new Thread(()->{
    Set<Long> saved=new HashSet<>();while(!ForgeBridge.finished)try{
     synchronized(ForgeBridge.class){var pending=ForgeBridge.pending;if(pending!=null&&!pending.consumed&&saved.add(pending.requestId)){
      List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getRegisteredPlayers()){
       List<Object> cards=new ArrayList<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Command})for(var c:p.getCardsIn(z))cards.add(Map.of("id",c.getId(),"name",c.getName(),"zone",z.name(),"timestamp",c.getGameTimestamp(),"tapped",c.isTapped(),"counters",counters(c),"power",c.getNetPower(),"toughness",c.getNetToughness()));
       players.add(Map.of("seat",ForgeBridge.game.getRegisteredPlayers().indexOf(p)+1,"life",p.getLife(),"counters",counters(p),"cards",cards));
      }
      Path dst=dir.resolve(pending.requestId+".json"),tmp=dir.resolve(pending.requestId+".tmp");Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",pending.requestId,"kind",pending.kind,"seat",pending.seat,"playerSeat",pending.playerSeat,"players",players)));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
     }}Thread.sleep(5);
    }catch(Exception e){throw new RuntimeException(e);}
   },"player-counter-read-only-audit");observer.setDaemon(true);observer.start();
  });
 }
}
