// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import forge.CardStorageReader;
import java.io.*;
import java.nio.file.*;

/** Prepared-index validation and deferred I/O checks; no card-effect coverage. */
public final class PreparedCardNameIndexFixture {
    static int checks;
    static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
        checks++;
    }
    static void write(Path index, int magic, int version, int sources, String... pairs) throws IOException {
        try (var data = new DataOutputStream(Files.newOutputStream(index))) {
            data.writeInt(magic); data.writeInt(version); data.writeInt(sources); data.writeInt(pairs.length / 2);
            for (String value : pairs) data.writeUTF(value);
        }
    }
    static CardStorageReader read(Path root) {
        return new CardStorageReader(root.toString(), null, true);
    }
    static void invalid(Path root, String label) {
        try { read(root); throw new AssertionError("Accepted " + label); }
        catch (IllegalStateException expected) { checks++; }
    }
    public static void main(String[] args) throws Exception {
        Path root = Path.of(args[0]).toAbsolutePath(); Files.createDirectories(root);
        Path index = root.resolve("prepared.bin");
        System.setProperty("mtgb.bridge.cardNameIndex", index.toString());
        int magic = 0x4d494458;
        // An unrelated missing script must not require scanning the whole installation.
        write(index, magic, 1, 1, "missing", "missing.txt");
        CardStorageReader reader = read(root);
        require(reader.attemptToLoadCard("not indexed") == null, "Unknown name remains absent");
        try { reader.attemptToLoadCard("Missing"); throw new AssertionError("Missing script silently accepted"); }
        catch (RuntimeException expected) { checks++; }
        Path card = root.resolve("opt.txt");
        Files.writeString(card, "Name:Opt\nManaCost:U\nTypes:Instant\nOracle:Draw a card.\n");
        write(index, magic, 1, 1, "opt", "opt.txt");
        require(read(root).attemptToLoadCard("Opt").getName().equals("Opt"), "Actual script loads");
        write(index, 0, 1, 1, "opt", "opt.txt"); invalid(root, "magic");
        write(index, magic, 2, 1, "opt", "opt.txt"); invalid(root, "version");
        write(index, magic, 1, 0, "opt", "opt.txt"); invalid(root, "source count");
        write(index, magic, 1, 2, "opt", "opt.txt"); invalid(root, "name count");
        write(index, magic, 1, 1, "", "opt.txt"); invalid(root, "empty name");
        write(index, magic, 1, 1, "opt", "../escape.txt"); invalid(root, "path escape");
        write(index, magic, 1, 1, "opt", "folder\\opt.txt"); invalid(root, "backslash");
        write(index, magic, 1, 1, "opt", "opt.bin"); invalid(root, "non-script");
        write(index, magic, 1, 1, "opt", "opt.txt", "opt", "opt.txt"); invalid(root, "duplicate");
        write(index, magic, 1, 1, "opt", "opt.txt");
        Files.write(index, new byte[]{1}, StandardOpenOption.APPEND); invalid(root, "trailing bytes");
        Files.write(index, new byte[]{0}); invalid(root, "truncated header");
        // Ordinary Forge and build-time index generation retain their original reader.
        System.clearProperty("mtgb.bridge.cardNameIndex");
        require(read(root).attemptToLoadCard("Opt").getName().equals("Opt"), "Unprepared lazy reader");
        System.out.println("PASS: " + checks + " prepared-index/API checks");
        System.exit(0);
    }
}
