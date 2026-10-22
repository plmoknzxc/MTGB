package mtgb.bridge;

import com.google.gson.*;
import forge.game.card.*;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.util.MyRandom;
import java.nio.file.*;
import java.util.*;

/** Rule-neutral observation regression, executed on the real native game thread. */
public final class ShuffleAuditFixture {
    static void check(boolean value,String message){if(!value)throw new AssertionError(message);}
    static List<Integer> ids(Player p){List<Integer> result=new ArrayList<>();for(Card c:p.getCardsIn(ZoneType.Library))result.add(c.getId());return result;}
    public static void main(String[] args)throws Exception{
        ForgeBridge.run(args,()->{
            try{
                Player p=ForgeBridge.game.getPlayers().get(0);CardCollection original=new CardCollection(p.getCardsIn(ZoneType.Library));
                var audit=ForgeBridge.shuffleAudit;Player.setLibraryAudit(null);
                List<List<Integer>> baseline=new ArrayList<>();long[] off=new long[300],on=new long[300];
                MyRandom.setRandom(new Random(711));
                for(int i=0;i<300;i++){long t=System.nanoTime();p.shuffle(null);off[i]=System.nanoTime()-t;baseline.add(ids(p));}
                p.getZone(ZoneType.Library).setCards(original);MyRandom.setRandom(new Random(711));Player.setLibraryAudit(audit);
                for(int i=0;i<300;i++){
                    long t=System.nanoTime();p.shuffle(null);on[i]=System.nanoTime()-t;check(ids(p).equals(baseline.get(i)),"Audit changed seeded permutation "+i);
                    if(i%16==0)Thread.sleep(2); // Drain between batches, outside operation timing.
                }
                audit.close("benchmark");check(!Files.exists(Path.of(ForgeBridge.config.get("shuffleAuditFile").getAsString()+".partial")),"Writer did not finalize");
                // Saturation must be explicit, and never affect a subsequent shuffle.
                JsonObject capacity=ForgeBridge.config.deepCopy();Path root=Path.of(ForgeBridge.config.get("testOutput").getAsString());
                capacity.addProperty("shuffleAuditFile",root.resolve("00000000000000000000000000000002.jsonl.gz").toString());
                var full=new BridgeShuffleAudit(capacity);full.beginDraw(p,null);
                var publish=BridgeShuffleAudit.class.getDeclaredMethod("publish",Map.class);publish.setAccessible(true);
                publish.invoke(full,new LinkedHashMap<>(Map.of("type","test_capacity","padding","x".repeat(3*1024*1024))));
                Player.setLibraryAudit(full);p.shuffle(null);full.close("capacity_test");
                // A filesystem failure is isolated to the writer.
                Path blocked=root.resolve("blocked");Files.writeString(blocked,"not a directory");
                JsonObject broken=ForgeBridge.config.deepCopy();broken.addProperty("shuffleAuditFile",blocked.resolve("audit.jsonl.gz").toString());
                var invalid=new BridgeShuffleAudit(broken);Player.setLibraryAudit(invalid);p.shuffle(null);invalid.close("io_test");
                check(p.getCardsIn(ZoneType.Library).size()==original.size(),"Audit failure changed card count");
                // Rotation only touches owned, old archives; an active partial and unrelated file survive.
                Path rotation=root.resolve("rotation");Files.createDirectories(rotation);
                Path old=rotation.resolve("11111111111111111111111111111111.jsonl.gz");Files.writeString(old,"old");Files.setLastModifiedTime(old,java.nio.file.attribute.FileTime.fromMillis(1));
                Path active=rotation.resolve("22222222222222222222222222222222.jsonl.gz.partial");Files.writeString(active,"active");
                Path unrelated=rotation.resolve("keep.txt");Files.writeString(unrelated,"keep");BridgeShuffleAudit.prune(rotation);
                check(!Files.exists(old)&&Files.exists(active)&&Files.exists(unrelated),"Rotation scope failure");
                Player.setLibraryAudit(null);Arrays.sort(on);Arrays.sort(off);
                Files.writeString(root.resolve("fixture-result.json"),ForgeBridge.JSON.toJson(Map.of("status","PASS","seededPermutations",300,"baselineMedianMicros",off[150]/1000,"auditMedianMicros",on[150]/1000,"auditP99Micros",on[297]/1000,"auditMaxMicros",on[299]/1000,"ioFailureContinued",true,"rotationScoped",true)));
                ForgeBridge.WIRE.println("{\"type\":\"fixture_done\"}");
            }catch(Exception e){throw new RuntimeException(e);}
        });
    }
}
