package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Initial positions plus read-only observation; all SVar writes come from native abilities. */
public final class VariableBranchFixture {
    static Map<String,Object> variables(Card card) {
        List<String> keys=new ArrayList<>(List.of("XDelirium","Limit","ExiledCMC","JoinForcesAmount","YouChose","Doodles","NumPlayerGiveup"));
        for(Player player:ForgeBridge.game.getPlayers())keys.add(player.toString());
        Map<String,Object> vars=new LinkedHashMap<>();for(String key:keys)if(card.hasSVar(key))vars.put(key,card.getSVar(key));
        return vars;
    }
    public static final class DamageAudit {
        final List<Object> events=Collections.synchronizedList(new ArrayList<>());
        void record(forge.game.card.CardView source,int target,String kind,int amount) {
            Map<String,Object> vars=new LinkedHashMap<>();
            for(Card card:ForgeBridge.game.getCardsInGame())if(card.getId()==source.getId()){vars=variables(card);break;}
            events.add(Map.of("source",source.getId(),"target",target,"kind",kind,"amount",amount,"vars",vars));
        }
        @com.google.common.eventbus.Subscribe public void card(forge.game.event.GameEventCardDamaged event) {record(event.source(),event.card().getId(),"card",event.amount());}
        @com.google.common.eventbus.Subscribe public void player(forge.game.event.GameEventPlayerDamaged event) {record(event.source(),event.target().getId(),"player",event.amount());}
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int seat=1;seat<=2;seat++) {
                Player player=ForgeBridge.game.getPlayers().get(seat-1);
                for(String land:new String[]{"Plains","Island","Swamp","Mountain","Forest"})
                    for(int i=0;i<6;i++)StateBranchFixture.place(land,player,ZoneType.Battlefield);
                StateBranchFixture.setup(player,ForgeBridge.config.getAsJsonObject(seat==1?"own":"other"));
                // Initial battlefield placement bypasses ETB loyalty setup.
                for(Card card:player.getCardsIn(ZoneType.Battlefield))if(card.getName().equals("Jace Beleren"))
                    card.setCounters(forge.game.card.CounterType.getType("LOYALTY"),3);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            DamageAudit damage=new DamageAudit();ForgeBridge.game.subscribeToEvents(damage);
            Thread observer=new Thread(()->{
                Set<Long> saved=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var request=ForgeBridge.pending;
                        if(request!=null&&!request.consumed&&saved.add(request.requestId)) {
                            List<Object> cards=new ArrayList<>();
                            for(int seat=1;seat<=2;seat++)for(ZoneType zone:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack})
                                for(Card card:ForgeBridge.game.getPlayers().get(seat-1).getCardsIn(zone)) {
                                    Map<String,Object> vars=variables(card);
                                    if(vars.isEmpty())continue;
                                    cards.add(Map.of("id",card.getId(),"name",card.getName(),"seat",seat,"zone",zone.name(),"vars",vars));
                                }
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(request.requestId+".tmp"),dst=dir.resolve(request.requestId+".json");
                            List<Object> stack=new ArrayList<>();for(var item:ForgeBridge.game.getStack()) {
                                var sa=item.getSpellAbility();stack.add(Map.of("id",sa.getHostCard().getId(),"x",sa.getHostCard().getXManaCostPaid(),"mana",sa.getPayingMana().stream().map(Object::toString).toList()));
                            }
                            List<Object> events;synchronized(damage.events){events=new ArrayList<>(damage.events);}
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",request.requestId,"cards",cards,"stack",stack,"damageEvents",events)));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception error){throw new RuntimeException(error);}
            },"variable-read-only-audit");observer.setDaemon(true);observer.start();
        });
    }
}
