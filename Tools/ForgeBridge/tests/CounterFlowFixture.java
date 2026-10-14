package mtgb.bridge;
import forge.game.card.Card;
import forge.game.card.CounterType;
import forge.game.card.CounterEnumType;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;
/** Initial positions/counters only. All outcomes use native paid actions and rules. */
public final class CounterFlowFixture {
 static Map<String,Integer> counters(com.google.common.collect.Multiset<CounterType> cs){
  Map<String,Integer> out=new TreeMap<>();for(var e:cs.entrySet())out.put(e.getElement() instanceof CounterEnumType?((CounterEnumType)e.getElement()).name():e.getElement().getName(),e.getCount());return out;
 }
 static Map<String,Object> card(Card c){
  List<String> remembered=new ArrayList<>();for(Object x:c.getRemembered())remembered.add(x.toString());
  Map<String,Object> m=new LinkedHashMap<>();m.put("id",c.getId());m.put("name",c.getName());m.put("zone",c.getZone().getZoneType().name());m.put("timestamp",c.getGameTimestamp());m.put("tapped",c.isTapped());m.put("remembered",remembered);m.put("counters",counters(c.getCounters()));m.put("suspended",c.hasSuspend() && c.isInZone(ZoneType.Exile) && c.getCounters(CounterEnumType.TIME)>0);m.put("token",c.isToken());return m;
 }
 public static void main(String[] args)throws Exception{
  ForgeBridge.run(args,()->{
   for(int s=1;s<=2;s++){
    Player p=ForgeBridge.game.getPlayers().get(s-1);
    for(String name:new String[]{"Island","Plains","Forest","Swamp","Mountain"})for(int j=0;j<8;j++)StateBranchFixture.place(name,p,ZoneType.Battlefield);
    var spec=ForgeBridge.config.getAsJsonObject(s==1?"own":"other");StateBranchFixture.setup(p,spec);
    if(spec.has("counters"))for(var e:spec.getAsJsonObject("counters").entrySet())for(Card c:p.getAllCards())if(c.getName().equals(e.getKey()))for(var ct:e.getValue().getAsJsonObject().entrySet())c.setCounters(CounterType.getType(ct.getKey()),ct.getValue().getAsInt());
    if(spec.has("playerCounters"))for(var ct:spec.getAsJsonObject("playerCounters").entrySet())p.setCounters(CounterType.getType(ct.getKey()),ct.getValue().getAsInt());
   }
   ForgeBridge.game.getAction().checkStaticAbilities();
   Thread t=new Thread(()->{Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try{
    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&seen.add(r.requestId)){
     List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers())players.add(Map.of("life",p.getLife(),"counters",counters(p.getCounters()),"cards",p.getAllCards().stream().map(CounterFlowFixture::card).toList(),"library",p.getCardsIn(ZoneType.Library).stream().map(Card::getId).toList()));
     Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(r.requestId+".tmp"),dst=d.resolve(r.requestId+".json");Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("players",players,"requestId",r.requestId)));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
    }}Thread.sleep(5);
   }catch(Exception e){throw new RuntimeException(e);}},"counter-flow-read-only");t.setDaemon(true);t.start();
  });
 }
}
