package mtgb.bridge;

import forge.game.ability.AbilityKey;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import forge.game.trigger.Trigger;
import forge.game.trigger.TriggerType;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.nio.file.*;
import java.util.*;

/** Opening positions only. The passive trigger records real runParams and always rejects. */
public final class ClashDeltaFixture {
    static Card place(String name, Player p, ZoneType zone) {
        var paper=FModel.getMagicDb().getCommonCards().getCard(name);
        if(paper==null)throw new IllegalArgumentException(name);
        Card c=Card.fromPaperCard(paper,p);p.getZone(zone).add(c);
        c.setGameTimestamp(ForgeBridge.game.getNextTimestamp());c.setSickness(false);return c;
    }
    static Map<String,Object> card(Card c) {
        Map<String,Object> m=new LinkedHashMap<>();m.put("id",c.getId());m.put("name",c.getName());m.put("zone",c.getZone().getZoneType().name());
        m.put("owner",c.getOwner().getId()+1);m.put("controller",c.getController().getId()+1);m.put("cmc",c.getCMC());m.put("timestamp",c.getGameTimestamp());m.put("tapped",c.isTapped());
        List<Object> memory=new ArrayList<>();for(Object o:c.getRemembered())memory.add(o instanceof Player?Map.of("player",((Player)o).getId()+1):o instanceof Card?Map.of("card",((Card)o).getId()):String.valueOf(o));m.put("remembered",memory);
        m.put("attachedTo",c.getEntityAttachedTo()==null?0:c.getEntityAttachedTo().getId());return m;
    }
    static final class PassiveClashedObserver extends Trigger {
        final List<Object> events=new ArrayList<>();final Set<Map<AbilityKey,Object>> seen=Collections.newSetFromMap(new IdentityHashMap<>());
        PassiveClashedObserver(Card host){super(Map.of("TriggerZones","Battlefield"),host,false);}
        @Override public TriggerType getMode(){return TriggerType.Clashed;}
        @Override public synchronized boolean performTest(Map<AbilityKey,Object> params){
            if(seen.add(params))events.add(Map.of("player",((Player)params.get(AbilityKey.Player)).getId()+1,"won",String.valueOf(params.get(AbilityKey.Won))));
            return false; // Never enters runSingleTrigger, executes an ability, or alters native results.
        }
        @Override public void setTriggeringObjects(SpellAbility sa,Map<AbilityKey,Object> params){throw new AssertionError("Passive observer must never execute");}
        @Override public String getImportantStackObjects(SpellAbility sa){return "";}
        synchronized List<Object> snapshot(){return List.copyOf(events);}
    }
    static final class Events {
        final List<Object> logs=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void log(forge.game.event.GameEventAddLog e){logs.add(Map.of("type",e.type().name(),"message",e.message()));}
        List<Object> snapshot(){synchronized(logs){return List.copyOf(logs);}}
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++){
                Player p=ForgeBridge.game.getPlayers().get(seat-1);
                for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library})for(Card c:new ArrayList<>(p.getCardsIn(z)))p.getZone(z).remove(c);
                var spec=ForgeBridge.config.getAsJsonArray("positions").get(seat-1).getAsJsonObject();
                for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Battlefield,ZoneType.Library})if(spec.has(z.name()))for(var name:spec.getAsJsonArray(z.name()))place(name.getAsString(),p,z);
            }
            Player own=ForgeBridge.game.getPlayers().get(0);Card land=place("Island",own,ZoneType.Battlefield);
            place("Island",own,ZoneType.Battlefield);place("Island",own,ZoneType.Battlefield);
            PassiveClashedObserver clashes=new PassiveClashedObserver(land);land.addTrigger(clashes);ForgeBridge.game.getTriggerHandler().registerOneTrigger(clashes);
            ForgeBridge.game.getAction().checkStaticAbilities();Events events=new Events();ForgeBridge.game.subscribeToEvents(events);
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try{
                    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&seen.add(r.requestId)){
                        List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()){
                            Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack})zones.put(z.name(),p.getCardsIn(z).stream().map(ClashDeltaFixture::card).toList());
                            players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"zones",zones,"pool",p.getManaPool().totalMana()));
                        }
                        List<Object> stack=new ArrayList<>();for(var si:ForgeBridge.game.getStack()){var a=si.getSpellAbility();stack.add(Map.of("id",si.getId(),"abilityId",a.getId(),"source",a.getHostCard().getId(),"api",String.valueOf(a.getApi()),"actor",a.getActivatingPlayer().getId()+1,"spell",a.isSpell()));}
                        Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(r.requestId+".tmp"),dst=d.resolve(r.requestId+".json");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",stack,"clashed",clashes.snapshot(),"logs",events.snapshot())));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                    }}Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"clash-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
