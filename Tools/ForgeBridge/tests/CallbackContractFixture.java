package mtgb.bridge;

import java.util.List;
import java.util.Map;

/** Adapter contract assertions only; not card effect or game-rule evidence. */
public final class CallbackContractFixture {
    static void check(boolean condition,String message){if(!condition)throw new AssertionError(message);}
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            var gui=ForgeBridge.guis.get(0).controller.getGui();
            String test=ForgeBridge.config.get("fixture").getAsString();
            switch(test) {
                case "empty":
                    check(gui.one("empty",List.of())==null,"Empty mandatory one must return null");
                    check(gui.oneOrNone("empty optional",List.of())==null,"Empty optional one must return null");
                    check(gui.getChoices("empty many",0,1,List.of()).isEmpty(),"Empty choices must return an empty list");
                    check(gui.insertInList("empty insertion","added",List.of()).equals(List.of("added")),"Empty insertion loses object");
                    break;
                case "display":
                    String value=gui.one("display single",List.of("a","b"),v->"display "+v);
                    check(value.equals("b"),"One returned a display label instead of original object");
                    var selected=gui.getChoices("display multiple",1,1,List.of("a","b"),List.of("a"),v->"display "+v);
                    check(selected.equals(List.of("b")),"Initial/display selection did not return the original object");
                    break;
                case "zero":
                    check(gui.getChoices("zero allowed",0,0,List.of("a","b")).isEmpty(),"Zero selection changed count");
                    break;
                case "insert-first":case "insert-middle":case "insert-last":
                    var result=gui.insertInList(test,"added",List.of("first","last"));
                    int at=test.equals("insert-first")?0:test.equals("insert-middle")?1:2;
                    check(result.get(at).equals("added"),"Wrong insertion position");
                    check(result.stream().filter(v->!v.equals("added")).toList().equals(List.of("first","last")),"Existing relative order changed");
                    break;
                default:throw new IllegalArgumentException(test);
            }
            ForgeBridge.send(Map.of("type","callback-contract-result","case",test,"status","PASS"));
        });
    }
}
