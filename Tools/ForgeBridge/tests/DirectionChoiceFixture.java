package mtgb.bridge;

import forge.ai.LobbyPlayerAi;
import forge.ai.PlayerControllerAi;
import forge.deck.Deck;
import forge.game.Game;
import forge.game.GameEntity;
import forge.game.card.Card;
import forge.game.card.CounterType;
import forge.game.player.Player;
import forge.game.player.RegisteredPlayer;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;
import forge.game.player.DelayedReveal;
import forge.util.collect.FCollectionView;
import java.nio.file.*;
import java.util.*;

/** Only opening state is arranged. Seats 3/4 retain native AI decisions;
 * read-only interception records their choices, not predetermined outcomes. */
public final class DirectionChoiceFixture {
    static final List<Object> aiChoices = new ArrayList<>();
    static Map<String,Object> card(Card c) {
        var m=new LinkedHashMap<String,Object>();
        m.put("id",c.getId());m.put("name",c.getName());m.put("zone",c.getZone().getZoneType().name());
        m.put("controller",ForgeBridge.game.getPlayers().indexOf(c.getController())+1);
        m.put("owner",ForgeBridge.game.getPlayers().indexOf(c.getOwner())+1);
        m.put("timestamp",c.getGameTimestamp());m.put("tapped",c.isTapped());
        m.put("counters",c.getCounters().toString());m.put("direction",String.valueOf(c.getChosenDirection()));
        return m;
    }
    static List<Object> field() {
        List<Object> out=new ArrayList<>();
        for(Card c:ForgeBridge.game.getCardsIn(ZoneType.Battlefield))out.add(card(c));return out;
    }
    static final class ObservedAi extends LobbyPlayerAi {
        ObservedAi(String name){super(name,Set.of());}
        @Override public Player createIngamePlayer(Game game,int id) {
            Player p=new Player(getName(),game,id);
            p.setFirstController(new PlayerControllerAi(game,p,this) {
                @Override public <T extends GameEntity> T chooseSingleEntityForEffect(FCollectionView<T> options,DelayedReveal reveal,SpellAbility sa,String title,boolean optional,Player target,Map<String,Object> params) {
                    T chosen=super.chooseSingleEntityForEffect(options,reveal,sa,title,optional,target,params);
                    if(sa.getHostCard().getName().equals("Order of Succession")) {
                        var event=new LinkedHashMap<String,Object>();
                        event.put("time",System.nanoTime());event.put("seat",game.getPlayers().indexOf(p)+1);
                        event.put("options",options.stream().map(GameEntity::getId).toList());
                        event.put("chosen",chosen==null?0:chosen.getId());event.put("fieldBeforeTransfer",field());
                        aiChoices.add(event);
                    }
                    return chosen;
                }
            });return p;
        }
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int i=0;i<4;i++) {
                Player p=ForgeBridge.game.getPlayers().get(i);
                RoyalStateFixture.setup(p,ForgeBridge.config.getAsJsonArray("positions").get(i).getAsJsonObject());
                if(i==0)for(String n:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int j=0;j<4;j++)RoyalStateFixture.place(n,p,ZoneType.Battlefield);
                for(Card c:p.getCardsIn(ZoneType.Battlefield))if(c.getName().equals("Aminatou, the Fateshifter"))c.setCounters(CounterType.getType("LOYALTY"),8);
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
                                for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(DirectionChoiceFixture::card).toList());
                                players.add(Map.of("seat",ForgeBridge.game.getPlayers().indexOf(p)+1,"life",p.getLife(),"zones",zones));
                            }
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString());
                            Path tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");
                            Files.writeString(tmp,ForgeBridge.JSON.toJson(Map.of("requestId",r.requestId,"time",System.nanoTime(),"players",players,"aiChoices",new ArrayList<>(aiChoices))));
                            Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"direction-read-only-audit");observer.setDaemon(true);observer.start();
        },registered->{
            for(int s=3;s<=4;s++) {
                Deck deck=new Deck("Direction native seat "+s);
                ForgeBridge.loadSection(deck.getMain(),ForgeBridge.config.getAsJsonArray("decks").get(0).getAsJsonObject().getAsJsonArray("cards"));
                var p=new RegisteredPlayer(deck).setPlayer(new ObservedAi("Player "+s));p.setStartingLife(30);registered.add(p);
            }
        });
    }
}
