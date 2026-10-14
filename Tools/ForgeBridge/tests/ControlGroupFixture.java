package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Opening positions only. Observations never alter effects, controllers or choices. */
public final class ControlGroupFixture {
    static Map<String,Object> card(Card c) {
        Map<String,Object> m=new LinkedHashMap<>();
        m.put("id",c.getId());m.put("name",c.getName());m.put("zone",c.getZone().getZoneType().name());
        m.put("owner",c.getOwner().getId()+1);m.put("controller",c.getController().getId()+1);
        m.put("timestamp",c.getGameTimestamp());m.put("layerTimestamp",c.getLayerTimestamp());
        try{var f=Card.class.getDeclaredField("tempControllers");f.setAccessible(true);Map<String,Integer> controllers=new TreeMap<>();for(var e:((Map<Long,Player>)f.get(c)).entrySet())controllers.put(e.getKey().toString(),e.getValue().getId()+1);m.put("temporaryControllers",controllers);}catch(ReflectiveOperationException e){throw new RuntimeException(e);}
        m.put("tapped",c.isTapped());m.put("power",c.getNetPower());m.put("toughness",c.getNetToughness());m.put("damage",c.getDamage());
        Map<String,Integer> counters=new TreeMap<>();for(var e:c.getCounters().entrySet())counters.put(e.getElement().toString(),e.getCount());m.put("counters",counters);
        List<Object> memory=new ArrayList<>();for(Object o:c.getRemembered())memory.add(o instanceof Player?Map.of("player",((Player)o).getId()+1):o instanceof Card?Map.of("card",((Card)o).getId()):String.valueOf(o));m.put("remembered",memory);return m;
    }
    static List<Object> stack() {
        List<Object> out=new ArrayList<>();for(var si:ForgeBridge.game.getStack()){
            var a=si.getSpellAbility();Map<String,Object> m=new LinkedHashMap<>();
            m.put("id",si.getId());m.put("abilityId",a.getId());m.put("source",a.getHostCard().getId());m.put("sourceName",a.getHostCard().getName());
            m.put("actor",si.getActivatingPlayer().getId()+1);m.put("cardController",a.getHostCard().getController().getId()+1);m.put("spell",a.isSpell());
            List<Integer> targets=new ArrayList<>();for(Player p:a.getTargets().getTargetPlayers())targets.add(p.getId()+1);m.put("targets",targets);out.add(m);
        }return out;
    }
    static final class Events {
        final List<Object> zones=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void changed(forge.game.event.GameEventZone e){if(e.card()!=null)zones.add(Map.of("id",e.card().getId(),"zone",e.zoneType().name(),"mode",e.mode().name()));}
        @com.google.common.eventbus.Subscribe public void shuffled(forge.game.event.GameEventShuffle e){zones.add(Map.of("shuffleSeat",e.player().getId()+1));}
        List<Object> snapshot(){synchronized(zones){return List.copyOf(zones);}}
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++){
                Player p=ForgeBridge.game.getPlayers().get(seat-1);
                RoyalStateFixture.setup(p,ForgeBridge.config.getAsJsonArray("positions").get(seat-1).getAsJsonObject());
                for(String n:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int j=0;j<6;j++)RoyalStateFixture.place(n,p,ZoneType.Battlefield);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();Events events=new Events();ForgeBridge.game.subscribeToEvents(events);
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try{
                    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&seen.add(r.requestId)){
                        List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()){
                            Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(ControlGroupFixture::card).toList());
                            players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"zones",zones,"pool",p.getManaPool().totalMana()));
                        }
                        Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(r.requestId+".tmp"),dst=d.resolve(r.requestId+".json");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",stack(),"zoneEvents",events.snapshot())));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                    }}Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"control-group-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
