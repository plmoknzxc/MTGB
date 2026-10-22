// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.gson.*;
import forge.game.Game;
import forge.game.card.*;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.security.MessageDigest;
import java.time.Instant;
import java.util.*;
import java.util.concurrent.*;
import java.util.zip.GZIPOutputStream;

/** Host-private, bounded passive observer. No filesystem calls on the game thread. */
final class BridgeShuffleAudit implements Player.LibraryAudit {
    static final long MAX_BYTES=32L*1024*1024, QUEUE_CHARS=2L*1024*1024;
    static final int MAX_RECORDS=100000, MAX_CARDS=8192;
    private final Path target,partial,status;
    private final BlockingQueue<String> queue=new ArrayBlockingQueue<>(256);
    private final Map<Game,Integer> games=new IdentityHashMap<>();
    private final Map<Game,Set<Integer>> identities=new IdentityHashMap<>();
    private final Map<Player,Deque<Long>> shuffles=new IdentityHashMap<>();
    private final Set<Long> draws=new HashSet<>();
    private final Thread writer;
    private volatile boolean closing,failed;
    private volatile String error="",reason="shutdown";
    private long sequence,queuedChars,totalChars,accepted,written,dropped,maxCaptureNanos;
    private final long started=System.nanoTime();
    private record Draw(Player player,long id) {}

    BridgeShuffleAudit(JsonObject config) {
        target=Path.of(config.get("shuffleAuditFile").getAsString()).toAbsolutePath();
        partial=target.resolveSibling(target.getFileName()+".partial");
        status=target.resolveSibling(target.getFileName()+".status.json");
        writer=new Thread(this::write,"mtgb-shuffle-audit");writer.setDaemon(true);writer.start();
        var header=new LinkedHashMap<String,Object>();header.put("type","start");header.put("formatVersion",1);header.put("createdUtc",Instant.now().toString());
        for(String key:List.of("session","roomId","gameNumber","seed","runtimeHash","forgeRevision","firstPlayerSeat","startingLife","noShuffle","useAnte","shuffleAlgorithm","shufflePasses","shufflePiles","shuffleMaxPacket","shuffleCuts","decks","authorizedAuxiliarySeriesPools"))
            if(config.has(key))header.put(key,config.get(key));
        header.put("limits",Map.of("maxUncompressedBytes",MAX_BYTES,"maxRecords",MAX_RECORDS,"queueMemoryChars",QUEUE_CHARS,"retentionDays",30,"archiveBudgetBytes",512L*1024*1024));
        publish(header);
    }

    private synchronized long publish(Map<String,Object> record) {
        if(closing||failed){dropped++;return -1;}
        record.put("seq",++sequence);record.put("elapsedMicros",(System.nanoTime()-started)/1000);
        String line=ForgeBridge.JSON.toJson(record);
        // JSON has mostly ASCII keys and IDs. UTF-8 bytes never exceed 3 per UTF-16 char.
        if(accepted>=MAX_RECORDS||totalChars+line.length()+1>MAX_BYTES/3||queuedChars+line.length()>QUEUE_CHARS||!queue.offer(line)){
            fail("capacity_limit");dropped++;return -1;
        }
        queuedChars+=line.length();totalChars+=line.length()+1;accepted++;return sequence;
    }
    private synchronized void fail(String message) {
        if(!failed)System.err.println("[shuffle-audit] incomplete: "+message);
        failed=true;error=message;closing=true;
    }
    private Map<String,Object> snapshot(Player player,SpellAbility cause) {
        Game game=player.getGame();int instance=games.computeIfAbsent(game,k->games.size()+1);
        Set<Integer> known=identities.computeIfAbsent(game,k->new HashSet<>());
        var cards=player.getCardsIn(ZoneType.Library);
        if(cards.size()>MAX_CARDS)throw new IllegalStateException("library_snapshot_limit");
        List<Integer> order=new ArrayList<>(cards.size());List<Object> added=new ArrayList<>();
        for(Card card:cards){
            order.add(card.getId());
            if(known.add(card.getId())){
                var paper=card.getPaperCard();
                added.add(Map.of("id",card.getId(),"printing",paper==null?"":paper.getEdition()+"/"+paper.getCollectorNumber(),"name",paper==null?card.getName():paper.getName(),"land",card.isLand()));
            }
        }
        var record=new LinkedHashMap<String,Object>();record.put("gameInstance",instance);record.put("seat",game.getPlayers().indexOf(player)+1);
        record.put("turn",game.getPhaseHandler().getTurn());record.put("phase",String.valueOf(game.getPhaseHandler().getPhase()));record.put("stage",String.valueOf(game.getAge()));
        record.put("library",order);if(!added.isEmpty())record.put("identities",added);
        if(cause!=null){record.put("sourceId",cause.getHostCard().getId());record.put("sourceName",cause.getHostCard().getName());record.put("api",String.valueOf(cause.getApi()));}
        return record;
    }
    private synchronized void captureTime(long begin){maxCaptureNanos=Math.max(maxCaptureNanos,System.nanoTime()-begin);}
    @Override public synchronized void shuffle(Player player,SpellAbility cause,boolean after) {
        if(closing||failed)return;long begin=System.nanoTime();
        try{
            var record=snapshot(player,cause);record.put("type",after?"shuffle_after":"shuffle_before");
            record.put("noShuffle",ForgeBridge.libraryOrder!=null&&ForgeBridge.libraryOrder.enabled());
            var pending=shuffles.computeIfAbsent(player,k->new ArrayDeque<>());
            if(after){record.put("beforeSeq",pending.isEmpty()?-1:pending.pop());publish(record);}
            else {long id=publish(record);if(id>0)pending.push(id);}
        }catch(RuntimeException e){fail(e.toString());}finally{captureTime(begin);}
    }
    @Override public synchronized Object beginDraw(Player player,SpellAbility cause) {
        if(closing||failed)return null;long begin=System.nanoTime();
        try{
            var record=snapshot(player,cause);record.put("type","draw_before");
            boolean bottom=player.hasKeyword("You draw cards from the bottom of your library instead of the top of your library.");
            record.put("fromBottom",bottom);var library=player.getCardsIn(ZoneType.Library);
            record.put("expectedId",library.isEmpty()?-1:library.get(bottom?library.size()-1:0).getId());
            long id=publish(record);if(id>0)draws.add(id);return id>0?new Draw(player,id):null;
        }catch(RuntimeException e){fail(e.toString());return null;}finally{captureTime(begin);}
    }
    @Override public synchronized void endDraw(Object context,CardCollectionView drawn) {
        if(!(context instanceof Draw draw)||closing||failed)return;long begin=System.nanoTime();
        try{
            var record=snapshot(draw.player,null);record.put("type","draw_after");record.put("beforeSeq",draw.id);
            List<Object> actual=new ArrayList<>();if(drawn!=null)for(Card card:drawn)if(card!=null)
                actual.add(Map.of("id",card.getId(),"zone",String.valueOf(card.getZone()==null?null:card.getZone().getZoneType())));
            record.put("drawn",actual);record.put("emptyLibrary",drawn==null);publish(record);draws.remove(draw.id);
        }catch(RuntimeException e){fail(e.toString());}finally{captureTime(begin);}
    }
    synchronized void finish(String why){if(!closing){reason=why;closing=true;}}
    void close(String why){finish(why);try{writer.join(1500);}catch(InterruptedException e){Thread.currentThread().interrupt();}}

