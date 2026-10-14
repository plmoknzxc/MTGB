package mtgb.bridge;

import forge.game.card.Card;
import forge.game.mana.Mana;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.game.spellability.AbilityManaPart;
import java.nio.file.*;
import java.util.*;

/** Initial positions only. Native operations produce every later result; observers read only. */
public final class ManaReplacementTailFixture {
    static final IdentityHashMap<Mana,Integer> identities=new IdentityHashMap<>();
    static final IdentityHashMap<AbilityManaPart,Integer> abilities=new IdentityHashMap<>();
    static synchronized Map<String,Object> mana(Mana m) {
        var a=m.getManaAbility();Map<String,Object> out=new LinkedHashMap<>();
        out.put("identity",identities.computeIfAbsent(m,x->identities.size()+1));out.put("color",m.toString());
        out.put("source",m.getSourceCard().getId());out.put("sourceName",m.getSourceCard().getName());
        out.put("originalPlayer",m.getPlayer().getId()+1);out.put("snow",m.isSnow());out.put("restricted",m.isRestricted());
        out.put("persistent",m.isPersistentMana());out.put("combat",m.isCombatMana());
        out.put("abilityIdentity",a==null?0:abilities.computeIfAbsent(a,x->abilities.size()+1));
        out.put("originalProduced",a==null?"":a.getOrigProduced());out.put("restriction",a==null?"":a.getManaRestrictions());
        out.put("extraRestriction",a==null?"":a.getExtraManaRestriction());return out;
    }
    static List<Object> pool(Player p){List<Object> out=new ArrayList<>();for(Mana m:p.getManaPool())out.add(mana(m));return out;}
    static Map<String,Object> card(Card c) {
        Map<String,Object> out=new LinkedHashMap<>();out.put("id",c.getId());out.put("name",c.getName());
        out.put("owner",c.getOwner().getId()+1);out.put("controller",c.getController().getId()+1);out.put("zone",c.getZone().getZoneType().name());
        out.put("timestamp",c.getGameTimestamp());out.put("tapped",c.isTapped());out.put("sick",c.isSick());
        List<String> chosen=new ArrayList<>();for(String s:c.getChosenColors())chosen.add(s);out.put("chosenColors",chosen);
        List<Object> remembered=new ArrayList<>();for(Object r:c.getRemembered())remembered.add(r instanceof Card?((Card)r).getId():String.valueOf(r));out.put("remembered",remembered);
        List<Object> replacements=new ArrayList<>();for(var re:c.getReplacementEffects())replacements.add(Map.of("id",re.getId(),"params",re.getMapParams(),"description",re.toString()));out.put("replacements",replacements);
        List<Object> produced=new ArrayList<>();for(var a:c.getManaAbilities())for(var part:a.getAllManaParts())for(Mana m:part.getLastManaProduced())produced.add(mana(m));out.put("lastProduced",produced);return out;
    }
    static List<Object> stack() {
        List<Object> out=new ArrayList<>();for(var si:ForgeBridge.game.getStack()) {
            var a=si.getSpellAbility();var host=a.getHostCard();Map<String,Object> row=new LinkedHashMap<>();
            row.put("id",si.getId());row.put("abilityId",a.getId());row.put("source",host.getId());row.put("name",host.getName());
            row.put("actor",a.getActivatingPlayer().getId()+1);row.put("api",a.getApi()==null?"":a.getApi().name());
            row.put("spell",a.isSpell());row.put("trigger",a.isTrigger());row.put("payingMana",a.getPayingMana().stream().map(ManaReplacementTailFixture::mana).toList());
            row.put("targets",a.getTargets().getTargetCards().stream().map(Card::getId).toList());row.put("description",a.getDescription());out.add(row);
        }return out;
    }
    static final class Events {
        final List<Object> all=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void poolChanged(forge.game.event.GameEventManaPool e){all.add(Map.of("event","manaPool","seat",e.player().getId()+1,"mode",e.mode().name(),"mana",pool(ForgeBridge.game.getPlayers().get(e.player().getId()))));}
        @com.google.common.eventbus.Subscribe public void life(forge.game.event.GameEventPlayerLivesChanged e){all.add(Map.of("event","life","seat",e.player().getId()+1,"before",e.oldLives(),"after",e.newLives()));}
        @com.google.common.eventbus.Subscribe public void zone(forge.game.event.GameEventZone e){if(e.card()!=null)all.add(Map.of("event","zone","id",e.card().getId(),"zone",e.zoneType().name(),"mode",e.mode().name()));}
        List<Object> snapshot(){synchronized(all){return List.copyOf(all);}}
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int i=0;i<2;i++)RoyalStateFixture.setup(ForgeBridge.game.getPlayers().get(i),ForgeBridge.config.getAsJsonArray("positions").get(i).getAsJsonObject());
            ForgeBridge.game.getAction().checkStaticAbilities();Events events=new Events();ForgeBridge.game.subscribeToEvents(events);
            Thread observer=new Thread(()->{Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try {
                synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&!seen.contains(r.requestId)) {
                    List<Object> players=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers()) {
                        Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(ManaReplacementTailFixture::card).toList());
                        players.add(Map.of("seat",p.getId()+1,"life",p.getLife(),"mana",pool(p),"zones",zones));
                    }
                    Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                    Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players,"stack",stack(),"events",events.snapshot(),"turn",ForgeBridge.game.getPhaseHandler().getTurn(),"phase",ForgeBridge.game.getPhaseHandler().getPhase().name())));
                    Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
                }}Thread.sleep(5);
            }catch(Exception e){throw new RuntimeException(e);}},"mana-replacement-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
