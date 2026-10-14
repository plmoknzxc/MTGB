package mtgb.bridge;
import java.util.*;
import java.util.concurrent.*;

/** Direct helper contract adapter, not a native card outcome. */
public final class ModalWaitContract {
    public static void main(String[] args)throws Exception {
        int checks=0;
        for(Object expected:new Object[]{null,3,true,"top:17",List.of("a","b"),Map.of("amount",4)}) {
            Object actual=ForgeBridge.awaitAnswer(CompletableFuture.completedFuture(expected));
            if(!Objects.equals(actual,expected)||!ForgeBridge.modalAnswers.isEmpty())throw new AssertionError("Normal typed answer");
            checks++;
        }
        var bad=new CompletableFuture<Object>();bad.completeExceptionally(new IllegalArgumentException("contract-error"));
        try {ForgeBridge.awaitAnswer(bad);throw new AssertionError("Expected error");}
        catch(ExecutionException e){if(!(e.getCause() instanceof IllegalArgumentException)||ForgeBridge.terminalCancellation(e))throw new AssertionError("Nonterminal errors must remain errors");checks++;}
        if(!ForgeBridge.modalAnswers.isEmpty())throw new AssertionError("Error wait leaked");checks++;
        ForgeBridge.finished=true;
        try {ForgeBridge.awaitAnswer(CompletableFuture.completedFuture("old"));throw new AssertionError("Terminal old answer accepted");}
        catch(RuntimeException e){if(!ForgeBridge.terminalCancellation(new IllegalStateException("wrapper",e)))throw new AssertionError("Terminal cause not recognized");checks++;}
        if(!ForgeBridge.modalAnswers.isEmpty())throw new AssertionError("Terminal wait registered");checks++;
        System.out.println("PASS_ADAPTER "+checks+" typed helper/error/terminal-registration assertions; no native rule or GUI result claim.");
    }
}
