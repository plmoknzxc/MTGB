// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.gson.*;
import forge.deck.Deck;
import forge.game.*;
import forge.game.card.Card;
import forge.game.card.CardView;
import forge.game.card.CardCollection;
import forge.game.card.CardCollectionView;
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

/** Per-match authoritative Forge process. Only explicit player responses advance choices. */
public final class ForgeBridge {
    static final Gson JSON = new Gson();
    static final PrintStream WIRE = new PrintStream(new FileOutputStream(FileDescriptor.out),true,StandardCharsets.UTF_8);
    static String SESSION;
    static final ExecutorService EDT = Executors.newSingleThreadExecutor(r -> new Thread(r, "mtgb-ui"));
    static Game game;
    static final List<Gui> guis = new ArrayList<>();
    static volatile Pending pending;
    static long revision;
    static final long started = System.nanoTime();
    static boolean finished;
    static JsonObject config;
    static Map<String,List<forge.item.PaperCard>> catalog;
    static final class BridgePlayer extends LobbyPlayerHuman {
        BridgePlayer(String name) { super(name); }
        @Override public PlayerController createMindSlaveController(Player master, Player slave) {
            throw new UnsupportedOperationException("Player-control changes require an adapted seat projection");
        }
        @Override public Player createIngamePlayer(Game game, int id) {
            Player player = new Player(getName(), game, id);
            player.setFirstController(new PlayerControllerHuman(game, player, this) {
                forge.game.spellability.SpellAbility preparedMana;
                byte preparedColor;
                @Override public List<forge.game.spellability.SpellAbility> chooseSpellAbilityToPlay() {
                    preparedMana=null;for(Gui gui:guis)if(gui.controller==this)gui.spreeModes.clear();return super.chooseSpellAbilityToPlay();
                }
                @Override public forge.game.spellability.SpellAbility getAbilityToPlay(Card host, List<forge.game.spellability.SpellAbility> abilities, forge.util.ITriggerEvent event) {
                    if(abilities.stream().anyMatch(a->a.isTrigger()))return super.getAbilityToPlay(host,abilities,event);
                    List<forge.game.spellability.SpellAbility> affordable=new ArrayList<>();
                    if(abilities.size()==1)affordable.addAll(abilities);
                    else getPlayer().runWithController(() -> {
                        for(var ability:abilities)if(ability.getPayCosts()==null || !ability.getPayCosts().hasManaCost()
                            || forge.ai.ComputerUtilMana.canPayManaCost(ability.copy(),getPlayer(),0,false))affordable.add(ability);
                    },new forge.ai.PlayerControllerAi(game,getPlayer(),getPlayer().getOriginalLobbyPlayer()));
                    if(affordable.isEmpty()){for(Gui gui:guis)if(gui.controller==this)gui.inputRevision++;return null;}
                    Gui choiceGui=guis.stream().filter(g->g.controller==this).findFirst().orElseThrow();
                    choiceGui.castingAbilities.clear();for(var a:affordable)choiceGui.castingAbilities.put(a.getId(),a);
                    forge.game.spellability.SpellAbility chosen;
                    try {chosen=super.getAbilityToPlay(host,affordable,event);}finally{choiceGui.castingAbilities.clear();}
                    if(chosen==null){for(Gui gui:guis)if(gui.controller==this)gui.inputRevision++;return null;}
                    // Choose a cancellable casting intent before Forge starts announcing
                    // the spell. Its later Charm callback has no cancellation contract.
                    if(chosen.isSpell()&&chosen.getApi()==forge.game.ability.ApiType.Charm&&host.hasKeyword(forge.game.keyword.Keyword.SPREE)&&!choiceGui.spreeModes.containsKey(host.getId())) {
                        var possible=forge.game.ability.effects.CharmEffect.makePossibleOptions(chosen);
                        int max=forge.game.ability.AbilityUtils.calculateAmount(host,chosen.getParamOrDefault("CharmNum","1"),chosen);
                        int min=forge.game.ability.AbilityUtils.calculateAmount(host,chosen.getParamOrDefault("MinCharmNum",Integer.toString(max)),chosen);
                        var modes=choiceGui.chooseSpree(chosen,possible,min,Math.min(max,possible.size()),true);
                        if(modes==null){choiceGui.inputRevision++;return null;}
                        choiceGui.spreeModes.put(host.getId(),modes.stream().map(m->m.getSVar("CharmOrder")).toList());
                    }
                    if(preparedMana!=null && preparedMana.getHostCard()==host && chosen.isManaAbility()) {preparedMana=chosen;return chosen;}
                    preparedMana=null;
                    // Ordinary any-color mana: obtain a cancellable intent BEFORE paying costs.
                    // The engine still performs the activation and mana production normally.
                    if(chosen!=null && chosen.isManaAbility() && chosen.getManaPart()!=null
                        && chosen.getManaPart().getOrigProduced().equals("Any") && !chosen.hasParam("ManaRestriction")) {
                        for(Gui gui:guis)if(gui.controller==this) {
                            List<Object> colors=new ArrayList<>(forge.card.ColorSet.fromMask(31).getOrderedColors());colors.add(null);
                            Object result=gui.modal("ManaColor",colors,false);
                            if(result==null){gui.inputRevision++;return null;}
                            preparedMana=chosen;preparedColor=((forge.card.MagicColor.Color)result).getColorMask();
                        }
                    }
                    return chosen;
                }
                @Override public byte chooseColor(String message, forge.game.spellability.SpellAbility ability, forge.card.ColorSet colors) {
                    if(preparedMana==ability && colors.hasAnyColor(preparedColor)) {
                        preparedMana=null;return preparedColor;
                    }
                    return super.chooseColor(message,ability,colors);
                }
                @Override public List<forge.game.spellability.AbilitySub> chooseModeForAbility(forge.game.spellability.SpellAbility sa,List<forge.game.spellability.AbilitySub> possible,int min,int max,boolean repeat) {
                    if(sa.getHostCard().hasKeyword(forge.game.keyword.Keyword.SPREE)&&possible.stream().allMatch(m->m.hasParam("ModeCost"))) {
                        Gui gui=guis.stream().filter(g->g.controller==this).findFirst().orElseThrow();
                        var chosen=gui.spreeModes.remove(sa.getHostCard().getId());
                        if(chosen==null)return gui.chooseSpree(sa,possible,min,max,false);
                        var result=new ArrayList<forge.game.spellability.AbilitySub>();
                        for(String id:chosen)result.add(possible.stream().filter(m->m.getSVar("CharmOrder").equals(id)).findFirst().orElseThrow());
                        return result;
                    }
                    return super.chooseModeForAbility(sa,possible,min,max,repeat);
                }
                @Override public boolean mulliganKeepHand(Player firstPlayer, int cardsToReturn) {
                    for(Gui gui:guis) if(gui.controller==this) gui.mulliganReturnCount=cardsToReturn;
                    CardCollection hand=new CardCollection(getPlayer().getCardsIn(ZoneType.Hand));
                    boolean keep=cardsToReturn>=hand.size() || super.mulliganKeepHand(firstPlayer,cardsToReturn);
                    if(keep && cardsToReturn>0) {
                        // Only bottom a kept hand, on this player's proper round.
                        CardCollectionView bottom=cardsToReturn>=hand.size()?hand:super.tuckCardsViaMulligan(hand,cardsToReturn);
                        for(Card card:bottom)game.getAction().moveToLibrary(card,-1,null);
                    }
                    return keep;
                }
                @Override public CardCollectionView tuckCardsViaMulligan(CardCollectionView hand, int cardsToReturn) {
                    // London redraw happens inside MulliganService's current seat.
                    // Defer bottoming and the next decision until its next round;
                    // asking here would skip the other player's decision entirely.
                    return new CardCollection();
                }
                @Override public Player chooseStartingPlayer(boolean isFirstGame) {
                    // The existing room owns the first-player policy for each game.
                    int seat = config.get("firstPlayerSeat").getAsInt();
                    if (seat < 1 || seat > 2) throw new IllegalArgumentException("Invalid first player seat");
                    return game.getPlayers().get(seat - 1);
                }
                @Override public org.apache.commons.lang3.tuple.ImmutablePair<CardCollection,CardCollection> arrangeForScry(CardCollection cards) {
                    for(Gui gui:guis) if(gui.controller==this) return gui.arrange(cards,false);
                    throw new IllegalStateException("Missing scry seat controller");
                }
                @Override public org.apache.commons.lang3.tuple.ImmutablePair<CardCollection,CardCollection> arrangeForSurveil(CardCollection cards) {
                    for(Gui gui:guis) if(gui.controller==this) return gui.arrange(cards,true);
                    throw new IllegalStateException("Missing surveil seat controller");
                }
                @Override protected void reveal(CardCollectionView cards, ZoneType zone, PlayerView owner, String message, boolean addSuffix) {
                    for(Gui gui:guis) if(gui.controller==this) { gui.revealCards(message==null?"展示卡牌":message,new ArrayList<>(CardView.getCollection(cards))); return; }
                }
            });
            return player;
        }
    }
    static final class Option {
        String id, label;
        int attackerId, defenderId;
        String defenderKind;
        boolean requiresPaymentCheck;
        Object card;
        Object ability;
        Object castingFace;
        String faceKind, modeCost;
        boolean selected;
        int order;
        transient Runnable action;
        Option(String id, String label, Runnable action) { this.id=id; this.label=label; this.action=action; }
    }
    static final class Pending {
        long requestId, contextId;
        int seat;
        int selectedCount, remainingCount, sourceCardId;
        String kind, message;
        Object sourceCard;
        String abilityText, abilityKind;
        List<Option> options;
        List<Object> previewCards=new ArrayList<>();
        List<Object> orderedAbilities=new ArrayList<>();
        List<Object> allocations=new ArrayList<>();
        int minCount, maxCount;
        String selectionPurpose;
        int selectedValue, requiredValue;
        int minValue,maxValue;
        transient boolean modal;
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
        Map<String,Object> m=envelope("fatal"); m.put("error", "Forge rules engine failed; see server diagnostics."); send(m); System.exit(2);
    }
    static void guiTask(Runnable r) {
        EDT.execute(() -> { try { r.run(); publishInput(); } catch(Throwable e) { fatal(e); } });
    }
    static Object base(Object proxy, Method method, Object[] args, String assets) throws Throwable {
        String n=method.getName();
        if(n.equals("getAssetsDir")) return assets;
        if(n.equals("getCurrentVersion")) return "MTGB-Forge";
        if(n.equals("isRunningOnDesktop")) return true;
        if(n.equals("isGuiThread")) return Thread.currentThread().getName().equals("mtgb-ui");
        if(n.equals("invokeInEdtLater")) { guiTask((Runnable)args[0]); return null; }
        if(n.equals("invokeInEdtNow") || n.equals("invokeInEdtAndWait")) {
            if(Thread.currentThread().getName().equals("mtgb-ui")) ((Runnable)args[0]).run();
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
        final Map<Integer,forge.game.spellability.SpellAbility> castingAbilities=new HashMap<>();
        final Map<Integer,List<String>> spreeModes=new HashMap<>();
        Input shownInput;
        long inputRevision, publishedInputRevision=-1;
        String message="";
        boolean ok, cancel;
        String okLabel="OK", cancelLabel="Cancel";
        Set<Integer> actionableCards = Set.of();
        int mulliganReturnCount;
        int interactionSourceCardId;
        int selectionMin,selectionMax;
        Input contextInput;
        long inputContextId;
        Input targetDraftInput;
        GameEntity targetDraft;
        Gui(int seat) { this.seat=seat; }
        @Override public Object invoke(Object proxy, Method method, Object[] a) throws Throwable {
            String n=method.getName();
            if(n.equals("getGameView")) return game.getView();
            if(n.equals("getGameSpeed")) return forge.gui.control.PlaybackSpeed.NORMAL;
            if(n.equals("setWeaklySelectable")) {
                Set<Integer> ids=new HashSet<>();
                for(Object card:(Iterable<?>)a[0]) ids.add(((CardView)card).getId());
                actionableCards=ids; return null;
            }
            if(n.equals("clearWeaklySelectable")) { actionableCards=Set.of(); return null; }
            if(n.equals("isNetGame")) return true; // disables local-only yield suggestions
            if(n.equals("setSelectables")) { selectionMin=(int)a[1];selectionMax=(int)a[2];return null; }
            if(n.equals("clearSelectables")) { selectionMin=selectionMax=0;return null; }
            if(n.equals("showPromptMessage")) { message=(String)a[1]; inputRevision++; return null; }
            if(n.equals("updateButtons") && a.length==6) {
                shownInput=controller.getInputQueue().getInput();
                inputRevision++;
                okLabel=(String)a[1]; cancelLabel=(String)a[2]; ok=(boolean)a[3]; cancel=(boolean)a[4]; return null;
            }
            if(n.equals("getAbilityToPlay")) return modal(n, (List<?>)a[1], false);
            if(n.equals("getInteger"))return chooseInteger((String)a[0],(int)a[1],(int)a[2],a[3] instanceof Boolean b && b);
            if(n.equals("assignCombatDamage"))return BridgeAllocation.combat(this,(CardView)a[0],(List<CardView>)a[1],(int)a[2],(GameEntityView)a[3],(boolean)a[4],(boolean)a[5]);
            if(n.equals("assignGenericAmount"))return BridgeAllocation.generic(this,(CardView)a[0],(Map<Object,Integer>)a[1],(int)a[2],(boolean)a[3],(String)a[4]);
            if(n.equals("one")) return modal(n+": "+a[0], (List<?>)a[1], false);
            if(n.equals("oneOrNone")) {
                List<Object> values=new ArrayList<>((List<?>)a[1]);values.add(null);
                return modal("oneOrNone: "+a[0],values,false);
            }
            if(n.equals("many") && a.length==7) {
                List<Object> values=new ArrayList<>((List<?>)a[4]);
                List<?> initial=a[5]==null?List.of():(List<?>)a[5];
                for(Object value:initial)if(!values.contains(value))values.add(value);
                return selectCards((String)a[0],values,(int)a[2],(int)a[3],initial,false);
            }
            if(n.equals("chooseSingleEntityForEffect")) {
                revealDelayed((DelayedReveal)a[2]);
                List<?> picked=selectCards((String)a[0],(List<?>)a[1],(boolean)a[3]?0:1,1,List.of(),false);
                return picked.isEmpty()?null:picked.get(0);
            }
            if(n.equals("chooseEntitiesForEffect")) {
                revealDelayed((DelayedReveal)a[4]);
                return selectCards((String)a[0],(List<?>)a[1],(int)a[2],(int)a[3],List.of(),false);
            }
            if(n.equals("reveal")) { revealCards((String)a[0],(List<?>)a[1]);return null; }
            if(n.equals("updateRevealedCards"))return null; // revealCards already acknowledged the exact list.
            if(n.equals("manipulateCardList"))return manipulate((String)a[0],(Iterable<CardView>)a[1],(Iterable<CardView>)a[2],(boolean)a[3],(boolean)a[4],(boolean)a[5]);
            if(n.equals("order") && a.length==9) {
                List<Object> values=new ArrayList<>((List<?>)a[4]);
                if(a[5]!=null)values.addAll((List<?>)a[5]);
                if((boolean)a[7])
                    throw new UnsupportedOperationException("Unadapted ordering: "+a[0]);
                if(values.stream().allMatch(v -> v instanceof forge.game.spellability.SpellAbilityView))return orderAbilities(values);
                int min=(int)a[3]<0?0:Math.max(0,values.size()-(int)a[3]);
                int max=(int)a[2]<0?values.size():Math.max(0,values.size()-(int)a[2]);
                return new IGuiGame.OrderResult<>(selectCards(a[0]+"\n"+a[1],values,min,max,List.of(),true),false);
            }
            if(n.equals("showOptionDialog")) return modalIndex(n+": "+a[0], (List<?>)a[3]);
            if(n.equals("confirm")) return modalIndex(n+": "+a[1], (List<?>)a[3])==0;
            if(n.equals("showConfirmDialog") && a.length==5) return modalIndex(n+": "+a[0], List.of(a[2],a[3]))==0;
            if(n.equals("openZones")) return null; // zone contents come from seat-filtered snapshots
            if(n.equals("getDayTime")) return "";
            if(n.equals("showErrorDialog")) throw new IllegalStateException(Arrays.toString(a));
            if(n.equals("applyDelta") || n.equals("applyYieldUpdate"))
                throw new UnsupportedOperationException("Unadapted information or control callback: "+n);
            if(method.isDefault()) return InvocationHandler.invokeDefault(proxy,method,a);
            if(method.getReturnType()==void.class) return null; // presentation-only callbacks
            if(Set.of("isGamePaused", "isUiSetToSkipPhase", "isSelecting").contains(n)) return false;
            throw new UnsupportedOperationException("IGuiGame."+n+" "+Arrays.toString(a));
        }
        String abilityLabel(Object value) {
            var ability=(forge.game.spellability.SpellAbilityView)value;
            return ability.getHostCard().getCurrentState().getName()+" — "+ability.getDescription();
        }
        Object choiceCard(Object value) {
            if(!(value instanceof CardView cv))return null;
            for(Card c:game.getCardsInGame())if(c.getId()==cv.getId())return card(c);
            Map<String,Object> fallback=new LinkedHashMap<>();fallback.put("id",cv.getId());fallback.put("name",cv.getCurrentState().getName());return fallback;
        }
        List<CardView> manipulate(String title,Iterable<CardView> all,Iterable<CardView> movable,boolean topOK,boolean bottomOK,boolean anywhere) {
            List<CardView> fixed=new ArrayList<>(), moving=new ArrayList<>(),top=new ArrayList<>(),bottom=new ArrayList<>();
            all.forEach(fixed::add);movable.forEach(moving::add);fixed.removeAll(moving);
            while(!moving.isEmpty()) {
                CompletableFuture<String> answer=new CompletableFuture<>();List<Option> options=new ArrayList<>();
                for(CardView cv:moving) {
                    if(topOK){Option o=new Option("move-top:"+cv.getId(),"置于顶部",()->answer.complete("top:"+cv.getId()));o.card=choiceCard(cv);options.add(o);}
                    if(bottomOK){Option o=new Option("move-bottom:"+cv.getId(),"置于底部",()->answer.complete("bottom:"+cv.getId()));o.card=choiceCard(cv);options.add(o);}
                    if(anywhere){Option o=new Option("move-at:"+cv.getId(),"选择插入位置",()->answer.complete("at:"+cv.getId()));o.card=choiceCard(cv);options.add(o);}
                }
                if(options.isEmpty())throw new IllegalStateException("No allowed card movement");
                Pending request=new Pending(seat,"MoveCards",title,options);request.modal=true;publish(request);
                try {
                    String result=answer.get();int id=Integer.parseInt(result.substring(result.indexOf(':')+1));
                    CardView cv=moving.stream().filter(c->c.getId()==id).findFirst().orElseThrow();moving.remove(cv);
                    if(result.startsWith("top:"))top.add(cv);else if(result.startsWith("bottom:"))bottom.add(cv);
                    else {
                        List<Integer> positions=new ArrayList<>();for(int i=0;i<=fixed.size();i++)positions.add(i);
                        // Only numerical positions are exposed; never serialize the unseen library.
                        int at=(Integer)modal("插入位置（0 为最上方）",positions,false);fixed.add(at,cv);
                    }
                }catch(Exception e){throw new IllegalStateException("Unanswered card movement",e);}
            }
            top.addAll(fixed);top.addAll(bottom);return top;
        }
        void revealDelayed(DelayedReveal reveal) {
            if(reveal!=null)revealCards(reveal.getMessagePrefix(),new ArrayList<>(reveal.getCards()));
        }
        void revealCards(String title,List<?> values) {
            if(values.isEmpty())return;
            CompletableFuture<Boolean> done=new CompletableFuture<>();
            Pending request=new Pending(seat,"RevealCards",title,List.of(new Option("confirm-cards","确认",()->done.complete(true))));
            request.modal=true;
            for(Object value:values){Object cv=choiceCard(value);if(cv!=null)request.previewCards.add(cv);}
            publish(request);
            try{done.get();}catch(Exception e){throw new IllegalStateException("Unanswered reveal",e);}
        }
        List<Object> selectCards(String title,List<?> values,int minimum,int maximum,List<?> initial,boolean ordered) {
            int min=Math.max(0,Math.min(values.size(),minimum));
            int max=maximum<0?values.size():Math.min(values.size(),maximum);
            List<Object> selected=new ArrayList<>(initial);
            while(true) {
                CompletableFuture<Integer> answer=new CompletableFuture<>();List<Option> options=new ArrayList<>();
                for(int i=0;i<values.size();i++) {
                    final int index=i;Object value=values.get(i);boolean chosen=selected.contains(value);
                    Option option=new Option("select:"+i,chosen?"取消选择":"选择",()->answer.complete(index));
                    option.card=choiceCard(value);option.selected=chosen;option.order=chosen?selected.indexOf(value)+1:0;
                    if(option.card==null)option.label=(chosen?"取消选择 · ":"")+String.valueOf(value);
                    options.add(option);
                }
                if(selected.size()>=min && selected.size()<=max)options.add(new Option("confirm-cards","确认选择",()->answer.complete(-1)));
                if(!selected.isEmpty())options.add(new Option("reset-cards","重新选择",()->answer.complete(-2)));
                Pending request=new Pending(seat,ordered?"OrderCards":"SelectCards",title,options);request.modal=true;
                request.selectedCount=selected.size();request.remainingCount=Math.max(0,min-selected.size());request.minCount=min;request.maxCount=max;
                publish(request);
                try {
                    int result=answer.get();if(result==-1)return selected;if(result==-2){selected.clear();continue;}
                    Object value=values.get(result);
                    if(!selected.remove(value)) {
                        if(max==1)selected.clear();
                        if(selected.size()<max)selected.add(value);
                    }
                }catch(Exception e){throw new IllegalStateException("Unanswered card selection",e);}
            }
        }
        org.apache.commons.lang3.tuple.ImmutablePair<CardCollection,CardCollection> arrange(CardCollection cards,boolean surveil) {
            List<Card> remaining=new ArrayList<>(cards);
            CardCollection top=new CardCollection(), bottom=new CardCollection();
            while(true) {
                CompletableFuture<String> answer=new CompletableFuture<>();
                List<Option> options=new ArrayList<>();
                for(Card card:remaining) {
                    Option up=new Option("scry-top:"+card.getId(),"置于牌库顶",()->answer.complete("top:"+card.getId()));up.card=card(card);options.add(up);
                    Option down=new Option("scry-bottom:"+card.getId(),surveil?"置于坟墓场":"置于牌库底",()->answer.complete("bottom:"+card.getId()));down.card=card(card);options.add(down);
                }
                if(remaining.isEmpty())options.add(new Option("confirm-scry",surveil?"确认刺探":"确认占卜",()->answer.complete("confirm")));
                if(!top.isEmpty() || !bottom.isEmpty())options.add(new Option("reset-scry","重新选择",()->answer.complete("reset")));
                String message="依次选择牌库顶、牌库底的顺序（先选的在上方）。";
                message+="\n牌库顶："+top.stream().map(Card::getName).collect(java.util.stream.Collectors.joining(" → "));
                message+="\n"+(surveil?"坟墓场：":"牌库底：")+bottom.stream().map(Card::getName).collect(java.util.stream.Collectors.joining(" → "));
                Pending request=new Pending(seat,surveil?"Surveil":"Scry",message,options);request.modal=true;
                for(Card c:top)request.previewCards.add(selectedCard(c,"牌库顶 · "+(top.indexOf(c)+1)));
                for(Card c:bottom)request.previewCards.add(selectedCard(c,(surveil?"坟墓场":"牌库底")+" · "+(bottom.indexOf(c)+1)));
                request.remainingCount=remaining.size();request.selectedCount=top.size()+bottom.size();
                publish(request);
                try {
                    String choice=answer.get();
                    if(choice.equals("confirm"))return org.apache.commons.lang3.tuple.ImmutablePair.of(top,bottom);
                    if(choice.equals("reset")){remaining=new ArrayList<>(cards);top.clear();bottom.clear();continue;}
                    int id=Integer.parseInt(choice.substring(choice.indexOf(':')+1));
                    Card card=remaining.stream().filter(c->c.getId()==id).findFirst().orElseThrow();
                    (choice.startsWith("top:")?top:bottom).add(card);remaining.remove(card);
                } catch(Exception e){throw new IllegalStateException("Unanswered scry",e);}
            }
        }
        Object orderAbilities(List<Object> values) {
            List<Object> remaining=new ArrayList<>(values), ordered=new ArrayList<>();
            while(true) {
                CompletableFuture<Integer> answer=new CompletableFuture<>();
                List<Option> options=new ArrayList<>();
                for(int i=0;i<remaining.size();i++) {
                    final int index=i;
                    Option option=new Option("order:"+i,abilityLabel(remaining.get(i)),()->answer.complete(index));
                    option.ability=abilityPreview(remaining.get(i),values.indexOf(remaining.get(i)));
                    options.add(option);
                }
                if(remaining.isEmpty())options.add(new Option("confirm-order","确认此结算顺序",()->answer.complete(-1)));
                if(!ordered.isEmpty())options.add(new Option("reset-order","重新排序",()->answer.complete(-2)));
                String message="依次选择先结算的异能；全部选完后确认。";
                for(int i=0;i<ordered.size();i++)message+="\n"+(i+1)+". "+abilityLabel(ordered.get(i));
                Pending request=new Pending(seat,"OrderAbilities",message,options);request.modal=true;
                for(Object value:ordered)request.orderedAbilities.add(abilityPreview(value,values.indexOf(value)));
                publish(request);
                try {
                    int choice=answer.get();
                    if(choice==-1)return new IGuiGame.OrderResult<>(ordered,false);
                    if(choice==-2){remaining=new ArrayList<>(values);ordered.clear();}
                    else ordered.add(remaining.remove(choice));
                } catch(Exception e){throw new IllegalStateException("Unanswered ability order",e);}
            }
        }
        Object abilityPreview(Object value,int id) {
            var ability=(forge.game.spellability.SpellAbilityView)value;
            return Map.of("id",id,"kind","ability","description",ability.getDescription(),"source",choiceCard(ability.getHostCard()));
        }
        Object selectStackTarget(String title,List<?> values,boolean index) {
            int selected=-1;
            while(true) {
                CompletableFuture<Integer> answer=new CompletableFuture<>();List<Option> options=new ArrayList<>();
                for(int i=0;i<values.size();i++) {
                    final int j=i;Object value=values.get(i);
                    Option option=new Option(value instanceof forge.game.spellability.StackItemView stack?"stack:"+stack.getId():"cancel",value==null?"取消":String.valueOf(value),()->answer.complete(j));
                    option.selected=selected==j;options.add(option);
                }
                if(selected>=0){options.add(new Option("ok","确认目标",()->answer.complete(-2)));options.add(new Option("reset-targets","重选",()->answer.complete(-3)));}
                Pending request=new Pending(seat,"SelectStackTarget",title,options);request.modal=true;request.selectedCount=selected>=0?1:0;request.maxCount=1;
                request.sourceCardId=interactionSourceCardId;
                for(Card c:game.getCardsInGame())if(c.getId()==interactionSourceCardId)request.sourceCard=card(c);
                publish(request);
                try {
                    int result=answer.get();
                    if(result==-2)return index?selected:values.get(selected);
                    if(result==-3){selected=-1;continue;}
                    if(values.get(result)==null)return index?result:null;
                    selected=selected==result?-1:result;
                }catch(Exception e){throw new IllegalStateException("Unanswered stack target",e);}
            }
        }
        Integer chooseInteger(String title,int min,int max,boolean descending) {
            if(min>max)return null;
            int value=descending?max:min;long context=++revision;
            while(true) {
                CompletableFuture<String> answer=new CompletableFuture<>();List<Option> options=new ArrayList<>();
                if(value>min)options.add(new Option("number-minus","−",()->answer.complete("-1")));
                if(value<max)options.add(new Option("number-plus","+",()->answer.complete("1")));
                if((long)value-5>=min)options.add(new Option("number-minus-five","−5",()->answer.complete("-5")));
                if((long)value+5<=max)options.add(new Option("number-plus-five","+5",()->answer.complete("5")));
                options.add(new Option("confirm-number","确认",()->answer.complete("confirm")));
                options.add(new Option("cancel","取消",()->answer.complete("cancel")));
                Pending request=new Pending(seat,"ChooseNumber",title,options);request.modal=true;request.contextId=context;
                request.selectedValue=value;request.minValue=min;request.maxValue=max;request.sourceCardId=interactionSourceCardId;
                for(Card c:game.getCardsInGame())if(c.getId()==interactionSourceCardId)request.sourceCard=card(c);
                publish(request);
                try {String action=answer.get();if(action.equals("cancel"))return null;if(action.equals("confirm"))return value;value+=Integer.parseInt(action);}
                catch(Exception e){throw new IllegalStateException("Unanswered number selection",e);}
            }
        }
        List<forge.game.spellability.AbilitySub> chooseSpree(forge.game.spellability.SpellAbility sa,List<forge.game.spellability.AbilitySub> possible,int min,int max,boolean cancellable) {
            Set<Integer> selected=new LinkedHashSet<>();long context=0;
            while(true) {
                CompletableFuture<Integer> answer=new CompletableFuture<>();List<Option> options=new ArrayList<>();
                for(int i=0;i<possible.size();i++) {
                    final int index=i;var mode=possible.get(i);
                    if(selected.size()>=max&&!selected.contains(i))continue;
                    Option option=new Option("mode:"+i,mode.getDescription(),()->answer.complete(index));
                    option.selected=selected.contains(i);option.modeCost=new forge.game.cost.Cost(mode.getParam("ModeCost"),false).toSimpleString();options.add(option);
                }
                if(selected.size()>=min)options.add(new Option("confirm-modes","确认",()->answer.complete(-1)));
                if(!selected.isEmpty())options.add(new Option("reset-modes","重新选择",()->answer.complete(-2)));
                if(cancellable)options.add(new Option("cancel","取消施放",()->answer.complete(-3)));
                Pending request=new Pending(seat,"ChooseModes","追欢：选择一项或多项额外费用",options);request.modal=true;
                if(context==0)context=request.requestId;request.contextId=context;request.minCount=min;request.maxCount=max;request.selectedCount=selected.size();
                request.sourceCardId=sa.getHostCard().getId();request.sourceCard=card(sa.getHostCard());publish(request);
                try {int choice=answer.get();
                    if(choice==-3)return null;
                    if(choice==-1){List<forge.game.spellability.AbilitySub> result=new ArrayList<>();for(int i:selected)result.add(possible.get(i));return result;}
                    if(choice==-2)selected.clear();else if(!selected.remove(choice))selected.add(choice);
                }catch(Exception e){throw new IllegalStateException("Unanswered spree modes",e);}
            }
        }
        void applyCastingFace(Option option,Object value) {
            if(!(value instanceof forge.game.spellability.SpellAbilityView av))return;
            var sa=castingAbilities.get(av.getId());if(sa==null)return;
            var host=sa.getHostCard();var state=sa.getCardState();if(state==null)return;
            if(!host.isModal()&&!sa.isAdventure()&&!sa.isOmen()&&!host.hasState(forge.card.CardStateName.Secondary)&&sa.getCardStateName()!=forge.card.CardStateName.PreparedSpell)return;
            Map<String,Object> face=new LinkedHashMap<>((Map<String,Object>)card(host));
            face.put("name",state.getName());face.put("currentRules",state.getOracleText());face.put("currentType",state.getType().toString());face.put("manaCost",state.getManaCost().toString());
            face.put("creature",state.getType().isCreature());face.put("land",state.getType().isLand());face.put("planeswalker",state.getType().isPlaneswalker());
            face.put("stateName",sa.getCardStateName().name());face.put("backFace",sa.getCardStateName()==forge.card.CardStateName.Backside);
            option.castingFace=face;option.faceKind=sa.getCardStateName()==forge.card.CardStateName.PreparedSpell?"prepare":host.isModal()?(sa.getCardStateName()==forge.card.CardStateName.Backside?"mdfc-back":"mdfc-front"):sa.isAdventure()?"adventure":sa.isOmen()?"omen":"main";
        }
        int modalIndex(String title,List<?> values) { return (Integer)modal(title,values,true); }
        Object modal(String title,List<?> values,boolean index) {
            if(title.equals("getAbilityToPlay")) {
                List<Object> playable=new ArrayList<>();
                playable.addAll(values);
                if(playable.isEmpty())return null;
                if(playable.size()==1)return playable.get(0);
                playable.add(null);values=playable;
            }
            // A sole spell/ability is not a choice. Keep genuine confirmation dialogs explicit.
            if(values.size()==1 && !(values.get(0) instanceof forge.game.spellability.StackItemView)
                && (title.equals("getAbilityToPlay") || title.startsWith("one: ")))
                return index ? 0 : values.get(0);
            boolean stackTarget=values.stream().anyMatch(v -> v instanceof forge.game.spellability.StackItemView);
            if(stackTarget)return selectStackTarget(title,values,index);
            CompletableFuture<Object> answer=new CompletableFuture<>();
            List<Option> options=new ArrayList<>();
            for(int i=0;i<values.size();i++) {
                final int j=i; Object value=values.get(i);
                String id=value instanceof forge.game.spellability.StackItemView stack ? "stack:"+stack.getId() : value==null?"cancel":"choice:"+i;
                options.add(new Option(id,value==null?"取消":String.valueOf(value),() -> answer.complete(index ? j : value)));
            }
            Pending request=new Pending(seat,stackTarget?"SelectStackTarget":"dialog",title,options);
            request.modal=true;
            for(int i=0;i<values.size();i++){options.get(i).card=choiceCard(values.get(i));if(title.equals("getAbilityToPlay"))applyCastingFace(options.get(i),values.get(i));}
            request.sourceCardId=interactionSourceCardId;
            if(!values.isEmpty() && values.get(0) instanceof forge.game.spellability.SpellAbility ability)
                request.sourceCardId=ability.getHostCard().getId();
            else if(!values.isEmpty() && values.get(0) instanceof forge.game.spellability.SpellAbilityView ability)
                request.sourceCardId=ability.getHostCard().getId();
            publish(request);
            try { return answer.get(); }
            catch(Exception e) { throw new IllegalStateException("Unanswered dialog: "+title,e); }
        }
    }
    static synchronized void publish(Pending p) {
        if(p.kind.equals("SelectCards") || p.kind.startsWith("InputSelectEntities") || p.kind.startsWith("InputSelectCards")) {
            // A search may grant sight of more cards than can legally be picked.
            // Preserve Forge's per-card viewing permission, including top-N limits.
            Set<Integer> offered=new HashSet<>(), owners=new HashSet<>(), previews=new HashSet<>();
            for(Option o:p.options)if(o.card instanceof Map<?,?> cv)offered.add(((Number)cv.get("id")).intValue());
            for(Object o:p.previewCards)if(o instanceof Map<?,?> cv)previews.add(((Number)cv.get("id")).intValue());
            for(Card c:game.getCardsIn(ZoneType.Library))if(offered.contains(c.getId()))owners.add(c.getOwner().getId());
            PlayerView viewer=guis.get(p.seat-1).controller.getPlayer().getView();
            for(Card c:game.getCardsIn(ZoneType.Library))if(owners.contains(c.getOwner().getId()) && !offered.contains(c.getId()) && !previews.contains(c.getId()) && c.getView().canBeShownTo(viewer))
                p.previewCards.add(selectedCard(c,"不可选"));
        }
        pending=p;
        Map<String,Object> m=envelope("request"); m.put("request",p); m.put("views",views());
        m.put("elapsedMs",(System.nanoTime()-started)/1_000_000); send(m);
    }
    static synchronized void publishInput() {
        if(game!=null && game.isGameOver()) { finish(); return; }
        if(game==null || (pending!=null && !pending.consumed && (pending.modal || Set.of("dialog","SelectStackTarget","OrderAbilities","Scry").contains(pending.kind)))) return;
        for(Gui g:guis) {
            Input inp=g.controller.getInputQueue().getInput();
            // A queued card selection can finish after the EDT callback returns. Do not
            // re-offer the old payment prompt before Forge has actually refreshed it.
            if(inp==null || inp!=g.shownInput || g.inputRevision==g.publishedInputRevision) continue;
            String kind=inp.getClass().getSimpleName();
            if(kind.isEmpty()) kind=inp.getClass().getSuperclass().getSimpleName();
            if(g.targetDraftInput!=inp){g.targetDraftInput=inp;g.targetDraft=null;}
            boolean singleTarget=inp instanceof InputSelectTargets && g.selectionMax==1;
            List<Option> opts=new ArrayList<>();
            if(singleTarget && g.targetDraft!=null)opts.add(new Option("ok","确认",() -> {
                GameEntity chosen=g.targetDraft;g.targetDraft=null;
                if(chosen instanceof Card c)g.controller.getInputProxy().selectCard(c.getView(),null,null);
                else if(chosen instanceof Player p)g.controller.getInputProxy().selectPlayer(p.getView(),null);
            }));
            else if(g.ok) opts.add(new Option("ok",g.okLabel,() -> g.controller.getInputProxy().selectButtonOK()));
            if(singleTarget && g.targetDraft!=null)opts.add(new Option("reset-targets","重选",() -> {g.targetDraft=null;g.inputRevision++;}));
            // Expose undo, not the human desktop client's yield-until-end-of-turn shortcut.
            if(g.cancel && (!(inp instanceof InputPassPriority) || g.controller.canUndoLastAction())
                && (!(inp instanceof InputAttack) || !game.getCombat().getAttackers().isEmpty()))
                opts.add(new Option("cancel",g.cancelLabel,() -> g.controller.getInputProxy().selectButtonCancel()));
            if(inp instanceof InputAttack attack) addAttackOptions(g,attack,opts);
            if(inp instanceof InputBlock block) addBlockOptions(g,block,opts);
            if(supportedInput(inp) && !(inp instanceof InputAttack) && !(inp instanceof InputBlock)) {
                List<ZoneType> zones=new ArrayList<>(List.of(ZoneType.Hand,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile));
                if(inp instanceof InputSelectEntitiesFromList || inp instanceof InputSelectTargets)zones.addAll(List.of(ZoneType.Library,ZoneType.Command,ZoneType.Sideboard));
                for(ZoneType z:zones) {
                    for(Card c:game.getCardsIn(z)) {
                        String activate=inp.getActivateAction(c);
                        boolean london=inp instanceof InputLondonMulligan
                            && z==ZoneType.Hand && c.getOwner()==g.controller.getPlayer();
                        if(london) activate="Select for London mulligan";
                        // getActivateAction describes a possible proposal, not affordability.
                        // Reuse Forge's priority-time prediction (includes mana sources and cost
                        // modifiers); do not count lands or reimplement costs in the Unity client.
                        if(inp instanceof InputPassPriority && z==ZoneType.Hand
                            && !g.actionableCards.contains(c.getId())) activate=null;
                        boolean visible=c.getView().canBeShownTo(g.controller.getPlayer().getView());
                        if(activate!=null) {
                            Option candidate=new Option("card:"+c.getId(),
                            (london && ((InputLondonMulligan)inp).getSelectedCards().contains(c) ? "[已选回牌库底] " : "")
                            +(!visible || c.isFaceDown() ? "Face-down card" : c.getName())+" ["+z+"]",() -> {
                                if(inp instanceof InputSelectEntitiesFromList selection)toggleSelection(g,selection,c);
                                else if(singleTarget){g.targetDraft=g.targetDraft==c?null:c;g.inputRevision++;}
                                else g.controller.getInputProxy().selectCard(c.getView(),null,null);
                            });
                            candidate.requiresPaymentCheck=inp instanceof InputPassPriority && !g.actionableCards.contains(c.getId());
                            if(inp instanceof InputSelectEntitiesFromList || inp instanceof InputSelectTargets) {
                                candidate.card=visible?card(c):Map.of("id",c.getId(),"name","Face-down card","faceDown",true);
                                if(inp instanceof InputSelectEntitiesFromList selection)candidate.selected=selection.getSelected().contains(c);
                                else if(singleTarget)candidate.selected=g.targetDraft==c;
                            }
                            opts.add(candidate);
                        }
                    }
                }
            }
            if(inp instanceof InputSelectTargets || inp instanceof InputSelectEntitiesFromList) {
                for(Player p:game.getPlayers()) if(canTargetPlayer(inp,p)) opts.add(new Option("player:"+p.getId(),p.getName(),() -> {
                    if(inp instanceof InputSelectEntitiesFromList selection)toggleSelection(g,selection,p);
                    else if(singleTarget){g.targetDraft=g.targetDraft==p?null:p;g.inputRevision++;}
                    else g.controller.getInputProxy().selectPlayer(p.getView(),null);
                }));
            }
            if(inp instanceof InputPayMana) {
                byte[] colors=forge.card.mana.ManaAtom.MANATYPES; String[] symbols={"W","U","B","R","G","C"};
                for(int i=0;i<colors.length;i++) { final byte color=colors[i];
                    if(g.controller.getPlayer().getManaPool().getAmountOfColor(color)>0)
                        opts.add(new Option("mana:"+symbols[i],"Pay {"+symbols[i]+"}",() -> g.controller.useMana(color)));
                }
            }
            if(!supportedInput(inp)) throw new UnsupportedOperationException("Unadapted input: "+kind);
            if(opts.isEmpty()) throw new UnsupportedOperationException("No supported options for "+kind+": "+g.message);
            Pending request=new Pending(g.seat,kind,g.message,opts);
            if(g.contextInput!=inp){g.contextInput=inp;g.inputContextId=++revision;}
            request.contextId=g.inputContextId;
            if(inp instanceof InputConfirm) {
                try {
                    Field shown=InputConfirm.class.getDeclaredField("card");shown.setAccessible(true);
                    CardView cv=(CardView)shown.get(inp);
                    if(cv!=null && cv.canBeShownTo(g.controller.getPlayer().getView()))request.previewCards.add(g.choiceCard(cv));
                }catch(ReflectiveOperationException e){throw new IllegalStateException("Pinned confirmation API changed",e);}
            }
            if(inp instanceof InputSelectEntitiesFromList selection) {
                request.selectedCount=selection.getSelected().size();request.minCount=g.selectionMin;request.maxCount=g.selectionMax;
                request.remainingCount=Math.max(0,g.selectionMin-request.selectedCount);
                if(!selection.getSelected().isEmpty())request.options.add(new Option("reset-cards","重新选择",()->{
                    for(Object previous:new ArrayList<>(selection.getSelected()))toggleSelection(g,selection,(GameEntity)previous);
                }));
                applyEvidenceRequirement(selection,request);
            }
            if(singleTarget) {
                request.selectedCount=g.targetDraft==null?0:1;request.minCount=g.selectionMin;request.maxCount=1;
                request.remainingCount=Math.max(0,g.selectionMin-request.selectedCount);
            }
            if(inp instanceof InputSelectTargets) {
                try {
                    Field field=InputSelectTargets.class.getDeclaredField("sa");field.setAccessible(true);
                    var ability=(forge.game.spellability.SpellAbility)field.get(inp);
                    request.sourceCardId=ability.getHostCard().getId();
                    request.sourceCard=card(ability.getHostCard());
                    request.abilityText=ability.getDescription();
                    if(request.abilityText==null || request.abilityText.isBlank())request.abilityText=ability.isTrigger()?ability.getTrigger().toString():ability.getStackDescription();
                    if(request.abilityText==null || request.abilityText.isBlank())request.abilityText=g.message;
                    request.abilityKind=ability.isTrigger()?"trigger":ability.isSpell()?"spell":"ability";
                    for(Option option:opts)if(option.id.startsWith("player:"))option.selected=g.targetDraft!=null && option.id.equals("player:"+g.targetDraft.getId());
                }catch(ReflectiveOperationException e){throw new IllegalStateException("Pinned target source API changed",e);}
            }
            if(inp instanceof InputConfirmMulligan)request.remainingCount=g.mulliganReturnCount;
            if(inp instanceof InputLondonMulligan london) {
                try {
                    Field returning=InputLondonMulligan.class.getDeclaredField("toReturn"); returning.setAccessible(true);
                    request.selectedCount=london.getSelectedCards().size();
                    request.remainingCount=returning.getInt(london)-request.selectedCount;
                } catch(ReflectiveOperationException e) {throw new IllegalStateException("Pinned mulligan API changed",e);}
            }
            g.publishedInputRevision=g.inputRevision;
            publish(request); return;
        }
    }
    // The pinned desktop input validates the CMC cost only after OK. Publish the
    // actual cost threshold and withhold OK until it is met, without parsing text.
    static void applyEvidenceRequirement(InputSelectEntitiesFromList selection,Pending request) {
        try {
            Class<?> base=forge.gamemodes.match.input.InputSelectManyBase.class;
            Field saField=base.getDeclaredField("sa"),typeField=base.getDeclaredField("tallyType"),tallyField=base.getDeclaredField("tally");
            saField.setAccessible(true);typeField.setAccessible(true);tallyField.setAccessible(true);
            var sa=(forge.game.spellability.SpellAbility)saField.get(selection);
            if(sa==null || !"CMC".equals(typeField.get(selection)) || sa.getPayCosts()==null ||
                sa.getPayCosts().getCostParts().stream().noneMatch(c->c instanceof forge.game.cost.CostCollectEvidence))return;
            request.selectionPurpose="collectEvidence";
            request.requiredValue=tallyField.getInt(selection);
            forge.game.card.CardCollection cards=new forge.game.card.CardCollection();
            for(Object value:selection.getSelected())if(value instanceof Card c)cards.add(c);
            request.selectedValue=forge.game.card.CardLists.getTotalCMC(cards);
            request.sourceCardId=sa.getHostCard().getId();request.sourceCard=card(sa.getHostCard());
            if(request.selectedValue<request.requiredValue)request.options.removeIf(o->o.id.equals("ok"));
        }catch(ReflectiveOperationException e){throw new IllegalStateException("Pinned evidence selection API changed",e);}
    }
    // Preserve Forge's selection and hasEnoughTargets validation, but leave the
    // choice open at maximum count. Desktop refresh() would immediately press OK.
    static void toggleSelection(Gui gui,InputSelectEntitiesFromList selection,GameEntity entity) {
        if(gui.controller.getInputQueue().getInput()!=selection || !selection.getValidChoices().contains(entity))return;
        try {
            Method toggle=InputSelectEntitiesFromList.class.getDeclaredMethod("selectEntity",GameEntity.class);
            toggle.setAccessible(true);
            if(selection.getSelected().contains(entity))toggle.invoke(selection,entity);
            else {
                if(gui.selectionMax==1)
                    for(Object previous:new ArrayList<>(selection.getSelected()))toggle.invoke(selection,previous);
                if(selection.getSelected().size()<gui.selectionMax)toggle.invoke(selection,entity);
            }
            selection.showMessage();
        }catch(ReflectiveOperationException e){throw new IllegalStateException("Pinned Forge selection API changed",e);}
    }
    static List<Object> views() {
        List<Object> views=new ArrayList<>();
        if(game==null) return views;
        var markerTriggers=pendingMarkerTriggers();
        for(int s=0;s<guis.size();s++) {
            Map<String,Object> view=new LinkedHashMap<>(); view.put("seat",s+1);
            view.put("canRespond",guis.get(s).controller.getPlayer().getView().hasAvailableActions());
            view.put("phase",String.valueOf(game.getPhaseHandler().getPhase()));
            view.put("dayTime",game.getDayTime()==null?"":game.isDay()?"day":"night");
            view.put("globalEffects",game.getDayTime()==null?List.of():List.of(Map.of("id",-2000001,"name",game.isDay()?"Day":"Night","helperArtName",game.isDay()?"Day":"Night","effectMarker",true,"markerKind","game","zone","Command","currentType","Game state","currentRules",game.isDay()?"当前为白昼。":"当前为黑夜。")));
            view.put("turn", game.getPhaseHandler().getTurn());
            view.put("turnSeat", game.getPlayers().indexOf(game.getPhaseHandler().getPlayerTurn()) + 1);
            view.put("prioritySeat", game.getPlayers().indexOf(game.getPhaseHandler().getPriorityPlayer()) + 1);
             view.put("stackCount",game.getStack().size());
            List<String> stack = new ArrayList<>();
            List<Object> stackItems = new ArrayList<>();
            for (var item : game.getStack()) {
                Card source=item.getSpellAbility().getHostCard();
                String description=source.isFaceDown() ? "Face-down spell" : item.getStackDescription();
                stack.add(description);
                Map<String,Object> entry=new LinkedHashMap<>();
                entry.put("id",item.getId());entry.put("description",description);
                entry.put("kind",item.getSpellAbility().isSpell()?"spell":item.getSpellAbility().isTrigger()?"trigger":"ability");
                entry.put("copied",item.getSpellAbility().isCopied());
                entry.put("controllerSeat",game.getPlayers().indexOf(item.getActivatingPlayer())+1);
                Map<String,Object> stackSource=(Map<String,Object>)card(source);
                Integer x=item.getSpellAbility().getXManaCostPaid();
                if(item.getSpellAbility().isSpell()&&x!=null&&!source.isFaceDown()) {
                    entry.put("xValue",x);entry.put("hasXValue",true);
                    stackSource.put("manaCost",String.valueOf(stackSource.get("manaCost")).replace("{X}","{"+x+"}"));
                }
                entry.put("source",stackSource);stackItems.add(entry);
            }
            view.put("stack", stack);
            view.put("stackItems", stackItems);
            Player viewer=guis.get(s).controller.getPlayer();
            List<Object> playableExile=new ArrayList<>(),playableGraveyard=new ArrayList<>();
            for(ZoneType playableZone:List.of(ZoneType.Graveyard,ZoneType.Exile)) for(Card c:game.getCardsIn(playableZone)) {
                boolean permitted=c.mayPlay(viewer).stream().anyMatch(o->o.grantsZonePermissions());
                if(playableZone==ZoneType.Graveyard&&c.getOwner()==viewer)
                    permitted|=c.getAllSpellAbilities().stream().anyMatch(a->a.getRestrictions().getZone()==ZoneType.Graveyard);
                boolean offered=pending!=null && pending.seat==s+1 && pending.kind.equals("InputPassPriority")
                    && pending.options.stream().anyMatch(o->o.id.equals("card:"+c.getId()));
                if((permitted||offered)&&c.getView().canBeShownTo(viewer.getView())&&!c.isFaceDown())(playableZone==ZoneType.Graveyard?playableGraveyard:playableExile).add(card(c));
            }
            view.put("playableExile",playableExile);view.put("playableGraveyard",playableGraveyard);
            List<Object> players=new ArrayList<>();
            for(int i=0;i<guis.size();i++) {
                Player p=guis.get(i).controller.getPlayer();
                Map<String,Object> pv=new LinkedHashMap<>(); pv.put("seat",i+1); pv.put("life",p.getLife());
                pv.put("targetId", p.getId());
                List<Object> playerCounters=new ArrayList<>();
                for(var entry:p.getCounters().entrySet())playerCounters.add(Map.of("kind",entry.getElement().toString(),"label",entry.getElement().getCounterOnCardDisplayName(),"count",entry.getCount()));
                pv.put("counters",playerCounters);pv.put("speed",p.getSpeed());
                pv.put("ringTemptedCount",p.getNumRingTemptedYou());pv.put("ringBearerId",p.getRingBearer()==null?0:p.getRingBearer().getId());
                pv.put("completedDungeons",p.getCompletedDungeons().stream().map(Card::getName).toList());
                pv.put("handCount",p.getCardsIn(ZoneType.Hand).size()); pv.put("libraryCount",p.getCardsIn(ZoneType.Library).size());
                pv.put("redMana",p.getManaPool().getAmountOfColor(forge.card.MagicColor.RED));
                int[] mana = new int[6]; byte[] manaColors = forge.card.mana.ManaAtom.MANATYPES;
                for(int m=0;m<mana.length;m++) mana[m]=p.getManaPool().getAmountOfColor(manaColors[m]);
                pv.put("manaPool", mana);
                List<Object> hand=new ArrayList<>();
                if(s==i) for(Card c:p.getCardsIn(ZoneType.Hand)) hand.add(card(c));
                pv.put("hand",hand);
                List<Object> field=new ArrayList<>(); for(Card c:p.getCardsIn(ZoneType.Battlefield)) {
                    Map<String,Object> visible=(Map<String,Object>)card(c);
                    if(c.isFaceDown() && c.getView().canFaceDownBeShownTo(viewer.getView())) {
                        var original=c.getState(forge.card.CardStateName.Original);
                        visible.put("knownFaceName",original.getName());
                        visible.put("knownFaceRules",original.getOracleText());
                        if(c.getPaperCard() instanceof forge.item.PaperCard)visible.put("knownFacePrintingKey",ForgeCatalog.key((forge.item.PaperCard)c.getPaperCard()));
                    }
                    field.add(visible);
                }
                pv.put("battlefield",field);
                List<Object> emblems=new ArrayList<>();
                for(Card c:p.getCardsIn(ZoneType.Command)) if(c.isEmblem())emblems.add(card(c));
                pv.put("emblems",emblems);
                List<Object> effects=new ArrayList<>();
                for(Card c:p.getCardsIn(ZoneType.Command)) if(!c.isEmblem() && (c.isImmutable() || c.getType().isDungeon())) {
                    Map<String,Object> marker=(Map<String,Object>)card(c);
                    boolean dungeon=c.getType().isDungeon();
                    marker.put("effectMarker",true);marker.put("markerKind",dungeon?"dungeon":"effect");
                    marker.put("currentType",dungeon?"Dungeon":"Effect");marker.put("currentRules",currentLandRules(c));
                    if(dungeon)marker.put("markerStatus",c.getCurrentRoom()==null?"":"当前房间："+c.getCurrentRoom());
                    else if(c==p.getTheRing())marker.put("markerStatus","魔戒已引诱你 "+p.getNumRingTemptedYou()+" 次");
                    else if(c.getOverlayText()!=null)marker.put("markerStatus",c.getOverlayText());
                    Card source=c.getEffectSource();
                    if(dungeon || source==null)marker.put("helperArtName",c.getName());
                    if(publicMarkerSource(source) && source.getPaperCard() instanceof forge.item.PaperCard)marker.put("printingKey",ForgeCatalog.key((forge.item.PaperCard)source.getPaperCard()));
                    else if(source!=null) {
                        // The effect exists publicly, but its hidden source must not reveal a card face.
                        marker.clear();marker.put("id",c.getId());marker.put("ownerSeat",i+1);marker.put("zone","Command");
                        marker.put("effectMarker",true);marker.put("markerKind","effect");marker.put("currentType","Effect");
                        marker.put("name","持续效果");
                        marker.put("currentRules","来源牌为非公开信息。");
                    }
                    effects.add(marker);
                }
                for(var trigger:markerTriggers) {
                    var spawning=trigger.getSpawningAbility();Card source=trigger.getHostCard();
                    Player markerController=spawning!=null && spawning.getActivatingPlayer()!=null?spawning.getActivatingPlayer():source==null?null:source.getController();
                    if(markerController!=p || !publicMarkerSource(source))continue;
                    Map<String,Object> marker=new LinkedHashMap<>();
                    marker.put("id",-1000000-trigger.getId());marker.put("name",source.getName());marker.put("ownerSeat",i+1);
                    marker.put("effectMarker",true);marker.put("markerKind","delayed");marker.put("zone","Command");
                    marker.put("currentType","Delayed trigger");marker.put("currentRules",trigger.toString());
                    if(source.getPaperCard() instanceof forge.item.PaperCard)marker.put("printingKey",ForgeCatalog.key((forge.item.PaperCard)source.getPaperCard()));
                    effects.add(marker);
                }
                pv.put("effectMarkers",effects);
                List<String> grave=new ArrayList<>(); for(Card c:p.getCardsIn(ZoneType.Graveyard)) grave.add(c.isFaceDown() ? "Face-down card" : c.getName());
                pv.put("graveyard",grave);
                List<Object> graveyardCards=new ArrayList<>(),exileCards=new ArrayList<>();
                for(Card c:p.getCardsIn(ZoneType.Graveyard)) graveyardCards.add(zoneCard(c,viewer));
                for(Card c:p.getCardsIn(ZoneType.Exile)) exileCards.add(zoneCard(c,viewer));
                pv.put("graveyardCards",graveyardCards);pv.put("exileCards",exileCards);
                pv.put("exileCount",p.getCardsIn(ZoneType.Exile).size());
                if(game.isGameOver()) {
                    List<Object> library=new ArrayList<>();
                    if(s==i) for(Card c:p.getCardsIn(ZoneType.Library)) library.add(card(c));
                    pv.put("reviewLibrary",library); pv.put("reviewGraveyard",graveyardCards); pv.put("reviewExile",exileCards);
                }
                players.add(pv);
            }
            view.put("players",players); views.add(view);
        }
        return views;
    }
    static boolean publicMarkerSource(Card source) {
        return source!=null && !source.isFaceDown() && source.getZone()!=null &&
            !List.of(ZoneType.Hand,ZoneType.Library,ZoneType.Sideboard).contains(source.getZone().getZoneType());
    }
    // Forge's pinned TriggerHandler exposes registration but no public snapshot accessor.
    static List<forge.game.trigger.Trigger> pendingMarkerTriggers() {
        try {
            var handler=game.getTriggerHandler();
            Field delayed=handler.getClass().getDeclaredField("delayedTriggers");delayed.setAccessible(true);
            Field perPlayer=handler.getClass().getDeclaredField("playerDefinedDelayedTriggers");perPlayer.setAccessible(true);
            Set<forge.game.trigger.Trigger> all=new LinkedHashSet<>((List<forge.game.trigger.Trigger>)delayed.get(handler));
            all.addAll(((com.google.common.collect.Multimap<Player,forge.game.trigger.Trigger>)perPlayer.get(handler)).values());
            // NextTurn/UpcomingTurn triggers are captured by phase commands before registration.
            Set<Object> visited=Collections.newSetFromMap(new IdentityHashMap<>());
            for(var phase:List.of(game.getUpkeep(),game.getEndOfCombat(),game.getEndOfTurn(),game.getCleanup())) {
                for(Field field:forge.game.phase.Phase.class.getDeclaredFields()) {
                    if(Modifier.isStatic(field.getModifiers()))continue;
                    field.setAccessible(true);collectMarkerTriggers(field.get(phase),all,visited);
                }
            }
            for(String name:List.of("extraTurns","extraPhases")) {
                Field field=game.getPhaseHandler().getClass().getDeclaredField(name);field.setAccessible(true);
                collectMarkerTriggers(field.get(game.getPhaseHandler()),all,visited);
            }
            return new ArrayList<>(all);
        } catch(ReflectiveOperationException ex) {throw new IllegalStateException("Pending effect marker snapshot unavailable",ex);}
    }
    static void collectMarkerTriggers(Object value,Set<forge.game.trigger.Trigger> result,Set<Object> visited)throws IllegalAccessException {
        if(value==null || !visited.add(value))return;
        if(value instanceof forge.game.trigger.Trigger trigger)result.add(trigger);
        else if(value instanceof forge.game.phase.ExtraTurn turn)collectMarkerTriggers(turn.getDelayedTriggers(),result,visited);
        else if(value instanceof forge.game.phase.ExtraPhase phase)collectMarkerTriggers(phase.getDelayedTriggers(),result,visited);
        else if(value instanceof com.google.common.collect.Multimap<?,?> map)collectMarkerTriggers(map.values(),result,visited);
        else if(value instanceof Map<?,?> map)collectMarkerTriggers(map.values(),result,visited);
        else if(value instanceof Iterable<?> list)for(Object item:list)collectMarkerTriggers(item,result,visited);
        else if(value instanceof forge.GameCommand) {
            // Inspect captured values only; never execute commands or walk cards/game state.
            for(Class<?> type=value.getClass();type!=null && type!=Object.class;type=type.getSuperclass())
                for(Field field:type.getDeclaredFields()) {
                    if(Modifier.isStatic(field.getModifiers()))continue;
                    field.setAccessible(true);collectMarkerTriggers(field.get(value),result,visited);
                }
        }
    }
    static Object zoneCard(Card c,Player viewer) {
        Map<String,Object> visible=(Map<String,Object>)card(c);
        if(c.isFaceDown() && c.getView().canFaceDownBeShownTo(viewer.getView())) {
            var original=c.getState(forge.card.CardStateName.Original);
            visible.put("knownFaceName",original.getName());visible.put("knownFaceRules",original.getOracleText());
            if(c.getPaperCard() instanceof forge.item.PaperCard)visible.put("knownFacePrintingKey",ForgeCatalog.key((forge.item.PaperCard)c.getPaperCard()));
        }
        return visible;
    }
    static String currentLandRules(Card c) {
        String text=c.getAbilityText().replaceAll("</?span[^>]*>", "").trim();
        // Intrinsic mana abilities from basic land subtypes are not in CardState's
        // printed ability text. Use the effective engine list, including removals.
        for(var ability:c.getManaAbilities()) {
            String line=ability.toString().trim();
            if(!line.isEmpty() && !text.contains(line))text+=(text.isEmpty()?"":"\n")+line;
        }
        return text;
    }

    static Object card(Card c) {
        Map<String,Object> result = new LinkedHashMap<>();
        result.put("id", c.getId()); result.put("name", c.isFaceDown() ? "Face-down card" : c.getName());
        result.put("ownerSeat",guis.stream().filter(g->g.controller.getPlayer()==c.getOwner()).map(g->g.seat).findFirst().orElse(0));
        result.put("zone",c.getZone()==null?"":c.getZone().getZoneType().name());
        result.put("tapped", c.isTapped()); result.put("faceDown", c.isFaceDown());
        result.put("emblem",c.isEmblem());
        if(c.isFaceDown()) {
            result.put("faceDownKind",c.isCloaked()?"cloak":c.isManifested()?"manifest":c.getCastSA()!=null && c.getCastSA().isCastFaceDown()?"cast":"other");
            result.put("currentType",c.getType().toString());
            result.put("currentRules",currentLandRules(c));
            result.put("keywords",c.getKeywords().stream().map(k->k.getKeyword().name()).distinct().sorted().toList());
        }
        result.put("creature", c.isCreature()); result.put("land", c.isLand());
        result.put("backFace", !c.isFaceDown() && c.isBackSide());
        result.put("stateName",c.isFaceDown()?"FaceDown":c.getCurrentStateName().name());
        if(!c.isFaceDown()) {
            boolean liveLand=c.isInZone(ZoneType.Battlefield) && c.isLand();
            result.put("originalType",c.getCurrentState().getType().toString());
            result.put("originalRules",c.getCurrentState().getOracleText());
            result.put("currentType",c.getType().toString());
            result.put("currentRules",liveLand ? currentLandRules(c) : c.getCurrentState().getOracleText());
            result.put("hasCurrentLandState",liveLand);
            boolean support=c.isInZone(ZoneType.Battlefield) && !c.isCreature() && !c.isPlaneswalker() && (c.isArtifact() || c.isEnchantment());
            result.put("stackableSupport",support && !c.isRoom() && !c.isCloned() && !c.isAttachedToEntity()
                && c.getAttachedCards().isEmpty() && !c.hasRemembered() && !c.hasImprintedCard() && !c.hasExiledCard()
                && !c.hasChosenType() && !c.hasChosenNumber() && !c.hasChosenPlayer()
                && !c.hasChosenColor() && c.getChangedCardTraits().isEmpty() && c.getChangedCardKeywords().isEmpty()
                && c.getChangedTextColorWords().isEmpty() && c.getChangedTextTypeWords().isEmpty()
                && c.getAbilityActivatedThisTurn().isEmpty() && c.getAbilityActivatedThisGame().isEmpty());
            if(c.isInZone(ZoneType.Battlefield)) for(var ability:c.getStaticAbilities()) {
                String variable=ability.getParam("CheckSVar");
                String compare=ability.getParam("SVarCompare");
                if(variable!=null && compare!=null && compare.matches("LT[0-9]+") && c.getSVar(variable).startsWith("Count$Devotion")) {
                    result.put("devotion",forge.game.ability.AbilityUtils.calculateAmount(c,variable,ability));
                    result.put("devotionRequired",Integer.parseInt(compare.substring(2)));
                    break;
                }
            }
            if(liveLand) {
                result.put("landTypesChanged",!c.getType().toString().equals(c.getCurrentState().getType().toString()));
                result.put("basicLandTypes",java.util.stream.Stream.of("Plains","Island","Swamp","Mountain","Forest")
                    .filter(t->c.getType().hasSubtype(t)).toList());
            }
            result.put("manaCost",c.getCurrentState().getManaCost().toString());
            // The prepared spell is a separate engine object; never turn its source creature into that spell.
            if(c.isInZone(ZoneType.Battlefield)&&c.getPreparedSpell()!=null)result.put("preparedSpell",card(c.getPreparedSpell()));
        }
        if(!c.isFaceDown() && c.isRoom()) {
            result.put("room",true);
            result.put("leftRoom",c.getState(forge.card.CardStateName.LeftSplit).getName());
            result.put("rightRoom",c.getState(forge.card.CardStateName.RightSplit).getName());
            result.put("leftRoomUnlocked",c.getUnlockedRooms().contains(forge.card.CardStateName.LeftSplit));
            result.put("rightRoomUnlocked",c.getUnlockedRooms().contains(forge.card.CardStateName.RightSplit));
        }
        result.put("power", c.getNetPower()); result.put("toughness", c.getNetToughness()); result.put("damage", c.getDamage());
        int basePower=c.getBasePower(),baseToughness=c.getBaseToughness();
        // Characteristic-defining values (such as */*) are the natural baseline,
        // while effects that set base P/T and counters remain visible changes.
        for(var pt:c.getSetPTCharacterDefiningTable().values()) {
            if(pt.getLeft()!=null)basePower=pt.getLeft();
            if(pt.getRight()!=null)baseToughness=pt.getRight();
        }
        result.put("hasStatBaseline",true);result.put("basePower",basePower);result.put("baseToughness",baseToughness);
        java.util.Set<String> intrinsic=new java.util.HashSet<>();
        for(var keyword:c.getCurrentState().getIntrinsicKeywords())intrinsic.add(keyword.getKeyword().name());
        // Ward granted by the face-down procedure is inherent to cloak/disguise.
        if(c.isFaceDown() && (c.isCloaked() || c.getCastSA()!=null && "Ward:2".equals(c.getCastSA().getParam("FaceDownKeyword"))))intrinsic.add("WARD");
        result.put("grantedKeywords",c.getKeywords().stream().map(k->k.getKeyword().name()).distinct().filter(k->!intrinsic.contains(k)).sorted().toList());
        List<Object> counters=new ArrayList<>();
        for(var entry:c.getCounters().entrySet())counters.add(Map.of("kind",entry.getElement().toString(),"label",entry.getElement().getCounterOnCardDisplayName(),"count",entry.getCount()));
        result.put("counters",counters);
        if(!c.isFaceDown())result.put("keywords",c.getKeywords().stream().map(k->k.getKeyword().name()).distinct().sorted().toList());
        if (!c.isFaceDown() && c.getPaperCard() instanceof forge.item.PaperCard)
            result.put("printingKey", ForgeCatalog.key((forge.item.PaperCard)c.getPaperCard()));
        if (!c.isFaceDown() && c.isToken()) {
            result.put("token",true);
            var spawning=c.getTokenSpawningAbility();
            if(spawning!=null && spawning.getHostCard().getPaperCard() instanceof forge.item.PaperCard)
                result.put("tokenSourcePrintingKey",ForgeCatalog.key((forge.item.PaperCard)spawning.getHostCard().getPaperCard()));
        }
        if (!c.isFaceDown()) {
            result.put("copy",c.isCloned() || c.getCopiedPermanent()!=null);
            if(c.getPaperCard() instanceof forge.item.PaperToken) {
                var token=(forge.item.PaperToken)c.getPaperCard();
                if(token.getCollectorNumber()!=null && !token.getCollectorNumber().isEmpty())
                    result.put("tokenArtKey","T"+token.getEdition()+"/"+token.getCollectorNumber());
            }
        }
        result.put("attacking", game.getCombat() != null && game.getCombat().isAttacking(c));
        result.put("blocking", game.getCombat() != null && game.getCombat().isBlocking(c));
        List<Integer> blocked=new ArrayList<>();
        if(game.getCombat()!=null)for(Card attacker:game.getCombat().getAttackers())if(game.getCombat().isBlocking(c,attacker))blocked.add(attacker.getId());
        result.put("blockedAttackerIds",blocked);
        GameEntity defender=game.getCombat()==null ? null : game.getCombat().getDefenderByAttacker(c);
        if(defender!=null) {
            result.put("defenderId",defender.getId());
            result.put("defenderKind",defender instanceof Player ? "player" : "card");
        }
        result.put("planeswalker",c.isPlaneswalker()); result.put("battle",c.isBattle());
        result.put("loyalty",c.getCounters(forge.game.card.CounterEnumType.LOYALTY));
        if(c.isEmblem()) {
            String[] imageParts=c.getImageKey().split("\\|");
            if(imageParts.length==3)result.put("tokenArtKey","T"+imageParts[1]+"/"+imageParts[2]);
            result.put("currentRules",currentLandRules(c));
            result.put("currentType","Emblem");
            Card source=c.getEffectSource();
            if(source!=null && source.getPaperCard() instanceof forge.item.PaperCard)
                result.put("tokenSourcePrintingKey",ForgeCatalog.key((forge.item.PaperCard)source.getPaperCard()));
        }
        result.put("defense",c.getCounters(forge.game.card.CounterEnumType.DEFENSE));
        return result;
    }
    static Object selectedCard(Card c,String selection) {
        Map<String,Object> result=(Map<String,Object>)card(c);result.put("selection",selection);return result;
    }
    static final forge.util.ITriggerEvent RECALL_ATTACKER=new forge.util.ITriggerEvent() {
        public int getButton(){return 3;} public int getX(){return 0;} public int getY(){return 0;}
    };
    static void addAttackOptions(Gui gui,InputAttack input,List<Option> options) {
        var combat=game.getCombat();
        for(Card attacker:gui.controller.getPlayer().getCreaturesInPlay()) {
            if(combat.isAttacking(attacker)) {
                Option remove=new Option("attack-remove:"+attacker.getId(),"Recall "+attacker.getName(),
                    () -> input.selectCard(attacker,null,RECALL_ATTACKER));
                remove.attackerId=attacker.getId();options.add(remove);
            }
            for(GameEntity defender:combat.getDefenders()) {
                if(!forge.game.combat.CombatUtil.canAttack(attacker,defender))continue;
                String kind=defender instanceof Player ? "player" : "card";
                Option option=new Option("attack:"+attacker.getId()+":"+kind+":"+defender.getId(),
                    attacker.getName()+" → "+defender.getName(),() -> {
                        // One validated wire response drives the existing human input sequence.
                        // Right-click recall clears any active band before reassigning this creature.
                        if(combat.isAttacking(attacker,defender)){gui.inputRevision++;return;}
                        if(combat.isAttacking(attacker))input.selectCard(attacker,null,RECALL_ATTACKER);
                        if(defender instanceof Player player)input.selectPlayer(player,null);
                        else input.selectCard((Card)defender,null,null);
                        input.selectCard(attacker,null,null);
                    });
                option.attackerId=attacker.getId();option.defenderId=defender.getId();option.defenderKind=kind;
                options.add(option);
            }
        }
    }
    static void addBlockOptions(Gui gui,InputBlock input,List<Option> options) {
        var combat=game.getCombat();
        for(Card blocker:gui.controller.getPlayer().getCreaturesInPlay()) {
            if(combat.isBlocking(blocker))options.add(new Option("block-remove:"+blocker.getId(),"取消阻挡",()->input.selectCard(blocker,null,RECALL_ATTACKER)));
            for(Card attacker:combat.getAttackers()) {
                if(!combat.isBlocking(blocker,attacker) && !forge.game.combat.CombatUtil.canBlock(attacker,blocker,combat))continue;
                Option option=new Option("block:"+blocker.getId()+":"+attacker.getId(),blocker.getName()+" → "+attacker.getName(),()->{
                    input.selectCard(attacker,null,null);input.selectCard(blocker,null,null);
                });
                option.attackerId=attacker.getId();option.defenderId=blocker.getId();option.selected=combat.isBlocking(blocker,attacker);options.add(option);
            }
        }
    }
    static void receive(JsonObject o) {
        String type=o.get("type").getAsString();
        if(type.equals("shutdown")) { send(envelope("bye")); System.exit(0); }
        if(type.equals("concede")) {
            int seat=o.get("seat").getAsInt();
            if(!SESSION.equals(o.get("session").getAsString()) || seat<1 || seat>2) throw new IllegalArgumentException("Invalid concede");
            // Forge concession is valid even while its rules worker awaits a human choice.
            guis.get(seat-1).controller.concede();
            finish(); return;
        }
        Pending p; Option option=null; String error=null;
        synchronized(ForgeBridge.class) {
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
                Map<String,Object> m=envelope("rejected"); m.put("error",error); m.put("requestId",o.get("requestId")); send(m);
                if(error.equals("stale-request") && p!=null && !p.consumed) publish(p);
                return;
            }
            p.consumed=true;
            Gui responding=guis.get(p.seat-1);
            if(option.id.startsWith("card:") && (p.kind.equals("InputPassPriority") || p.kind.contains("PayMana")))
                responding.interactionSourceCardId=Integer.parseInt(option.id.substring(5));
            else if(p.kind.equals("InputPassPriority"))responding.interactionSourceCardId=0;
        }
        Map<String,Object> ack=envelope("accepted"); ack.put("requestId",p.requestId); send(ack);
        if(p.modal || p.kind.equals("dialog")) option.action.run(); else guiTask(option.action);
    }
    static void loadSection(forge.deck.CardPool pool,JsonArray entries) {
        if(entries==null) return;
        for(JsonElement e:entries) {
            JsonObject entry=e.getAsJsonObject();
            String key=entry.get("setCode").getAsString().toUpperCase(Locale.ROOT)+"/"+entry.get("collectorNumber").getAsString();
            List<forge.item.PaperCard> found=catalog.get(key);
            if(found==null || found.size()!=1) throw new IllegalArgumentException("Missing or ambiguous printing: "+key);
            int count=entry.get("count").getAsInt();
            if(count<=0 || count>250) throw new IllegalArgumentException("Invalid deck count");
            pool.add(found.get(0),count);
        }
    }
    static boolean supportedInput(Input input) {
        return input instanceof InputPassPriority || input instanceof InputPayMana || input instanceof InputSelectTargets
            || input instanceof InputAttack || input instanceof InputBlock
            || input.getClass().getSimpleName().equals("InputMulligan")
            || input.getClass().getSimpleName().equals("InputConfirmMulligan")
            || input instanceof InputLondonMulligan
            || input instanceof InputSelectEntitiesFromList
            || input instanceof InputConfirm;
    }
    static boolean canTargetPlayer(Input input,Player player) {
        try {
            if(input instanceof InputSelectEntitiesFromList) {
                Field choices=InputSelectEntitiesFromList.class.getDeclaredField("validChoices");choices.setAccessible(true);
                for(Object candidate:(Iterable<?>)choices.get(input))if(candidate==player)return true;
                return false;
            }
            // The fixed upstream version exposes card candidates publicly, but not player candidates.
            Field ability=InputSelectTargets.class.getDeclaredField("sa"); ability.setAccessible(true);
            Field mandatory=InputSelectTargets.class.getDeclaredField("mustTargetFiltered"); mandatory.setAccessible(true);
            Field predicate=InputSelectTargets.class.getDeclaredField("filter"); predicate.setAccessible(true);
            java.util.function.Predicate<forge.game.GameObject> filter=(java.util.function.Predicate<forge.game.GameObject>)predicate.get(input);
            return !player.hasLost() && !mandatory.getBoolean(input)
                && ((forge.game.spellability.SpellAbility)ability.get(input)).canTarget(player)
                && (filter==null || filter.test(player));
        } catch(ReflectiveOperationException e) { throw new IllegalStateException("Pinned target API changed",e); }
    }
    static synchronized void finish() {
        if(finished || game==null || !game.isGameOver()) return;
        finished=true; pending=null;
        int winner=0;
        for(Gui gui:guis) if(game.getOutcome().isWinner(gui.controller.getPlayer().getRegisteredPlayer())) winner=gui.seat;
        Map<String,Object> message=envelope("ended"); message.put("winnerSeat",winner); message.put("views",views()); send(message);
    }
    public static void main(String[] args) throws Exception {
        run(args,null);
    }
    // Package-level entry for the isolated engine acceptance harness. Normal startup has no hook.
    static void run(String[] args,Runnable startGameHook) throws Exception {
        System.setErr(new PrintStream(new FileOutputStream(FileDescriptor.err),true,StandardCharsets.UTF_8));
        System.setOut(new PrintStream(System.err,true,StandardCharsets.UTF_8));
        Thread.setDefaultUncaughtExceptionHandler((t,e) -> fatal(e));
        config=JsonParser.parseString(java.nio.file.Files.readString(java.nio.file.Path.of(args[1]))).getAsJsonObject();
        SESSION=config.get("session").getAsString();
        String assets=new File(args[0]).getCanonicalPath().replace('\\','/')+"/";
        GuiBase.setInterface((IGuiBase)Proxy.newProxyInstance(IGuiBase.class.getClassLoader(),new Class<?>[]{IGuiBase.class},(p,m,a)->base(p,m,a,assets)));
        Thread input=new Thread(() -> {
            try(BufferedReader reader=new BufferedReader(new InputStreamReader(System.in,StandardCharsets.UTF_8))) {
                String line; while((line=reader.readLine())!=null) receive(JsonParser.parseString(line).getAsJsonObject());
                System.exit(0);
            } catch(Throwable e) { fatal(e); }
        },"mtgb-wire"); input.setDaemon(true); input.start();
        FModel.initialize(null,prefs -> {
            prefs.setPref(FPref.UI_LANGUAGE,"en-US"); prefs.setPref(FPref.PLAYER_NAME,"Proof");
            prefs.setPref(FPref.DECKGEN_CARDBASED,false); prefs.setPref(FPref.LOAD_CARD_SCRIPTS_LAZILY,false);
            prefs.setPref(FPref.YIELD_AUTO_PASS_NO_ACTIONS,false);
            prefs.setPref(FPref.UI_SHOW_ACTIONABLE_HIGHLIGHTS,true); return null;
        });
        MyRandom.setRandom(new Random(config.get("seed").getAsInt()));
        catalog=ForgeCatalog.printings();
        List<RegisteredPlayer> players=new ArrayList<>();
        JsonArray decks=config.getAsJsonArray("decks");
        for(int seat=1;seat<=2;seat++) {
            JsonObject source=decks.get(seat-1).getAsJsonObject();
            Deck deck=new Deck("MTGB Player "+seat);
            loadSection(deck.getMain(),source.getAsJsonArray("cards"));
            loadSection(deck.getOrCreate(forge.deck.DeckSection.Sideboard),source.getAsJsonArray("sideboard"));
            RegisteredPlayer registered=new RegisteredPlayer(deck).setPlayer(new BridgePlayer("Player "+seat));
            registered.setStartingLife(config.get("startingLife").getAsInt());
            players.add(registered);
        }
        GameRules rules=new GameRules(GameType.Constructed); rules.setGamesPerMatch(1);
        Match match=new Match(rules,players,"Unity bridge proof"); game=match.createGame();
        for(int i=0;i<2;i++) {
            Gui handler=new Gui(i+1); handler.controller=(PlayerControllerHuman)game.getPlayers().get(i).getController();
            handler.controller.setGui((IGuiGame)Proxy.newProxyInstance(IGuiGame.class.getClassLoader(),new Class<?>[]{IGuiGame.class},handler));
            guis.add(handler);
        }
        game.getAction().invoke(() -> {
            // Forge's own worker exception handler can itself fail in headless mode.
            // Report failures before they escape it so clients never wait indefinitely.
            try { match.startGame(game,startGameHook); finish(); } catch(Throwable e) { fatal(e); }
        });
        new CountDownLatch(1).await(); // Forge game workers are daemon threads.
    }
}
