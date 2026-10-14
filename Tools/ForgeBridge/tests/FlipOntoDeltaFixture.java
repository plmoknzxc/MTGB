package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.util.MyRandom;
import java.nio.file.*;
import java.util.*;

/** Opening positions and ordinary seeded Random only; later observations never mutate play. */
public final class FlipOntoDeltaFixture {
    static final List<Object> randomCalls=Collections.synchronizedList(new ArrayList<>());
    static final List<Card> sources=new ArrayList<>();
    static final Map<Integer,forge.game.spellability.SpellAbility> abilities=new LinkedHashMap<>();
    static final class RecordedRandom extends Random {
        RecordedRandom(long seed){super(seed);}
        boolean flip(){return Arrays.stream(Thread.currentThread().getStackTrace()).anyMatch(f->f.getClassName().endsWith("FlipOntoBattlefieldEffect"));}
        @Override public float nextFloat(){float v=super.nextFloat();if(flip())randomCalls.add(Map.of("method","nextFloat","value",v));return v;}
        @Override public int nextInt(int b){int v=super.nextInt(b);if(flip())randomCalls.add(Map.of("method","nextInt","bound",b,"value",v));return v;}
    }
    static List<Object> remembered(){List<Object> rows=new ArrayList<>();for(Card opening:sources){Card c=abilities.containsKey(opening.getId())?abilities.get(opening.getId()).getHostCard():opening;List<Object> ids=new ArrayList<>();for(Object o:c.getRemembered())ids.add(o instanceof Card?((Card)o).getId():String.valueOf(o));rows.add(Map.of("id",c.getId(),"remembered",ids));}return rows;}
    static Map<String,Object> card(Card c){
        Map<String,Object> m=new LinkedHashMap<>();m.put("id",c.getId());m.put("name",c.getName());m.put("owner",c.getOwner().getId()+1);m.put("controller",c.getController().getId()+1);
        m.put("zone",c.getZone().getZoneType().name());m.put("timestamp",c.getGameTimestamp());m.put("tapped",c.isTapped());m.put("token",c.isToken());m.put("tokenCard",c.isTokenCard());
        m.put("creature",c.isCreature());m.put("land",c.isLand());m.put("artifact",c.isArtifact());m.put("attachedTo",c.getEntityAttachedTo()==null?0:c.getEntityAttachedTo().getId());
        m.put("power",c.getNetPower());m.put("toughness",c.getNetToughness());m.put("damage",c.getDamage());return m;
    }
    static final class Events {
        final List<Object> all=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void damaged(forge.game.event.GameEventCardDamaged e){all.add(Map.of("event","damage","source",e.source().getId(),"target",e.card().getId(),"amount",e.amount(),"remembered",remembered()));}
        @com.google.common.eventbus.Subscribe public void zone(forge.game.event.GameEventZone e){if(e.card()!=null)all.add(Map.of("event","zone","id",e.card().getId(),"zone",e.zoneType().name(),"mode",e.mode().name(),"remembered",remembered()));}
        List<Object> snapshot(){synchronized(all){return List.copyOf(all);}}
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++){
                Player p=ForgeBridge.game.getPlayers().get(seat-1);var spec=ForgeBridge.config.getAsJsonArray("positions").get(seat-1).getAsJsonObject();RoyalStateFixture.setup(p,spec);
                if(spec.has("tokens"))for(var name:spec.getAsJsonArray("tokens"))p.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(name.getAsString())).forEach(c->c.setTokenCard(true));
                if(spec.has("realTokens"))for(var name:spec.getAsJsonArray("realTokens"))p.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(name.getAsString())).forEach(c->{c.setTokenCard(true);c.setGamePieceType(forge.card.GamePieceType.TOKEN);});
                if(spec.has("attach")){var a=spec.getAsJsonObject("attach");Card attachment=p.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(a.get("attachment").getAsString())).findFirst().orElseThrow();Card target=p.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(a.get("target").getAsString())).findFirst().orElseThrow();attachment.attachToEntity(target,null);}
                for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Battlefield})for(Card c:p.getCardsIn(z))if(c.getName().equals("Falling Star")||c.getName().equals("Chaos Orb"))sources.add(c);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();Events events=new Events();ForgeBridge.game.subscribeToEvents(events);MyRandom.setRandom(new RecordedRandom(ForgeBridge.config.get("fixtureSeed").getAsLong()));
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try{
                    synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&!seen.contains(r.requestId)){
                        List<Object> players=new ArrayList<>(),stack=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()){
                            Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack})zones.put(z.name(),p.getCardsIn(z).stream().map(FlipOntoDeltaFixture::card).toList());players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"pool",p.getManaPool().totalMana(),"zones",zones));
                        }
                        for(var si:ForgeBridge.game.getStack()){var a=si.getSpellAbility();Map<String,Object> x=new LinkedHashMap<>();x.put("source",a.getHostCard().getId());x.put("trigger",a.isTrigger());x.put("api",a.getApi()==null?"":a.getApi().name());x.put("mana",a.getPayingMana().stream().map(m->Map.of("source",m.getSourceCard().getId(),"color",String.valueOf(m.getColor()))).toList());x.put("description",a.getDescription());stack.add(x);if(a.getApi()==forge.game.ability.ApiType.FlipOntoBattlefield)abilities.put(a.getHostCard().getId(),a);}
                        List<Object> resolved=abilities.entrySet().stream().map(e->(Object)Map.of("source",e.getKey(),"timesFlipped",e.getValue().getSVar("TimesFlipped"))).toList();
                        Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",stack,"abilities",resolved,"remembered",remembered(),"events",events.snapshot(),"randomCalls",List.copyOf(randomCalls))));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                    }}Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"flip-delta-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
