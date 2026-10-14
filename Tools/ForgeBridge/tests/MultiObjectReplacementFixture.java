package mtgb.bridge;
import forge.game.card.*;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.card.CardStateName;
import java.nio.file.*;
import java.util.*;
/** Opening state and read-only observations; all changes after opening use native actions. */
public final class MultiObjectReplacementFixture {
 static Map<String,Integer> counters(Card c){Map<String,Integer> out=new TreeMap<>();for(var e:c.getCounters().entrySet())out.put(e.getElement().toString(),e.getCount());return out;}
 public static void main(String[] args)throws Exception{
  ForgeBridge.run(args,()->{
   Player own=ForgeBridge.game.getRegisteredPlayers().get(0),other=ForgeBridge.game.getRegisteredPlayers().get(1);
   for(String land:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int i=0;i<8;i++)RoyalStateFixture.place(land,own,ZoneType.Battlefield);
   RoyalStateFixture.setup(own,ForgeBridge.config.getAsJsonObject("own"));RoyalStateFixture.setup(other,ForgeBridge.config.getAsJsonObject("other"));
   if(ForgeBridge.config.get("session").getAsString().startsWith("spark"))for(Card c:own.getCardsIn(ZoneType.Battlefield)){
    if(c.getName().equals("Grizzly Bears"))c.setCounters(CounterType.getType("P1P1"),7);
    if(c.getName().equals("Jace Beleren"))c.setCounters(CounterType.getType("LOYALTY"),8);
   }
   ForgeBridge.game.getAction().checkStaticAbilities();Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString());
   Thread observer=new Thread(()->{
    Set<Long> saved=new HashSet<>();while(!ForgeBridge.finished)try{
     synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&saved.add(r.requestId)){
      List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getRegisteredPlayers()){
       List<Object> cards=new ArrayList<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Command})for(Card c:p.getCardsIn(z)){
        Map<String,Object> o=new LinkedHashMap<>();o.put("id",c.getId());o.put("name",c.getName());o.put("originalName",c.getOriginalState(CardStateName.Original).getName());o.put("zone",z.name());o.put("timestamp",c.getGameTimestamp());o.put("tapped",c.isTapped());o.put("counters",counters(c));o.put("power",c.getNetPower());o.put("toughness",c.getNetToughness());o.put("damage",c.getDamage());o.put("type",c.getType().toString());o.put("cloned",c.isCloned());o.put("cloneOrigin",c.getCloneOrigin()==null?-1:c.getCloneOrigin().getId());cards.add(o);
       }
       players.add(Map.of("seat",ForgeBridge.game.getRegisteredPlayers().indexOf(p)+1,"life",p.getLife(),"cards",cards));
      }
      Path dst=dir.resolve(r.requestId+".json"),tmp=dir.resolve(r.requestId+".tmp");Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"kind",r.kind,"seat",r.seat,"playerSeat",r.playerSeat,"players",players)));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
     }}Thread.sleep(5);
    }catch(Exception e){throw new RuntimeException(e);}
   },"multi-object-read-only-audit");observer.setDaemon(true);observer.start();
  });
 }
}
