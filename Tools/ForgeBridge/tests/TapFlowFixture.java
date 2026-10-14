package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;
/** Initial positions only. The observer never resolves or modifies an effect. */
public final class TapFlowFixture {
 static Map<String,Object> card(Card c){
  List<String> remembered=new ArrayList<>();for(Object x:c.getRemembered())remembered.add(x.toString());
  Map<String,Object> m=new LinkedHashMap<>();m.put("id",c.getId());m.put("name",c.getName());m.put("zone",c.getZone().getZoneType().name());m.put("timestamp",c.getGameTimestamp());m.put("tapped",c.isTapped());m.put("remembered",remembered);m.put("counters",c.getCounters().toString());return m;
 }
 public static void main(String[] args)throws Exception{
  ForgeBridge.run(args,()->{
   for(int s=1;s<=2;s++){
    Player p=ForgeBridge.game.getPlayers().get(s-1);
    for(String name:new String[]{"Island","Plains","Forest"})for(int j=0;j<10;j++)StateBranchFixture.place(name,p,ZoneType.Battlefield);
    var spec=ForgeBridge.config.getAsJsonObject(s==1?"own":"other");StateBranchFixture.setup(p,spec);
    if(spec.has("tapped"))for(var n:spec.getAsJsonArray("tapped"))for(Card c:p.getCardsIn(ZoneType.Battlefield))if(c.getName().equals(n.getAsString()))c.setTapped(true);
   }
   ForgeBridge.game.getAction().checkStaticAbilities();
   Thread t=new Thread(()->{Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try{
    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&seen.add(r.requestId)){
     List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers())players.add(Map.of("life",p.getLife(),"cards",p.getAllCards().stream().map(TapFlowFixture::card).toList(),"library",p.getCardsIn(ZoneType.Library).stream().map(Card::getId).toList()));
     Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(r.requestId+".tmp"),dst=d.resolve(r.requestId+".json");Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("players",players,"requestId",r.requestId)));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
    }}Thread.sleep(5);
   }catch(Exception e){throw new RuntimeException(e);}},"tap-flow-read-only");t.setDaemon(true);t.start();
  });
 }
}
