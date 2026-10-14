// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;
import forge.CardStorageReader;
import java.io.*;
import java.nio.file.*;
import java.util.*;

/** Build-time cache of Forge's own name/face/placeholder lookup; never guesses names from text. */
public final class ForgeCardNameIndex {
    public static void main(String[] args) throws Exception {
        System.clearProperty("mtgb.bridge.cardNameIndex");
        Path root=Path.of(args[0]).toAbsolutePath().normalize();
        ForgeEnvironment.initialize(root.resolve("../..").normalize().toString(),true);
        var readerField=forge.StaticData.class.getDeclaredField("cardReader");readerField.setAccessible(true);
        CardStorageReader reader=(CardStorageReader)readerField.get(forge.StaticData.instance());
        var field=CardStorageReader.class.getDeclaredField("cardFilesByCardName");field.setAccessible(true);
        @SuppressWarnings("unchecked") NavigableMap<String,File> names=(NavigableMap<String,File>)field.get(reader);
        int sources=CardStorageReader.collectCardFiles(new ArrayList<>(),root.toFile()).size();
        Path output=root.resolve("mtgb-name-index.bin"),temporary=root.resolve("mtgb-name-index.tmp");
        try(DataOutputStream data=new DataOutputStream(new BufferedOutputStream(Files.newOutputStream(temporary)))) {
            data.writeInt(0x4d494458);data.writeInt(1);data.writeInt(sources);data.writeInt(names.size());
            for(var row:names.entrySet()) {
                Path source=row.getValue().toPath().toAbsolutePath().normalize();
                if(!source.startsWith(root))throw new IOException("Index source outside card directory");
                data.writeUTF(row.getKey());data.writeUTF(root.relativize(source).toString().replace('\\','/'));
            }
        }
        Files.move(temporary,output,StandardCopyOption.REPLACE_EXISTING);
        System.setProperty("mtgb.bridge.cardNameIndex",output.toString());
        @SuppressWarnings("unchecked") NavigableMap<String,File> restored=(NavigableMap<String,File>)field.get(new CardStorageReader(root.toString(),null,true));
        if(!names.keySet().equals(restored.keySet()))throw new IOException("Prepared name set differs from native index");
        for(String name:names.keySet())
            if(!names.get(name).toPath().toAbsolutePath().normalize().equals(restored.get(name).toPath().toAbsolutePath().normalize()))
                throw new IOException("Prepared name maps to a different native script");
        System.out.println("Prepared native card-name index: "+names.size()+" names, "+sources+" scripts");
        ForgeEnvironment.UI.shutdownNow();System.exit(0);
    }
}
