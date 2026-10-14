package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;
/** Starting positions only; all token/counter/knowledge outcomes come from native inputs. */
public final class BranchXFixture {
    static Map<String,Object> card(Card c) {
        Map<String,Object> m=LifePowerFixture.card(c);
        m.put("types",c.getType().toString());m.put("manifested",c.isManifested());m.put("faceDown",c.isFaceDown());
        m.put("xPaid",c.getXManaCostPaid());m.put("castX",c.getCastSA()==null?null:c.getCastSA().getXManaCostPaid());
        m.put("basePower",c.getBasePower());m.put("baseToughness",c.getBaseToughness());
        return m;
    }
    public static final class ZoneAudit {
        final List<Map<String,Object>> events=Collections.synchronizedList(new ArrayList<>());
        @com.google.common.eventbus.Subscribe public void changed(forge.game.event.GameEventZone e) {
            if(e.card()!=null)events.add(Map.of("id",e.card().getId(),"zone",e.zoneType().name(),"mode",e.mode().name()));
        }
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
                                item.put("life",p.getLife());item.put("controlled",p.isControlled());item.put("controllingSeat",p.getControllingPlayer()==null?0:ForgeBridge.game.getRegisteredPlayers().indexOf(p.getControllingPlayer())+1);item.put("sideboard",p.getCardsIn(ZoneType.Sideboard).stream().map(BranchXFixture::card).toList());item.put("surveilled",p.getSurveilThisTurn());item.put("investigated",p.getInvestigateNumThisTurn());item.put("explored",p.getNumExploredThisTurn());
                                item.put("cards",p.getAllCards().stream().map(BranchXFixture::card).toList());item.put("library",p.getCardsIn(ZoneType.Library).stream().map(Card::getId).toList());players.add(item);
                            }
                            Path d=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=d.resolve(r.requestId+".tmp"),dst=d.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("players",players,"requestId",r.requestId,"stack",CombatRelationFixture.stack(),"blockError","","zoneEvents",zoneAudit.snapshot())));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"token-explore-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
