import com.google.gson.*;
import forge.game.keyword.Keyword;
import org.apache.commons.lang3.tuple.Pair;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.lang.reflect.Method;

/** Parser parity check only. Does not instantiate cards or validate effects. */
public final class DeclarationKeywordClassifier {
    public static void main(String[] args) throws Exception {
        Method method = Keyword.class.getDeclaredMethod("getKeywordDetails", String.class);
        method.setAccessible(true);
        JsonArray input = JsonParser.parseString(Files.readString(Path.of(args[0]), StandardCharsets.UTF_8)).getAsJsonArray();
        JsonObject output = new JsonObject();
        for (JsonElement element : input) {
            String value = element.getAsString();
            Pair<?, ?> result = (Pair<?, ?>) method.invoke(null, value);
            Keyword keyword = (Keyword) result.getLeft();
            if (keyword == Keyword.UNDEFINED) output.add(value, JsonNull.INSTANCE);
            else output.addProperty(value, keyword.toString());
        }
        Files.writeString(Path.of(args[1]), new GsonBuilder().serializeNulls().setPrettyPrinting().create().toJson(output), StandardCharsets.UTF_8);
        System.out.println("SOURCE_PARSER_PARITY " + output.size());
    }
}
