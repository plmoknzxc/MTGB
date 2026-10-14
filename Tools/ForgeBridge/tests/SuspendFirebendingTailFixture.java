package mtgb.bridge;

import forge.game.card.Card;
import forge.game.mana.Mana;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.game.spellability.SpellAbility;
import forge.game.spellability.AbilityManaPart;
import forge.game.keyword.Keyword;
import java.nio.file.*;
import java.util.*;

/** Opening positions only; original native abilities and phases produce every result. */
public final class SuspendFirebendingTailFixture {
    static final IdentityHashMap<Mana,Integer> manaIds=new IdentityHashMap<>();
    static final IdentityHashMap<AbilityManaPart,Integer> partIds=new IdentityHashMap<>();
    static final Set<SpellAbility> observed=Collections.newSetFromMap(new IdentityHashMap<>());
    static Map<String,Object> mana(Mana m) {
        var a=m.getManaAbility();Map<String,Object> row=new LinkedHashMap<>();
        row.put("identity",manaIds.computeIfAbsent(m,x->manaIds.size()+1));row.put("color",m.toString());
        row.put("source",m.getSourceCard().getId());row.put("sourceName",m.getSourceCard().getName());
        row.put("actor",m.getPlayer().getId()+1);row.put("combat",m.isCombatMana());row.put("snow",m.isSnow());
        row.put("restricted",m.isRestricted());row.put("abilityIdentity",a==null?0:partIds.computeIfAbsent(a,x->partIds.size()+1));return row;
    }
    static List<Object> pool(Player p){List<Object> result=new ArrayList<>();for(Mana m:p.getManaPool())result.add(mana(m));return result;}
    static Map<String,Object> ability(SpellAbility a) {
        Map<String,Object> row=new LinkedHashMap<>();row.put("id",a.getId());row.put("source",a.getHostCard().getId());row.put("name",a.getHostCard().getName());
        row.put("actor",a.getActivatingPlayer()==null?0:a.getActivatingPlayer().getId()+1);row.put("api",a.getApi()==null?"":a.getApi().name());
        row.put("spell",a.isSpell());row.put("trigger",a.isTrigger());row.put("static",a instanceof forge.game.spellability.AbilityStatic);
        row.put("description",a.getDescription());Map<String,String> parameters=new LinkedHashMap<>(a.getMapParams());for(String key:new String[]{"Produced","CombatMana","Amount","Defined"})if(a.hasParam(key))parameters.put(key,a.getParam(key));row.put("params",parameters);row.put("cost",a.getPayCosts()==null?"":a.getPayCosts().toSimpleString());
        row.put("payingMana",a.getPayingMana().stream().map(SuspendFirebendingTailFixture::mana).toList());
        row.put("targetCards",a.getTargets().getTargetCards().stream().map(Card::getId).toList());
        List<Integer> targetPlayers=new ArrayList<>();for(Player p:a.getTargets().getTargetPlayers())targetPlayers.add(p.getId()+1);row.put("targetPlayers",targetPlayers);return row;
    }
    static Map<String,Object> card(Card c) {
        Map<String,Object> row=new LinkedHashMap<>();row.put("id",c.getId());row.put("name",c.getName());
        row.put("owner",c.getOwner().getId()+1);row.put("controller",c.getController().getId()+1);row.put("zone",c.getZone().getZoneType().name());
        row.put("timestamp",c.getGameTimestamp());row.put("tapped",c.isTapped());row.put("sick",c.isSick());row.put("power",c.getNetPower());row.put("toughness",c.getNetToughness());
        row.put("suspended",c.hasSuspend());row.put("haste",c.hasKeyword(Keyword.HASTE));row.put("xPaid",c.getXManaCostPaid());
        Map<String,Integer> counters=new LinkedHashMap<>();for(var e:c.getCounters().entrySet())counters.put(e.getElement().toString(),e.getCount());row.put("counters",counters);
        List<String> keywords=new ArrayList<>();for(var k:c.getKeywords())keywords.add(k.getOriginal());row.put("keywords",keywords);
        List<Object> triggers=new ArrayList<>();for(var t:c.getTriggers())triggers.add(Map.of("id",t.getId(),"params",t.getMapParams(),"description",t.toString()));row.put("triggers",triggers);
        List<Object> abilities=new ArrayList<>();for(var a:c.getSpellAbilities()){observed.add(a);abilities.add(ability(a));}row.put("abilities",abilities);
        List<Object> produced=new ArrayList<>();for(var a:c.getManaAbilities())for(var part:a.getAllManaParts())for(Mana m:part.getLastManaProduced())produced.add(mana(m));row.put("lastProduced",produced);
        row.put("attacking",ForgeBridge.game.getCombat()!=null&&ForgeBridge.game.getCombat().isAttacking(c));return row;
    }
    static List<Object> stack() {
        List<Object> out=new ArrayList<>();for(var si:ForgeBridge.game.getStack()) {var a=si.getSpellAbility();observed.add(a);Map<String,Object> row=new LinkedHashMap<>(ability(a));row.put("stackId",si.getId());out.add(row);}return out;
    }
    static final class Events {
        final List<Object> rows=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void poolChanged(forge.game.event.GameEventManaPool e){rows.add(Map.of("event","manaPool","seat",e.player().getId()+1,"mode",e.mode().name(),"mana",pool(ForgeBridge.game.getPlayers().get(e.player().getId()))));}
        @com.google.common.eventbus.Subscribe public void zone(forge.game.event.GameEventZone e){if(e.card()!=null)rows.add(Map.of("event","zone","source",e.card().getId(),"zone",e.zoneType().name(),"mode",e.mode().name()));}
        List<Object> snapshot(){synchronized(rows){return List.copyOf(rows);}}
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int i=0;i<2;i++)RoyalStateFixture.setup(ForgeBridge.game.getPlayers().get(i),ForgeBridge.config.getAsJsonArray("positions").get(i).getAsJsonObject());
            ForgeBridge.game.getAction().checkStaticAbilities();Events events=new Events();ForgeBridge.game.subscribeToEvents(events);
            Thread watcher=new Thread(()->{Set<Long> saved=new HashSet<>();while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&!saved.contains(r.requestId)) {
                    List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()) {
                        Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(SuspendFirebendingTailFixture::card).toList());
                        players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"mana",pool(p),"zones",zones,"castsTurn",p.getSpellsCastThisTurn(),"castsGame",p.getSpellsCastThisGame()));
                    }
                    List<Object> currentStack=stack();List<Object> paid=new ArrayList<>();for(var a:observed)if(!a.getPayingMana().isEmpty())paid.add(ability(a));
                    Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                    Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",currentStack,"paidAbilities",paid,"events",events.snapshot(),"turn",ForgeBridge.game.getPhaseHandler().getTurn(),"phase",ForgeBridge.game.getPhaseHandler().getPhase().name())));
                    Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);saved.add(r.requestId);
                }}Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}},"suspend-firebending-native-read-only");watcher.setDaemon(true);watcher.start();
        });
    }
}
