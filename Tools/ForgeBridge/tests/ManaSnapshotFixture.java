package mtgb.bridge;

import forge.card.MagicColor;
import forge.game.card.Card;
import forge.game.mana.Mana;
import forge.game.mana.ManaPool;
import forge.model.FModel;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.atomic.AtomicReference;

/** Storage concurrency adapter, separate from actual payment/undo flow tests. */
public final class ManaSnapshotFixture {
    public static void main(String[] args) throws Exception {
        ForgeBridge.run(args, () -> {
            try {
                var player=ForgeBridge.game.getPlayers().get(0);
                var source=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard("Island"),player);
                var mana=new Mana(MagicColor.BLUE,source,null,player);
                // Isolated pool: never concurrently mutate a real game's resources.
                var pool=new ManaPool(player);
                pool.addManaNoEvent(mana);
                var frozen=pool.snapshot();
                pool.resetPool();
                if(frozen.size()!=1 || frozen.get(0)!=mana || !pool.snapshot().isEmpty())
                    throw new AssertionError("Snapshot membership and native identity");
                var start=new CountDownLatch(1);
                var failure=new AtomicReference<Throwable>();
                Thread writer=new Thread(() -> {
                    try {
                        start.await();
                        for(int i=0;i<100000;i++) {
                            pool.addManaNoEvent(mana);
                            if(!pool.removeManaNoEvent(mana))throw new AssertionError("Removal lost native mana");
                        }
                    } catch(Throwable e) { failure.set(e); }
                },"mana-snapshot-writer");
                writer.start();start.countDown();
                for(int i=0;i<100000;i++) {
                    var snapshot=pool.snapshot();
                    if(snapshot.size()>1 || snapshot.stream().anyMatch(m->m!=mana))
                        throw new AssertionError("Snapshot identity or duplication");
                    int count=ForgePoolMana.count(snapshot,MagicColor.BLUE,false);
                    int buckets=ForgePoolMana.entries(snapshot,player).stream()
                        .mapToInt(e->(int)e.get("count")).sum();
                    if(count!=snapshot.size() || buckets!=count)
                        throw new AssertionError("Display fields disagree on same snapshot");
                }
                writer.join(10000);
                if(writer.isAlive())throw new AssertionError("Writer deadlocked");
                if(failure.get()!=null)throw new AssertionError(failure.get());
                if(!pool.snapshot().isEmpty())throw new AssertionError("Resource leak");
                Files.writeString(Path.of(ForgeBridge.config.get("snapshotResult").getAsString()),
                    "PASS: frozen membership, native identity, 100000 concurrent reads/writes, matching display fields, empty final pool\n");
            } catch(Exception e) { throw new RuntimeException(e); }
        });
    }
}
