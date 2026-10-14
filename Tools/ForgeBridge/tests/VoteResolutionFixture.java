package mtgb.bridge;

import com.google.common.collect.ListMultimap;
import com.google.common.eventbus.Subscribe;
import forge.ai.LobbyPlayerAi;
import forge.ai.PlayerControllerAi;
import forge.deck.Deck;
import forge.game.Game;
import forge.game.card.Card;
import forge.game.event.GameEventRandomLog;
import forge.game.player.Player;
import forge.game.player.RegisteredPlayer;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Opening setup and read-only observation. Extra native test seats answer legal
 * vote inputs from the fixture script; no effect outcome is assigned here.
 * These are not production human clients or proof of four-player transport. */
public final class VoteResolutionFixture {
    static final List<String> logs=new ArrayList<>();
    static final List<Object> extraVotes=new ArrayList<>();
    public static final class Listener {
        @Subscribe public void log(GameEventRandomLog e){logs.add(e.message());}
    }
    static String label(Object option) {
        return option instanceof Card?((Card)option).getName():option instanceof Player?((Player)option).getName():option.toString().trim();
    }
    static Map<String,Object> card(Card c) {
        var m=new LinkedHashMap<String,Object>();m.put("id",c.getId());m.put("name",c.getName());m.put("zone",c.getZone().getZoneType().name());
        m.put("controller",ForgeBridge.game.getPlayers().indexOf(c.getController())+1);m.put("owner",ForgeBridge.game.getPlayers().indexOf(c.getOwner())+1);m.put("timestamp",c.getGameTimestamp());m.put("tapped",c.isTapped());
        var remembered=new ArrayList<String>();c.getRemembered().forEach(o->remembered.add(o.toString()));m.put("remembered",remembered);m.put("counters",c.getCounters().toString());m.put("damage",c.getDamage());m.put("stun",c.getCounters(forge.game.card.CounterEnumType.STUN));m.put("p1p1",c.getCounters(forge.game.card.CounterEnumType.P1P1));m.put("lore",c.getCounters(forge.game.card.CounterEnumType.LORE));m.put("power",c.getNetPower());m.put("toughness",c.getNetToughness());m.put("imprinted",c.getImprintedCards().stream().map(Card::getId).toList());m.put("aid",c.getSVar("Aid"));if(c.isCreature()){var attacks=new LinkedHashMap<String,Boolean>();for(Player target:ForgeBridge.game.getPlayers())if(target!=c.getController())attacks.put(String.valueOf(ForgeBridge.game.getPlayers().indexOf(target)+1),forge.game.combat.CombatUtil.canAttack(c,target));m.put("canAttack",attacks);}return m;
    }
    static final class VoteSeat extends LobbyPlayerAi {
        final int seat;
        VoteSeat(int s){super("Player "+s,Set.of());seat=s;}
        @Override public Player createIngamePlayer(Game game,int id) {
            Player p=new Player(getName(),game,id);
            p.setFirstController(new PlayerControllerAi(game,p,this) {
                int at;
                @Override public Object vote(SpellAbility sa,String prompt,List<Object> options,ListMultimap<Object,Player> votes,Player forPlayer,boolean optional) {
                    var script=ForgeBridge.config.getAsJsonObject("extraVoteInputs").getAsJsonArray(String.valueOf(seat));
                    if(at>=script.size())throw new IllegalStateException("Unexpected extra native vote input");
                    String wanted=script.get(at++).getAsString();Object chosen=null;
                    if(!wanted.equals("ABSTAIN"))chosen=options.stream().filter(o->label(o).equals(wanted)).findFirst().orElseThrow();
                    else if(!optional)throw new IllegalStateException("Abstaining from mandatory vote");
                    extraVotes.add(Map.of("actor",seat,"credited",game.getPlayers().indexOf(forPlayer)+1,"options",options.stream().map(VoteResolutionFixture::label).toList(),"chosen",chosen==null?"ABSTAIN":label(chosen),"optional",optional));
                    return chosen;
                }
                @Override public int chooseNumber(SpellAbility sa,String title,int min,int max,Map<String,Object> params) {
                    int value=0;
                    if(ForgeBridge.config.has("extraOptionalVotes")&&ForgeBridge.config.getAsJsonObject("extraOptionalVotes").has(String.valueOf(seat)))value=ForgeBridge.config.getAsJsonObject("extraOptionalVotes").get(String.valueOf(seat)).getAsInt();
                    if(value<min||value>max)throw new IllegalStateException("Illegal scripted optional votes");return value;
                }
            });return p;
        }
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            for(int i=0;i<ForgeBridge.game.getPlayers().size();i++) {
                Player p=ForgeBridge.game.getPlayers().get(i);RoyalStateFixture.setup(p,ForgeBridge.config.getAsJsonArray("positions").get(i).getAsJsonObject());
                if(i==0)for(String n:new String[]{"Plains","Island","Swamp","Mountain","Forest"})for(int j=0;j<4;j++)RoyalStateFixture.place(n,p,ZoneType.Battlefield);
            }
            if(ForgeBridge.config.has("borrowed"))for(var entry:ForgeBridge.config.getAsJsonArray("borrowed")){var b=entry.getAsJsonObject();Player owner=ForgeBridge.game.getPlayers().get(b.get("owner").getAsInt()-1),controller=ForgeBridge.game.getPlayers().get(b.get("controller").getAsInt()-1);Card c=RoyalStateFixture.place(b.get("name").getAsString(),owner,ZoneType.Battlefield);c.addTempController(controller,ForgeBridge.game.getNextTimestamp());}
            ForgeBridge.game.getAction().checkStaticAbilities();ForgeBridge.game.subscribeToEvents(new Listener());
            Thread observer=new Thread(()->{
                Set<Long> seen=new HashSet<>();
                while(!ForgeBridge.finished)try {
                    synchronized(ForgeBridge.class) {
                        var r=ForgeBridge.pending;
                        if(r!=null&&!r.consumed&&seen.add(r.requestId)) {
                            var players=new ArrayList<Object>();
                            for(Player p:ForgeBridge.game.getPlayers()) {
                                var zones=new LinkedHashMap<String,Object>();
                                for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Command})zones.put(z.name(),p.getCardsIn(z).stream().map(VoteResolutionFixture::card).toList());
                                players.add(Map.of("seat",ForgeBridge.game.getPlayers().indexOf(p)+1,"id",p.getId(),"life",p.getLife(),"zones",zones));
                            }
                            var phase=ForgeBridge.game.getPhaseHandler();var data=Map.of("requestId",r.requestId,"players",players,"voteLogs",new ArrayList<>(logs),"extraVotes",new ArrayList<>(extraVotes),"turnSeat",ForgeBridge.game.getPlayers().indexOf(phase.getPlayerTurn())+1,"nextTurnSeat",ForgeBridge.game.getPlayers().indexOf(phase.getNextTurn())+1,"turn",phase.getTurn(),"phase",phase.getPhase().name());
                            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString());Path tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");Files.writeString(tmp,ForgeBridge.JSON.toJson(data));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);
                        }
                    }
                    Thread.sleep(5);
                }catch(Exception e){throw new RuntimeException(e);}
            },"vote-read-only-native-audit");observer.setDaemon(true);observer.start();
        },registered->{
            int count=ForgeBridge.config.get("nativeSeats").getAsInt();
            for(int seat=3;seat<=count;seat++) {
                Deck deck=new Deck("Native vote seat "+seat);ForgeBridge.loadSection(deck.getMain(),ForgeBridge.config.getAsJsonArray("decks").get(0).getAsJsonObject().getAsJsonArray("cards"));
                var p=new RegisteredPlayer(deck).setPlayer(new VoteSeat(seat));p.setStartingLife(30);registered.add(p);
            }
        });
    }
}
