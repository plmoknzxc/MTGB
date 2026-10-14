package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;
/** Starting positions only; all token/counter/knowledge outcomes come from native inputs. */
public final class StateGuardFixture {
    static Map<String,Object> card(Card c) {
        Map<String,Object> m=LifePowerFixture.card(c);
        m.put("name",c.getState(forge.card.CardStateName.Original).getName());m.put("phased",c.isPhasedOut());m.put("held",c.isWontPhaseInNormal());m.put("types",c.getType().toString());m.put("manifested",c.isManifested());m.put("faceDown",c.isFaceDown());
        m.put("xPaid",c.getXManaCostPaid());m.put("castX",c.getCastSA()==null?null:c.getCastSA().getXManaCostPaid());
        m.put("manaValue",c.getCMC());m.put("chosenEvenOdd",c.getChosenEvenOdd()==null?"":c.getChosenEvenOdd().name());m.put("chosenCards",c.getChosenCards().stream().map(Card::getId).toList());m.put("chosenPlayer",c.getChosenPlayer()==null?0:c.getChosenPlayer().getId()+1);m.put("chosenType",c.getChosenType());m.put("chosenType2",c.getChosenType2());m.put("chosenColors",c.getChosenColors());m.put("namedCards",c.getNamedCards());
        m.put("currentColors",c.getColor().getColor());m.put("notedTypes",com.google.common.collect.Lists.newArrayList(c.getNotedTypes()));m.put("viewChosenType",c.getView().getChosenType());m.put("viewChosenType2",c.getView().getChosenType2());m.put("state",c.getCurrentStateName().name());m.put("currentName",c.getName());
        m.put("chosenPlayerView",c.getView().getChosenPlayer()==null?0:c.getView().getChosenPlayer().getId()+1);
        m.put("flipResults",c.getGame().getPlayers().stream().map(p->c.getFlipResult(p)==null?"":c.getFlipResult(p)).toList());m.put("protectingPlayer",c.getProtectingPlayer()==null?0:c.getProtectingPlayer().getId()+1);
        List<Integer> rememberedPlayers=new ArrayList<>();for(Object x:c.getRemembered())if(x instanceof Player)rememberedPlayers.add(((Player)x).getId()+1);m.put("rememberedPlayers",rememberedPlayers);
        m.put("creatureTypes",com.google.common.collect.Lists.newArrayList(c.getType().getCreatureTypes()));m.put("exiledWith",c.getExiledCards().stream().map(Card::getId).toList());m.put("deathtouch",c.hasKeyword(forge.game.keyword.Keyword.DEATHTOUCH));m.put("basePower",c.getBasePower());m.put("baseToughness",c.getBaseToughness());
        m.put("transformedTimestamp",c.getTransformedTimestamp());m.put("mergedIds",c.getMergedCards().stream().map(Card::getId).toList());return m;
    }
    static final List<forge.game.spellability.SpellAbility> held=new ArrayList<>();
    static void remember(forge.game.spellability.SpellAbility a) {
        if(a==null||held.stream().anyMatch(x->x==a))return;
        held.add(a);remember(a.getSubAbility());
        if(a instanceof forge.game.trigger.WrappedAbility)remember(((forge.game.trigger.WrappedAbility)a).getWrappedAbility());
    }
    static List<Map<String,Object>> guards() {
        for(var si:ForgeBridge.game.getStack())remember(si.getSpellAbility());
        List<Map<String,Object>> out=new ArrayList<>();
        for(var a:held)if(a.getApi()==forge.game.ability.ApiType.SetState) {
            Map<String,Object> m=new LinkedHashMap<>();m.put("identity",System.identityHashCode(a));m.put("api",a.getApi().name());m.put("mode",a.getParamOrDefault("Mode",""));m.put("source",a.getHostCard().getId());m.put("sourceTimestamp",a.getHostCard().getGameTimestamp());m.put("stored",a.getSVar("StoredTransform"));out.add(m);
        }return out;
    }
    static List<Map<String,Object>> delayed() {
        try {
            var h=ForgeBridge.game.getTriggerHandler();var f=h.getClass().getDeclaredField("delayedTriggers");f.setAccessible(true);
            List<Map<String,Object>> out=new ArrayList<>();
            for(Object o:(List<?>)f.get(h)){var t=(forge.game.trigger.Trigger)o;out.add(Map.of("sourceId",t.getHostCard().getId(),"sourceTimestamp",t.getHostCard().getGameTimestamp(),"mode",t.getParam("Mode"),"sourceName",t.getHostCard().getName()));}
            return out;
        }catch(Exception e){throw new RuntimeException(e);}
    }
    static List<Card> all(Player p){Map<Integer,Card> out=new LinkedHashMap<>();for(Card c:p.getAllCards())out.put(c.getId(),c);for(Card c:p.getZone(ZoneType.Battlefield).getCards(false))out.put(c.getId(),c);return new ArrayList<>(out.values());}
    public static final class ZoneAudit {
        final List<Map<String,Object>> selections=Collections.synchronizedList(new ArrayList<>());
        final List<Map<String,Object>> lifeEvents=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void anyEvent(forge.game.event.GameEvent e){
            for(Player p:ForgeBridge.game.getPlayers())for(Card c:all(p))if((c.getName().equals("Whimsy")||c.getName().equals("Faerie Dragon"))&&(c.hasChosenCard()||c.hasChosenPlayer()||c.getRememberedCount()!=0)){
                List<String> remembered=new ArrayList<>();for(Object o:c.getRemembered())remembered.add(o.toString());
                Map<String,Object> s=new LinkedHashMap<>();s.put("sourceId",c.getId());s.put("event",e.getClass().getSimpleName());s.put("chosenCards",c.getChosenCards().stream().map(Card::getId).toList());s.put("chosenPlayer",c.getChosenPlayer()==null?0:c.getChosenPlayer().getId()+1);s.put("remembered",remembered);selections.add(s);
            }
        }
        @com.google.common.eventbus.Subscribe public void lifeChanged(forge.game.event.GameEventPlayerLivesChanged e){lifeEvents.add(Map.of("seat",e.player().getId()+1,"old",e.oldLives(),"new",e.newLives()));}
        List<Map<String,Object>> selectionSnapshot(){synchronized(selections){return List.copyOf(selections);}}
        List<Map<String,Object>> lifeSnapshot(){synchronized(lifeEvents){return List.copyOf(lifeEvents);}}
        final List<Map<String,Object>> logs=Collections.synchronizedList(new ArrayList<>());
        final List<Map<String,Object>> typeEvents=Collections.synchronizedList(new ArrayList<>());
        final List<Map<String,Object>> damage=Collections.synchronizedList(new ArrayList<>());
        final List<Map<String,Object>> named=Collections.synchronizedList(new ArrayList<>());
        final List<Map<String,Object>> events=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void changed(forge.game.event.GameEventZone e) {
            if(e.card()!=null&&e.mode().name().equals("Added")){
                for(Player p:ForgeBridge.game.getPlayers())for(Card c:p.getCardsIn(ZoneType.Battlefield))if(!c.getChosenType().isEmpty()||!c.getChosenType2().isEmpty())typeEvents.add(Map.of("sourceId",c.getId(),"chosenType",c.getChosenType(),"chosenType2",c.getChosenType2(),"movedId",e.card().getId(),"zone",e.zoneType().name()));
            }
            if(e.card()!=null&&e.mode().name().equals("Added")&&e.zoneType()==ZoneType.Battlefield){
                for(Player p:ForgeBridge.game.getPlayers())for(Card c:p.getCardsIn(ZoneType.Battlefield))if(!c.getNamedCards().isEmpty())named.add(Map.of("sourceId",c.getId(),"named",List.copyOf(c.getNamedCards()),"createdId",e.card().getId()));
            }
            if(e.card()!=null)events.add(Map.of("id",e.card().getId(),"zone",e.zoneType().name(),"mode",e.mode().name()));
        }
        @com.google.common.eventbus.Subscribe public void damaged(forge.game.event.GameEventPlayerDamaged e){
            List<String> flips=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers())for(Card c:p.getAllCards())if(c.getId()==e.source().getId()){for(Player q:ForgeBridge.game.getPlayers())flips.add(c.getFlipResult(q)==null?"":c.getFlipResult(q));break;}
            damage.add(Map.of("sourceId",e.source().getId(),"targetSeat",e.target().getId()+1,"amount",e.amount(),"flips",flips));
        }
        @com.google.common.eventbus.Subscribe public void logged(forge.game.event.GameEventRandomLog e){
            List<Map<String,Object>> sources=new ArrayList<>();
            for(Player p:ForgeBridge.game.getPlayers())for(Card c:all(p))if((c.getName().equals("Whimsy")||c.getName().equals("Faerie Dragon"))&&c.getZone().getZoneType()!=ZoneType.Library)sources.add(card(c));
            logs.add(Map.of("message",e.message(),"sources",sources));
        }
        List<Map<String,Object>> logSnapshot(){synchronized(logs){return List.copyOf(logs);}}
        List<Map<String,Object>> typeSnapshot(){synchronized(typeEvents){return List.copyOf(typeEvents);}}
        List<Map<String,Object>> damageSnapshot(){synchronized(damage){return List.copyOf(damage);}}
        List<Map<String,Object>> namedSnapshot(){synchronized(named){return List.copyOf(named);}}
        List<Map<String,Object>> snapshot(){synchronized(events){return List.copyOf(events);}}
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++) {
                Player p=ForgeBridge.game.getPlayers().get(seat-1);
                for(String land:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int j=0;j<8;j++)StateBranchFixture.place(land,p,ZoneType.Battlefield);
                StateBranchFixture.setup(p,ForgeBridge.config.getAsJsonObject(seat==1?"own":"other"));
                var spec=ForgeBridge.config.getAsJsonObject(seat==1?"own":"other");
                if(spec.has("Sideboard"))for(var name:spec.getAsJsonArray("Sideboard"))StateBranchFixture.place(name.getAsString(),p,ZoneType.Sideboard);
                if(seat==1&&ForgeBridge.config.get("emptyLibrary").getAsBoolean())
                    for(Card c:new ArrayList<>(p.getCardsIn(ZoneType.Library))){p.getZone(ZoneType.Library).remove(c);p.getZone(ZoneType.Exile).add(c);}
            }
            int limit=ForgeBridge.config.get("libraryLimit").getAsInt();
            if(limit>=0) {
                Player p=ForgeBridge.game.getPlayers().get(0);
                List<Card> cards=new ArrayList<>(p.getCardsIn(ZoneType.Library));
                for(int i=limit;i<cards.size();i++){p.getZone(ZoneType.Library).remove(cards.get(i));p.getZone(ZoneType.Exile).add(cards.get(i));}
            }
            if(ForgeBridge.config.has("faceDown"))for(var x:ForgeBridge.config.getAsJsonArray("faceDown")) {
                var a=x.getAsJsonArray();var p=ForgeBridge.game.getPlayers().get(a.get(0).getAsInt()-1);
                p.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(a.get(1).getAsString())).findFirst().orElseThrow().turnFaceDown(true);
            }
            if(ForgeBridge.config.has("attachments"))for(var x:ForgeBridge.config.getAsJsonArray("attachments")) {
                var a=x.getAsJsonArray();var p=ForgeBridge.game.getPlayers().get(a.get(0).getAsInt()-1);
                var field=p.getCardsIn(ZoneType.Battlefield);
                field.stream().filter(c->c.getName().equals(a.get(1).getAsString())).findFirst().orElseThrow().attachToEntity(field.stream().filter(c->c.getName().equals(a.get(2).getAsString())).findFirst().orElseThrow(),null);
            }
            if(ForgeBridge.config.get("otherLibraryLimit").getAsInt()>=0){var p=ForgeBridge.game.getPlayers().get(1);var lib=new ArrayList<>(p.getCardsIn(ZoneType.Library));for(int i=ForgeBridge.config.get("otherLibraryLimit").getAsInt();i<lib.size();i++){p.getZone(ZoneType.Library).remove(lib.get(i));p.getZone(ZoneType.Exile).add(lib.get(i));}}
            ForgeBridge.game.getAction().checkStaticAbilities();
            ZoneAudit zoneAudit=new ZoneAudit();ForgeBridge.game.subscribeToEvents(zoneAudit);
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&seen.add(r.requestId)) {
                            List<Object> players=new ArrayList<>();
                            for(Player p:ForgeBridge.game.getPlayers()) {
                                Map<String,Object> item=new LinkedHashMap<>();
                                item.put("poolRed",p.getManaPool().getAmountOfColor(forge.card.MagicColor.RED));item.put("poolTotal",p.getManaPool().totalMana());item.put("poolGreen",p.getManaPool().getAmountOfColor(forge.card.MagicColor.GREEN));item.put("life",p.getLife());item.put("controlled",p.isControlled());item.put("controllingSeat",p.getControllingPlayer()==null?0:ForgeBridge.game.getRegisteredPlayers().indexOf(p.getControllingPlayer())+1);item.put("sideboard",p.getCardsIn(ZoneType.Sideboard).stream().map(StateGuardFixture::card).toList());item.put("surveilled",p.getSurveilThisTurn());item.put("investigated",p.getInvestigateNumThisTurn());item.put("explored",p.getNumExploredThisTurn());
                                item.put("cards",all(p).stream().map(StateGuardFixture::card).toList());item.put("library",p.getCardsIn(ZoneType.Library).stream().map(Card::getId).toList());players.add(item);
                            }
                            Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(r.requestId+".tmp"),dst=d.resolve(r.requestId+".json");
                            Map<String,Object> report=new LinkedHashMap<>();report.put("night",ForgeBridge.game.isNight());report.put("guards",guards());report.put("players",players);report.put("requestId",r.requestId);report.put("stack",CombatRelationFixture.stack());report.put("blockError","");report.put("zoneEvents",zoneAudit.snapshot());report.put("damageEvents",zoneAudit.damageSnapshot());report.put("namedEvents",zoneAudit.namedSnapshot());report.put("cleanupLogs",zoneAudit.logSnapshot());report.put("chosenTypeEvents",zoneAudit.typeSnapshot());report.put("delayed",delayed());report.put("selections",zoneAudit.selectionSnapshot());report.put("lifeEvents",zoneAudit.lifeSnapshot());
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(report));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"token-explore-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
