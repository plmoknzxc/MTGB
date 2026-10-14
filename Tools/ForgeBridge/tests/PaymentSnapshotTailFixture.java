package mtgb.bridge;

import forge.game.card.Card;
import forge.game.mana.Mana;
import forge.game.player.Player;
import forge.game.player.PlayerController;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Initial placement/control only. Observer reads native state and keeps its own event log. */
public final class PaymentSnapshotTailFixture {
    static Object field(Object o,String name)throws Exception {
        var f=o.getClass().getDeclaredField(name);f.setAccessible(true);return f.get(o);
    }
    static Map<String,Object> mana(Mana m) {
        Map<String,Object> v=new LinkedHashMap<>(DrainManaDeltaFixture.mana(m));
        var mp=m.getManaAbility();
        v.put("sourceGameSame",m.getSourceCard().getGame()==ForgeBridge.game);
        v.put("payerGameSame",m.getPlayer().getGame()==ForgeBridge.game);
        v.put("sourceObjectIdentity",System.identityHashCode(m.getSourceCard()));
        v.put("abilitySource",mp==null?-1:mp.getSourceCard().getId());
        v.put("abilitySourceGameSame",mp==null||mp.getSourceCard().getGame()==ForgeBridge.game);
        v.put("abilitySourceObjectIdentity",mp==null?0:System.identityHashCode(mp.getSourceCard()));
        v.put("cannotCounter",mp!=null&&mp.isCannotCounterPaidWith());
        v.put("triggersWhenSpent",mp!=null&&mp.getTriggersWhenSpent());
        v.put("addsKeywords",mp==null?"":String.valueOf(mp.getKeywords()));
        return v;
    }
    static List<Object> pool(Player p){List<Object> v=new ArrayList<>();for(Mana m:p.getManaPool())v.add(mana(m));return v;}
    static Map<String,Object> card(Card c) {
        Map<String,Object> v=new LinkedHashMap<>(DrainManaDeltaFixture.card(c));
        List<String> hidden=new ArrayList<>();for(String x:c.getHiddenExtrinsicKeywords())hidden.add(x);v.put("hidden",hidden);
        try {
            var table=(com.google.common.collect.Table<Long,Long,List<String>>)field(c,"hiddenExtrinsicKeywords");
            v.put("hiddenCells",table.cellSet().stream().map(x->Map.of("timestamp",x.getRowKey(),"staticId",x.getColumnKey(),"keywords",List.copyOf(x.getValue()))).toList());
        }catch(Exception e){throw new RuntimeException(e);}
        v.put("objectIdentity",System.identityHashCode(c));
        List<Object> abilities=new ArrayList<>();for(var a:c.getManaAbilities()) {
            Map<String,Object> av=new LinkedHashMap<>();av.put("id",a.getId());av.put("undoable",a.isUndoable());
            av.put("source",a.getHostCard().getId());av.put("description",a.getDescription());
            av.put("parts",a.getAllManaParts().stream().map(mp->Map.of("identity",System.identityHashCode(mp),"source",mp.getSourceCard().getId(),"sourceGameSame",mp.getSourceCard().getGame()==ForgeBridge.game,"restriction",mp.getManaRestrictions(),"extraRestriction",mp.getExtraManaRestriction())).toList());abilities.add(av);
        }v.put("manaAbilities",abilities);return v;
    }
    static Map<String,Object> snapshot(long requestId, Events events)throws Exception {
        Map<String,Object> v=new LinkedHashMap<>();v.put("requestId",requestId);
        List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()) {
            Map<String,Object> pv=new LinkedHashMap<>(),zones=new LinkedHashMap<>();pv.put("seat",p.getId()+1);pv.put("life",p.getLife());
            pv.put("spellsCastThisTurn",p.getSpellsCastThisTurn());pv.put("spellsCastThisGame",p.getSpellsCastThisGame());pv.put("mana",pool(p));
            pv.put("achievementSpellsCast",p.getAchievementTracker().spellsCast);pv.put("achievementMaxStormCount",p.getAchievementTracker().maxStormCount);
            for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(PaymentSnapshotTailFixture::card).toList());
            pv.put("zones",zones);players.add(pv);
        }v.put("players",players);v.put("stack",DrainManaDeltaFixture.stack());v.put("delayed",DrainManaDeltaFixture.delayed());
        var stack=ForgeBridge.game.getStack();v.put("frozen",stack.isFrozen());
        List<Object> frozen=new ArrayList<>();for(Object x:(Iterable<?>)field(stack,"frozenStack")) {
            var si=(forge.game.spellability.SpellAbilityStackInstance)x;var a=si.getSpellAbility();
            frozen.add(Map.of("id",si.getId(),"source",a.getHostCard().getId(),"name",a.getHostCard().getName(),"actor",a.getActivatingPlayer().getId()+1,"trigger",a.isTrigger(),"description",a.getDescription()));
        }v.put("frozenStack",frozen);
        List<Object> undo=new ArrayList<>();for(Object x:(Iterable<?>)field(stack,"undoStack")) {var a=(forge.game.spellability.SpellAbility)x;undo.add(Map.of("id",a.getId(),"source",a.getHostCard().getId(),"undoable",a.isUndoable()));}v.put("undoStack",undo);
        var owner=(Player)field(stack,"undoStackOwner");v.put("undoOwner",owner==null?0:owner.getId()+1);
        List<Object> waiting=new ArrayList<>();for(Object x:(Iterable<?>)field(ForgeBridge.game.getTriggerHandler(),"waitingTriggers"))waiting.add(Map.of("type",x.getClass().getName(),"text",x.toString(),"identity",System.identityHashCode(x)));v.put("waiting",waiting);
        List<Object> costs=new ArrayList<>();for(var e:ForgeBridge.game.costPaymentStack) {
            Map<String,Object> cv=new LinkedHashMap<>();cv.put("part",e.cost().getClass().getSimpleName());cv.put("snapshotPresent",field(e.payment(),"cancellationSnapshot")!=null);cv.put("restored",field(e.payment(),"cancellationRestored"));costs.add(cv);
        }v.put("costs",costs);v.put("events",events.snapshot());v.put("turn",ForgeBridge.game.getPhaseHandler().getTurn());v.put("phase",ForgeBridge.game.getPhaseHandler().getPhase().name());return v;
    }
    static final class Events {
        final List<Object> all=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void cast(forge.game.event.GameEventSpellAbilityCast e) {all.add(Map.of("event","cast","source",e.sa().getHostCard().getId(),"spell",e.sa().isSpell(),"stack",e.si().getId(),"description",e.toString()));}
        @com.google.common.eventbus.Subscribe public void damage(forge.game.event.GameEventPlayerDamaged e){all.add(Map.of("event","damage","source",e.source().getId(),"target",e.target().getId()+1,"amount",e.amount()));}
        @com.google.common.eventbus.Subscribe public void life(forge.game.event.GameEventPlayerLivesChanged e){all.add(Map.of("event","life","player",e.player().getId()+1,"before",e.oldLives(),"after",e.newLives()));}
        @com.google.common.eventbus.Subscribe public void zone(forge.game.event.GameEventZone e){if(e.card()!=null)all.add(Map.of("event","zone","id",e.card().getId(),"zone",e.zoneType().name(),"mode",e.mode().name()));}
        List<Object> snapshot(){synchronized(all){return List.copyOf(all);}}
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()-> {
            for(int i=0;i<2;i++)RoyalStateFixture.setup(ForgeBridge.game.getPlayers().get(i),ForgeBridge.config.getAsJsonObject(i==0?"own":"other"));
            var own=ForgeBridge.game.getPlayers().get(0);own.getController().getFullControl().add(PlayerController.FullControlFlag.ChooseCostOrder);own.getController().getFullControl().add(PlayerController.FullControlFlag.NoPaymentFromManaAbility);
            ForgeBridge.game.getAction().checkStaticAbilities();Events events=new Events();ForgeBridge.game.subscribeToEvents(events);
            Thread observer=new Thread(()-> {
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&!seen.contains(r.requestId)) {
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(snapshot(r.requestId,events)));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                    }}Thread.sleep(5);
                }catch(Exception e){e.printStackTrace();throw new RuntimeException(e);}
            },"payment-snapshot-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
