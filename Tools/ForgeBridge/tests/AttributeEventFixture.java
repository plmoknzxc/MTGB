package mtgb.bridge;

import forge.game.card.Card;
import forge.game.mana.Mana;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.game.trigger.Trigger;
import java.nio.file.*;
import java.util.*;

/** Initial positions only; later characteristics and zones change through actual engine actions. */
public final class AttributeEventFixture {
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
        Map<String,Object> out=new LinkedHashMap<>();out.put("id",c.getId());out.put("name",c.getName());out.put("harnessed",c.isHarnessed());out.put("solved",c.isSolved());out.put("suspected",c.isSuspected());out.put("saddled",c.isSaddled());out.put("saddleCount",c.getTimesSaddledThisTurn());out.put("plotted",c.isPlotted());out.put("prepared",c.isPrepared());out.put("preparedEffect",c.getPrepared()==null?0:c.getPrepared().getId());out.put("preparedCard",c.getPreparedSpell()==null?0:c.getPreparedSpell().getId());
        out.put("originalName",c.getCurrentState().getName());out.put("token",c.isToken());out.put("loyalty",c.getCounters(forge.game.card.CounterEnumType.LOYALTY));out.put("plusCounters",c.getCounters(forge.game.card.CounterEnumType.P1P1));out.put("chosenName",c.getNamedCard());out.put("chosenCards",c.getChosenCards().stream().map(Card::getId).toList());out.put("namedCards",c.getNamedCards());out.put("chosenType",c.getChosenType());out.put("attachedTo",c.getAttachedTo()==null?0:c.getAttachedTo().getId());out.put("hasNameOverwrite",c.hasNameOverwrite());out.put("hasNonLegendaryCreatureNames",c.hasNonLegendaryCreatureNames());out.put("nameChanges",names(c));Map<String,Object> probes=new LinkedHashMap<>();for(String name:List.of("Grizzly Bears","Silvercoat Lion","Isamaru, Hound of Konda","Legitimate Businessperson","Vitu-Ghazi"))probes.put(name,c.sharesNameWith(name));out.put("sharesNames",probes);out.put("zone",c.getZone().getZoneType().name());out.put("owner",c.getOwner().getId()+1);out.put("controller",c.getController().getId()+1);
        out.put("timestamp",c.getGameTimestamp());out.put("tapped",c.isTapped());out.put("sick",c.isSick());
        out.put("power",c.getNetPower());out.put("toughness",c.getNetToughness());out.put("printedPower",c.getBasePower());out.put("printedToughness",c.getBaseToughness());
        out.put("currentPower",c.getCurrentPower());out.put("currentToughness",c.getCurrentToughness());out.put("tempPower",c.getTempPowerBoost());out.put("tempToughness",c.getTempToughnessBoost());
        out.put("ptCda",pt(c.getSetPTCharacterDefiningTable()));out.put("ptExternal",pt(c.getSetPTTable()));
        out.put("printedType",c.getCurrentState().getType().toString());out.put("type",c.getType().toString());out.put("allCreatureTypes",c.getType().hasAllCreatureTypes());
        out.put("printedColors",(int)c.getCurrentState().getColor().getColor());out.put("colors",(int)c.getColor().getColor());
        out.put("creature",c.isCreature());out.put("planeswalker",c.isPlaneswalker());out.put("canBlock",forge.game.combat.CombatUtil.canBlock(c));
        out.put("hiddenKeywords",com.google.common.collect.Lists.newArrayList(c.getHiddenExtrinsicKeywords()));out.put("keywords",c.getUnhiddenKeywords().getValues().stream().map(k->k.getOriginal()).toList());
        out.put("statics",c.getStaticAbilities().stream().map(a->Map.of("id",a.getId(),"source",a.getHostCard().getId(),"intrinsic",a.isIntrinsic(),"cda",a.isCharacteristicDefining(),"params",a.getMapParams())).toList());
        for(String field:List.of("changedCardTypesCharacterDefining","changedCardTypes","changedCardColorsCharacterDefining","changedCardColors"))out.put(field,table(c,field));
        return out;
    }
    static List<Object> pt(com.google.common.collect.Table<Long,Long,org.apache.commons.lang3.tuple.Pair<Integer,Integer>> t) {
        List<Object> out=new ArrayList<>();for(var x:t.cellSet()){Map<String,Object> o=new LinkedHashMap<>();o.put("timestamp",x.getRowKey());o.put("staticId",x.getColumnKey());o.put("power",x.getValue().getLeft());o.put("toughness",x.getValue().getRight());out.add(o);}return out;
    }
    static List<Object> table(Card c,String name) {
        try {var f=Card.class.getDeclaredField(name);f.setAccessible(true);var t=(com.google.common.collect.Table<?,?,?>)f.get(c);List<Object> out=new ArrayList<>();
            for(var x:t.cellSet()){Map<String,Object> o=new LinkedHashMap<>();o.put("timestamp",x.getRowKey());o.put("staticId",x.getColumnKey());o.put("value",ForgeBridge.JSON.toJson(x.getValue()));out.add(o);}return out;
        }catch(Exception e){throw new IllegalStateException(name,e);}
    }

    static List<Object> stack() {
        List<Object> out=new ArrayList<>();for(var si:ForgeBridge.game.getStack()) {
            var a=si.getSpellAbility();Map<String,Object> item=new LinkedHashMap<>();item.put("id",si.getId());item.put("abilityId",a.getId());
            item.put("source",a.getHostCard().getId());item.put("name",a.getHostCard().getName());item.put("actor",a.getActivatingPlayer().getId()+1);
            item.put("api",a.getApi()==null?"":a.getApi().name());Map<String,Object> payload=new LinkedHashMap<>();for(var key:List.of(forge.game.ability.AbilityKey.Card,forge.game.ability.AbilityKey.Player,forge.game.ability.AbilityKey.Crew)){Object value=a.getTriggeringObject(key);if(value instanceof Card)payload.put(key.name(),Map.of("cardId",((Card)value).getId()));else if(value instanceof Player)payload.put(key.name(),Map.of("playerSeat",((Player)value).getId()+1));else if(value instanceof Iterable){List<Object> values=new ArrayList<>();for(Object x:(Iterable<?>)value)values.add(remembered(x));payload.put(key.name(),values);}else if(value!=null)payload.put(key.name(),String.valueOf(value));}item.put("triggerObjects",payload);item.put("trigger",a.isTrigger());item.put("spell",a.isSpell());
            item.put("warp",a.isWarp());item.put("payingMana",a.getPayingMana().stream().map(AttributeEventFixture::mana).toList());item.put("targetCards",a.getTargets().getTargetCards().stream().map(Card::getId).toList());
            List<Integer> targets=new ArrayList<>();for(Player p:a.getTargets().getTargetPlayers())targets.add(p.getId()+1);
            item.put("targetPlayers",targets);item.put("description",a.getDescription());Object map=a.getTriggeringObject(forge.game.ability.AbilityKey.Map);if(map instanceof Map){List<Object> losses=new ArrayList<>();for(var e:((Map<?,?>)map).entrySet())if(e.getKey() instanceof Player)losses.add(Map.of("player",((Player)e.getKey()).getId()+1,"amount",e.getValue()));item.put("lifeLostMap",losses);}out.add(item);
        }return out;
    }
    static Object remembered(Object x){return x instanceof Card?Map.of("cardId",((Card)x).getId()):x instanceof Player?Map.of("playerSeat",((Player)x).getId()+1):x;}
    static List<Object> names(Card c) {
        try {var f=Card.class.getDeclaredField("changedCardNames");f.setAccessible(true);var t=(com.google.common.collect.Table<?,?,?>)f.get(c);List<Object> out=new ArrayList<>();
            for(var x:t.cellSet()){Map<String,Object> o=new LinkedHashMap<>();o.put("timestamp",x.getRowKey());o.put("staticId",x.getColumnKey());for(String key:List.of("newName","addNonLegendaryCreatureNames","isOverwrite")){var method=x.getValue().getClass().getDeclaredMethod(key);method.setAccessible(true);o.put(key,method.invoke(x.getValue()));}out.add(o);}return out;
        }catch(Exception e){throw new IllegalStateException("Read-only name table",e);}
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
        @com.google.common.eventbus.Subscribe public void tokens(forge.game.event.GameEventTokenCreated e){all.add(Map.of("event","tokenCreated"));}
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
                            Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(AttributeEventFixture::card).toList());
                            players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"zones",zones,"mana",pool(p)));
                        }
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",stack(),"events",events.snapshot(),"turn",ForgeBridge.game.getPhaseHandler().getTurn(),"phase",ForgeBridge.game.getPhaseHandler().getPhase().name(),"combatNull",ForgeBridge.game.getCombat()==null)));
                        Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                    }}Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"names-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
