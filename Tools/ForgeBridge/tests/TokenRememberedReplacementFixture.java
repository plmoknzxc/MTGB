package mtgb.bridge;

import forge.game.ability.AbilityKey;
import forge.game.card.Card;
import forge.game.card.TokenCreateTable;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Initial positions only. All later changes are engine input; table/event observer is read-only. */
public final class TokenRememberedReplacementFixture {
    static Object remembered(Object x) {
        if (x instanceof Player p) return Map.of("kind","Player","seat",p.getId()+1,"nativeId",p.getId(),"identity",System.identityHashCode(p));
        if (x instanceof Card c) return Map.of("kind","Card","id",c.getId(),"identity",System.identityHashCode(c));
        return Map.of("kind",x.getClass().getName(),"value",x.toString());
    }
    static List<Object> memory(Card c) {List<Object> out=new ArrayList<>();for(Object x:c.getRemembered())out.add(remembered(x));return out;}
    static Map<String,Object> card(Card c) {
        Map<String,Object> out=new LinkedHashMap<>();out.put("id",c.getId());out.put("identity",System.identityHashCode(c));
        out.put("name",c.getName());out.put("originalName",c.getCurrentState().getName());out.put("owner",c.getOwner().getId()+1);
        out.put("controller",c.getController().getId()+1);out.put("timestamp",c.getGameTimestamp());
        try {var f=Card.class.getDeclaredField("controllerTimestamp");f.setAccessible(true);out.put("controllerTimestamp",f.getLong(c));}catch(Exception e){throw new IllegalStateException(e);}
        out.put("zone",c.getZone()==null?"":c.getZone().getZoneType().name());out.put("token",c.isToken());out.put("remembered",memory(c));
        out.put("power",c.getNetPower());out.put("toughness",c.getNetToughness());out.put("type",c.getType().toString());
        out.put("keywords",c.getUnhiddenKeywords().getValues().stream().map(k->k.getOriginal()).toList());
        out.put("cloneOrigin",c.getCloneOrigin()==null?0:c.getCloneOrigin().getId());
        out.put("copiedPermanent",c.getCopiedPermanent()==null?0:c.getCopiedPermanent().getId());return out;
    }
    static Map<String,Object> mana(forge.game.mana.Mana m) {return Map.of("identity",System.identityHashCode(m),"color",m.toString(),"sourceId",m.getSourceCard().getId(),"originalPlayer",m.getPlayer().getId()+1);}
    static List<Object> stack() {
        List<Object> out=new ArrayList<>();for(var si:ForgeBridge.game.getStack()) {
            var a=si.getSpellAbility();Map<String,Object> x=new LinkedHashMap<>();x.put("id",si.getId());x.put("source",a.getHostCard().getId());
            x.put("name",a.getHostCard().getName());x.put("api",a.getApi()==null?"":a.getApi().name());x.put("spell",a.isSpell());x.put("trigger",a.isTrigger());
            x.put("actor",a.getActivatingPlayer().getId()+1);x.put("params",a.getMapParams());x.put("payingMana",a.getPayingMana().stream().map(TokenRememberedReplacementFixture::mana).toList());out.add(x);
        }return out;
    }
    static List<Object> tables() {
        List<Object> out=new ArrayList<>();Set<TokenCreateTable> seen=Collections.newSetFromMap(new IdentityHashMap<>());
        for(Player p:ForgeBridge.game.getPlayers())for(ZoneType z:new ZoneType[]{ZoneType.Battlefield,ZoneType.Command})for(Card host:p.getCardsIn(z))for(var re:host.getReplacementEffects()) {
            var a=re.getOverridingAbility();if(a==null)continue;
            Object original=a.getReplacingObject(AbilityKey.OriginalParams);if(!(original instanceof Map<?,?> params))continue;
            Object value=params.get(AbilityKey.Token);if(!(value instanceof TokenCreateTable t)||!seen.add(t))continue;
            List<Object> cells=new ArrayList<>();for(var cell:t.cellSet())cells.add(Map.of("creator",cell.getRowKey().getId()+1,"amount",cell.getValue(),"prototype",card(cell.getColumnKey())));
            Map<String,Object> item=new LinkedHashMap<>();item.put("identity",System.identityHashCode(t));item.put("observedThroughEffect",re.getId());item.put("observedThroughSource",host.getId());
            item.put("hasRun",re.hasRun());item.put("effectParams",a.getMapParams());item.put("cells",cells);out.add(item);
        }return out;
    }
    static final class Events {
        final List<Object> all=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void zone(forge.game.event.GameEventZone e) {
            if(e.card()!=null&&e.zoneType()==ZoneType.Battlefield&&e.card().isToken())for(Card c:ForgeBridge.game.getCardsInGame())if(c.getId()==e.card().getId()) {
                all.add(Map.of("event","tokenZone","mode",e.mode().name(),"card",card(c),"tables",tables()));break;
            }
        }
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
                            Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(TokenRememberedReplacementFixture::card).toList());
                            players.add(Map.of("seat",p.getId()+1,"identity",System.identityHashCode(p),"nativeId",p.getId(),"life",p.getLife(),"zones",zones));
                        }
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                        Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",stack(),"tables",tables(),"events",events.snapshot())));
                        Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                    }}Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"token-remembered-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
