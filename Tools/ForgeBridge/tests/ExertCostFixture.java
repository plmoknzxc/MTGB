package mtgb.bridge;

import forge.game.card.CounterType;
import forge.game.player.PlayerController;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Opening positions/cost ordering only. All payments and outcomes are native. */
public final class ExertCostFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int i=0;i<2;i++)StateBranchFixture.setup(ForgeBridge.game.getPlayers().get(i),ForgeBridge.config.getAsJsonObject(i==0?"own":"other"));
            var own=ForgeBridge.game.getPlayers().get(0);
            // Optional exact opening library for affordability boundaries; never used after play starts.
            if(ForgeBridge.config.getAsJsonObject("own").has("libraryLimit")) {
                int limit=ForgeBridge.config.getAsJsonObject("own").get("libraryLimit").getAsInt();
                var library=own.getZone(ZoneType.Library);
                for(var card:new ArrayList<>(own.getCardsIn(ZoneType.Library)).subList(limit,library.size()))library.remove(card);
            }
            own.getController().getFullControl().add(PlayerController.FullControlFlag.ChooseCostOrder);
            own.getController().getFullControl().add(PlayerController.FullControlFlag.NoPaymentFromManaAbility);
            for(var card:own.getCardsIn(ZoneType.Battlefield))if(card.getName().equals("Pentavus"))card.setCounters(CounterType.getType("P1P1"),5);
            if(ForgeBridge.config.getAsJsonObject("own").has("attach")) {
                var spec=ForgeBridge.config.getAsJsonObject("own").getAsJsonObject("attach");
                var equipment=own.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(spec.get("equipment").getAsString())).findFirst().orElseThrow();
                var creature=own.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals(spec.get("creature").getAsString())).findFirst().orElseThrow();
                equipment.attachToEntity(creature,null);
            }
            if(ForgeBridge.config.getAsJsonObject("own").has("counters"))
                for(var row:ForgeBridge.config.getAsJsonObject("own").getAsJsonArray("counters")) {
                    var spec=row.getAsJsonObject();
                    for(var card:own.getCardsIn(ZoneType.Battlefield))if(card.getName().equals(spec.get("name").getAsString()))
                        card.setCounters(CounterType.getType(spec.get("kind").getAsString()),spec.get("count").getAsInt());
                }
            ForgeBridge.game.getAction().checkStaticAbilities();
            // Opening-only pre-existing opponent marker: cancellation must preserve it.
            if (ForgeBridge.config.has("initialExertedOpponent")) {
                var other = ForgeBridge.game.getPlayers().get(1);
                other.getCardsIn(ZoneType.Battlefield).stream().filter(c -> c.getName().equals("Grizzly Bears")).findFirst().orElseThrow().exert(other);
            }
            Thread observer=new Thread(()->{
                Set<Long> saved=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&saved.add(r.requestId)) {
                            List<Object> players=new ArrayList<>();
                            for(var p:ForgeBridge.game.getPlayers()) {
                                Map<String,Object> pv=new LinkedHashMap<>(),zones=new LinkedHashMap<>();
                                pv.put("seat",ForgeBridge.game.getPlayers().indexOf(p)+1);pv.put("life",p.getLife());
                                pv.put("counters",p.getCounters().toString());
                                for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Command}) {
                                    List<Object> cards=new ArrayList<>();
                                    for(var c:p.getCardsIn(z))cards.add(Map.of("id",c.getId(),"name",c.getName(),"tapped",c.isTapped(),"counters",c.getCounters().toString(),"attachedTo",c.getEntityAttachedTo()==null?-1:c.getEntityAttachedTo().getId(),"exertedThisTurn",c.getExertedThisTurn(),"exertedBy",ForgeBridge.game.getPlayers().stream().filter(c::isExertedBy).map(x->ForgeBridge.game.getPlayers().indexOf(x)+1).toList()));
                                    zones.put(z.name(),cards);
                                }
                                pv.put("zones",zones);players.add(pv);
                            }
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players)));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"cost-primitive-read-only-observer");observer.setDaemon(true);observer.start();
        });
    }
}
