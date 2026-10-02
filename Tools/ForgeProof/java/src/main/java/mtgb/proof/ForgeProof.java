// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.proof;

import com.google.gson.*;
import forge.deck.Deck;
import forge.game.*;
import forge.game.card.Card;
import forge.game.card.CardView;
import forge.game.player.*;
import forge.game.zone.ZoneType;
import forge.gamemodes.match.input.*;
import forge.gui.GuiBase;
import forge.gui.interfaces.*;
import forge.localinstance.properties.ForgePreferences.FPref;
import forge.model.FModel;
import forge.player.LobbyPlayerHuman;
import forge.player.PlayerControllerHuman;
import forge.util.MyRandom;
import java.io.*;
import java.lang.reflect.*;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.*;

/** Bounded integration probe. Only the published options are supported; no rule emulation. */
public final class ForgeProof {
    static final Gson JSON = new Gson();
    static final PrintStream WIRE = new PrintStream(new FileOutputStream(FileDescriptor.out),true,StandardCharsets.UTF_8);
    static final String SESSION = UUID.randomUUID().toString();
    static final ExecutorService EDT = Executors.newSingleThreadExecutor(r -> new Thread(r, "proof-ui"));
    static Game game;
    static final List<Gui> guis = new ArrayList<>();
    static volatile Pending pending;
    static long revision;
    static final long started = System.nanoTime();
    static boolean fixtureReady;
    static final class Option {
        String id, label;
        transient Runnable action;
        Option(String id, String label, Runnable action) { this.id=id; this.label=label; this.action=action; }
    }
    static final class Pending {
        long requestId;
        int seat;
        String kind, message;
        List<Option> options;
        transient boolean consumed;
        Pending(int seat, String kind, String message, List<Option> options) {
            this.requestId=++revision; this.seat=seat; this.kind=kind; this.message=message; this.options=options;
        }
    }
    static synchronized void send(Object o) { WIRE.println(JSON.toJson(o)); WIRE.flush(); }
    static Map<String,Object> envelope(String type) {
        Map<String,Object> m=new LinkedHashMap<>(); m.put("type",type); m.put("session",SESSION); return m;
    }
    static void fatal(Throwable e) {
        e.printStackTrace(System.err);
        Map<String,Object> m=envelope("fatal"); m.put("error", e.toString()); send(m); System.exit(2);
    }
    static void guiTask(Runnable r) {
        EDT.execute(() -> { try { r.run(); publishInput(); } catch(Throwable e) { fatal(e); } });
    }
    static Object base(Object proxy, Method method, Object[] args, String assets) throws Throwable {
        String n=method.getName();
        if(n.equals("getAssetsDir")) return assets;
        if(n.equals("getCurrentVersion")) return "MTGB-Proof";
        if(n.equals("isRunningOnDesktop")) return true;
        if(n.equals("isGuiThread")) return Thread.currentThread().getName().equals("proof-ui");
        if(n.equals("invokeInEdtLater")) { guiTask((Runnable)args[0]); return null; }
        if(n.equals("invokeInEdtNow") || n.equals("invokeInEdtAndWait")) {
            if(Thread.currentThread().getName().equals("proof-ui")) ((Runnable)args[0]).run();
            else EDT.submit((Runnable)args[0]).get();
            return null;
        }
        if(n.equals("encodeSymbols")) return args[0];
        if(n.equals("getAvatarCount") || n.equals("getSleevesCount")) return 1;
        if(n.equals("getScreenScale")) return 1f;
        if(n.equals("showBugReportDialog")) throw new IllegalStateException(Arrays.toString(args));
        if(method.isDefault()) return InvocationHandler.invokeDefault(proxy,method,args);
        if(method.getReturnType()==boolean.class) return false;
        if(method.getReturnType()==void.class) return null;
        throw new UnsupportedOperationException("IGuiBase."+n);
    }
    static final class Gui implements InvocationHandler {
        final int seat;
        PlayerControllerHuman controller;
        Input shownInput;
        String message="";
        boolean ok, cancel;
        String okLabel="OK", cancelLabel="Cancel";
        Gui(int seat) { this.seat=seat; }
        @Override public Object invoke(Object proxy, Method method, Object[] a) throws Throwable {
            String n=method.getName();
            if(n.equals("getGameView")) return game.getView();
            if(n.equals("getGameSpeed")) return forge.gui.control.PlaybackSpeed.NORMAL;
            if(n.equals("isNetGame")) return true; // disables local-only yield suggestions
            if(n.equals("showPromptMessage")) { message=(String)a[1]; return null; }
            if(n.equals("updateButtons") && a.length==6) {
                shownInput=controller.getInputQueue().getInput();
                okLabel=(String)a[1]; cancelLabel=(String)a[2]; ok=(boolean)a[3]; cancel=(boolean)a[4]; return null;
            }
            if(n.equals("getAbilityToPlay")) return modal(n, (List<?>)a[1], false);
            if(n.equals("one")) return modal(n+": "+a[0], (List<?>)a[1], false);
            if(n.equals("showOptionDialog")) return modalIndex(n+": "+a[0], (List<?>)a[3]);
            if(n.equals("confirm")) return modalIndex(n+": "+a[1], (List<?>)a[3])==0;
            if(n.equals("showConfirmDialog") && a.length==5) return modalIndex(n+": "+a[0], List.of(a[2],a[3]))==0;
            if(n.equals("tempShowZones")) return a[1];
            if(n.equals("openZones")) return new forge.player.PlayerZoneUpdates();
            if(n.equals("getDayTime")) return "";
            if(n.equals("showErrorDialog")) throw new IllegalStateException(Arrays.toString(a));
            if(method.isDefault()) return InvocationHandler.invokeDefault(proxy,method,a);
            if(method.getReturnType()==void.class) return null; // presentation-only callbacks
            if(method.getReturnType()==boolean.class) return false;
            throw new UnsupportedOperationException("IGuiGame."+n+" "+Arrays.toString(a));
        }
        int modalIndex(String title,List<?> values) { return (Integer)modal(title,values,true); }
        Object modal(String title,List<?> values,boolean index) {
            CompletableFuture<Object> answer=new CompletableFuture<>();
            List<Option> options=new ArrayList<>();
            for(int i=0;i<values.size();i++) {
                final int j=i; Object value=values.get(i);
                options.add(new Option("choice:"+i,String.valueOf(value),() -> answer.complete(index ? j : value)));
            }
            publish(new Pending(seat,"dialog",title,options));
            try { return answer.get(120,TimeUnit.SECONDS); }
            catch(Exception e) { throw new IllegalStateException("Unanswered dialog: "+title,e); }
        }
    }
    static synchronized void publish(Pending p) {
        pending=p;
        Map<String,Object> m=envelope("request"); m.put("request",p); m.put("views",views());
        m.put("elapsedMs",(System.nanoTime()-started)/1_000_000); send(m);
    }
    static synchronized void publishInput() {
        if(game==null || (pending!=null && !pending.consumed && pending.kind.equals("dialog"))) return;
        for(Gui g:guis) {
            Input inp=g.controller.getInputQueue().getInput();
            if(inp==null || inp!=g.shownInput) continue;
            String kind=inp.getClass().getSimpleName();
            List<Option> opts=new ArrayList<>();
            if(g.ok) opts.add(new Option("ok",g.okLabel,() -> g.controller.getInputProxy().selectButtonOK()));
            // Cancel/undo deliberately not exposed: it is outside this probe.
            if(inp instanceof InputPassPriority || inp instanceof InputPayMana) {
                for(ZoneType z:List.of(ZoneType.Hand,ZoneType.Battlefield)) {
                    for(Card c:g.controller.getPlayer().getCardsIn(z)) {
                        String activate=inp.getActivateAction(c);
                        if(activate!=null) opts.add(new Option("card:"+c.getId(),c.getName()+" ["+z+"]",() -> g.controller.getInputProxy().selectCard(c.getView(),null,null)));
                    }
                }
            }
            if(inp instanceof InputSelectTargets) {
                // This proof's only targeted spell is Lightning Bolt: both players are legal.
                for(Player p:game.getPlayers()) opts.add(new Option("player:"+p.getId(),p.getName(),() -> g.controller.getInputProxy().selectPlayer(p.getView(),null)));
            }
            if(inp instanceof InputPayMana && g.controller.getPlayer().getManaPool().getAmountOfColor(forge.card.MagicColor.RED)>0) {
                opts.add(new Option("mana:R","Pay {R} from mana pool",() -> g.controller.useMana(forge.card.MagicColor.RED)));
            }
            if(opts.isEmpty()) throw new UnsupportedOperationException("No supported options for "+kind+": "+g.message);
            publish(new Pending(g.seat,kind,g.message,opts)); return;
        }
    }
    static List<Object> views() {
        List<Object> views=new ArrayList<>();
        if(game==null) return views;
        for(int s=0;s<guis.size();s++) {
            Map<String,Object> view=new LinkedHashMap<>(); view.put("seat",s+1);
            view.put("phase",String.valueOf(game.getPhaseHandler().getPhase()));
            view.put("fixtureReady",fixtureReady); view.put("stackCount",game.getStack().size());
            List<Object> players=new ArrayList<>();
            for(int i=0;i<game.getPlayers().size();i++) {
                Player p=game.getPlayers().get(i);
                Map<String,Object> pv=new LinkedHashMap<>(); pv.put("seat",i+1); pv.put("life",p.getLife());
                pv.put("handCount",p.getCardsIn(ZoneType.Hand).size()); pv.put("libraryCount",p.getCardsIn(ZoneType.Library).size());
                pv.put("redMana",p.getManaPool().getAmountOfColor(forge.card.MagicColor.RED));
                List<Object> hand=new ArrayList<>();
                if(s==i) for(Card c:p.getCardsIn(ZoneType.Hand)) hand.add(card(c));
                pv.put("hand",hand);
                List<Object> field=new ArrayList<>(); for(Card c:p.getCardsIn(ZoneType.Battlefield)) field.add(card(c));
                pv.put("battlefield",field);
                List<String> grave=new ArrayList<>(); for(Card c:p.getCardsIn(ZoneType.Graveyard)) grave.add(c.getName());
                pv.put("graveyard",grave); players.add(pv);
            }
            view.put("players",players); views.add(view);
        }
        return views;
    }
    static Object card(Card c) { return Map.of("id",c.getId(),"name",c.getName(),"tapped",c.isTapped()); }
    static void receive(JsonObject o) {
        String type=o.get("type").getAsString();
        if(type.equals("shutdown")) { send(envelope("bye")); System.exit(0); }
        Pending p; Option option=null; String error=null;
        synchronized(ForgeProof.class) {
            p=pending;
            if(!SESSION.equals(o.get("session").getAsString())) error="wrong-session";
            else if(p==null || p.consumed || o.get("requestId").getAsLong()!=p.requestId) error="stale-request";
            else if(o.get("seat").getAsInt()!=p.seat) error="wrong-seat";
            else {
                String id=o.get("option").getAsString();
                for(Option x:p.options) if(x.id.equals(id)) option=x;
                if(option==null) error="invalid-option";
            }
            if(error!=null) {
                Map<String,Object> m=envelope("rejected"); m.put("error",error); m.put("requestId",o.get("requestId")); m.put("views",views()); send(m); return;
            }
            p.consumed=true;
        }
        Map<String,Object> ack=envelope("accepted"); ack.put("requestId",p.requestId); send(ack);
        if(p.kind.equals("dialog")) option.action.run(); else guiTask(option.action);
    }
    public static void main(String[] args) throws Exception {
        System.setOut(new PrintStream(System.err,true,StandardCharsets.UTF_8));
        Thread.setDefaultUncaughtExceptionHandler((t,e) -> fatal(e));
        String assets=new File(args[0]).getCanonicalPath().replace('\\','/')+"/";
        GuiBase.setInterface((IGuiBase)Proxy.newProxyInstance(IGuiBase.class.getClassLoader(),new Class<?>[]{IGuiBase.class},(p,m,a)->base(p,m,a,assets)));
        Thread input=new Thread(() -> {
            try(BufferedReader reader=new BufferedReader(new InputStreamReader(System.in,StandardCharsets.UTF_8))) {
                String line; while((line=reader.readLine())!=null) receive(JsonParser.parseString(line).getAsJsonObject());
                System.exit(0);
            } catch(Throwable e) { fatal(e); }
        },"proof-wire"); input.setDaemon(true); input.start();
        FModel.initialize(null,prefs -> {
            prefs.setPref(FPref.UI_LANGUAGE,"en-US"); prefs.setPref(FPref.PLAYER_NAME,"Proof");
            prefs.setPref(FPref.DECKGEN_CARDBASED,false); prefs.setPref(FPref.LOAD_CARD_SCRIPTS_LAZILY,true);
            prefs.setPref(FPref.YIELD_AUTO_PASS_NO_ACTIONS,false); return null;
        });
        MyRandom.setRandom(new Random(42));
        List<RegisteredPlayer> players=new ArrayList<>();
        for(int seat=1;seat<=2;seat++) {
            Deck deck=new Deck("Proof "+seat);
            deck.getMain().add(FModel.getMagicDb().getCommonCards().getCard("Mountain"),56);
            deck.getMain().add(FModel.getMagicDb().getCommonCards().getCard("Lightning Bolt"),4);
            players.add(new RegisteredPlayer(deck).setPlayer(new LobbyPlayerHuman("Player "+seat)));
        }
        GameRules rules=new GameRules(GameType.Constructed); rules.setGamesPerMatch(1);
        Match match=new Match(rules,players,"Unity bridge proof"); game=match.createGame();
        for(int i=0;i<2;i++) {
            Gui handler=new Gui(i+1); handler.controller=(PlayerControllerHuman)game.getPlayers().get(i).getController();
            handler.controller.setGui((IGuiGame)Proxy.newProxyInstance(IGuiGame.class.getClassLoader(),new Class<?>[]{IGuiGame.class},handler));
            guis.add(handler);
        }
        game.getAction().invoke(() -> match.startGame(game, () -> {
            // Test fixture only: arrange hand before normal turn actions. Never modify life, mana, or stack.
            Player p=game.getPlayers().get(0); game.getPhaseHandler().setPlayerTurn(p);
            boolean hasBolt=false; for(Card c:p.getCardsIn(ZoneType.Hand)) if(c.getName().equals("Lightning Bolt")) hasBolt=true;
            if(!hasBolt) for(Card c:new ArrayList<>(p.getCardsIn(ZoneType.Library))) if(c.getName().equals("Lightning Bolt")) {
                game.getAction().moveToLibrary(p.getCardsIn(ZoneType.Hand).get(0),null);
                game.getAction().moveToHand(c,null); break;
            }
            fixtureReady=true;
        }));
        new CountDownLatch(1).await(); // Forge game workers are daemon threads.
    }
}
