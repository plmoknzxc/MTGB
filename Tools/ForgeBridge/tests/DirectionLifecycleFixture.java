package mtgb.bridge;

import forge.deck.Deck;
import forge.game.GameEntity;
import forge.game.card.Card;
import forge.game.card.CounterType;
import forge.game.combat.CombatUtil;
import forge.game.player.Player;
import forge.game.player.RegisteredPlayer;
import forge.game.staticability.StaticAbility;
import forge.game.staticability.StaticAbilityMode;
import forge.game.staticability.StaticAbilityCantAttackBlock;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Opening state only; read-only native defender legality and actual effect lifetime. */
public final class DirectionLifecycleFixture {
    static Map<String,Object> card(Card c) {
        Map<String,Object> result=DirectionChoiceFixture.card(c);
        result.put("loyalty",c.getCounters(CounterType.getType("LOYALTY")));
        return result;
    }
    static List<Object> defenders() {
        List<StaticAbility> rules=new ArrayList<>();
        for(Card c:ForgeBridge.game.getCardsIn(ZoneType.STATIC_ABILITIES_SOURCE_ZONES))for(var ability:c.getStaticAbilities())
            if(ability.hasParam("DefenderNotNearestToYouInChosenDirection")&&ability.checkConditions(StaticAbilityMode.CantAttack))rules.add(ability);
        List<Object> rows=new ArrayList<>();
        for(Player p:ForgeBridge.game.getPlayers()) {
            Card attacker=p.getCardsIn(ZoneType.Battlefield).stream().filter(c->c.getName().equals("Grizzly Bears")||c.getName().equals("Wall of Roots")).findFirst().orElseThrow();
            for(GameEntity target:CombatUtil.getAllPossibleDefenders(p)) {
                boolean blocked=false;
                for(var ability:rules)if(StaticAbilityCantAttackBlock.applyCantAttackAbility(ability,attacker,target))blocked=true;
                var row=new LinkedHashMap<String,Object>();row.put("attacker",attacker.getId());row.put("seat",ForgeBridge.game.getPlayers().indexOf(p)+1);row.put("target",target.getId());
                row.put("kind",target instanceof Player?"player":((Card)target).isBattle()?"battle":"planeswalker");
                Player defender=target instanceof Player?(Player)target:((Card)target).isBattle()?((Card)target).getProtectingPlayer():((Card)target).getController();
                row.put("defenderSeat",ForgeBridge.game.getPlayers().indexOf(defender)+1);
                row.put("directionAllowed",!blocked);row.put("actualLegal",CombatUtil.canAttack(attacker,target));
                row.put("ruleSources",rules.stream().map(a->Map.of("id",a.getHostCard().getId(),"direction",String.valueOf(a.getHostCard().getChosenDirection()))).toList());rows.add(row);
            }
        }return rows;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int i=0;i<4;i++) {
                var p=ForgeBridge.game.getPlayers().get(i);RoyalStateFixture.setup(p,ForgeBridge.config.getAsJsonArray("positions").get(i).getAsJsonObject());
                if(i==0)for(String n:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int j=0;j<4;j++)RoyalStateFixture.place(n,p,ZoneType.Battlefield);
                for(Card c:p.getCardsIn(ZoneType.Battlefield)) {
                    if(c.isPlaneswalker())c.setCounters(CounterType.getType("LOYALTY"),c.getName().equals("Teyo, Geometric Tactician")?3:5);
                    if(c.isBattle()){c.setCounters(CounterType.getType("DEFENSE"),5);c.setProtectingPlayer(ForgeBridge.game.getPlayers().get((i+1)%4));}
                }
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&seen.add(r.requestId)) {
                            var players=new ArrayList<Object>();
                            for(Player p:ForgeBridge.game.getPlayers()) {
                                var zones=new LinkedHashMap<String,Object>();
                                for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(DirectionLifecycleFixture::card).toList());
                                players.add(Map.of("seat",ForgeBridge.game.getPlayers().indexOf(p)+1,"id",p.getId(),"life",p.getLife(),"zones",zones));
                            }
                            var phase=ForgeBridge.game.getPhaseHandler();
                            var data=Map.of("requestId",r.requestId,"players",players,"defenders",defenders(),"turnSeat",ForgeBridge.game.getPlayers().indexOf(phase.getPlayerTurn())+1,"turn",phase.getTurn(),"phase",phase.getPhase().name());
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString());Path tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(data));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"direction-lifecycle-read-only");observer.setDaemon(true);observer.start();
        },registered->{
            for(int seat=3;seat<=4;seat++) {
                Deck deck=new Deck("Lifecycle native seat "+seat);ForgeBridge.loadSection(deck.getMain(),ForgeBridge.config.getAsJsonArray("decks").get(0).getAsJsonObject().getAsJsonArray("cards"));
                var p=new RegisteredPlayer(deck).setPlayer(new DirectionChoiceFixture.ObservedAi("Player "+seat));p.setStartingLife(30);registered.add(p);
            }
        });
    }
}
