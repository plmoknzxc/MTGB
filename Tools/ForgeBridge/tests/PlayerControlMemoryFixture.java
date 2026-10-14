package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;
/** Starting positions only; all token/counter/knowledge outcomes come from native inputs. */
public final class PlayerControlMemoryFixture {
    static Map<String,Object> card(Card c) {
        Map<String,Object> m=LifePowerFixture.card(c);
        m.put("name",c.getState(forge.card.CardStateName.Original).getName());m.put("phased",c.isPhasedOut());m.put("held",c.isWontPhaseInNormal());m.put("types",c.getType().toString());m.put("manifested",c.isManifested());m.put("faceDown",c.isFaceDown());
        m.put("xPaid",c.getXManaCostPaid());m.put("castX",c.getCastSA()==null?null:c.getCastSA().getXManaCostPaid());
        m.put("manaValue",c.getCMC());m.put("chosenEvenOdd",c.getChosenEvenOdd()==null?"":c.getChosenEvenOdd().name());m.put("chosenCards",c.getChosenCards().stream().map(Card::getId).toList());m.put("chosenPlayer",c.getChosenPlayer()==null?0:c.getChosenPlayer().getId()+1);m.put("chosenType",c.getChosenType());m.put("chosenType2",c.getChosenType2());m.put("chosenColors",c.getChosenColors());m.put("namedCards",c.getNamedCards());
        m.put("chosenPlayerView",c.getView().getChosenPlayer()==null?0:c.getView().getChosenPlayer().getId()+1);
        m.put("flipResults",c.getGame().getPlayers().stream().map(p->c.getFlipResult(p)==null?"":c.getFlipResult(p)).toList());m.put("protectingPlayer",c.getProtectingPlayer()==null?0:c.getProtectingPlayer().getId()+1);
        List<Integer> rememberedPlayers=new ArrayList<>();for(Object x:c.getRemembered())if(x instanceof Player)rememberedPlayers.add(((Player)x).getId()+1);m.put("rememberedPlayers",rememberedPlayers);
        m.put("basePower",c.getBasePower());m.put("baseToughness",c.getBaseToughness());
        return m;
    }
    static List<Card> all(Player p){Map<Integer,Card> out=new LinkedHashMap<>();for(Card c:p.getAllCards())out.put(c.getId(),c);for(Card c:p.getZone(ZoneType.Battlefield).getCards(false))out.put(c.getId(),c);return new ArrayList<>(out.values());}
    public static final class ZoneAudit {
        final List<Map<String,Object>> damage=Collections.synchronizedList(new ArrayList<>());
        final List<Map<String,Object>> named=Collections.synchronizedList(new ArrayList<>());
        final List<Map<String,Object>> events=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void changed(forge.game.event.GameEventZone e) {
            if(e.card()!=null&&e.mode().name().equals("Added")&&e.zoneType()==ZoneType.Battlefield){
                for(Player p:ForgeBridge.game.getPlayers())for(Card c:p.getCardsIn(ZoneType.Battlefield))if(!c.getNamedCards().isEmpty())named.add(Map.of("sourceId",c.getId(),"named",List.copyOf(c.getNamedCards()),"createdId",e.card().getId()));
            }
            if(e.card()!=null)events.add(Map.of("id",e.card().getId(),"zone",e.zoneType().name(),"mode",e.mode().name()));
        }
        @com.google.common.eventbus.Subscribe public void damaged(forge.game.event.GameEventPlayerDamaged e){
            List<String> flips=new ArrayList<>();for(Player p:ForgeBridge.game.getPlayers())for(Card c:p.getAllCards())if(c.getId()==e.source().getId()){for(Player q:ForgeBridge.game.getPlayers())flips.add(c.getFlipResult(q)==null?"":c.getFlipResult(q));break;}
            damage.add(Map.of("sourceId",e.source().getId(),"targetSeat",e.target().getId()+1,"amount",e.amount(),"flips",flips));
        }
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
                                item.put("poolRed",p.getManaPool().getAmountOfColor(forge.card.MagicColor.RED));item.put("poolTotal",p.getManaPool().totalMana());item.put("poolGreen",p.getManaPool().getAmountOfColor(forge.card.MagicColor.GREEN));item.put("life",p.getLife());item.put("controlled",p.isControlled());item.put("controllingSeat",p.getControllingPlayer()==null?0:ForgeBridge.game.getRegisteredPlayers().indexOf(p.getControllingPlayer())+1);item.put("sideboard",p.getCardsIn(ZoneType.Sideboard).stream().map(PlayerControlMemoryFixture::card).toList());item.put("surveilled",p.getSurveilThisTurn());item.put("investigated",p.getInvestigateNumThisTurn());item.put("explored",p.getNumExploredThisTurn());
                                item.put("cards",all(p).stream().map(PlayerControlMemoryFixture::card).toList());item.put("library",p.getCardsIn(ZoneType.Library).stream().map(Card::getId).toList());players.add(item);
                            }
                            Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(r.requestId+".tmp"),dst=d.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("players",players,"requestId",r.requestId,"stack",CombatRelationFixture.stack(),"blockError","","zoneEvents",zoneAudit.snapshot(),"damageEvents",zoneAudit.damageSnapshot(),"namedEvents",zoneAudit.namedSnapshot())));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"token-explore-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
