package mtgb.bridge;

import forge.ai.LobbyPlayerAi;
import forge.deck.Deck;
import forge.game.card.Card;
import forge.game.player.RegisteredPlayer;
import forge.game.zone.ZoneType;
import forge.util.MyRandom;
import java.nio.file.*;
import java.util.*;

/** Opening setup and standard seeded RNG only. All later transfers are native
 * outcomes of legal casts/activations/attacks. Extra native seats are AI, not clients. */
public final class ControlVariantFixture {
    static final List<Object> randomCalls=new ArrayList<>();
    static Map<String,Object> card(Card c) {
        var m=new LinkedHashMap<String,Object>();
        m.put("id",c.getId());m.put("name",c.getName());m.put("zone",c.getZone().getZoneType().name());
        m.put("owner",ForgeBridge.game.getPlayers().indexOf(c.getOwner())+1);
        m.put("controller",ForgeBridge.game.getPlayers().indexOf(c.getController())+1);
        m.put("timestamp",c.getGameTimestamp());m.put("tapped",c.isTapped());
        m.put("power",c.getNetPower());m.put("toughness",c.getNetToughness());m.put("damage",c.getDamage());
        Map<String,Integer> counters=new TreeMap<>();
        for(var e:c.getCounters().entrySet())counters.put(e.getElement().toString(),e.getCount());
        m.put("counters",counters);return m;
    }
    static List<Object> field(){return ForgeBridge.game.getCardsIn(ZoneType.Battlefield).stream().map(c->(Object)card(c)).toList();}
    static final class RecordedRandom extends Random {
        RecordedRandom(long seed){super(seed);}
        @Override public int nextInt(int bound){
            int value=super.nextInt(bound);
            if(Arrays.stream(Thread.currentThread().getStackTrace()).anyMatch(f->f.getClassName().endsWith("ControlGainVariantEffect")))
                randomCalls.add(Map.of("bound",bound,"value",value,"fieldBeforeTransfer",field()));
            return value;
        }
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int i=0;i<ForgeBridge.game.getPlayers().size();i++) {
                var p=ForgeBridge.game.getPlayers().get(i);
                var spec=ForgeBridge.config.getAsJsonArray("positions").get(i).getAsJsonObject();
                RoyalStateFixture.setup(p,spec);
                if(i==0)for(String n:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int j=0;j<4;j++)RoyalStateFixture.place(n,p,ZoneType.Battlefield);
                if(spec.has("tapped"))for(var name:spec.getAsJsonArray("tapped"))p.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(name.getAsString())).forEach(c->c.setTapped(true));
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            MyRandom.setRandom(new RecordedRandom(ForgeBridge.config.get("fixtureSeed").getAsLong()));
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&seen.add(r.requestId)) {
                            var players=new ArrayList<Object>();
                            for(var p:ForgeBridge.game.getPlayers()) {
                                var zones=new LinkedHashMap<String,Object>();
                                for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(ControlVariantFixture::card).toList());
                                players.add(Map.of("seat",ForgeBridge.game.getPlayers().indexOf(p)+1,"life",p.getLife(),"zones",zones));
                            }
                            var combat=ForgeBridge.game.getCombat();
                            var attackers=combat==null?List.of():combat.getAttackers().stream().map(Card::getId).toList();
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"time",System.nanoTime(),"players",players,"attackers",attackers,"randomCalls",new ArrayList<>(randomCalls))));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"control-variant-read-only-audit");observer.setDaemon(true);observer.start();
        },registered->{
            for(int s=3;s<=ForgeBridge.config.getAsJsonArray("positions").size();s++) {
                Deck deck=new Deck("Control native seat "+s);
                ForgeBridge.loadSection(deck.getMain(),ForgeBridge.config.getAsJsonArray("decks").get(0).getAsJsonObject().getAsJsonArray("cards"));
                var p=new RegisteredPlayer(deck).setPlayer(new LobbyPlayerAi("Player "+s,Set.of()));p.setStartingLife(30);registered.add(p);
            }
        });
    }
}
