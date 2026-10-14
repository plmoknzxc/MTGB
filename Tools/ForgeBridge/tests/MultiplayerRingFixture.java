package mtgb.bridge;

import forge.ai.LobbyPlayerAi;
import forge.deck.Deck;
import forge.game.player.RegisteredPlayer;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Four native seats via the existing test registration hook; only initial state is arranged.
 * Seats 3/4 are native AI with no interaction cards. Production lobby/UI remain two-seat.
 * The observer reads all four native players, independently of the two bridge views. */
public final class MultiplayerRingFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            int actor=ForgeBridge.config.get("actor").getAsInt();
            for(int i=0;i<4;i++) {
                var p=ForgeBridge.game.getPlayers().get(i);
                var spec=new com.google.gson.JsonObject();
                if(i+1==actor) {
                    var hand=new com.google.gson.JsonArray();
                    for(int j=0;j<4;j++)hand.add("Birthday Escape");
                    spec.add("Hand",hand);
                    var field=new com.google.gson.JsonArray();field.add("Grizzly Bears");
                    for(int j=0;j<6;j++)field.add("Island");
                    spec.add("Battlefield",field);
                    var library=new com.google.gson.JsonArray();
                    for(int j=0;j<10;j++)library.add("Island");
                    spec.add("Library",library);
                }
                RoyalStateFixture.setup(p,spec);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            Thread observer=new Thread(()->{
                Set<Long> saved=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&saved.add(r.requestId)) {
                            List<Object> players=new ArrayList<>();
                            for(var p:ForgeBridge.game.getPlayers()) {
                                Map<String,Object> pv=new LinkedHashMap<>();
                                pv.put("seat",ForgeBridge.game.getPlayers().indexOf(p)+1);
                                pv.put("id",p.getId());pv.put("life",p.getLife());
                                pv.put("ringTempted",p.getNumRingTemptedYou());
                                pv.put("ringBearer",p.getRingBearer()==null?0:p.getRingBearer().getId());
                                pv.put("counters",p.getCounters().toString());
                                Map<String,Object> zones=new LinkedHashMap<>();
                                for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Command}) {
                                    List<Object> cards=new ArrayList<>();
                                    for(var c:p.getCardsIn(z))cards.add(Map.of("id",c.getId(),"name",c.getName(),"tapped",c.isTapped(),"power",c.getNetPower(),"toughness",c.getNetToughness()));
                                    zones.put(z.name(),cards);
                                }
                                pv.put("zones",zones);players.add(pv);
                            }
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString());
                            Path tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"players",players)));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"four-seat-ring-read-only-observer");observer.setDaemon(true);observer.start();
        },registered->{
            for(int seat=3;seat<=4;seat++) {
                Deck deck=new Deck("Ring native seat "+seat);
                ForgeBridge.loadSection(deck.getMain(),ForgeBridge.config.getAsJsonArray("decks").get(0).getAsJsonObject().getAsJsonArray("cards"));
                var player=new RegisteredPlayer(deck).setPlayer(new LobbyPlayerAi("Player "+seat,Set.of()));
                player.setStartingLife(30);registered.add(player);
            }
        });
    }
}
