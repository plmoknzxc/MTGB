// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.gson.*;
import forge.deck.*;
import forge.item.PaperCard;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;

/** Editor-only construction inspection. Never starts a game or changes multiplayer rules. */
public final class CommanderDeckInspector {
    public static void main(String[] args) throws Exception {
        PrintStream output=System.out;
        System.setOut(new PrintStream(System.err,true,StandardCharsets.UTF_8));
        JsonObject source=JsonParser.parseString(Files.readString(Path.of(args[1]))).getAsJsonObject();
        System.setProperty("mtgb.bridge.cardNameIndex",Path.of(args[0],"res/cardsfolder/mtgb-name-index.bin").toString());
        ForgeEnvironment.initialize(args[0],true);
        JsonArray pool=new JsonArray();
        for(String section:List.of("cards","sideboard","commanders","candidates"))
            if(source.has(section))for(JsonElement e:source.getAsJsonArray(section))pool.add(e);
        JsonObject projection=new JsonObject();projection.add("cards",pool);
        JsonArray decks=new JsonArray();decks.add(projection);
        JsonObject config=new JsonObject();config.add("decks",decks);
        var catalog=ForgeCatalog.registeredPrintings(Path.of(args[0],"../catalog.jsonl").normalize(),config);
        Deck deck=new Deck("Commander editor");
        for(var section:Map.of("cards",DeckSection.Main,"sideboard",DeckSection.Sideboard,"commanders",DeckSection.Commander).entrySet())
            if(source.has(section.getKey()))for(JsonElement value:source.getAsJsonArray(section.getKey())){
                JsonObject e=value.getAsJsonObject();int count=e.get("count").getAsInt();
                if(count>0)deck.getOrCreate(section.getValue()).add(resolve(catalog,e),count);
            }
        DeckFormat format=source.get("format").getAsString().equals("brawl")?DeckFormat.Brawl:DeckFormat.Commander;
        JsonObject result=new JsonObject();JsonArray legal=new JsonArray(),partners=new JsonArray();
        var selected=deck.getCommanders();
        for(var entry:catalog.entrySet()){
            PaperCard card=entry.getValue().get(0);
            if(format.isLegalCommander(card.getRules()))legal.add(entry.getKey());
            if(selected.size()==1 && !selected.get(0).getName().equals(card.getName())
                && format.isLegalCommander(card.getRules()) && selected.get(0).getRules().canBePartnerCommanders(card.getRules()))partners.add(entry.getKey());
        }
        result.add("legal",legal);result.add("partners",partners);
        String problem=format.getDeckConformanceProblem(deck);
        // Keep engine text in diagnostics; UI uses localized issue categories.
        if(problem!=null)System.err.println(problem);
        result.addProperty("problem",problem==null?"":problem.contains("color identity")?"identity":problem.contains("partnership")?"partners":problem.contains("illegal commander")?"commander":selected.isEmpty()?"missing":problem.contains("copies")?"copies":problem.contains("sideboard")?"sideboard":problem.contains("cards")?"size":"invalid");
        output.println(new Gson().toJson(result));
        ForgeEnvironment.UI.shutdownNow();System.exit(0);
    }
    private static PaperCard resolve(Map<String,List<PaperCard>> catalog,JsonObject e){
        String key=e.get("setCode").getAsString().toUpperCase(Locale.ROOT)+"/"+e.get("collectorNumber").getAsString();
        return catalog.get(key).get(0);
    }
}
