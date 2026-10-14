package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import java.nio.file.*;
import java.util.*;

/** Initial cards plus read-only request/thread observations; no outcome setters. */
public final class ControlledConcessionFixture {
    public static void main(String[] args) throws Exception {
        ForgeBridge.run(args, () -> {
            Player own=ForgeBridge.game.getPlayers().get(0), other=ForgeBridge.game.getPlayers().get(1);
            for(String land:new String[]{"Plains","Island","Swamp","Mountain","Forest"})
                for(int i=0;i<6;i++)RoyalStateFixture.place(land,own,ZoneType.Battlefield);
            RoyalStateFixture.setup(own,ForgeBridge.config.getAsJsonObject("own"));
            RoyalStateFixture.setup(other,ForgeBridge.config.getAsJsonObject("other"));
            ForgeBridge.game.getAction().checkStaticAbilities();
            Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString());
            Thread probe=new Thread(() -> {
                Set<Long> saved=new HashSet<>();long terminalStart=0;
                try {
                    while(true) {
                        synchronized(ForgeBridge.class) {
                            var pending=ForgeBridge.pending;
                            if(pending!=null&&!pending.consumed&&saved.add(pending.requestId)) {
                                List<Object> players=new ArrayList<>();
                                for(int i=0;i<ForgeBridge.game.getRegisteredPlayers().size();i++) {
                                    Player p=ForgeBridge.game.getRegisteredPlayers().get(i);
                                    List<Object> cards=new ArrayList<>();
                                    for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile})
                                        for(Card c:p.getCardsIn(z))cards.add(Map.of("id",c.getId(),"name",c.getName(),"zone",z.name(),"tapped",c.isTapped()));
                                    players.add(Map.of("seat",i+1,"life",p.getLife(),"cards",cards));
                                }
                                write(dir.resolve(pending.requestId+".json"),Map.of("requestId",pending.requestId,"modal",pending.modal,"kind",pending.kind,"seat",pending.seat,"playerSeat",pending.playerSeat,"players",players));
                            }
                            if(ForgeBridge.finished) {
                                if(terminalStart==0)terminalStart=System.nanoTime();
                                List<Object> threads=new ArrayList<>();int blockedChoices=0,activeGameWorkers=0;
                                for(var entry:Thread.getAllStackTraces().entrySet()) {
                                    List<String> stack=new ArrayList<>();boolean choice=false, gameWorker=false;
                                    for(var frame:entry.getValue()) {
                                        stack.add(frame.toString());
                                        if(frame.getClassName().startsWith("mtgb.bridge.")&&(frame.getMethodName().equals("modal")||frame.getMethodName().equals("awaitAnswer")))choice=true;
                                        if(frame.getClassName().equals("forge.game.Match")&&frame.getMethodName().equals("startGame"))gameWorker=true;
                                    }
                                    if(choice&&gameWorker)blockedChoices++;
                                    if(gameWorker)activeGameWorkers++;
                                    if(gameWorker||choice)threads.add(Map.of("name",entry.getKey().getName(),"state",entry.getKey().getState().name(),"stack",stack));
                                }
                                write(dir.resolve("terminal.json"),Map.of("finished",true,"gameOver",ForgeBridge.game.isGameOver(),"pendingAbsent",ForgeBridge.pending==null,"blockedNativeChoices",blockedChoices,"activeGameWorkers",activeGameWorkers,"threads",threads,"elapsedMillis",(System.nanoTime()-terminalStart)/1000000));
                                if(System.nanoTime()-terminalStart>2500000000L)return;
                            }
                        }
                        Thread.sleep(20);
                    }
                } catch(Exception e) {throw new RuntimeException(e);}
            },"controlled-concession-read-only-probe");
            probe.setDaemon(true);probe.start();
        });
    }
    static void write(Path path,Object value)throws Exception {
        Path temp=path.resolveSibling(path.getFileName()+".tmp");
        Files.writeString(temp,ForgeBridge.JSON.toJson(value));
        Files.move(temp,path,StandardCopyOption.REPLACE_EXISTING);
    }
}
