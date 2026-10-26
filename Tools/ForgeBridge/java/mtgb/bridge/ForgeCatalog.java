// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.gson.Gson;
import com.google.gson.*;
import forge.card.*;
import forge.item.PaperCard;
import forge.model.FModel;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.io.*;
import java.util.*;

/** Exports display data and exact printing identities; effects stay in Forge scripts. */
public final class ForgeCatalog {
    public static String key(PaperCard card) {
        CardEdition edition = FModel.getMagicDb().getEditions().get(card.getEdition());
        String set = edition == null ? card.getEdition() : edition.getScryfallCode();
        if (set == null || set.isBlank()) set = card.getEdition();
        return set.toUpperCase(Locale.ROOT) + "/" + card.getCollectorNumber();
    }

    public static Map<String, List<PaperCard>> printings() {
        Map<String, List<PaperCard>> result = new TreeMap<>();
        List<PaperCard> all = new ArrayList<>(FModel.getMagicDb().getCommonCards().getAllCards());
        all.addAll(FModel.getMagicDb().getVariantCards().getAllCards());
        for (PaperCard card : all) {
            if (card.getCollectorNumber() == null || card.getCollectorNumber().isBlank()) continue;
            List<PaperCard> candidates = result.computeIfAbsent(key(card), k -> new ArrayList<>());
            if (!candidates.contains(card)) candidates.add(card);
        }
        return result;
    }

    /** Resolve registered printings through Forge's lazy database, without building a full catalog. */
    static Map<String, List<PaperCard>> registeredPrintings(Path path, JsonObject config) throws IOException {
        Set<String> required = new HashSet<>();
        for (String field : List.of("decks", "authorizedAuxiliarySeriesPools")) {
            if (!config.has(field) || config.get(field).isJsonNull()) continue;
            for (JsonElement deck : config.getAsJsonArray(field)) {
                if (deck.isJsonNull()) continue;
                for (String section : List.of("cards", "sideboard", "commanders", "attractions", "contraptions")) {
                    JsonObject source = deck.getAsJsonObject();
                    if (!source.has(section) || source.get(section).isJsonNull()) continue;
                    for (JsonElement entry : source.getAsJsonArray(section)) {
                        JsonObject row = entry.getAsJsonObject();
                        required.add(row.get("setCode").getAsString().toUpperCase(Locale.ROOT) + "/" + row.get("collectorNumber").getAsString());
                    }
                }
            }
        }
        Map<String, List<PaperCard>> result = new HashMap<>();
        Set<String> seen = new HashSet<>();
        // The packaged, hash-verified exporter writes key first. Parse only required rows.
        java.util.regex.Pattern prefix = java.util.regex.Pattern.compile("^\\{\\\"key\\\":\\s*\\\"([^\\\"\\\\]*)\\\"\\s*,");
        try (BufferedReader reader = Files.newBufferedReader(path, StandardCharsets.UTF_8)) {
            String line;
            while ((line = reader.readLine()) != null) {
                java.util.regex.Matcher match = prefix.matcher(line);
                if (!match.find()) throw new IOException("Invalid exported catalog row");
                String key = match.group(1);
                if (!required.contains(key)) continue;
                JsonObject row = JsonParser.parseString(line).getAsJsonObject();
                if (!seen.add(key) || row.get("ambiguous").getAsBoolean())
                    throw new IllegalArgumentException("Ambiguous printing: " + key);
                String name = row.get("name").getAsString(), set = row.get("forgeSet").getAsString();
                String number = key.substring(key.indexOf('/') + 1);
                PaperCard card = FModel.getMagicDb().getCommonCards().getCard(name, set, number);
                if (card == null || !key(card).equals(key)) card = FModel.getMagicDb().getVariantCards().getCard(name, set, number);
                if (card == null || !key(card).equals(key) || card.getArtIndex() != row.get("artIndex").getAsInt())
                    throw new IllegalArgumentException("Missing exact printing: " + key);
                result.put(key, List.of(card));
            }
        }
        if (!result.keySet().containsAll(required)) throw new IllegalArgumentException("Missing registered printing");
        return result;
    }

    static Map<String, Object> face(ICardFace face) {
        Map<String, Object> item = new LinkedHashMap<>();
        item.put("name", face.getName());
        item.put("types", face.getType().getCoreTypes());
        item.put("supertypes", face.getType().getSupertypes());
        item.put("subtypes", face.getType().getSubtypes());
        item.put("manaCost", face.getManaCost().toString());
        item.put("text", face.getOracleText());
        item.put("power", face.getPower());
        item.put("toughness", face.getToughness());
        item.put("loyalty", face.getInitialLoyalty());
        item.put("defense", face.getDefense());
        return item;
    }

    public static void main(String[] args) throws Exception {
        System.setErr(new PrintStream(new FileOutputStream(FileDescriptor.err),true,StandardCharsets.UTF_8));
        System.setOut(new PrintStream(System.err, true, StandardCharsets.UTF_8));
        ForgeEnvironment.initialize(args[0]);
        Gson gson = new Gson();
        int count = 0, ambiguous = 0;
        Path output = Path.of(args[1]);
        Files.createDirectories(output.toAbsolutePath().getParent());
        Path temporary = output.resolveSibling(output.getFileName() + ".tmp");
        try (BufferedWriter writer = Files.newBufferedWriter(temporary, StandardCharsets.UTF_8)) {
            for (Map.Entry<String, List<PaperCard>> entry : printings().entrySet()) {
                for (PaperCard card : entry.getValue()) {
                    Map<String, Object> item = new LinkedHashMap<>();
                    item.put("key", entry.getKey());
                    item.put("forgeSet", card.getEdition());
                    item.put("name", card.getName());
                    item.put("artIndex", card.getArtIndex());
                    Integer specificLimit = forge.deck.DeckFormat.canHaveSpecificNumberInDeck(card);
                    item.put("deckLimit", forge.deck.DeckFormat.canHaveAnyNumberOf(card) ? -1 : (specificLimit == null ? 4 : specificLimit));
                    item.put("splitType", card.getRules().getSplitType().toString());
                    item.put("ambiguous", entry.getValue().size() != 1);
                    List<Object> faces = new ArrayList<>();
                    faces.add(face(card.getRules().getMainPart()));
                    if (card.getRules().getOtherPart() != null) faces.add(face(card.getRules().getOtherPart()));
                    item.put("faces", faces);
                    writer.write(gson.toJson(item)); writer.newLine(); count++;
                }
                if (entry.getValue().size() != 1) ambiguous++;
            }
        }
        if (count == 0) throw new IllegalStateException("Forge returned an empty official catalog.");
        Files.move(temporary, output, StandardCopyOption.REPLACE_EXISTING);
        System.err.println("MTGB Forge catalog: " + count + " printings; " + ambiguous + " ambiguous keys.");
        ForgeEnvironment.UI.shutdownNow();
        System.exit(0);
    }
}
