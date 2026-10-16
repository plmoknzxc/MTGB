package mtgb.bridge;

import forge.StaticData;
import forge.card.*;
import forge.util.storage.StorageReaderBase;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.lang.management.ManagementFactory;
import java.util.*;

/** Read-only catalog/API checks, not card-effect coverage. */
public final class CatalogLoadingFixture {
    static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
    static void verifyIndex(CardEdition.Collection editions, Map<String,Object> result) {
        Set<String> names = new TreeSet<>(String.CASE_INSENSITIVE_ORDER);
        for (CardEdition edition : editions) for (var entry : edition.getAllCardsInSet()) names.add(entry.name());
        int count = 0;
        for (String name : names) {
            List<CardEdition> expected = new ArrayList<>();
            for (CardEdition edition : editions) if (!edition.getCardInSet(name).isEmpty()) expected.add(edition);
            require(expected.equals(editions.getEditionsContaining(List.of(name))), "Edition sequence: " + name);
            if (count++ < 100) require(expected.equals(editions.getEditionsContaining(List.of(name.toUpperCase(Locale.ROOT), name.toLowerCase(Locale.ROOT)))), "Case/duplicate names: " + name);
        }
        require(editions.getEditionsContaining(List.of("MTGB absent catalog fixture name")).isEmpty(), "Absent name");
        var mutable = new CardEdition.Collection(new StorageReaderBase<CardEdition>(null) {
            public Map<String,CardEdition> readAll() { return new LinkedHashMap<>(); }
            public String getFullPath() { return "catalog fixture"; }
            public String getItemKey(CardEdition edition) { return edition.getCode(); }
        });
        require(mutable.getEditionsContaining(List.of("Opt")).isEmpty(), "Initially empty index");
        CardEdition opt = editions.getEditionsContaining(List.of("Opt")).get(0);
        mutable.add(opt);
        require(mutable.getEditionsContaining(List.of("opt")).equals(List.of(opt)), "Added edition invalidates index");
        mutable.append(new CardEdition.Collection(new StorageReaderBase<CardEdition>(null) {
            public Map<String,CardEdition> readAll() { return new LinkedHashMap<>(); }
            public String getFullPath() { return "empty custom editions"; }
            public String getItemKey(CardEdition edition) { return edition.getCode(); }
        }));
        require(mutable.getEditionsContaining(List.of("Opt")).equals(List.of(opt)), "Custom append keeps candidates");
        try { mutable.add(opt); throw new AssertionError("Locked editions accepted mutation"); }
        catch (UnsupportedOperationException expected) { }
        result.put("verifiedEditionNames", count);
    }
    public static void main(String[] args) throws Exception {
        long start = System.nanoTime();
        ForgeBridge.run(args, null, players -> {
            try {
                Map<String,Object> result = new LinkedHashMap<>();
                result.put("startupMs", (System.nanoTime() - start) / 1e6);
                var data = StaticData.instance(); var db = data.getCommonCards();
                require(db.getAllCards().stream().noneMatch(c -> c.getName().equals("Opt")), "Unexpected eager catalog");
                require(data.fetchCard("Opt") != null, "Off-deck lazy lookup");
                long load = System.nanoTime(); data.ensureAllCardsLoaded();
                result.put("loadMs", (System.nanoTime() - load) / 1e6);
                var names = db.streamAllFaces().map(ICardFace::getName).distinct().sorted().toList();
                require(names.contains("Lightning Bolt") && names.size() > 30000, "Incomplete full name enumeration");
                result.put("faceNames", names.size()); result.put("printings", db.getAllCards().size());
                List<String> rows = new ArrayList<>();
                for (var database : List.of(db, data.getVariantCards())) {
                    for (var p : database.getAllCards()) rows.add(ForgeBridge.JSON.toJson(Arrays.asList("print", p.getName(), p.getEdition(), p.getCollectorNumber(), p.getArtIndex(), p.getFunctionalVariant(), p.getRarity().toString(), p.getRules().getSplitType().toString())));
                    database.streamAllFaces().forEach(f -> rows.add(ForgeBridge.JSON.toJson(Arrays.asList("face", f.getName(), f.getType().toString(), f.getManaCost().toString(), f.getOracleText(), f.getPower(), f.getToughness(), f.getInitialLoyalty(), f.getDefense()))));
                }
                Collections.sort(rows);
                var digest = java.security.MessageDigest.getInstance("SHA-256");
                for (String row : rows) { digest.update(row.getBytes(StandardCharsets.UTF_8)); digest.update((byte)'\n'); }
                result.put("catalogSha256", HexFormat.of().formatHex(digest.digest())); result.put("catalogRows", rows.size()); rows.clear();
                int prints = db.getAllCards().size(); long repeat = System.nanoTime(); data.ensureAllCardsLoaded();
                result.put("repeatLoadMs", (System.nanoTime() - repeat) / 1e6);
                require(prints == db.getAllCards().size(), "Repeated full load duplicates printings");
                if (Boolean.getBoolean("catalog.verifyIndex")) verifyIndex(data.getEditions(), result);
                System.gc(); result.put("retainedHeapMiB", ManagementFactory.getMemoryMXBean().getHeapMemoryUsage().getUsed() / 1048576.0);
                result.put("success", true);
                Files.writeString(Path.of(System.getProperty("catalog.output")), ForgeBridge.JSON.toJson(result));
                System.err.println("PASS: native full catalog, lazy lookup, exact printing/face digest, repeat load and requested edition-index boundaries");
                System.exit(0);
            } catch (Exception e) { throw new RuntimeException(e); }
        });
    }
}
