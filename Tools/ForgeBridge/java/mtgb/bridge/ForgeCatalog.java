// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.gson.Gson;
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
