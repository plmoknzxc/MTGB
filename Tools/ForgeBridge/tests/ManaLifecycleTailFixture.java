package mtgb.bridge;

import forge.game.card.Card;
import forge.game.mana.Mana;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.game.trigger.Trigger;
import java.nio.file.*;
import java.util.*;

/** Initial positions only; every later pool change comes from actual native operations. */
public final class ManaLifecycleTailFixture {
    static final IdentityHashMap<Mana,Integer> identities=new IdentityHashMap<>();
    static synchronized Map<String,Object> mana(Mana m) {
        int id=identities.computeIfAbsent(m,x->identities.size()+1);
        Map<String,Object> item=new LinkedHashMap<>();
        item.put("identity",id);item.put("color",m.toString());item.put("source",m.getSourceCard().getId());
        item.put("sourceName",m.getSourceCard().getName());item.put("originalPlayer",m.getPlayer().getId()+1);
        item.put("snow",m.isSnow());item.put("restricted",m.isRestricted());item.put("persistent",m.isPersistentMana());item.put("combat",m.isCombatMana());
        item.put("restriction",m.getManaAbility()==null?"":m.getManaAbility().getManaRestrictions());
        item.put("extraRestriction",m.getManaAbility()==null?"":m.getManaAbility().getExtraManaRestriction());
        item.put("abilityIdentity",m.getManaAbility()==null?0:System.identityHashCode(m.getManaAbility()));return item;
    }
    static List<Object> pool(Player p) {List<Object> out=new ArrayList<>();for(Mana m:p.getManaPool())out.add(mana(m));return out;}
    static List<Object> pools(){return ForgeBridge.game.getPlayers().stream().map(p->(Object)Map.of("seat",p.getId()+1,"mana",pool(p))).toList();}
    static Map<String,Object> card(Card c) {
        Map<String,Object> out=new LinkedHashMap<>();out.put("id",c.getId());out.put("name",c.getName());
        out.put("zone",c.getZone().getZoneType().name());out.put("owner",c.getOwner().getId()+1);out.put("controller",c.getController().getId()+1);
        out.put("timestamp",c.getGameTimestamp());out.put("tapped",c.isTapped());out.put("sick",c.isSick());
        List<Object> remembered=new ArrayList<>();for(Object r:c.getRemembered())remembered.add(r instanceof Card?((Card)r).getId():r);out.put("remembered",remembered);
        List<Object> produced=new ArrayList<>();for(var a:c.getManaAbilities())for(var mp:a.getAllManaParts())for(Mana m:mp.getLastManaProduced())produced.add(mana(m));
        out.put("lastProduced",produced);out.put("power",c.getNetPower());out.put("toughness",c.getNetToughness());out.put("plusCounters",c.getCounters(forge.game.card.CounterEnumType.P1P1));out.put("effectSource",c.getEffectSource()==null?0:c.getEffectSource().getId());return out;
    }
    static List<Object> stack() {
        List<Object> out=new ArrayList<>();for(var si:ForgeBridge.game.getStack()) {
            var a=si.getSpellAbility();Map<String,Object> item=new LinkedHashMap<>();item.put("id",si.getId());item.put("abilityId",a.getId());
            item.put("source",a.getHostCard().getId());item.put("name",a.getHostCard().getName());item.put("actor",a.getActivatingPlayer().getId()+1);
            item.put("api",a.getApi()==null?"":a.getApi().name());item.put("trigger",a.isTrigger());item.put("spell",a.isSpell());
            item.put("payingMana",a.getPayingMana().stream().map(ManaLifecycleTailFixture::mana).toList());item.put("triggerRemembered",a.getTriggerRemembered());
            List<Integer> targets=new ArrayList<>();for(Player p:a.getTargets().getTargetPlayers())targets.add(p.getId()+1);
            item.put("targetPlayers",targets);item.put("description",a.getDescription());Object map=a.getTriggeringObject(forge.game.ability.AbilityKey.Map);if(map instanceof Map){List<Object> losses=new ArrayList<>();for(var e:((Map<?,?>)map).entrySet())if(e.getKey() instanceof Player)losses.add(Map.of("player",((Player)e.getKey()).getId()+1,"amount",e.getValue()));item.put("lifeLostMap",losses);}out.add(item);
        }return out;
    }
    static List<Object> delayed()throws Exception {
        var field=forge.game.trigger.TriggerHandler.class.getDeclaredField("delayedTriggers");field.setAccessible(true);
        List<Object> out=new ArrayList<>();for(Trigger t:new ArrayList<>((List<Trigger>)field.get(ForgeBridge.game.getTriggerHandler()))) {
            Map<String,Object> item=new LinkedHashMap<>();item.put("id",t.getId());item.put("source",t.getHostCard().getId());item.put("params",t.getMapParams());
            item.put("remembered",t.getTriggerRemembered());out.add(item);
        }return out;
    }
    static final class Events {
        final List<Object> all=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void phase(forge.game.event.GameEventTurnPhase e){all.add(Map.of("event","phase","phase",e.phase().name(),"turn",ForgeBridge.game.getPhaseHandler().getTurn(),"player",e.playerTurn().getId()+1,"pools",pools()));}
        @com.google.common.eventbus.Subscribe public void combatEnd(forge.game.event.GameEventCombatEnded e){all.add(Map.of("event","combatEnd","attackers",e.attackers().stream().map(c->c.getId()).toList(),"blockers",e.blockers().stream().map(c->c.getId()).toList()));}
        @com.google.common.eventbus.Subscribe public void poolChanged(forge.game.event.GameEventManaPool e) {
            Map<String,Object> item=new LinkedHashMap<>();item.put("event","manaPool");item.put("player",e.player().getId()+1);item.put("mode",e.mode().name());
            item.put("pools",pools());item.put("phase",ForgeBridge.game.getPhaseHandler().getPhase().name());all.add(item);
        }
        @com.google.common.eventbus.Subscribe public void damage(forge.game.event.GameEventPlayerDamaged e){all.add(Map.of("event","damage","source",e.source().getId(),"target",e.target().getId()+1,"amount",e.amount(),"combat",e.combat()));}
        @com.google.common.eventbus.Subscribe public void life(forge.game.event.GameEventPlayerLivesChanged e){all.add(Map.of("event","life","player",e.player().getId()+1,"before",e.oldLives(),"after",e.newLives()));}
        @com.google.common.eventbus.Subscribe public void zone(forge.game.event.GameEventZone e){if(e.card()!=null)all.add(Map.of("event","zone","id",e.card().getId(),"zone",e.zoneType().name(),"mode",e.mode().name()));}
        List<Object> snapshot(){synchronized(all){return List.copyOf(all);}}
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()-> {
            for(int seat=1;seat<=2;seat++)RoyalStateFixture.setup(ForgeBridge.game.getPlayers().get(seat-1),ForgeBridge.config.getAsJsonArray("positions").get(seat-1).getAsJsonObject());
            ForgeBridge.game.getAction().checkStaticAbilities();Events events=new Events();ForgeBridge.game.subscribeToEvents(events);
            Thread observer=new Thread(()-> {
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&!seen.contains(r.requestId)) {
                        List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()) {
                            Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(ManaLifecycleTailFixture::card).toList());
                            players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"zones",zones,"mana",pool(p)));
                        }
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",stack(),"delayed",delayed(),"events",events.snapshot(),"turn",ForgeBridge.game.getPhaseHandler().getTurn(),"phase",ForgeBridge.game.getPhaseHandler().getPhase().name(),"combatNull",ForgeBridge.game.getCombat()==null)));
                        Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                    }}Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"lifecycle-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
