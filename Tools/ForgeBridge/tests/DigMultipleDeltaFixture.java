package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Opening arrangement only; actual paid spells and attack triggers determine results. */
public final class DigMultipleDeltaFixture {
    static Object identity(Object value) {
        if(value instanceof Card)return ((Card)value).getId();
        if(value instanceof Iterable){List<Object> out=new ArrayList<>();for(Object x:(Iterable<?>)value)out.add(identity(x));return out;}
        return value==null?null:String.valueOf(value);
    }
    static Map<String,Object> card(Card c) {
        Map<String,Object> m=new LinkedHashMap<>();m.put("id",c.getId());m.put("name",c.getName());m.put("printedName",c.getState(forge.card.CardStateName.Original).getName());m.put("currentName",c.getCurrentState().getName());
        m.put("owner",c.getOwner().getId()+1);m.put("controller",c.getController().getId()+1);m.put("zone",c.getZone().getZoneType().name());m.put("timestamp",c.getGameTimestamp());m.put("tapped",c.isTapped());m.put("remembered",identity(c.getRemembered()));m.put("imprinted",identity(c.getImprintedCards()));return m;
    }
    static List<Object> stack(){List<Object> out=new ArrayList<>();for(var si:ForgeBridge.game.getStack()){
        var a=si.getSpellAbility();Map<String,Object> m=new LinkedHashMap<>();m.put("id",si.getId());m.put("source",a.getHostCard().getId());m.put("name",a.getHostCard().getName());m.put("actor",a.getActivatingPlayer().getId()+1);m.put("spell",a.isSpell());m.put("trigger",a.isTrigger());m.put("api",a.getApi()==null?"":a.getApi().name());m.put("x",a.getXManaCostPaid()==null?-1:a.getXManaCostPaid());out.add(m);
    }return out;}
    static final class Events {
        final List<Object> all=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void zone(forge.game.event.GameEventZone e){if(e.card()!=null)all.add(Map.of("id",e.card().getId(),"zone",e.zoneType().name(),"mode",e.mode().name()));}
        List<Object> snapshot(){synchronized(all){return List.copyOf(all);}}
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int i=0;i<2;i++){Player p=ForgeBridge.game.getPlayers().get(i);RoyalStateFixture.setup(p,ForgeBridge.config.getAsJsonArray("positions").get(i).getAsJsonObject());for(String name:new String[]{"Forest","Plains","Island"})for(int j=0;j<4;j++)RoyalStateFixture.place(name,p,ZoneType.Battlefield);}
            ForgeBridge.game.getAction().checkStaticAbilities();Events events=new Events();ForgeBridge.game.subscribeToEvents(events);
            Thread observer=new Thread(()->{Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try{
                synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&!seen.contains(r.requestId)){
                    List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()){
                        Map<String,Object> zs=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack,ZoneType.Command})zs.put(z.name(),p.getCardsIn(z).stream().map(DigMultipleDeltaFixture::card).toList());
                        players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"pool",p.getManaPool().totalMana(),"zones",zs));
                    }
                    Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(r.requestId+".tmp"),dst=d.resolve(r.requestId+".json");Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",stack(),"events",events.snapshot(),"turn",ForgeBridge.game.getPhaseHandler().getTurn())));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                }}Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}},"dig-multiple-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
