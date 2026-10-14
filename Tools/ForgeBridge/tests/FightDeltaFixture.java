package mtgb.bridge;

import forge.game.card.Card;
import forge.game.ability.AbilityKey;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Opening positions only. Subscribers and pending snapshots only read native state. */
public final class FightDeltaFixture {
    static Map<String,Object> card(Card c) {
        Map<String,Object> m=new LinkedHashMap<>();
        m.put("id",c.getId());m.put("name",c.getName());m.put("printedName",c.getState(forge.card.CardStateName.Original).getName());m.put("currentName",c.getCurrentState().getName());
        m.put("zone",c.getZone().getZoneType().name());m.put("owner",c.getOwner().getId()+1);m.put("controller",c.getController().getId()+1);
        m.put("timestamp",c.getGameTimestamp());m.put("faceDown",c.isFaceDown());m.put("tapped",c.isTapped());
        m.put("power",c.getNetPower());m.put("toughness",c.getNetToughness());m.put("damage",c.getDamage());m.put("fought",c.getFoughtThisTurn());
        Map<String,Integer> cs=new TreeMap<>();for(var e:c.getCounters().entrySet())cs.put(e.getElement().toString(),e.getCount());m.put("counters",cs);
        List<Object> hs=new ArrayList<>();for(var e:c.getDamageHistory().getAllDmgInstances()) {
            Map<String,Object> h=new LinkedHashMap<>();h.put("amount",e.getLeft());h.put("combat",e.getRight());
            var lki=ForgeBridge.game.getDamageLKI(e);if(lki!=null){h.put("source",lki.getLeft().getId());h.put("target",lki.getRight().getId());h.put("sourcePower",lki.getLeft().getNetPower());}hs.add(h);
        }m.put("damageHistory",hs);return m;
    }
    static Object identity(Object o) {
        if(o instanceof Card)return ((Card)o).getId();
        if(o instanceof Iterable){List<Object> xs=new ArrayList<>();for(Object x:(Iterable<?>)o)xs.add(identity(x));return xs;}
        return o==null?null:String.valueOf(o);
    }
    static List<Object> stack() {
        List<Object> out=new ArrayList<>();for(var si:ForgeBridge.game.getStack()) {
            var a=si.getSpellAbility();Map<String,Object> m=new LinkedHashMap<>();m.put("id",si.getId());m.put("abilityId",a.getId());
            m.put("source",a.getHostCard().getId());m.put("sourceName",a.getHostCard().getName());m.put("spell",a.isSpell());m.put("trigger",a.isTrigger());
            m.put("api",a.getApi()==null?"":a.getApi().name());m.put("actor",a.getActivatingPlayer().getId()+1);
            m.put("fighter",identity(a.getTriggeringObject(AbilityKey.Fighter)));m.put("fighters",identity(a.getTriggeringObject(AbilityKey.Fighters)));
            m.put("targets",a.getTargets().getTargetCards().stream().map(Card::getId).toList());
            m.put("damageMapPresent",a.getDamageMap()!=null);m.put("description",a.getDescription());out.add(m);
        }return out;
    }
    static final class Events {
        final List<Object> all=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void damaged(forge.game.event.GameEventCardDamaged e) {
            Map<String,Object> m=new LinkedHashMap<>();m.put("event","damage");m.put("source",e.source().getId());m.put("target",e.card().getId());m.put("amount",e.amount());m.put("damageType",e.type().name());
            m.put("phase",ForgeBridge.game.getPhaseHandler().getPhase().name());all.add(m);
        }
        @com.google.common.eventbus.Subscribe public void changed(forge.game.event.GameEventZone e){if(e.card()!=null)all.add(Map.of("event","zone","id",e.card().getId(),"zone",e.zoneType().name(),"mode",e.mode().name()));}
        @com.google.common.eventbus.Subscribe public void stats(forge.game.event.GameEventCardStatsChanged e){all.add(Map.of("event","stats","cards",e.cards().stream().map(c->Map.of("id",c.getId(),"name",c.getCurrentState().getName(),"power",c.getCurrentState().getPower(),"toughness",c.getCurrentState().getToughness())).toList()));}
        List<Object> snapshot(){synchronized(all){return List.copyOf(all);}}
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()-> {
            for(int seat=1;seat<=2;seat++) {
                Player p=ForgeBridge.game.getPlayers().get(seat-1);
                RoyalStateFixture.setup(p,ForgeBridge.config.getAsJsonArray("positions").get(seat-1).getAsJsonObject());
                for(String n:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int j=0;j<8;j++)RoyalStateFixture.place(n,p,ZoneType.Battlefield);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();Events events=new Events();ForgeBridge.game.subscribeToEvents(events);
            Thread observer=new Thread(()-> {
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&!seen.contains(r.requestId)) {
                        List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()) {
                            Map<String,Object> zs=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack,ZoneType.Command})zs.put(z.name(),p.getCardsIn(z).stream().map(FightDeltaFixture::card).toList());
                            players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"zones",zs,"pool",p.getManaPool().totalMana()));
                        }
                        Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(r.requestId+".tmp"),dst=d.resolve(r.requestId+".json");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",stack(),"events",events.snapshot(),"turn",ForgeBridge.game.getPhaseHandler().getTurn())));
                        Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                    }}Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"fight-delta-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
