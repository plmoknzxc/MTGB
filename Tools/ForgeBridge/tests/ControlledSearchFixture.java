package mtgb.bridge;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;
/** Opening positions and read-only control/zone observations; no outcome setters. */
public final class ControlledSearchFixture {
 public static void main(String[] args)throws Exception {
  ForgeBridge.run(args,()->{
   Player own=ForgeBridge.game.getRegisteredPlayers().get(0),other=ForgeBridge.game.getRegisteredPlayers().get(1);
   for(String land:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int i=0;i<6;i++)RoyalStateFixture.place(land,own,ZoneType.Battlefield);
   RoyalStateFixture.setup(own,ForgeBridge.config.getAsJsonObject("own"));RoyalStateFixture.setup(other,ForgeBridge.config.getAsJsonObject("other"));
   if(ForgeBridge.config.has("tapOther"))for(var c:other.getCardsIn(ZoneType.Battlefield))if(c.getName().equals(ForgeBridge.config.get("tapOther").getAsString()))c.setTapped(true);
   ForgeBridge.game.getAction().checkStaticAbilities();
   Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString());
   Thread observer=new Thread(()->{
    Set<Long> saved=new HashSet<>();
    while(!ForgeBridge.finished)try{
     synchronized(ForgeBridge.class){
      var pending=ForgeBridge.pending;
      if(pending!=null&&!pending.consumed&&saved.add(pending.requestId)){
       List<Object> players=new ArrayList<>();
       for(Player p:ForgeBridge.game.getRegisteredPlayers()){
        var search=p.getControlledWhileSearching();List<Object> cards=new ArrayList<>();
        for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile})for(var c:p.getCardsIn(z))cards.add(Map.of("id",c.getId(),"name",c.getName(),"zone",z.name(),"tapped",c.isTapped()));
        var controllers=Player.class.getDeclaredField("controlledBy");controllers.setAccessible(true);
        players.add(Map.of("seat",ForgeBridge.game.getRegisteredPlayers().indexOf(p)+1,"controllingSeat",p.getControllingPlayer()==null?0:ForgeBridge.game.getRegisteredPlayers().indexOf(p.getControllingPlayer())+1,"searchControllerSeat",search==null?0:ForgeBridge.game.getRegisteredPlayers().indexOf(search.getValue())+1,"searchTimestamp",search==null?0:search.getKey(),"controllerEntries",((Map<?,?>)controllers.get(p)).size(),"cards",cards));
       }
       Path dst=dir.resolve(pending.requestId+".json"),tmp=dir.resolve(pending.requestId+".tmp");Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",pending.requestId,"kind",pending.kind,"seat",pending.seat,"playerSeat",pending.playerSeat,"players",players)));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
      }
     }
     Thread.sleep(5);
    }catch(Exception e){throw new RuntimeException(e);}
   },"controlled-search-read-only-probe");observer.setDaemon(true);observer.start();
  });
 }
}