    private void saveStatus(Map<String,Object> value)throws IOException {
        Path temporary=status.resolveSibling(status.getFileName()+".tmp");
        Files.writeString(temporary,ForgeBridge.JSON.toJson(value));
        Files.move(temporary,status,StandardCopyOption.REPLACE_EXISTING);
    }
    // Only files owned by this audit directory are rotated. Active partial files
    // are retained; a 30-day-old partial exceeds the supported game lifetime.
    static void prune(Path directory)throws IOException {
        long cutoff=System.currentTimeMillis()-TimeUnit.DAYS.toMillis(30),total=0;
        List<Path> files=new ArrayList<>();
        try(var stream=Files.list(directory)){
            for(Path path:stream.toList())if(Files.isRegularFile(path,LinkOption.NOFOLLOW_LINKS)&&path.getFileName().toString().matches("[0-9a-f]{32}\\.jsonl\\.gz(?:\\.partial)?")){
                if(path.toString().endsWith(".partial")){
                    if(Files.getLastModifiedTime(path).toMillis()<cutoff){Files.delete(path);Files.deleteIfExists(path.resolveSibling(path.getFileName().toString().replace(".partial","")+".status.json"));}
                }else{files.add(path);total+=Files.size(path);}
            }
        }
        files.sort(Comparator.comparingLong(p->{try{return Files.getLastModifiedTime(p).toMillis();}catch(IOException e){return Long.MAX_VALUE;}}));
        for(Path path:files)if(Files.getLastModifiedTime(path).toMillis()<cutoff||total>512L*1024*1024){long size=Files.size(path);Files.delete(path);Files.deleteIfExists(path.resolveSibling(path.getFileName()+".status.json"));total-=size;}
    }
    private void write() {
        try{
            Files.createDirectories(target.getParent());prune(target.getParent());
            saveStatus(Map.of("state","recording","complete",false,"file",target.getFileName().toString()));
            var digest=MessageDigest.getInstance("SHA-256");String hash;
            try(var zip=new GZIPOutputStream(Files.newOutputStream(partial,StandardOpenOption.CREATE_NEW),32768,true)){
                long flushed=0;
                while(true){
                    String line=queue.poll(100,TimeUnit.MILLISECONDS);
                    if(line==null){if(written!=flushed){zip.flush();flushed=written;}if(closing&&queue.isEmpty())break;continue;}
                    synchronized(this){queuedChars-=line.length();}
                    byte[] bytes=(line+"\n").getBytes(StandardCharsets.UTF_8);zip.write(bytes);digest.update(bytes);written++;
                    // Persist a readable prefix periodically without flushing on every draw.
                    if(written%64==0){zip.flush();flushed=written;}
                }
                hash=HexFormat.of().formatHex(digest.digest());
                synchronized(this){if(!failed&&(!draws.isEmpty()||shuffles.values().stream().anyMatch(q->!q.isEmpty()))){failed=true;error="interrupted_operations";}}
                var footer=new LinkedHashMap<String,Object>();footer.put("type","end");footer.put("complete",!failed);footer.put("gameFinished",reason.equals("game_ended"));footer.put("reason",reason);footer.put("error",error);footer.put("records",written);footer.put("dropped",dropped);footer.put("recordsSHA256",hash);footer.put("maxCaptureMicros",maxCaptureNanos/1000);
                zip.write((ForgeBridge.JSON.toJson(footer)+"\n").getBytes(StandardCharsets.UTF_8));
            }
            Files.move(partial,target);
            saveStatus(Map.of("state",failed?"incomplete":"closed","complete",!failed,"reason",reason,"records",written,"error",error,"recordsSHA256",hash));
            prune(target.getParent());
        }catch(Exception e){
            fail("writer: "+e);try{saveStatus(Map.of("state","incomplete","complete",false,"error",error,"records",written));}catch(IOException ignored){}
        }
    }
}
