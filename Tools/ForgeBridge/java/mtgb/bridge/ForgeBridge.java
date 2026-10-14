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
import forge.player.HumanCostDecision.InputSelectCardToRemoveCounter;
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
    static final List<Gui> controlGuis = new CopyOnWriteArrayList<>();
    static final BridgeLibraryKnowledge libraryKnowledge=new BridgeLibraryKnowledge();
    static volatile Pending pending;
    static long revision;
    static int restartRevision;
    static final BridgeMatchRemovals matchRemovals = new BridgeMatchRemovals();
    static final BridgeSeriesOwnership seriesOwnership = new BridgeSeriesOwnership();
    static final java.util.concurrent.atomic.AtomicLong noticeSequence=new java.util.concurrent.atomic.AtomicLong();
    static final long started = System.nanoTime();
    static volatile boolean finished;
    static final Set<CompletableFuture<?>> modalAnswers=new HashSet<>();
    private static final class GameEnded extends RuntimeException {}
    // Modal GUI callbacks do not use Forge's InputQueue.onGameOver latch.
    // Register their waits atomically with finish, so conceding can unwind the
    // rules worker without choosing a fabricated result or emitting a fatal.
    static <T> T awaitAnswer(CompletableFuture<T> answer) throws InterruptedException,ExecutionException {
        synchronized(ForgeBridge.class) {
            if(finished || game!=null && game.isGameOver())throw new GameEnded();
            modalAnswers.add(answer);
        }
        try {return answer.get();}
        finally {synchronized(ForgeBridge.class){modalAnswers.remove(answer);}}
    }
    static boolean terminalCancellation(Throwable error) {
        for(Throwable cause=error;cause!=null;cause=cause.getCause())
            if(cause instanceof GameEnded)return true;
        return false;
    }
    // A pre-cast AI estimate cannot see keywords granted only to wasCast objects.
    // Offer the native casting proposal; Forge still validates the actual intent.
    // requiresPaymentCheck keeps this separate from a positive affordability proof.
    static boolean hasNativeCastingPaymentChoice(Card card,Player payer) {
        // A perpetual ability active only on the stack cannot inform the AI's
        // hand-time estimate. Offer an unpaid native intent, not an affordability
        // guarantee; the actual stack ability and payment still decide legality.
        for(var ability:card.getStaticAbilities())
            if(ability.checkMode(forge.game.staticability.StaticAbilityMode.ManaConvert)
                && ability.hasParam("EffectZone")
                && ZoneType.listValueOf(ability.getParam("EffectZone")).contains(ZoneType.Stack)
                && ability.matchesValidParam("ValidPlayer",payer)
                && ability.matchesValidParam("ValidCard",card)
                && card.getAllPossibleAbilities(payer,true).stream()
                    .anyMatch(a->a.isSpell() && ability.matchesValidParam("ValidSA",a)))return true;
        if(card.hasKeyword(forge.game.keyword.Keyword.OFFERING)
            ||card.hasKeyword(forge.game.keyword.Keyword.EMERGE)||card.hasKeyword(forge.game.keyword.Keyword.ASSIST))return true;
        if(card.isArtifact())return false;
        for(Card source:game.getCardsIn(ZoneType.Battlefield))if(source.getController()==payer)
            for(var ability:source.getStaticAbilities())if(ability.hasParam("AddKeyword")
                && ability.getParam("AddKeyword").contains("Improvise"))return true;
        return false;
    }
    static JsonObject config;
    static Map<String,List<forge.item.PaperCard>> catalog;
    static final class BridgePlayer extends LobbyPlayerHuman {
        BridgePlayer(String name) { super(name); }
        @Override public PlayerController createMindSlaveController(Player master, Player slave) {
            BridgeController controller=new BridgeController(slave,this,(PlayerControllerHuman)master.getController());
            Gui handler=new Gui(guiFor((PlayerControllerHuman)master.getController()).seat);
            handler.controller=controller;
            controller.setGui((IGuiGame)Proxy.newProxyInstance(IGuiGame.class.getClassLoader(),new Class<?>[]{IGuiGame.class},handler));
            controlGuis.add(handler);
            return controller;
        }
        @Override public Player createIngamePlayer(Game game, int id) {
            Player player = new Player(getName(), game, id);
            player.setFirstController(new BridgeController(game,player,this));
            return player;
        }
    }
    // Both ordinary and controlled players use the same production overrides.
    // Forge's controlled-player constructor shares the master's input queue/proxy.
    static final class BridgeController extends PlayerControllerHuman {
        BridgeController(Game game,Player player,LobbyPlayerHuman lobby){super(game,player,lobby);}
        BridgeController(Player player,LobbyPlayerHuman lobby,PlayerControllerHuman master){super(player,lobby,master);}
        static final class PaymentIntent {
            final forge.game.spellability.SpellAbility ability;
            final boolean cancelAllowed;
            boolean cancelled;
            PaymentIntent(forge.game.spellability.SpellAbility ability,boolean effect) {
                this.ability=ability;this.cancelAllowed=!effect&&!ability.getPayCosts().isMandatory();
            }
        }
        private final Deque<PaymentIntent> paymentIntents=new ArrayDeque<>();
        @Override public CardCollectionView chooseCardsToDelve(int generic,CardCollection grave) {
            Gui gui=guiFor(this);var old=gui.manualCostAbility;
            var intent=paymentIntents.peek();gui.manualCostAbility=intent==null?null:intent.ability;
            if(gui.manualCostAbility!=null&&!grave.isEmpty()&&generic>0)manualTapPayments.add(gui.manualCostAbility);
            try{return super.chooseCardsToDelve(generic,grave);}finally{gui.manualCostAbility=old;}
        }
        @Override public CardCollectionView choosePermanentsToSacrifice(forge.game.spellability.SpellAbility sa,int min,int max,CardCollectionView cards,String message) {
            Gui gui=guiFor(this);var old=gui.manualCostAbility;
            if(sa!=null&&(sa.isOffering()||sa.isEmerge())){gui.manualCostAbility=sa;manualTapPayments.add(sa);}
            try{return super.choosePermanentsToSacrifice(sa,min,max,cards,message);}finally{gui.manualCostAbility=old;}
        }
        @Override public Player choosePlayerToAssistPayment(forge.util.collect.FCollectionView<Player> players,forge.game.spellability.SpellAbility sa,String title,int max) {
            Gui gui=guiFor(this);var old=gui.manualCostAbility;gui.manualCostAbility=sa;manualTapPayments.add(sa);
            try{return super.choosePlayerToAssistPayment(players,sa,title,max);}finally{gui.manualCostAbility=old;}
        }
        @Override public boolean helpPayForAssistSpell(forge.game.mana.ManaCostBeingPaid cost,forge.game.spellability.SpellAbility sa,int max,int requested) {
            Gui gui=guiFor(this);var old=gui.manualCostAbility;gui.manualCostAbility=sa;
            try{return super.helpPayForAssistSpell(cost,sa,max,requested);}finally{gui.manualCostAbility=old;}
        }
        @Override public boolean confirmPayment(forge.game.cost.CostPart cost,String question,
            forge.game.spellability.SpellAbility ability) {
            Gui gui=(Gui)Proxy.getInvocationHandler(getGui());
            var previous=gui.paymentConfirmation;
            // Forage's false answer chooses graveyard payment; it is not abort.
            gui.paymentConfirmation=cost instanceof forge.game.cost.CostForage?null:ability;
            try{return super.confirmPayment(cost,question,ability);}
            finally{gui.paymentConfirmation=previous;}
        }
        @Override public boolean payManaCost(forge.card.mana.ManaCost mana,forge.game.cost.CostPartMana part,
            forge.game.spellability.SpellAbility ability,String prompt,forge.game.mana.ManaConversionMatrix matrix,boolean effect) {
            PaymentIntent intent=new PaymentIntent(ability,effect);paymentIntents.push(intent);manualTapPayments.remove(ability);
            try {return super.payManaCost(mana,part,ability,prompt,matrix,effect)&&!intent.cancelled;}
            finally {paymentIntents.pop();manualTapPayments.remove(ability);}
        }
        @Override public boolean applyManaToCost(forge.game.mana.ManaCostBeingPaid mana,
            forge.game.spellability.SpellAbility ability,String prompt,forge.game.mana.ManaConversionMatrix matrix,boolean effect) {
            PaymentIntent intent=paymentIntents.peek();
            // Stop the actual unpaid native transaction. Never interpret cancel
            // as zero tap choices followed by ordinary automatic mana payment.
            if(intent!=null&&intent.ability==ability&&intent.cancelled)return false;
            return super.applyManaToCost(mana,ability,prompt,matrix,effect);
        }
        @Override public Map<Card,forge.card.mana.ManaCostShard> chooseCardsForConvokeOrImprovise(
            forge.game.spellability.SpellAbility ability,forge.card.mana.ManaCost mana,
            forge.game.card.CardCollectionView untapped,boolean artifacts,boolean creatures,Integer maximum) {
            var input=new InputSelectCardsForConvokeOrImprovise(this,getPlayer(),ability,mana,untapped,artifacts,creatures,maximum);
            PaymentIntent intent=paymentIntents.peek();
            input.setCancelAllowed(intent!=null&&intent.ability==ability&&intent.cancelAllowed);
            input.showAndWait();
            if(input.hasCancelled()&&intent!=null&&intent.ability==ability)intent.cancelled=true;
            return input.getConvokeMap();
        }
        @Override public boolean chooseTargetsFor(forge.game.spellability.SpellAbility ability) {
            Gui gui=guiFor(this);var previous=gui.targetingAbility;gui.targetingAbility=ability;
            try{return super.chooseTargetsFor(ability);}finally{gui.targetingAbility=previous;}
        }
        @Override public forge.game.spellability.TargetChoices chooseNewTargetsFor(forge.game.spellability.SpellAbility ability,java.util.function.Predicate<forge.game.GameObject> filter,boolean optional) {
            Gui gui=guiFor(this);var previous=gui.targetingAbility;gui.targetingAbility=ability;
            try{return super.chooseNewTargetsFor(ability,filter,optional);}finally{gui.targetingAbility=previous;}
        }
        @Override public String chooseCardName(forge.game.spellability.SpellAbility ability,
                java.util.function.Predicate<forge.card.ICardFace> predicate,String valid,String message) {
            forge.StaticData.instance().ensureAllCardsLoaded();
            List<forge.card.ICardFace> auxiliary=forge.StaticData.instance().getVariantCards().streamAllFaces()
                .filter(f->f.getType().hasSubtype("Attraction")||f.getType().hasSubtype("Contraption"))
                .filter(predicate).toList();
            if(auxiliary.isEmpty())return super.chooseCardName(ability,predicate,valid,message);
            List<forge.card.ICardFace> faces=java.util.stream.Stream.concat(
                forge.StaticData.instance().getCommonCards().streamAllFaces().filter(predicate),auxiliary.stream())
                .distinct().toList();
            while(true) {
                // Use native face identity mapping, ordering, chooser and final validity check.
                var face=super.chooseSingleCardFace(ability,faces,message);
                if(face==null)return "";
                var paper=forge.StaticData.instance().getCommonCards().getCard(face.getName());
                if(paper==null)paper=forge.StaticData.instance().getVariantCards().getCard(face.getName());
                Card candidate=Card.fromPaperCard(paper,getPlayer());
                forge.game.card.CardUtil.turnToRightFace(face.getName(),candidate);
                if(candidate.isValid(valid.split(","),ability.getHostCard().getController(),ability.getHostCard(),ability))
                    return face.getName();
            }
        }
        @Override public forge.game.replacement.ReplacementEffect chooseSingleReplacementEffect(List<forge.game.replacement.ReplacementEffect> values) {
            Gui gui=guiFor(this);Map<Integer,forge.game.replacement.ReplacementEffect> previous=gui.replacementChoices;
            Map<Integer,forge.game.replacement.ReplacementEffect> current=new LinkedHashMap<>();
            for(var value:values)if(current.put(value.getId(),value)!=null)throw new IllegalStateException("Duplicate native replacement identity");
            gui.replacementChoices=current;
            try{return super.chooseSingleReplacementEffect(values);}
            finally{gui.replacementChoices=previous;}
        }
        @Override public boolean confirmReplacementEffect(forge.game.replacement.ReplacementEffect effect,
            forge.game.spellability.SpellAbility ability,GameEntity affected,String question) {
            Gui gui=guiFor(this);var previous=gui.optionalReplacement;GameEntity previousAffected=gui.replacementAffected;
            gui.optionalReplacement=effect;gui.replacementAffected=affected;
            try{return super.confirmReplacementEffect(effect,ability,affected,question);}
            finally{gui.optionalReplacement=previous;gui.replacementAffected=previousAffected;}
        }
        @Override public boolean payCostToPreventEffect(forge.game.cost.Cost cost,
            forge.game.spellability.SpellAbility ability,boolean alreadyPaid,forge.util.collect.FCollectionView<Player> payers) {
            Gui gui=guiFor(this);Card previous=gui.libraryCostSource;
            if(cost.getCostParts().stream().anyMatch(c->c instanceof forge.game.cost.CostPutCardToLib)
                &&cost.getCostParts().stream().allMatch(c->c instanceof forge.game.cost.CostPutCardToLib
                    ||c instanceof forge.game.cost.CostPartMana m&&m.getMana().isZero()))
                gui.libraryCostSource=ability.getHostCard();
            try {return super.payCostToPreventEffect(cost,ability,alreadyPaid,payers);}
            finally {gui.libraryCostSource=previous;}
        }
        @Override public void notifyOfValue(forge.game.spellability.SpellAbility ability,forge.game.GameObject target,String value) {
            Gui gui=guiFor(this);NoticeContext previous=gui.noticeContext.get();
            gui.noticeContext.set(new NoticeContext(ability,target,value));
            try {super.notifyOfValue(ability,target,value);}
            finally {if(previous==null)gui.noticeContext.remove();else gui.noticeContext.set(previous);}
        }
        @Override public CardCollectionView chooseCardsForEffect(CardCollectionView values,
            forge.game.spellability.SpellAbility ability,String title,int min,int max,boolean optional,Map<String,Object> params) {
            CardCollectionView chosen=super.chooseCardsForEffect(values,ability,title,min,max,optional,params);
            if(ability!=null&&ability.getApi()==forge.game.ability.ApiType.FlipOntoBattlefield)
                guiFor(this).flipLocations.put(ability,chosen.isEmpty()?null:chosen.getFirst());
            return chosen;
        }
        @Override public boolean chooseCardsPile(forge.game.spellability.SpellAbility ability,CardCollectionView first,
            CardCollectionView second,String faceDown) {
            // Defined piles chosen by the activating player are the effect's look/
            // turn-face-up step. Opposing choices must not inherit that look grant.
            // Native Human tempShowCards grants this permission and retains the
            // player's knowledge of face-down exile after the temporary dialog.
            boolean look=ability.hasParam("DefinedPiles")&&faceDown.equals("False")
                &&getPlayer()==ability.getActivatingPlayer();
            if(look){tempShowCards(first);tempShowCards(second);}
            try{return guiFor(this).choosePile(ability,first,second,faceDown);}
            finally{if(look)endTempShowCards();}
        }
        @Override public <T extends GameEntity> T chooseSingleEntityForEffect(forge.util.collect.FCollectionView<T> values,
            DelayedReveal reveal,forge.game.spellability.SpellAbility ability,String title,boolean optional,Player targeted,Map<String,Object> params) {
            Gui gui=guiFor(this);Map<Integer,Card> previous=new HashMap<>(gui.choiceObjects);
            var previousSource=gui.sourceChoiceAbility;
            if(ability!=null&&ability.getApi()==forge.game.ability.ApiType.ChooseSource){
                // Native source chooser inserts negative-ID heading cards. They
                // are grouping labels, never legal choices or missing card art.
                var actual=new forge.util.collect.FCollection<T>();
                for(T value:values)if(!(value instanceof Card c&&c.getId()<0&&c.getName().startsWith("--")))actual.add(value);
                values=actual;gui.sourceChoiceAbility=ability;
            }
            for(T value:values)if(value instanceof Card card)gui.choiceObjects.put(card.getId(),card);
            try {return super.chooseSingleEntityForEffect(values,reveal,ability,title,optional,targeted,params);}
            finally {gui.choiceObjects.clear();gui.choiceObjects.putAll(previous);gui.sourceChoiceAbility=previousSource;}
        }
        @Override public CardCollectionView orderMoveToZoneList(CardCollectionView cards,
            ZoneType destination,forge.game.spellability.SpellAbility source) {
            // These collections have no player-visible zone order. Keep Forge's
            // original batch intact; ETB choices and trigger order remain native.
            if(destination==ZoneType.Battlefield || destination==ZoneType.Exile)return cards;
            // Keep only the exact native ordering candidates, including a Meld
            // original that has just left the hidden linked-card container.
            Gui gui=guiFor(this);Map<Integer,Card> previous=new HashMap<>(gui.choiceObjects);
            for(Card card:cards)gui.choiceObjects.put(card.getId(),card);
            try{return super.orderMoveToZoneList(cards,destination,source);}
            finally{gui.choiceObjects.clear();gui.choiceObjects.putAll(previous);}
        }
        @Override public boolean confirmAction(forge.game.spellability.SpellAbility ability,
            PlayerActionConfirmMode mode,String message,List<String> options,Card cardToShow,Map<String,Object> params) {
            // A destination decision concerns the object being moved, not the
            // ability's producer. Preserve the native decider and response IDs.
            if(cardToShow==null && mode==PlayerActionConfirmMode.ChangeZoneToAltDestination && ability!=null) {
                Set<Card> affected=new LinkedHashSet<>();
                for(var current=ability;current!=null;current=current.getParent()) {
                    for(Card target:current.getTargets().getTargetCards())affected.add(target);
                    for(var target:current.getTargets().getTargetSpells())if(target.isSpell())affected.add(target.getHostCard());
                }
                if(affected.size()==1)cardToShow=affected.iterator().next();
            }
            // Generated copies can have a None zone without belonging to its
            // collection yet. Retain the exact native confirmation argument,
            // only for this GUI callback, rather than guessing a face by name.
            Gui gui=guiFor(this);Map<Integer,Card> previous=new HashMap<>(gui.choiceObjects);
            if(cardToShow!=null)gui.choiceObjects.put(cardToShow.getId(),cardToShow);
            try{return super.confirmAction(ability,mode,message,options,cardToShow,params);}
            finally{gui.choiceObjects.clear();gui.choiceObjects.putAll(previous);}
        }
        @Override public Object vote(forge.game.spellability.SpellAbility ability,String prompt,List<Object> options,
            com.google.common.collect.ListMultimap<Object,Player> votes,Player forPlayer,boolean optional) {
            Gui gui=guiFor(this);Player previous=gui.votingPlayer;var previousAbility=gui.votingAbility;
            gui.votingPlayer=forPlayer;gui.votingAbility=ability;
            try {return super.vote(ability,prompt,options,votes,forPlayer,optional);}
            finally {gui.votingPlayer=previous;gui.votingAbility=previousAbility;}
        }
        @Override public <T extends GameEntity> List<T> chooseEntitiesForEffect(forge.util.collect.FCollectionView<T> values,int min,int max,
            DelayedReveal reveal,forge.game.spellability.SpellAbility ability,String title,Player targeted,Map<String,Object> params) {
            Gui gui=guiFor(this);Map<Integer,Card> previous=new HashMap<>(gui.choiceObjects);
            for(T value:values)if(value instanceof Card card)gui.choiceObjects.put(card.getId(),card);
            try {return super.chooseEntitiesForEffect(values,min,max,reveal,ability,title,targeted,params);}
            finally {gui.choiceObjects.clear();gui.choiceObjects.putAll(previous);}
        }
                forge.game.spellability.SpellAbility preparedMana;
                byte preparedColor;
                @Override public List<forge.game.spellability.SpellAbility> chooseSpellAbilityToPlay() {
                    preparedMana=null;guiFor(this).spreeModes.clear();return super.chooseSpellAbilityToPlay();
                }
                @Override public forge.game.spellability.SpellAbility getAbilityToPlay(Card host, List<forge.game.spellability.SpellAbility> abilities, forge.util.ITriggerEvent event) {
                    if(abilities.stream().anyMatch(a->a.isTrigger()))return super.getAbilityToPlay(host,abilities,event);
                    List<forge.game.spellability.SpellAbility> affordable=new ArrayList<>();
                    if(abilities.size()==1)affordable.addAll(abilities);
                    else getPlayer().runWithController(() -> {
                        for(var ability:abilities)if(ability.getPayCosts()==null || !ability.getPayCosts().hasManaCost()
                            || forge.ai.AvailableActions.canAffordHuman(ability,getPlayer())
                            || ability.isSpell() && hasNativeCastingPaymentChoice(host,getPlayer()))affordable.add(ability);
                    },new forge.ai.PlayerControllerAi(game,getPlayer(),getPlayer().getOriginalLobbyPlayer()));
                    affordable.removeIf(a->!namePermits(a,getPlayer()));
                    if(affordable.isEmpty()){guiFor(this).inputRevision++;return null;}
                    Gui choiceGui=guiFor(this);
                    choiceGui.castingAbilities.clear();for(var a:affordable)choiceGui.castingAbilities.put(a.getId(),a);
                    forge.game.spellability.SpellAbility chosen;
                    try {chosen=super.getAbilityToPlay(host,affordable,event);}finally{choiceGui.castingAbilities.clear();}
                    if(chosen==null){guiFor(this).inputRevision++;return null;}
                    // Choose a cancellable casting intent before Forge starts announcing
                    // the spell. Its later Charm callback has no cancellation contract.
                    boolean fixedModes=chosen.getParamOrDefault("CharmNum","1").matches("[0-9]+")
                        &&chosen.getParamOrDefault("MinCharmNum","1").matches("[0-9]+")
                        &&!chosen.hasParam("CanRepeatModes")&&!chosen.hasParam("Optional")&&!chosen.hasParam("Random")
                        &&!chosen.hasParam("Chooser")&&!chosen.hasParam("Pawprint")&&!chosen.isEntwine();
                    if(chosen.isSpell()&&chosen.getApi()==forge.game.ability.ApiType.Charm
                        &&(host.hasKeyword(forge.game.keyword.Keyword.SPREE)||fixedModes)&&!choiceGui.spreeModes.containsKey(host.getId())) {
                        var possible=forge.game.ability.effects.CharmEffect.makePossibleOptions(chosen);
                        int max=forge.game.ability.AbilityUtils.calculateAmount(host,chosen.getParamOrDefault("CharmNum","1"),chosen);
                        int min=forge.game.ability.AbilityUtils.calculateAmount(host,chosen.getParamOrDefault("MinCharmNum",Integer.toString(max)),chosen);
                        var modes=choiceGui.chooseSpree(chosen,possible,min,Math.min(max,possible.size()),true);
                        if(modes==null){choiceGui.inputRevision++;return null;}
                        choiceGui.spreeModes.put(host.getId(),modes.stream().map(m->Integer.toString(chosen.getAdditionalAbilityList("Choices").indexOf(m))).toList());
                    }
                    if(preparedMana!=null && preparedMana.getHostCard()==host && chosen.isManaAbility()) {preparedMana=chosen;return chosen;}
                    preparedMana=null;
                    // Ordinary any-color mana: obtain a cancellable intent BEFORE paying costs.
                    // The engine still performs the activation and mana production normally.
                    if(chosen!=null && chosen.isManaAbility() && chosen.getManaPart()!=null
                        && chosen.getManaPart().getOrigProduced().equals("Any") && !chosen.hasParam("ManaRestriction")) {
                        Gui gui=guiFor(this); {
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
                    if((sa.isSpell()&&guiFor(this).spreeModes.containsKey(sa.getHostCard().getId()))
                        ||(sa.getHostCard().hasKeyword(forge.game.keyword.Keyword.SPREE)&&possible.stream().allMatch(m->m.hasParam("ModeCost")))) {
                        Gui gui=guiFor(this);
                        var chosen=gui.spreeModes.remove(sa.getHostCard().getId());
                        if(chosen==null)return gui.chooseSpree(sa,possible,min,max,false);
                        var result=new ArrayList<forge.game.spellability.AbilitySub>();
                        // Native candidate/bound changes during announcement must
                        // return to the native callback, not apply an earlier intent.
                        for(String id:chosen) {
                            var mode=possible.stream().filter(m->Integer.toString(sa.getAdditionalAbilityList("Choices").indexOf(m)).equals(id)).findFirst().orElse(null);
                            if(mode==null)return gui.chooseSpree(sa,possible,min,max,false);
                            result.add(mode);
                        }
                        if(result.size()<min||result.size()>max)return gui.chooseSpree(sa,possible,min,max,false);
                        return result;
                    }
                    if(!repeat)return guiFor(this).chooseSpree(sa,possible,min,max,false);
                    return super.chooseModeForAbility(sa,possible,min,max,repeat);
                }
                @Override public List<forge.game.spellability.OptionalCostValue> chooseOptionalCosts(forge.game.spellability.SpellAbility sa,List<forge.game.spellability.OptionalCostValue> costs) {
                    Gui gui=guiFor(this);var previous=gui.optionalCostAbility;gui.optionalCostAbility=sa;
                    try{return super.chooseOptionalCosts(sa,costs);}finally{gui.optionalCostAbility=previous;}
                }
                @Override public boolean mulliganKeepHand(Player firstPlayer, int cardsToReturn) {
                    guiFor(this).mulliganReturnCount=cardsToReturn;
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
                    return guiFor(this).arrange(cards,false);
                }
                @Override public org.apache.commons.lang3.tuple.ImmutablePair<CardCollection,CardCollection> arrangeForSurveil(CardCollection cards) {
                    return guiFor(this).arrange(cards,true);
                }
                @Override public boolean willPutCardOnTop(Card card) {
                    boolean putOnTop=super.willPutCardOnTop(card);
                    // Preserve the knowledge of this single known card across
                    // the native Library->Library move, as in single-card scry.
                    libraryKnowledge.arranged(guiFor(this).seat,List.of(card),putOnTop?List.of(card):List.of());
                    return putOnTop;
                }
                @Override protected void reveal(CardCollectionView cards, ZoneType zone, PlayerView owner, String message, boolean addSuffix) {
                    guiFor(this).revealCards(message==null?"展示卡牌":message,new ArrayList<>(CardView.getCollection(cards)));
                }
    }
    static Gui guiFor(PlayerControllerHuman controller){return (Gui)Proxy.getInvocationHandler(controller.getGui());}
    static boolean retainsLivingInput(Gui gui) {
        if(!gui.handedOff || !gui.controller.getPlayer().isInGame())return false;
        if(!(gui.controller.getPlayer().getController() instanceof PlayerControllerHuman current) || guiFor(current).seat!=gui.seat)return false;
        Input input=gui.controller.getInputQueue().getInput();
        return input!=null && input.getOwner()!=null && input.getOwner().getId()==gui.controller.getPlayer().getId();
    }
    static List<Gui> inputGuis(){
        // Finished control intervals need not retain their choice caches.
        controlGuis.removeIf(g->g.publishedInputRevision>=0 && g.controller.getPlayer().getControllingPlayer()==null && !retainsLivingInput(g));
        List<Gui> active=new ArrayList<>(guis);active.addAll(controlGuis);
        active.removeIf(g->g.controller!=g.controller.getPlayer().getController() && !retainsLivingInput(g));
        return active;
    }
    static synchronized void returnLivingControlInput(int departedSeat) {
        Pending request=pending;
        if(request==null || request.consumed || request.gui==null || request.seat!=departedSeat)return;
        Gui gui=request.gui;
        Player owner=gui.controller.getPlayer();
        if(!owner.isInGame() || owner.getController()==gui.controller || !(owner.getController() instanceof PlayerControllerHuman current))return;
        int seat=guiFor(current).seat;
        if(seat<1 || seat>guis.size() || !guis.get(seat-1).controller.getPlayer().isInGame())return;
        // The existing input/future and its callbacks still belong to this
        // living player. Keep the captured controller's shared queue and choice
        // state; only the physical recipient and wire identity change.
        gui.seat=seat;gui.handedOff=true;
        request.seat=seat;request.requestId=++revision;
        publish(request);
    }
    static final class Option {
        String id, label;
        int attackerId, defenderId;
        String defenderKind;
        boolean requiresPaymentCheck;
        Object card;
        Object ability;
        Object castingFace;
        String faceKind, modeCost, cardName, rulesText;
        boolean selected, disabled;
        int order;
        transient Runnable action;
        Option(String id, String label, Runnable action) { this.id=id; this.label=label; this.action=action; }
    }
    static final class Pending {
        long requestId, contextId;
        int seat, playerSeat;
        int selectedCount, remainingCount, sourceCardId,replacementAffectedPlayerSeat,votingPlayerSeat;
        boolean secretVote;
        String kind, message;
        Object sourceCard,replacementAffectedCard;
        String abilityText, abilityKind;
        List<Option> options;
        List<Object> previewCards=new ArrayList<>();
        List<Object> orderedAbilities=new ArrayList<>();
        List<Object> allocations=new ArrayList<>();
        List<Object> piles=new ArrayList<>();
        int minCount, maxCount, reservedLifePayment;
        String selectionPurpose, remainingManaCost, paymentMode;
        int selectedValue, requiredValue;
        int minValue,maxValue;
        transient boolean modal;
        transient boolean consumed;
        transient Gui gui;
        transient java.util.function.Consumer<List<Integer>> acceptOrder;
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
        EDT.execute(() -> { try { r.run(); publishInput(); } catch(Throwable e) { if(terminalCancellation(e))finish();else fatal(e); } });
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
        if(BASE_FALSE_CALLBACKS.contains(n))return false;
        if(BASE_PRESENTATION_CALLBACKS.contains(n))return null;
        throw new UnsupportedOperationException("IGuiBase."+n);
    }
    static final Set<String> BASE_FALSE_CALLBACKS=Set.of("isLibgdxPort","isSupportedAudioFormat","hasNetGame");
    // Assets, audio and sleep management belong to the Unity process. Unknown
    // void/boolean callbacks must fail rather than silently discard a decision.
    static final Set<String> BASE_PRESENTATION_CALLBACKS=Set.of("clearImageCache","preventSystemSleep","startAltSoundSystem");
    static final class NoticeContext {
        final Card source;
        final String value,kind;
        final int actorSeat;
        NoticeContext(forge.game.spellability.SpellAbility ability,forge.game.GameObject target,String value) {
            source=ability==null?null:ability.getHostCard();this.value=value;
            actorSeat=target instanceof Player player?game.getRegisteredPlayers().indexOf(player)+1:0;
            // Fail closed: API type alone cannot make a recipient-specific notification public.
            // Only these pinned direct callers publish random outcomes to every player.
            var caller=StackWalker.getInstance().walk(frames->frames
                .dropWhile(f->!f.getClassName().equals("forge.game.GameAction")||!f.getMethodName().equals("notifyOfValue"))
                .skip(1).findFirst().orElse(null));
            kind=caller!=null&&caller.getClassName().equals("forge.game.ability.effects.RollDiceEffect")&&caller.getMethodName().equals("rollDiceForPlayer")?"dice":
                caller!=null&&caller.getClassName().equals("forge.game.ability.effects.FlipCoinEffect")&&caller.getMethodName().equals("flipCoin")?"coin":
                caller!=null&&caller.getClassName().equals("forge.game.ability.effects.FlipOntoBattlefieldEffect")&&caller.getMethodName().equals("resolve")?"flip":"notice";
            if(kind.equals("flip")) {
                flipCount=Integer.parseInt(ability.getSVar("TimesFlipped"));
                actorSeatForFlip=game.getRegisteredPlayers().indexOf(ability.getActivatingPlayer())+1;
                if(ability.getActivatingPlayer().getController() instanceof BridgeController controller)
                    flipLocation=guiFor(controller).flipLocations.get(ability);
                var local=forge.util.Localizer.getInstance();
                if(value.equals(local.getMessage("lblFlippedOver",flipCount)))flipOutcome="turned";
                else if(value.equals(local.getMessage("lblDidNotFlipOver")))flipOutcome="no-turn";
                else if(value.equals(local.getMessage("lblDidNotLandOnCards")))flipOutcome=flipLocation==null?"empty":"miss";
                else {
                    var matcher=java.util.regex.Pattern.compile("\\((\\d+)\\)").matcher(value);
                    while(matcher.find()) {
                        int id=Integer.parseInt(matcher.group(1));
                        for(Card c:game.getCardsIn(ZoneType.Battlefield))if(c.getId()==id)flipHits.add(c);
                    }
                    if(flipHits.size()==1&&value.equals(local.getMessage("lblLandedOnOneCard",flipHits.get(0)))
                            ||flipHits.size()==2&&value.equals(local.getMessage("lblLandedOnTwoCards",flipHits.get(0),flipHits.get(1))))flipOutcome="hit";
                    else flipHits.clear(); // Unknown native formatting has no guessed identities.
                }
            }
        }
        int flipCount,actorSeatForFlip;
        String flipOutcome="";
        Card flipLocation;
        final List<Card> flipHits=new ArrayList<>();
    }
    static final class Gui implements InvocationHandler {
        int seat;
        boolean handedOff;
        final Set<Integer> highlightedCards=new HashSet<>();
        final ThreadLocal<NoticeContext> noticeContext=new ThreadLocal<>();
        final Deque<Object> notices=new ArrayDeque<>();
        void addNotice(String message,String title) {
            synchronized(ForgeBridge.class) {
                NoticeContext context=noticeContext.get();Map<String,Object> notice=new LinkedHashMap<>();
                notice.put("id",noticeSequence.incrementAndGet());notice.put("message",message);
                notice.put("kind",context==null?"notice":context.kind);notice.put("value",context==null?message:context.value);
                notice.put("actorSeat",context==null?0:context.actorSeat);
                notice.put("publicNotice",context!=null&&!context.kind.equals("notice"));
                Card source=context==null?null:context.source;
                boolean visible=source!=null&&!source.isFaceDown()&&source.getView().canBeShownTo(controller.getPlayer().getView());
                notice.put("title",context==null?title:visible?source.getName():"");
                if(visible)notice.put("sourceCard",card(source));
                if(context!=null&&context.kind.equals("flip")) {
                    notice.put("actorSeat",context.actorSeatForFlip);
                    notice.put("flipCount",context.flipCount);notice.put("flipOutcome",context.flipOutcome);
                    if(context.flipLocation!=null)notice.put("flipLocation",card(context.flipLocation));
                    notice.put("flipHits",context.flipHits.stream().map(ForgeBridge::card).toList());
                }
                notices.addLast(notice);while(notices.size()>16)notices.removeFirst();
            }
        }
        PlayerControllerHuman controller;
        final Map<forge.game.spellability.SpellAbility,Card> flipLocations=new WeakHashMap<>();
        Card libraryCostSource;
        forge.game.spellability.SpellAbility paymentConfirmation,manualCostAbility;
        Map<Integer,forge.game.replacement.ReplacementEffect> replacementChoices=Map.of();
        forge.game.replacement.ReplacementEffect optionalReplacement;
        GameEntity replacementAffected;
        forge.game.spellability.SpellAbility sourceChoiceAbility;
        forge.game.spellability.SpellAbility targetingAbility;
        forge.game.spellability.SpellAbility optionalCostAbility;
        Player votingPlayer;
        forge.game.spellability.SpellAbility votingAbility;
        void publish(Pending request){request.gui=this;ForgeBridge.publish(request);}
        final Map<Integer,forge.game.spellability.SpellAbility> castingAbilities=new HashMap<>();
        // Native choices can contain newly created objects that are not in any zone yet.
        // Keep their actual identity only for the lifetime of the choosing callback.
        final Map<Integer,Card> choiceObjects=new HashMap<>();
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
            if(n.equals("setHighlighted")) {
                synchronized(ForgeBridge.class) {
                    for(Object entity:(Iterable<?>)a[0])if(entity instanceof CardView card) {
                        if((boolean)a[1])highlightedCards.add(card.getId());else highlightedCards.remove(card.getId());
                    }
                }
                return null;
            }
            if(n.equals("getGameView")) return game.getView();
            if(n.equals("message")) {
                addNotice(String.valueOf(a[0]),a.length>1?String.valueOf(a[1]):"");
                // InputBlock.onOk reports invalid declarations without refreshing
                // buttons/prompt. Its consumed wire decision would otherwise
                // remain the only request while native blocking still waits.
                // Recheck on the UI worker so a newer input cannot be reopened.
                if(shownInput instanceof InputBlock)guiTask(()->{
                    synchronized(ForgeBridge.class) {
                        if(pending!=null&&pending.consumed&&pending.gui==this&&pending.kind.equals("InputBlock")
                            &&controller.getInputQueue().getInput()==shownInput)inputRevision++;
                    }
                });
                return null;
            }
            if(n.equals("getGamestate")) return null; // no developer-supplied saved state in a live match
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
            if(n.equals("one") && a.length==3) return modal(n+": "+a[0], (List<?>)a[1], false,(java.util.function.Function<Object,String>)a[2]);
            if(n.equals("oneOrNone")) {
                if(((List<?>)a[1]).isEmpty())return null;
                List<Object> values=new ArrayList<>((List<?>)a[1]);values.add(null);
                return modal("oneOrNone: "+a[0],values,false);
            }
            if(n.equals("getChoices") && a.length==6)
                return selectCards((String)a[0],(List<?>)a[3],(int)a[1],(int)a[2],
                    a[4]==null?List.of():(List<?>)a[4],false,(java.util.function.Function<Object,String>)a[5]);
            if(n.equals("insertInList"))return insertInList((String)a[0],a[1],(List<?>)a[2]);
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
                if(values.stream().allMatch(v -> v instanceof forge.game.replacement.ReplacementEffectView))
                    return orderReplacementEffects(values,a[5]==null?List.of():(List<?>)a[5],min,max);
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
            // Reviewed snapshot-driven presentation callbacks only. A new upstream
            // void method must not silently swallow a required player decision.
            if(method.getReturnType()==void.class && PRESENTATION_CALLBACKS.contains(n)) return null;
            if(Set.of("isGamePaused", "isUiSetToSkipPhase", "isSelecting").contains(n)) return false;
            throw new UnsupportedOperationException("IGuiGame."+n+" "+Arrays.toString(a));
        }
        static final Set<String> PRESENTATION_CALLBACKS=Set.of(
            "setGameView","setOriginalGameController","setGameController","setSpectator","openView",
            "afterGameEnd","showCombat","flashIncorrectAction","alertUser","finishGame","handleGameEvent",
            "setPanelSelection","setCard","setPlayerAvatar","setHighlighted","setGamePause",
            "setGameSpeed","updateDayTime","awaitNextInput","cancelAwaitNextInput","showWaitingTimer",
            "updateAutoPassPrompt","setCurrentPlayer","setNetGame");
        List<Object> insertInList(String title,Object added,List<?> previous) {
            List<Object> result=new ArrayList<>(previous);
            if(previous.isEmpty()){result.add(added);return result;}
            List<String> positions=new ArrayList<>();positions.add("First");
            for(Object item:previous)positions.add("After "+choiceLabel(item));
            int position=modalIndex(title,positions);result.add(position,added);return result;
        }
        String choiceLabel(Object value) {
            if(value instanceof forge.game.spellability.SpellAbilityView)return abilityLabel(value);
            if(value instanceof CardView card)return card.getCurrentState().getName();
            if(value instanceof PlayerView player)return player.getName();
            return String.valueOf(value);
        }
        String abilityLabel(Object value) {
            var ability=(forge.game.spellability.SpellAbilityView)value;
            return ability.getHostCard().getCurrentState().getName()+" — "+ability.getDescription();
        }
        final Map<String,forge.card.ICardFace> dungeonFaces=new HashMap<>();
        boolean dungeonFacesLoaded;
        forge.card.ICardFace dungeonChoiceFace(Object value) {
            if(value instanceof forge.card.ICardFace face && face.getType().isDungeon())return face;
            if(value instanceof forge.game.card.CardFaceView view) {
                if(!dungeonFacesLoaded) {
                    for(var rules:forge.StaticData.instance().getAllTokens().getRules().values())
                        if(rules.getType().isDungeon())dungeonFaces.put(rules.getMainPart().getName(),rules.getMainPart());
                    dungeonFacesLoaded=true;
                }
                return dungeonFaces.get(view.getName());
            }
            return null;
        }
        Object choiceCard(Object value) {
            var face=dungeonChoiceFace(value);
            if(face!=null) {
                Map<String,Object> result=new LinkedHashMap<>();
                result.put("name",face.getName());result.put("helperArtName",face.getName());
                result.put("effectMarker",true);result.put("markerKind","dungeon");result.put("zone","Command");
                result.put("currentType",face.getType().toString());result.put("currentRules",face.getOracleText());
                return result;
            }
            if(value instanceof CardView.CardStateView state && isRoomDoorState(state)) {
                Card room=game.getCardsInGame().stream().filter(c->c.getId()==state.getCard().getId()).findFirst().orElseThrow();
                var nativeState=room.getState(state.getState());
                Map<String,Object> result=new LinkedHashMap<>((Map<String,Object>)zoneCard(room,controller.getPlayer()));
                result.put("name",nativeState.getName());
                result.put("originalName",nativeState.getName());
                result.put("stateName",state.getState().name());
                result.put("currentType",nativeState.getType().toString());
                result.put("currentRules",nativeState.getOracleText());
                result.put("manaCost",nativeState.getManaCost().toString());
                return result;
            }
            if(!(value instanceof CardView cv))return null;
            Card chosen=choiceObjects.get(cv.getId());if(chosen!=null)return zoneCard(chosen,controller.getPlayer());
            for(Card c:game.getCardsInGame())if(c.getId()==cv.getId())return zoneCard(c,controller.getPlayer());
            // None holds actual temporary cards for native reveal/choice requests.
            // Resolve only the exact CardView already supplied to this GUI.
            for(Card c:game.getCardsIn(ZoneType.None))if(c.getId()==cv.getId())return zoneCard(c,controller.getPlayer());
            Map<String,Object> fallback=new LinkedHashMap<>();fallback.put("id",cv.getId());fallback.put("name",cv.getCurrentState().getName());return fallback;
        }
        boolean isRoomDoorState(CardView.CardStateView state) {
            if(state.getState()!=forge.card.CardStateName.LeftSplit && state.getState()!=forge.card.CardStateName.RightSplit)return false;
            return game.getCardsInGame().stream().anyMatch(c->c.getId()==state.getCard().getId() && !c.isFaceDown() && c.isRoom());
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
                    String result=awaitAnswer(answer);int id=Integer.parseInt(result.substring(result.indexOf(':')+1));
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
            // The selector already displays this exact private candidate list.
            if(reveal!=null)libraryKnowledge.remember(seat,new ArrayList<>(reveal.getCards()));
        }
        void revealCards(String title,List<?> values) {
            if(values.isEmpty())return;
            libraryKnowledge.remember(seat,values);
            List<Object> cards=new ArrayList<>();
            for(Object value:values){Object cv=choiceCard(value);
                if(cv instanceof Map<?,?> face && face.get("knownFacePrintingKey")!=null) {
                    Map<String,Object> preview=new LinkedHashMap<>((Map<String,Object>)cv);
                    preview.put("name",face.get("knownFaceName"));preview.put("printingKey",face.get("knownFacePrintingKey"));
                    preview.put("faceDown",false);preview.put("currentRules",face.get("knownFaceRules"));cv=preview;
                }
                if(cv!=null)cards.add(cv);}
            BridgeCardNotices.revealed(this,title,cards);
        }
        List<Object> selectCards(String title,List<?> values,int minimum,int maximum,List<?> initial,boolean ordered) {
            return selectCards(title,values,minimum,maximum,initial,ordered,null);
        }
        List<Object> selectCards(String title,List<?> values,int minimum,int maximum,List<?> initial,boolean ordered,java.util.function.Function<Object,String> display) {
            if(values.isEmpty())return List.of();
            int min=Math.max(0,Math.min(values.size(),minimum));
            int max=maximum<0?values.size():Math.min(values.size(),maximum);
            if(max<min)throw new IllegalArgumentException("Inconsistent selection bounds: "+minimum+".."+maximum);
            List<Object> selected=new ArrayList<>();
            for(Object item:initial)if(values.contains(item)&&!selected.contains(item)&&selected.size()<max)selected.add(item);
            while(true) {
                CompletableFuture<Integer> answer=new CompletableFuture<>();List<Option> options=new ArrayList<>();
                for(int i=0;i<values.size();i++) {
                    final int index=i;Object value=values.get(i);boolean chosen=selected.contains(value);
                    Option option=new Option("select:"+i,chosen?"取消选择":"选择",()->answer.complete(index));
                    option.card=choiceCard(value);option.selected=chosen;option.order=chosen?selected.indexOf(value)+1:0;
                    if(option.card==null)option.label=display==null?choiceLabel(value):display.apply(value);
                    if(value instanceof forge.game.spellability.OptionalCostValue cost)option.modeCost=cost.getCost().toSimpleString();
                    options.add(option);
                }
                if(selected.size()>=min && selected.size()<=max)options.add(new Option("confirm-cards","确认选择",()->answer.complete(-1)));
                if(!selected.isEmpty())options.add(new Option("reset-cards","重新选择",()->answer.complete(-2)));
                boolean hasCards=options.stream().anyMatch(o->o.card!=null);
                Pending request=new Pending(seat,ordered?"OrderCards":hasCards?"SelectCards":"ChooseOptions",title,options);request.modal=true;
                request.selectedCount=selected.size();request.remainingCount=Math.max(0,min-selected.size());request.minCount=min;request.maxCount=max;
                if(title.replace("\r", "").equals("Exert Attackers?\nExerted"))request.selectionPurpose="exertAttack";
                if(!values.isEmpty() && values.get(0) instanceof forge.game.spellability.SpellAbilityView ability) {
                    request.sourceCardId=ability.getHostCard().getId();
                    for(Card card:game.getCardsInGame())if(card.getId()==request.sourceCardId)request.sourceCard=card(card);
                }
                if(sourceChoiceAbility!=null){request.selectionPurpose="chooseSource";request.sourceCardId=sourceChoiceAbility.getHostCard().getId();request.sourceCard=card(sourceChoiceAbility.getHostCard());}
                if(optionalCostAbility!=null){request.selectionPurpose="optionalCosts";request.sourceCardId=optionalCostAbility.getHostCard().getId();request.sourceCard=card(optionalCostAbility.getHostCard());}
                publish(request);
                try {
                    int result=awaitAnswer(answer);if(result==-1){if(ordered)libraryKnowledge.ordered(seat,selected);return selected;}if(result==-2){selected.clear();continue;}
                    Object value=values.get(result);
                    if(!selected.remove(value)) {
                        if(max==1)selected.clear();
                        if(selected.size()<max)selected.add(value);
                        if(!ordered && values.size()==1 && max==1 && selected.size()==1)return selected;
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
                request.maxCount=cards.size();
                for(Card c:top)request.previewCards.add(selectedCard(c,"牌库顶 · "+(top.indexOf(c)+1)));
                for(Card c:bottom)request.previewCards.add(selectedCard(c,(surveil?"坟墓场":"牌库底")+" · "+(bottom.indexOf(c)+1)));
                request.remainingCount=remaining.size();request.selectedCount=top.size()+bottom.size();
                publish(request);
                try {
                    String choice=awaitAnswer(answer);
                    if(choice.equals("confirm")){libraryKnowledge.arranged(seat,new ArrayList<>(cards),new ArrayList<>(top));return org.apache.commons.lang3.tuple.ImmutablePair.of(top,bottom);}
                    if(choice.equals("reset")){remaining=new ArrayList<>(cards);top.clear();bottom.clear();continue;}
                    int id=Integer.parseInt(choice.substring(choice.indexOf(':')+1));
                    Card card=remaining.stream().filter(c->c.getId()==id).findFirst().orElseThrow();
                    (choice.startsWith("top:")?top:bottom).add(card);remaining.remove(card);
                    if(cards.size()==1) {
                        libraryKnowledge.arranged(seat,new ArrayList<>(cards),new ArrayList<>(top));
                        return org.apache.commons.lang3.tuple.ImmutablePair.of(top,bottom);
                    }
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
                options.add(new Option("submit-order","确认此结算顺序",()->{}));
                request.minCount=request.maxCount=values.size();
                request.acceptOrder=ids->{ordered.clear();for(int id:ids)ordered.add(values.get(id));answer.complete(-1);};
                for(Object value:ordered)request.orderedAbilities.add(abilityPreview(value,values.indexOf(value)));
                publish(request);
                try {
                    int choice=awaitAnswer(answer);
                    if(choice==-1)return new IGuiGame.OrderResult<>(ordered,false);
                    if(choice==-2){remaining=new ArrayList<>(values);ordered.clear();}
                    else ordered.add(remaining.remove(choice));
                } catch(Exception e){throw new IllegalStateException("Unanswered ability order",e);}
            }
        }
        Object orderReplacementEffects(List<Object> values,List<?> initial,int minimum,int maximum) {
            List<Object> ordered=new ArrayList<>(initial),remaining=new ArrayList<>(values);remaining.removeAll(ordered);
            while(true) {
                CompletableFuture<Integer> answer=new CompletableFuture<>();List<Option> options=new ArrayList<>();
                for(int i=0;i<remaining.size();i++) {
                    final int index=i;var replacement=(forge.game.replacement.ReplacementEffectView)remaining.get(i);
                    Option option=new Option("order:"+replacement.getId(),nativeReplacement(replacement).toString(),()->answer.complete(index));
                    option.ability=replacementPreview(replacement);options.add(option);
                }
                if(ordered.size()>=minimum && ordered.size()<=maximum)
                    options.add(new Option("confirm-order","确认替代顺序",()->answer.complete(-1)));
                if(!ordered.isEmpty())options.add(new Option("reset-order","重新排序",()->answer.complete(-2)));
                Pending request=new Pending(seat,"OrderAbilities","选择先应用的替代效果，然后确认。",options);request.modal=true;
                request.selectionPurpose="replacementOrder";request.minCount=minimum;request.maxCount=maximum;
                if(minimum==values.size() && maximum==values.size()) {
                    options.add(new Option("submit-order","确认替代顺序",()->{}));
                    request.acceptOrder=ids->{ordered.clear();for(int id:ids)ordered.add(values.stream().filter(v->((forge.game.replacement.ReplacementEffectView)v).getId()==id).findFirst().orElseThrow());answer.complete(-1);};
                }
                request.selectedCount=ordered.size();request.remainingCount=remaining.size();
                for(Object value:ordered)request.orderedAbilities.add(replacementPreview((forge.game.replacement.ReplacementEffectView)value));
                publish(request);
                try {
                    int choice=awaitAnswer(answer);if(choice==-1)return new IGuiGame.OrderResult<>(ordered,false);
                    if(choice==-2){ordered.clear();remaining=new ArrayList<>(values);}
                    else if(ordered.size()<maximum)ordered.add(remaining.remove(choice));
                }catch(Exception e){throw new IllegalStateException("Unanswered replacement order",e);}
            }
        }
        Object replacementPreview(forge.game.replacement.ReplacementEffectView value) {
            return replacementPreview(nativeReplacement(value));
        }
        Object replacementPreview(forge.game.replacement.ReplacementEffect nativeEffect) {
            return Map.of("id",nativeEffect.getId(),"kind","replacement","description",nativeEffect.toString(),"source",card(nativeEffect.getHostCard()));
        }
        forge.game.replacement.ReplacementEffect nativeReplacement(forge.game.replacement.ReplacementEffectView value) {
            var nativeEffect=replacementChoices.get(value.getId());
            if(nativeEffect==null)throw new IllegalStateException("Replacement view outside native decision context: "+value.getId());
            return nativeEffect;
        }
        Object abilityPreview(Object value,int id) {
            var ability=(forge.game.spellability.SpellAbilityView)value;
            return Map.of("id",id,"kind","ability","description",ability.getDescription(),"source",choiceCard(ability.getHostCard()));
        }
        Object selectStackTarget(String title,List<?> values,boolean index) {
            final List<?> originalValues=values;
            // Forge's Card.inZoneStack predicate also matches a triggered
            // ability whose source is still on the stack. Card targets without
            // an explicit TargetType are card spells, never those abilities.
            var targeting=targetingAbility;
            if(targeting!=null && !targeting.hasParam("TargetType") && targeting.getTargetRestrictions()!=null
                && Arrays.stream(targeting.getTargetRestrictions().getValidTgts()).allMatch(s->s.startsWith("Card.")||s.equals("Card"))) {
                values=new ArrayList<>(values);
                values.removeIf(value->{
                    if(value instanceof forge.game.spellability.StackItemView stack)
                        for(var item:game.getStack())if(item.getView().getId()==stack.getId())return !item.getSpellAbility().isSpell();
                    return false;
                });
            }
            final List<?> choices=values;
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
                if(targeting!=null) {
                    request.sourceCardId=targeting.getHostCard().getId();request.sourceCard=card(targeting.getHostCard());
                    request.abilityText=targeting.getDescription();
                    if(request.abilityText==null || request.abilityText.isBlank())request.abilityText=targeting.getStackDescription();
                    request.abilityKind=targeting.isTrigger()?"trigger":targeting.isSpell()?"spell":"ability";
                }
                publish(request);
                try {
                    int result=awaitAnswer(answer);
                    if(result==-2)return index?originalValues.indexOf(choices.get(selected)):choices.get(selected);
                    if(result==-3){selected=-1;continue;}
                    if(choices.get(result)==null || !(choices.get(result) instanceof forge.game.spellability.StackItemView))return index?originalValues.indexOf(choices.get(result)):choices.get(result);
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
                try {String action=awaitAnswer(answer);if(action.equals("cancel"))return null;if(action.equals("confirm"))return value;value+=Integer.parseInt(action);}
                catch(Exception e){throw new IllegalStateException("Unanswered number selection",e);}
            }
        }
        List<forge.game.spellability.AbilitySub> chooseSpree(forge.game.spellability.SpellAbility sa,List<forge.game.spellability.AbilitySub> possible,int min,int max,boolean cancellable) {
            Set<Integer> selected=new LinkedHashSet<>();long context=0;
            boolean spree=sa.getHostCard().hasKeyword(forge.game.keyword.Keyword.SPREE);
            while(true) {
                CompletableFuture<Integer> answer=new CompletableFuture<>();List<Option> options=new ArrayList<>();
                for(int i=0;i<possible.size();i++) {
                    final int index=i;var mode=possible.get(i);
                    Option option=new Option("mode:"+i,mode.getDescription(),()->answer.complete(index));
                    option.selected=selected.contains(i);option.modeCost=mode.hasParam("ModeCost")?new forge.game.cost.Cost(mode.getParam("ModeCost"),false).toSimpleString():null;
                    option.order=sa.getAdditionalAbilityList("Choices").indexOf(mode)+1;
                    option.disabled=!option.selected && (selected.size()>=max || spree&&!canAffordSpree(sa,possible,selected,i));options.add(option);
                }
                if(selected.size()>=min)options.add(new Option("confirm-modes","确认",()->answer.complete(-1)));
                if(!selected.isEmpty())options.add(new Option("reset-modes","重新选择",()->answer.complete(-2)));
                if(cancellable)options.add(new Option("cancel","取消施放",()->answer.complete(-3)));
                Pending request=new Pending(seat,"ChooseModes",spree?"Spree":"Choose modes",options);request.modal=true;request.selectionPurpose=spree?"spree":"modes";
                if(context==0)context=request.requestId;request.contextId=context;request.minCount=min;request.maxCount=max;request.selectedCount=selected.size();
                request.sourceCardId=sa.getHostCard().getId();request.sourceCard=card(sa.getHostCard());publish(request);
                try {int choice=awaitAnswer(answer);
                    if(choice==-3)return null;
                    if(choice==-1){List<forge.game.spellability.AbilitySub> result=new ArrayList<>();for(int i:selected)result.add(possible.get(i));return result;}
                    if(choice==-2)selected.clear();else if(!selected.remove(choice))selected.add(choice);
                }catch(Exception e){throw new IllegalStateException("Unanswered spree modes",e);}
            }
        }
        boolean canAffordSpree(forge.game.spellability.SpellAbility sa,List<forge.game.spellability.AbilitySub> possible,Set<Integer> selected,int added) {
            var intent=sa.copy();intent.setActivatingPlayer(sa.getActivatingPlayer()!=null?sa.getActivatingPlayer():game.getPlayers().get(seat-1));intent.setSubAbility(null);
            var modes=new ArrayList<forge.game.spellability.AbilitySub>();
            for(int i=0;i<possible.size();i++)if(selected.contains(i)||i==added)modes.add(possible.get(i));
            // Forge chains copies and applies its own Spree cost adjustment, reductions,
            // pool and usable sources. Never compare the UI's displayed pool to a cost.
            forge.game.ability.effects.CharmEffect.chainAbilities(intent,modes);
            Player payer=intent.getActivatingPlayer();boolean[] affordable={false};
            payer.runWithController(()->affordable[0]=forge.ai.AvailableActions.canAffordHuman(intent,payer),new forge.ai.PlayerControllerAi(game,payer,payer.getOriginalLobbyPlayer()));
            return affordable[0];
        }
        void applyCastingFace(Option option,Object value) {
            if(!(value instanceof forge.game.spellability.SpellAbilityView av))return;
            var sa=castingAbilities.get(av.getId());if(sa==null)return;
            option.rulesText=sa.hasParam("SpellDescription")?sa.getParam("SpellDescription"):sa.getDescription();
            var host=sa.getHostCard();var state=sa.getCardState();if(state==null)return;
            boolean evokeChoice=sa.isSpell()&&host.hasKeyword(forge.game.keyword.Keyword.EVOKE);
            if(!sa.isSpell())return;
            boolean specialFace=evokeChoice||host.isModal()||sa.isAdventure()||sa.isOmen()||host.hasState(forge.card.CardStateName.Secondary)||sa.getCardStateName()==forge.card.CardStateName.PreparedSpell;
            // Ordinary spells also need their real face. Keep a keyword cost
            // alternative's existing mode description instead of flattening it.
            if(!specialFace && !sa.isBasicSpell())return;
            Map<String,Object> face=new LinkedHashMap<>((Map<String,Object>)card(host));
            face.put("name",state.getName());face.put("currentRules",state.getOracleText());face.put("currentType",state.getType().toString());face.put("manaCost",state.getManaCost().toString());
            face.put("creature",state.getType().isCreature());face.put("land",state.getType().isLand());face.put("planeswalker",state.getType().isPlaneswalker());
            face.put("stateName",sa.getCardStateName().name());face.put("backFace",sa.getCardStateName()==forge.card.CardStateName.Backside);
            option.castingFace=face;option.faceKind=sa.getCardStateName()==forge.card.CardStateName.PreparedSpell?"prepare":host.isModal()?(sa.getCardStateName()==forge.card.CardStateName.Backside?"mdfc-back":"mdfc-front"):sa.isAdventure()?"adventure":sa.isOmen()?"omen":"main";
            String pilotCost=ForgeCardCostPreview.forAbility(sa);
            if(pilotCost!=null)face.put("previewManaCost",pilotCost);
            if(evokeChoice){option.faceKind=sa.isEvoke()?"evoke":"normal";option.modeCost=sa.getPayCosts().toSimpleString();}
        }
        boolean choosePile(forge.game.spellability.SpellAbility ability,CardCollectionView first,CardCollectionView second,String faceDown) {
            CompletableFuture<Boolean> answer=new CompletableFuture<>();
            Pending request=new Pending(seat,"ChoosePile","Choose a pile",List.of(
                new Option("pile:1","Pile 1",()->answer.complete(true)),
                new Option("pile:2","Pile 2",()->answer.complete(false))));
            request.modal=true;request.selectionPurpose="pile";request.minCount=1;request.maxCount=1;
            Card source=ability.getHostCard();
            if(publicMarkerSource(source) || source.getView().canBeShownTo(controller.getPlayer().getView())) {
                request.sourceCardId=source.getId();request.sourceCard=card(source);
            }
            // Mirror the pinned native Human controller's False / One / True policy.
            // Concealed piles contain no object identity or face metadata in the wire payload.
            List<CardCollectionView> groups=List.of(first,second);
            for(int i=0;i<groups.size();i++) {
                boolean hidden=i==0?!faceDown.equals("False"):faceDown.equals("True");
                List<Object> cards=new ArrayList<>();
                if(!hidden)for(Card value:groups.get(i))cards.add(zoneCard(value,controller.getPlayer()));
                request.piles.add(Map.of("pileId",i+1,"cardCount",groups.get(i).size(),"concealed",hidden,"cards",cards));
            }
            publish(request);
            try{return awaitAnswer(answer);}catch(Exception e){throw new IllegalStateException("Unanswered pile selection",e);}
        }
        int modalIndex(String title,List<?> values) { return (Integer)modal(title,values,true); }
        Object modal(String title,List<?> values,boolean index) {
            return modal(title,values,index,null);
        }
        Object modal(String title,List<?> values,boolean index,java.util.function.Function<Object,String> display) {
            if(values.isEmpty()) {
                if(title.startsWith("one: ") || title.equals("getAbilityToPlay"))return null;
                throw new IllegalArgumentException("Empty mandatory dialog: "+title);
            }
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
                options.add(new Option(id,value==null?"取消":display==null?String.valueOf(value):display.apply(value),() -> {
                    if(value==null)cancelManualPayment(this);
                    answer.complete(index ? j : value);
                }));
            }
            Pending request=new Pending(seat,stackTarget?"SelectStackTarget":"dialog",title,options);
            request.modal=true;
            boolean cardNames=values.stream().anyMatch(v->v instanceof forge.game.card.CardFaceView)
                && values.stream().allMatch(v->v==null || v instanceof forge.game.card.CardFaceView && dungeonChoiceFace(v)==null);
            if(cardNames)request.selectionPurpose="cardName";
            for(int i=0;i<values.size();i++) {
                if(cardNames && values.get(i)!=null)options.get(i).cardName=((forge.game.card.CardFaceView)values.get(i)).getName();
                else options.get(i).card=choiceCard(values.get(i));
                if(title.equals("getAbilityToPlay"))applyCastingFace(options.get(i),values.get(i));
            }
            if(options.stream().anyMatch(o->"evoke".equals(o.faceKind)))request.selectionPurpose="castingCosts";
            if(!values.isEmpty() && values.stream().allMatch(v->dungeonChoiceFace(v)!=null)) {
                request.selectionPurpose="dungeon";
                for(int i=0;i<options.size();i++)((Map<String,Object>)options.get(i).card).put("id",-2100000-i);
            }
            if(!values.isEmpty() && values.stream().allMatch(v->v instanceof forge.game.spellability.SpellAbilityView ability && ability.getHostCard().getCurrentState().getType().isDungeon())) {
                request.selectionPurpose="dungeonRoom";
                int hostId=((forge.game.spellability.SpellAbilityView)values.get(0)).getHostCard().getId();
                var host=game.getCardsIn(ZoneType.Command).stream().filter(c->c.getId()==hostId).findFirst().orElseThrow();
                request.sourceCard=card(host);
                var map=(Map<String,Object>)request.sourceCard;
                map.put("effectMarker",true);map.put("markerKind","dungeon");map.put("helperArtName",host.getName());map.put("dungeonRoom",host.getCurrentRoom());
            }
            request.sourceCardId=interactionSourceCardId;
            if(!values.isEmpty() && values.get(0) instanceof forge.game.spellability.SpellAbility ability)
                request.sourceCardId=ability.getHostCard().getId();
            else if(!values.isEmpty() && values.get(0) instanceof forge.game.spellability.SpellAbilityView ability)
                request.sourceCardId=ability.getHostCard().getId();
            if(values.stream().allMatch(v->v instanceof CardView.CardStateView state && isRoomDoorState(state))) {
                request.selectionPurpose="roomDoor";
                int roomId=((CardView.CardStateView)values.get(0)).getCard().getId();
                if(values.stream().allMatch(v->((CardView.CardStateView)v).getCard().getId()==roomId)) {
                    request.sourceCardId=roomId;
                    request.sourceCard=card(game.getCardsInGame().stream().filter(c->c.getId()==roomId).findFirst().orElseThrow());
                }
            }
            var activeCost=game.costPaymentStack.peek();
            if((manualCostAbility!=null||activeCost!=null&&!(activeCost.cost() instanceof forge.game.cost.CostPartMana))
                && !values.stream().allMatch(v->v==null||v instanceof Number)) {
                // Scalar costs use a reversible native-object draft, just like cards.
                request.selectionPurpose="costScalar";request.contextId=++revision;
                request.minCount=request.maxCount=1;final int[] chosen={-1};
                final List<?> costValues=values;
                Option next=new Option("ok","Next",()->answer.complete(index?chosen[0]:costValues.get(chosen[0])));
                next.disabled=true;
                for(int i=0;i<values.size();i++)if(values.get(i)!=null) {
                    final int at=i;Option option=options.get(i);
                    option.action=()->{chosen[0]=chosen[0]==at?-1:at;request.selectedCount=chosen[0]<0?0:1;next.disabled=chosen[0]<0;
                        for(Option item:options)item.selected=chosen[0]>=0&&item==option;
                        request.consumed=false;request.requestId=++revision;publish(request);};
                }
                options.add(next);
            }
            publish(request);
            try { return awaitAnswer(answer); }
            catch(Exception e) { throw new IllegalStateException("Unanswered dialog: "+title,e); }
        }
    }
    // Upstream canPlay predicts timing/costs; prohibitions are checked only later
    // by PlaySpellAbility. Filter rules whose named/type/color choices are already
    // fixed on their source. Announced amount-dependent rules remain final checks.
    static boolean namePermits(forge.game.spellability.SpellAbility ability,Player player) {
        Card host=ability.getHostCard();
        if(ability.isSpell())host=Objects.requireNonNullElse(ability.getAlternateHost(host),host);
        var mode=ability.isSpell()?forge.game.staticability.StaticAbilityMode.CantBeCast:forge.game.staticability.StaticAbilityMode.CantBeActivated;
        List<Card> sources=new ArrayList<>();game.getCardsIn(ZoneType.STATIC_ABILITIES_SOURCE_ZONES).forEach(sources::add);
        if(ability.isSpell())sources.add(host);
        for(Card source:sources)for(var rule:source.getStaticAbilities()) {
            String valid=rule.getParamOrDefault("ValidCard","");
            if(!(valid.contains(".NamedCard") || valid.contains(".ChosenType") || valid.contains(".ChosenColor"))
                || rule.hasParam("cmcGT") || !rule.checkConditions(mode))continue;
            boolean forbidden=ability.isSpell()
                ?forge.game.staticability.StaticAbilityCantBeCast.applyCantBeCastAbility(rule,ability,host,player)
                :forge.game.staticability.StaticAbilityCantBeCast.applyCantBeActivatedAbility(rule,ability,host,player);
            if(forbidden)return false;
        }
        return true;
    }
    static synchronized void publish(Pending p) {
        if(finished || game!=null && game.isGameOver()){finish();throw new GameEnded();}
        if(p.gui!=null)p.playerSeat=game.getRegisteredPlayers().indexOf(p.gui.controller.getPlayer())+1;
        // Include numeric, scalar and list callbacks in the same real cost context.
        var cost=game.costPaymentStack.peek();
        if(cost!=null&&!(cost.cost() instanceof forge.game.cost.CostPartMana)) {
            p.paymentMode="manual";
            var ability=cost.payment().getAbility();p.sourceCardId=ability.getHostCard().getId();p.sourceCard=card(ability.getHostCard());
        }
        if(p.gui!=null&&p.gui.votingPlayer!=null&&p.gui.votingAbility!=null) {
            p.votingPlayerSeat=game.getRegisteredPlayers().indexOf(p.gui.votingPlayer)+1;
            p.secretVote=p.gui.votingAbility.hasParam("Secretly");
            p.sourceCardId=p.gui.votingAbility.getHostCard().getId();
            p.sourceCard=card(p.gui.votingAbility.getHostCard());
        }
        if(p.message.equals("one: Choose Phyrexian symbols to pay with life before nonmana payment"))p.paymentMode="manual";
        if(p.gui!=null&&p.gui.manualCostAbility!=null) {
            var ability=p.gui.manualCostAbility;p.paymentMode="manual";
            p.sourceCardId=ability.getHostCard().getId();p.sourceCard=card(ability.getHostCard());
        }
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
        // Native mulligan queue notifications can show the same London input
        // twice before a player replies. Keep an unchanged decision actionable;
        // consuming it, changing selection, or a new input still creates a new ID.
        // An explicit resend of pending itself must remain deliverable.
        if(p!=pending && p.kind.equals("InputLondonMulligan") && pending!=null && !pending.consumed
            && p.gui==pending.gui && p.contextId==pending.contextId
            && p.selectedCount==pending.selectedCount && p.remainingCount==pending.remainingCount
            && Objects.equals(p.message,pending.message)
            && JSON.toJsonTree(p.options).equals(JSON.toJsonTree(pending.options)))return;
        pending=p;
        Map<String,Object> m=envelope("request"); m.put("request",p); m.put("views",views());
        m.put("elapsedMs",(System.nanoTime()-started)/1_000_000); send(m);
    }
    static synchronized void publishInput() {
        if(game!=null && game.isGameOver()) { finish(); return; }
        if(game==null || (pending!=null && !pending.consumed && (pending.modal || Set.of("dialog","SelectStackTarget","OrderAbilities","Scry").contains(pending.kind)))) return;
        for(Gui g:inputGuis()) {
            Input inp=g.controller.getInputQueue().getInput();
            // A queued card selection can finish after the EDT callback returns. Do not
            // re-offer the old payment prompt before Forge has actually refreshed it.
            if(inp==null || inp!=g.shownInput || g.inputRevision==g.publishedInputRevision) continue;
            // Native phase transitions can briefly ask a departed active player
            // to pass again. Release that input without publishing legal actions
            // for a player who has left. Shared living-player queues stay intact.
            if(!g.controller.getPlayer().isInGame()) {
                if(inp instanceof InputSynchronized stopped && inp.getOwner()!=null
                    && inp.getOwner().getId()==g.controller.getPlayer().getId()) {
                    g.publishedInputRevision=g.inputRevision;guiTask(stopped::stop);
                }
                continue;
            }
            String kind=inp.getClass().getSimpleName();
            if(kind.isEmpty()) kind=inp.getClass().getSuperclass().getSimpleName();
            if(inp instanceof forge.player.HumanCostDecision.BeholdInput)kind="InputSelectCardsFromList";
            if(g.targetDraftInput!=inp){g.targetDraftInput=inp;g.targetDraft=null;}
            boolean singleTarget=inp instanceof InputSelectTargets && g.selectionMax==1;
            Set<GameEntity> chosenTargets=inp instanceof InputSelectTargets && !singleTarget?selectedTargets(inp):Set.of();
            List<Option> opts=new ArrayList<>();
            if(singleTarget && g.targetDraft!=null)opts.add(new Option("ok","确认",() -> {
                GameEntity chosen=g.targetDraft;g.targetDraft=null;
                if(chosen instanceof Card c)g.controller.getInputProxy().selectCard(c.getView(),null,null);
                else if(chosen instanceof Player p)g.controller.getInputProxy().selectPlayer(p.getView(),null);
            }));
            else if(g.ok) opts.add(new Option("ok",g.okLabel,() -> {
                g.controller.getInputProxy().selectButtonOK();
                // This upstream input swaps zones without refreshing its prompt.
                if(inp instanceof InputChooseStartingHand)g.inputRevision++;
            }));
            if(singleTarget && g.targetDraft!=null)opts.add(new Option("reset-targets","重选",() -> {g.targetDraft=null;g.inputRevision++;}));
            // Expose undo, not the human desktop client's yield-until-end-of-turn shortcut.
            if(g.cancel && (!(inp instanceof InputPassPriority) || g.controller.canUndoLastAction())
                && (!(inp instanceof InputAttack) || !game.getCombat().getAttackers().isEmpty()))
                opts.add(new Option("cancel",g.cancelLabel,() -> g.controller.getInputProxy().selectButtonCancel()));
            if(inp instanceof InputAttack attack) addAttackOptions(g,attack,opts);
            if(inp instanceof InputBlock block) addBlockOptions(g,block,opts);
            if(supportedInput(inp) && !(inp instanceof InputAttack) && !(inp instanceof InputBlock)) {
                List<ZoneType> zones=new ArrayList<>(List.of(ZoneType.Hand,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile));
                // Native getActivateAction checks the library permission and exact top
                // identity. Hidden and unauthorized library cards never become options.
                if(inp instanceof InputPassPriority)zones.addAll(List.of(ZoneType.Library,ZoneType.Command));
                if(inp instanceof InputSelectEntitiesFromList || inp instanceof InputSelectTargets)zones.addAll(List.of(ZoneType.Library,ZoneType.Command,ZoneType.Sideboard,ZoneType.Ante));
                for(ZoneType z:zones) {
                    for(Card c:game.getCardsIn(z)) {
                        String activate=inp.getActivateAction(c);
                        boolean counterExhausted=activate==null && inp instanceof InputSelectCardToRemoveCounter selection && selection.getSelected().contains(c);
                        if(counterExhausted)activate="Selected counters";
                        if(chosenTargets.contains(c))activate="Deselect target";
                        boolean london=inp instanceof InputLondonMulligan
                            && z==ZoneType.Hand && c.getOwner()==g.controller.getPlayer();
                        if(london) activate="Select for London mulligan";
                        // getActivateAction describes a possible proposal, not affordability.
                        // Reuse Forge's priority-time prediction (includes mana sources and cost
                        // modifiers); do not count lands or reimplement costs in the Unity client.
                        // AvailableActions predicts through an AI controller. Its convoke
                        // policy deliberately preserves creatures before combat, so a false
                        // prediction cannot veto the human's legal tap-payment decision.
                        if(inp instanceof InputPassPriority && z==ZoneType.Hand
                            && !g.actionableCards.contains(c.getId())
                            && !c.hasKeyword(forge.game.keyword.Keyword.CONVOKE)
                            && !c.hasKeyword(forge.game.keyword.Keyword.IMPROVISE)
                            && !hasNativeCastingPaymentChoice(c,g.controller.getPlayer())) activate=null;
                        if(inp instanceof InputPassPriority && activate!=null
                            && c.getAllPossibleAbilities(g.controller.getPlayer(),true).stream().noneMatch(a->namePermits(a,g.controller.getPlayer())))activate=null;
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
                            candidate.disabled=counterExhausted;
                            if(inp instanceof InputSelectEntitiesFromList || inp instanceof InputSelectTargets || inp instanceof InputSelectCardsForConvokeOrImprovise || inp instanceof InputSelectCardToRemoveCounter) {
                                candidate.card=visible?card(c):Map.of("id",c.getId(),"name","Face-down card","faceDown",true);
                                if(inp instanceof InputSelectEntitiesFromList selection)candidate.selected=selection.getSelected().contains(c);
                                else if(inp instanceof InputSelectCardsForConvokeOrImprovise selection)candidate.selected=selection.getSelected().contains(c);
                                else if(inp instanceof InputSelectCardToRemoveCounter selection) {
                                    candidate.order=selection.getCounterTable().column(c).values().stream().mapToInt(com.google.common.collect.Multiset::size).sum();
                                    candidate.selected=candidate.order>0;
                                }
                                else if(singleTarget)candidate.selected=g.targetDraft==c;
                                else if(inp instanceof InputSelectTargets)candidate.selected=chosenTargets.contains(c);
                            }
                            opts.add(candidate);
                        }
                    }
                }
            }
            if(inp instanceof InputSelectTargets || inp instanceof InputSelectEntitiesFromList) {
                for(Player p:game.getPlayers()) if(chosenTargets.contains(p)||canTargetPlayer(inp,p)) {
                    Option playerOption=new Option("player:"+p.getId(),p.getName(),() -> {
                    if(inp instanceof InputSelectEntitiesFromList selection)toggleSelection(g,selection,p);
                    else if(singleTarget){g.targetDraft=g.targetDraft==p?null:p;g.inputRevision++;}
                    else g.controller.getInputProxy().selectPlayer(p.getView(),null);
                    });
                    if(inp instanceof InputSelectEntitiesFromList selection)playerOption.selected=selection.getSelected().contains(p);
                    opts.add(playerOption);
                }
            }
            String nativeRemainingManaCost=null;
            int reservedLifePayment=0;
            if(inp instanceof InputPayMana) {
                // The native payment input also accepts its payer's life total.
                // Read native pending shards/reserved life, never infer legality
                // from the translated prompt or displayed mana-pool colors.
                if(inp instanceof InputPayManaOfCostPayment) {
                    try {
                        Map<String,Object> fields=new HashMap<>();
                        for(String name:List.of("player","manaCost","phyLifeToLose","saPaidFor","effect")) {
                            Field field=InputPayMana.class.getDeclaredField(name);field.setAccessible(true);fields.put(name,field.get(inp));
                        }
                        Player payer=(Player)fields.get("player");
                        var cost=(forge.game.mana.ManaCostBeingPaid)fields.get("manaCost");
                        var ability=(forge.game.spellability.SpellAbility)fields.get("saPaidFor");
                        nativeRemainingManaCost=cost.toString();
                        reservedLifePayment=(int)fields.get("phyLifeToLose");
                        boolean lifeShard=cost.containsPhyrexianMana() || payer.hasKeyword("PayLifeInsteadOf:B") && cost.hasAnyKind(forge.card.mana.ManaAtom.BLACK);
                        if(lifeShard && payer.canPayLife((int)fields.get("phyLifeToLose")+2,(boolean)fields.get("effect"),ability))
                            opts.add(new Option("player:"+payer.getId(),"Pay 2 life",()->g.controller.getInputProxy().selectPlayer(payer.getView(),null)));
                    }catch(ReflectiveOperationException e){throw new IllegalStateException("Pinned native life-payment contract changed",e);}
                }
                for(var bucket:ForgePoolMana.entries(ForgePoolMana.payer((InputPayMana)inp),g.controller.getPlayer())) {
                    String bucketId=(String)bucket.get("id");
                    opts.add(new Option(bucketId,"Pay "+((boolean)bucket.get("snow")?"{S} ":"")+"{"+bucket.get("symbol")+"}",() -> {
                            long before=g.inputRevision;
                            ForgePoolMana.pay((InputPayMana)inp,bucketId);
                            // An unusable color/restricted mana can leave this
                            // input unchanged without showMessage(). Its wire
                            // choice was consumed; reopen that same native input.
                            if(g.controller.getInputQueue().getInput()==inp&&g.inputRevision==before)g.inputRevision++;
                        }));
                }
            }
            if(!supportedInput(inp)) throw new UnsupportedOperationException("Unadapted input: "+kind);
            if(opts.isEmpty()) throw new UnsupportedOperationException("No supported options for "+kind+": "+g.message);
            Pending request=new Pending(g.seat,kind,g.message,opts);
            request.gui=g;
            request.remainingManaCost=nativeRemainingManaCost;
            if(inp instanceof InputPayMana payment)request.paymentMode=paymentMode(payment);
            request.reservedLifePayment=reservedLifePayment;
            if(inp instanceof InputPayMana && (reservedLifePayment>0 || opts.stream().anyMatch(o->o.id.startsWith("player:"))))request.paymentMode="manual";
            if(g.contextInput!=inp){g.contextInput=inp;g.inputContextId=++revision;}
            request.contextId=g.inputContextId;
            if(inp instanceof InputConfirm) {
                var confirmationCost=game.costPaymentStack.peek();
                boolean paymentQuestion=g.paymentConfirmation!=null || confirmationCost!=null
                    && confirmationCost.cost() instanceof forge.game.cost.CostRemoveCounter
                    && confirmationCost.cost().getAmount().equals("All")
                    && opts.stream().anyMatch(o->o.id.equals("ok")&&o.label.equals("Yes"))
                    && opts.stream().anyMatch(o->o.id.equals("cancel")&&o.label.equals("No"));
                if(confirmationCost!=null&&confirmationCost.cost() instanceof forge.game.cost.CostForage) {
                    boolean otherPayment=g.message.equals("Choose forage payment: Food or graveyard");
                    request.selectionPurpose=otherPayment?"forageMethod":"costConfirmation";
                    for(Option option:opts)if(option.id.equals("ok"))option.label=otherPayment?"Sacrifice Food":"Next";
                        else if(option.id.equals("cancel"))option.label=otherPayment?"Exile three cards from graveyard":"Cancel";
                }
                if(paymentQuestion) {
                    request.selectionPurpose="costConfirmation";request.paymentMode="manual";
                    for(Option option:opts)if(option.id.equals("ok"))option.label="Next";else if(option.id.equals("cancel"))option.label="Cancel";
                }
                try {
                    Field shown=InputConfirm.class.getDeclaredField("card");shown.setAccessible(true);
                    CardView cv=(CardView)shown.get(inp);
                    if(cv!=null && cv.canBeShownTo(g.controller.getPlayer().getView())){request.previewCards.add(g.choiceCard(cv));request.sourceCardId=cv.getId();request.sourceCard=g.choiceCard(cv);}
                }catch(ReflectiveOperationException e){throw new IllegalStateException("Pinned confirmation API changed",e);}
                if(g.optionalReplacement!=null){
                    request.selectionPurpose="optionalReplacement";request.sourceCardId=g.optionalReplacement.getHostCard().getId();request.sourceCard=card(g.optionalReplacement.getHostCard());
                    request.orderedAbilities.add(g.replacementPreview(g.optionalReplacement));
                    if(g.replacementAffected instanceof Player affected)request.replacementAffectedPlayerSeat=game.getRegisteredPlayers().indexOf(affected)+1;
                    if(g.replacementAffected instanceof Card affected&&affected.getView().canBeShownTo(g.controller.getPlayer().getView()))request.replacementAffectedCard=card(affected);
                }
            }
            if(g.manualCostAbility!=null && inp instanceof InputSelectManyBase) {
                for(Option option:opts)if(option.id.equals("cancel")) {
                    Runnable original=option.action;option.action=()->{cancelManualPayment(g);original.run();};
                }
            }
            // A choice during an actual nonmana cost uses manual progression.
            // The live cost stack distinguishes payment from resolution choices.
            if(inp instanceof InputSelectManyBase) {
                var entry=game.costPaymentStack.peek();
                if(entry!=null&&!(entry.cost() instanceof forge.game.cost.CostPartMana)) {
                    request.paymentMode="manual";
                    var paying=entry.payment().getAbility();
                    request.sourceCardId=paying.getHostCard().getId();request.sourceCard=card(paying.getHostCard());
                }
            }
            if(inp instanceof InputSelectEntitiesFromList selection) {
                request.selectedCount=selection.getSelected().size();request.minCount=g.selectionMin;request.maxCount=g.selectionMax;
                request.remainingCount=Math.max(0,g.selectionMin-request.selectedCount);
                for(Option option:opts)if(option.id.startsWith("player:"))
                    option.selected=selection.getSelected().stream().anyMatch(t->option.id.equals("player:"+((GameEntity)t).getId()));
                if(request.paymentMode!=null&&request.paymentMode.equals("manual")&&request.selectedCount>=request.maxCount)
                    for(Option option:opts)if(!option.selected&&(option.id.startsWith("card:")||option.id.startsWith("player:")))option.disabled=true;
                if(!selection.getSelected().isEmpty())request.options.add(new Option("reset-cards","重新选择",()->{
                    for(Object previous:new ArrayList<>(selection.getSelected()))toggleSelection(g,selection,(GameEntity)previous);
                }));
                applyEvidenceRequirement(selection,request);
                if(selection instanceof forge.player.HumanCostDecision.BeholdInput behold) {
                    request.selectionPurpose=behold.isExiling()?"beholdExile":"behold";
                    var ability=behold.getPaymentAbility();
                    request.sourceCardId=ability.getHostCard().getId();request.sourceCard=card(ability.getHostCard());
                }
                if(g.libraryCostSource!=null) {
                    request.selectionPurpose=opts.stream().anyMatch(o->o.id.startsWith("player:"))?"chooseLibraryCostOwner":"putLibraryCost";
                    request.sourceCardId=g.libraryCostSource.getId();request.sourceCard=card(g.libraryCostSource);
                }
            }
            if(inp instanceof InputSelectCardsForConvokeOrImprovise selection) {
                request.selectedCount=selection.getSelected().size();
                request.minCount=0;
                if(!selection.getSelected().isEmpty())request.options.add(new Option("reset-cards","重新选择",()->{
                    for(Card previous:new ArrayList<>(selection.getSelected()))
                        g.controller.getInputProxy().selectCard(previous.getView(),null,null);
                }));
                applyTapPaymentContext(selection,request);
            }
            if(inp instanceof InputSelectCardToRemoveCounter selection) {
                // This input counts counters, not distinct cards. Repeated clicks
                // add one native choice; it auto-finishes at the required count.
                // Keep its own type chooser and cancellation, never toggle cards.
                request.selectionPurpose="removeCounters";
                request.selectedCount=selection.getCounterTable().totalValues();
                if(request.selectedCount>0)request.options.add(new Option("reset-cards","重新选择",()->{
                    for(GameEntity chosen:selection.getSelected())g.highlightedCards.remove(chosen.getId());
                    selection.getCounterTable().clear();selection.showMessage();publishInput();
                }));
                try {
                    Field minimum=InputSelectManyBase.class.getDeclaredField("min"),maximum=InputSelectManyBase.class.getDeclaredField("max");
                    minimum.setAccessible(true);maximum.setAccessible(true);
                    request.minCount=minimum.getInt(selection);request.maxCount=maximum.getInt(selection);
                    request.remainingCount=Math.max(0,request.minCount-request.selectedCount);
                    if(request.selectedCount>=request.maxCount)for(Option option:opts)if(option.id.startsWith("card:"))option.disabled=true;
                    Field field=InputSelectManyBase.class.getDeclaredField("sa");field.setAccessible(true);
                    var ability=(forge.game.spellability.SpellAbility)field.get(selection);
                    if(ability!=null){request.sourceCardId=ability.getHostCard().getId();request.sourceCard=card(ability.getHostCard());}
                }catch(ReflectiveOperationException e){throw new IllegalStateException("Pinned counter-payment API changed",e);}
            }
            if(singleTarget) {
                request.selectedCount=g.targetDraft==null?0:1;request.minCount=g.selectionMin;request.maxCount=1;
                request.remainingCount=Math.max(0,g.selectionMin-request.selectedCount);
            }
            if(inp instanceof InputSelectTargets) {
                try {
                    Field field=InputSelectTargets.class.getDeclaredField("sa");field.setAccessible(true);
                    var ability=(forge.game.spellability.SpellAbility)field.get(inp);
                    if(!singleTarget) {
                        Field fixed=InputSelectTargets.class.getDeclaredField("numTargets");fixed.setAccessible(true);
                        Integer count=(Integer)fixed.get(inp);
                        request.selectedCount=chosenTargets.size();
                        request.minCount=count==null?ability.getMinTargets():count;
                        request.maxCount=count==null?ability.getMaxTargets():count;
                        request.remainingCount=Math.max(0,request.minCount-request.selectedCount);
                    }
                    request.sourceCardId=ability.getHostCard().getId();
                    request.sourceCard=card(ability.getHostCard());
                    request.abilityText=ability.getDescription();
                    if(request.abilityText==null || request.abilityText.isBlank())request.abilityText=ability.isTrigger()?ability.getTrigger().toString():ability.getStackDescription();
                    if(request.abilityText==null || request.abilityText.isBlank())request.abilityText=g.message;
                    request.abilityKind=ability.isTrigger()?"trigger":ability.isSpell()?"spell":"ability";
                    for(Option option:opts)if(option.id.startsWith("player:"))option.selected=singleTarget?
                        g.targetDraft!=null && option.id.equals("player:"+g.targetDraft.getId()):
                        chosenTargets.stream().anyMatch(t->option.id.equals("player:"+t.getId()));
                    // A full native draft stays open now; preserve removal
                    // choices and disable extra additions at its actual max.
                    if(request.maxCount>=0&&request.selectedCount>=request.maxCount || ability.isDividedAsYouChoose()&&ability.getStillToDivide()<=0)
                        for(Option option:opts)if(!option.selected&&(option.id.startsWith("card:")||option.id.startsWith("player:")))option.disabled=true;
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
    // Convoke, improvise and waterbend use the same upstream input. Forge owns
    // reduction/color legality, toggling and payment; only project its current state.
    // Keep the native ability identity, not card names or UI request revisions.
    // Weak keys release completed/cancelled payment intents with their abilities.
    static final Set<forge.game.spellability.SpellAbility> manualTapPayments=
        Collections.newSetFromMap(Collections.synchronizedMap(new WeakHashMap<>()));
    static void cancelManualPayment(Gui gui) {
        if(gui.manualCostAbility!=null&&gui.controller instanceof BridgeController controller) {
            var intent=controller.paymentIntents.peek();
            // A foreign Assist payer may decline assistance. Only the caster's
            // own reversible announcement is a cancellation of the spell.
            if(intent!=null&&intent.ability==gui.manualCostAbility&&intent.cancelAllowed)intent.cancelled=true;
        }
    }
    static String paymentMode(InputPayMana input) {
        try {
            Field abilityField=InputPayMana.class.getDeclaredField("saPaidFor");abilityField.setAccessible(true);
            var ability=(forge.game.spellability.SpellAbility)abilityField.get(input);
            if(ability==null)return "mana";
            if(manualTapPayments.contains(ability))return "manual";
            var cost=ability.getPayCosts();
            if(input instanceof InputPayManaOfCostPayment) {
                Field matrixField=InputPayManaOfCostPayment.class.getDeclaredField("extraMatrix");matrixField.setAccessible(true);
                if(matrixField.get(input) instanceof forge.game.cost.CostPayment payment) {
                    Field adjusted=forge.game.cost.CostPayment.class.getDeclaredField("adjustedCost");adjusted.setAccessible(true);
                    var actual=(forge.game.cost.Cost)adjusted.get(payment);
                    cost=actual!=null?actual:payment.getCost();
                }
            }
            return cost!=null&&cost.getCostParts().stream().anyMatch(c->!(c instanceof forge.game.cost.CostPartMana)
                && !(c instanceof forge.game.cost.CostTap) && !(c instanceof forge.game.cost.CostUntap)
                && !(c instanceof forge.game.cost.CostAddMana) && !(c instanceof forge.game.cost.CostRevealChosen))
                ?"manual":"mana";
        }catch(ReflectiveOperationException error){throw new IllegalStateException("Pinned native payment-mode API changed",error);}
    }
    static void applyTapPaymentContext(InputSelectCardsForConvokeOrImprovise selection,Pending request) {
        try {
            Class<?> type=InputSelectCardsForConvokeOrImprovise.class;
            Field artifacts=type.getDeclaredField("artifacts"),creatures=type.getDeclaredField("creatures"),cost=type.getDeclaredField("remainingCost");
            Field maximum=InputSelectManyBase.class.getDeclaredField("max"),ability=InputSelectManyBase.class.getDeclaredField("sa");
            for(Field field:List.of(artifacts,creatures,cost,maximum,ability))field.setAccessible(true);
            request.selectionPurpose=artifacts.getBoolean(selection)?(creatures.getBoolean(selection)?"waterbend":"improvise"):"convoke";
            request.remainingManaCost=cost.get(selection).toString();
            request.maxCount=maximum.getInt(selection);
            var sa=(forge.game.spellability.SpellAbility)ability.get(selection);
            if(sa!=null){manualTapPayments.add(sa);request.paymentMode="manual";request.sourceCardId=sa.getHostCard().getId();request.sourceCard=card(sa.getHostCard());}
        }catch(ReflectiveOperationException error){throw new IllegalStateException("Pinned tap-payment input API changed",error);}
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
        var departureTriggers=ForgePendingDeparture.withStack(markerTriggers);
        for(int s=0;s<guis.size();s++) {
            seriesOwnership.publishChanges();
            Map<String,Object> view=new LinkedHashMap<>(); view.put("seat",s+1); view.put("anteEnabled",game.getRules().useAnte());
            view.put("notices",new ArrayList<>(guis.get(s).notices));
            Player acting=pending!=null && pending.seat==s+1 && pending.gui!=null?pending.gui.controller.getPlayer():guis.get(s).controller.getPlayer();
            boolean canRespond=acting.isInGame() && !game.isGameOver() && acting.getView().hasAvailableActions();
            if(canRespond && pending!=null && pending.seat==s+1 && pending.gui!=null && pending.kind.equals("InputPassPriority")) {
                Gui decision=pending.gui;
                Set<Integer> offered=new HashSet<>();
                for(Option option:pending.options)if(option.id.startsWith("card:"))offered.add(Integer.parseInt(option.id.substring(5)));
                canRespond=false;
                for(Card candidate:game.getCardsInGame())if(offered.contains(candidate.getId()) && decision.actionableCards.contains(candidate.getId())
                    && candidate.getAllPossibleAbilities(acting,true).stream().anyMatch(a->!a.isManaAbility()&&namePermits(a,acting))) {canRespond=true;break;}
            }
            view.put("canRespond",canRespond);
            view.put("attackStateKnown",true);
            view.put("hasLegalAttackers",game.getPhaseHandler().getPlayerTurn()!=null
                &&game.getPhaseHandler().getPlayerTurn().getCreaturesInPlay().stream().anyMatch(forge.game.combat.CombatUtil::canAttack));
            view.put("restartRevision",restartRevision);
            view.put("phase",String.valueOf(game.getPhaseHandler().getPhase()));
            view.put("dayTime",game.getDayTime()==null?"":game.isDay()?"day":"night");
            view.put("globalEffects",game.getDayTime()==null?List.of():List.of(Map.of("id",-2000001,"name",game.isDay()?"Day":"Night","helperArtName",game.isDay()?"Day":"Night","effectMarker",true,"markerKind","game","zone","Command","currentType","Game state","currentRules",game.isDay()?"当前为白昼。":"当前为黑夜。")));
            view.put("turn", game.getPhaseHandler().getTurn());
            // Active players shrink on concession; authenticated seats remain
            // the registered order, including the terminal public stack.
            view.put("turnSeat", game.getRegisteredPlayers().indexOf(game.getPhaseHandler().getPlayerTurn()) + 1);
            view.put("prioritySeat", game.getRegisteredPlayers().indexOf(game.getPhaseHandler().getPriorityPlayer()) + 1);
             view.put("stackCount",game.getStack().size());
            List<String> stack = new ArrayList<>();
            List<Object> stackItems = new ArrayList<>();
            for (var item : game.getStack()) {
                Card source=item.getSpellAbility().getHostCard();
                String description=source.isFaceDown() ? "Face-down spell" : item.getStackDescription();
                stack.add(description);
                Map<String,Object> entry=new LinkedHashMap<>();
                entry.put("id",item.getId());entry.put("description",description);
                if(!source.isFaceDown()){var ability=item.getSpellAbility();entry.put("rulesText",ability.hasParam("SpellDescription")?ability.getParam("SpellDescription"):ability.getDescription());}
                entry.put("kind",item.getSpellAbility().isSpell()?"spell":item.getSpellAbility().isTrigger()?"trigger":"ability");
                entry.put("copied",item.getSpellAbility().isCopied());
                entry.put("controllerSeat",game.getRegisteredPlayers().indexOf(item.getActivatingPlayer())+1);
                // Public stack state comes from the paid native ability, never a
                // GUI's pending choices (which can be cancelled or reset).
                var cast=item.getSpellAbility().getRootAbility();
                if(cast.isSpell()&&!source.isFaceDown()) {
                    List<Object> modes=new ArrayList<>();
                    if(cast.getApi()==forge.game.ability.ApiType.Charm)for(var mode=cast.getSubAbility();mode!=null;mode=mode.getSubAbility()) {
                        // CharmEffect chains cloned chosen modes; getChosenList is
                        // not that public result, and CharmOrder is renumbered.
                        if(mode.getSVarInt("CharmOrder")<1)continue;
                        int order=0,matches=0;var printed=cast.getAdditionalAbilityList("Choices");
                        if(printed!=null)for(int i=0;i<printed.size();i++) {
                            var original=printed.get(i);
                            if(original.getApi()==mode.getApi()&&Objects.equals(original.getParam("SpellDescription"),mode.getParam("SpellDescription"))&&Objects.equals(original.getParam("ModeCost"),mode.getParam("ModeCost"))) {order=i+1;matches++;}
                        }
                        if(matches!=1)order=0;
                        modes.add(Map.of("order",order,"description",mode.getDescription(),"cost",mode.hasParam("ModeCost")?new forge.game.cost.Cost(mode.getParam("ModeCost"),false).toSimpleString():""));
                    }
                    List<Object> costs=new ArrayList<>();
                    for(var cost:cast.getOptionalCosts())costs.add(Map.of("kind",cost.name(),"count",1));
                    if(cast.getAlternativeCost()!=null)costs.add(Map.of("kind",cast.getAlternativeCost().name(),"count",1));
                    for(var keyword:cast.getOptionalKeywords()) {
                        int count=cast.getOptionalKeywordAmount(keyword);
                        if(count>0)costs.add(Map.of("kind",keyword.name(),"count",count));
                    }
                    entry.put("chosenModes",modes);entry.put("castingChoices",costs);
                }
                Map<String,Object> stackSource=(Map<String,Object>)card(source);
                Integer x=item.getSpellAbility().getXManaCostPaid();
                if(item.getSpellAbility().isSpell()&&x!=null&&!source.isFaceDown()) {
                    entry.put("xValue",x);entry.put("hasXValue",true);
                    stackSource.put("manaCost",String.valueOf(stackSource.get("manaCost")).replace("{X}","{"+x+"}"));
                }
                // Frozen before casting payment; resolution taxes do not replace it.
                if(cast.isSpell()&&!source.isFaceDown()) {
                    String castingCost=cast.getSVar("MTGBCastingManaCost");
                    if(!castingCost.isEmpty())stackSource.put("castingManaCost",castingCost);
                }
                entry.put("source",stackSource);
                entry.put("targets",ForgeStackTargets.project(item,guis.get(s).controller.getPlayer()));
                stackItems.add(entry);
            }
            view.put("stack", stack);
            view.put("stackItems", stackItems);
            Player viewer=guis.get(s).controller.getPlayer();
            List<Object> playableExile=new ArrayList<>(),playableGraveyard=new ArrayList<>();
            for(ZoneType playableZone:List.of(ZoneType.Graveyard,ZoneType.Exile)) for(Card c:game.getCardsIn(playableZone)) {
                boolean permitted=c.mayPlay(acting).stream().anyMatch(o->o.grantsZonePermissions());
                if(playableZone==ZoneType.Graveyard&&c.getOwner()==acting)
                    permitted|=c.getAllSpellAbilities().stream().anyMatch(a->a.getRestrictions().getZone()==ZoneType.Graveyard);
                boolean offered=pending!=null && pending.seat==s+1 && pending.kind.equals("InputPassPriority")
                    && pending.options.stream().anyMatch(o->o.id.equals("card:"+c.getId()));
                boolean knownFace=c.isFaceDown()&&c.getView().canFaceDownBeShownTo(viewer.getView());
                if((permitted||offered)&&(c.getView().canBeShownTo(viewer.getView())&&!c.isFaceDown()||knownFace))
                    (playableZone==ZoneType.Graveyard?playableGraveyard:playableExile).add(zoneCard(c,viewer));
            }
            view.put("playableExile",playableExile);view.put("playableGraveyard",playableGraveyard);
            // Command-zone special actions (for example Companion) need the same
            // visible card surface as other non-hand actions. Native options still
            // decide timing and payment; a public card can remain visible while idle.
            List<Object> playableCommand=new ArrayList<>();
            for(Card c:game.getCardsIn(ZoneType.Command)) {
                if(c.getOwner()!=acting || c.isImmutable() || c.isFaceDown()
                        || !c.getView().canBeShownTo(viewer.getView()))continue;
                boolean commandAbility=c.getAllPossibleAbilities(acting,true).stream()
                        .anyMatch(a->a.getRestrictions().getZone()==ZoneType.Command);
                if(c.isCommander() || commandAbility)playableCommand.add(zoneCard(c,viewer));
            }
            view.put("playableCommand",playableCommand);
            List<Object> players=new ArrayList<>();
            for(int i=0;i<guis.size();i++) {
                Player p=guis.get(i).controller.getPlayer();
                Map<String,Object> pv=new LinkedHashMap<>(); pv.put("seat",i+1); pv.put("life",p.getLife());
                pv.put("controllingSeat",p.getControllingPlayer()==null?0:game.getRegisteredPlayers().indexOf(p.getControllingPlayer())+1);
                pv.put("targetId", p.getId());
                List<Object> playerCounters=new ArrayList<>();
                for(var entry:p.getCounters().entrySet())playerCounters.add(Map.of("kind",entry.getElement().toString(),"label",entry.getElement().getCounterOnCardDisplayName(),"count",entry.getCount()));
                pv.put("counters",playerCounters);pv.put("speed",p.getSpeed());
                pv.put("ringTemptedCount",p.getNumRingTemptedYou());pv.put("ringBearerId",p.getRingBearer()==null?0:p.getRingBearer().getId());
                pv.put("ringLevel",Math.min(4,p.getNumRingTemptedYou()));
                pv.put("completedDungeons",p.getCompletedDungeons().stream().map(Card::getName).toList());
                pv.put("anteCards",p.getCardsIn(ZoneType.Ante).stream().map(ForgeBridge::card).toList()); pv.put("handCount",p.getCardsIn(ZoneType.Hand).size()); pv.put("libraryCount",p.getCardsIn(ZoneType.Library).size());
                pv.put("attractionDeckCount",p.getCardsIn(ZoneType.AttractionDeck).size());
                pv.put("contraptionDeckCount",p.getCardsIn(ZoneType.ContraptionDeck).size());
                pv.put("crankCounter",p.getCrankCounter());
                pv.put("junkyardCards",p.getCardsIn(ZoneType.Junkyard).stream().map(c->zoneCard(c,viewer)).toList());
                pv.putAll(libraryKnowledge.top(p,s+1,viewer));
                pv.put("redMana",p.getManaPool().getAmountOfColor(forge.card.MagicColor.RED));
                int[] mana = new int[6], snowMana = new int[6]; byte[] manaColors = forge.card.mana.ManaAtom.MANATYPES;
                for(int m=0;m<mana.length;m++) {
                    mana[m]=p.getManaPool().getAmountOfColor(manaColors[m]);
                    snowMana[m]=ForgePoolMana.count(p,manaColors[m],true);
                }
                pv.put("manaPool", mana);
                pv.put("snowManaPool", snowMana);
                pv.put("manaPoolEntries",ForgePoolMana.entries(p,viewer));
                List<Object> hand=libraryKnowledge.hand(p,s+1,viewer);
                pv.put("hand",hand);
                List<Object> field=new ArrayList<>(); for(Card c:p.getCardsIn(ZoneType.Battlefield)) {
                    Map<String,Object> visible=(Map<String,Object>)card(c);
                    visible.put("pendingDepartures",ForgePendingDeparture.forCard(c,departureTriggers));
                    if(c.isFaceDown() && (c.getView().canFaceDownBeShownTo(viewer.getView()) || libraryKnowledge.knowsBattlefieldFace(c,s+1))) {
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
                    // Native bookkeeping (prepared permissions, static carriers,
                    // etc.) is represented on its actual card, not as an empty marker.
                    if(!c.getRenderForUI())continue;
                    // Native keyword bookkeeping has no public card artwork. Its
                    // protection-only/empty text is represented by the typed state
                    // below; retain mixed/non-protection containers unchanged.
                    if("Keyword Effects".equals(c.getName()) && forge.ImageKeys.HIDDEN_CARD.equals(c.getImageKey())
                        && p.getKeywords().getValues().stream().allMatch(k->k.getKeyword()==forge.game.keyword.Keyword.PROTECTION))continue;
                    Map<String,Object> marker=(Map<String,Object>)card(c);
                    boolean dungeon=c.getType().isDungeon();
                    marker.put("effectMarker",true);marker.put("markerKind",dungeon?"dungeon":"effect");
                    marker.put("currentType",dungeon?"Dungeon":"Effect");marker.put("currentRules",currentLandRules(c));
                    if(dungeon){marker.put("dungeonRoom",c.getCurrentRoom());marker.put("markerStatus",c.getCurrentRoom()==null?"":"当前房间："+c.getCurrentRoom());}
                    else if(c==p.getTheRing())marker.put("markerStatus","魔戒已引诱你 "+p.getNumRingTemptedYou()+" 次");
                    else if(c.getOverlayText()!=null)marker.put("markerStatus",c.getOverlayText());
                    Card source=c.getEffectSource();
                    // Export the captured result, never recalculate from a source
                    // whose power can change after this public effect is created.
                    if(source!=null && "Dragonborn Immolator".equals(source.getName()) && c.isBoon() && c.getChosenNumber()!=null)
                        marker.put("markerStatus","Noted number: "+c.getChosenNumber());
                    else if(source!=null && "A-Maelstrom Muse".equals(source.getName()) && c.hasSVar("CurrPower") && c.getSVar("CurrPower").matches("Number\\$-?\\d+"))
                        marker.put("markerStatus","Noted number: "+c.getSVar("CurrPower").substring(7));
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
                    marker.put("markerGroupKey",ForgePendingDeparture.groupingKey(trigger));
                    marker.put("backFace",source.isBackSide());
                    if(source.getPaperCard() instanceof forge.item.PaperCard)marker.put("printingKey",ForgeCatalog.key((forge.item.PaperCard)source.getPaperCard()));
                    effects.add(marker);
                }
                // Player protection is public effective state. One stable marker lists the native protections;
                // duplicates and duration remain entirely owned by Forge.
                var protections=p.getKeywords().getValues().stream()
                    .filter(k->k.getKeyword()==forge.game.keyword.Keyword.PROTECTION)
                    .map(k->k.getOriginal()).distinct().sorted().toList();
                if(!protections.isEmpty())effects.add(Map.of("id",-3000000-i,"ownerSeat",i+1,
                    "name","Player protection","helperArtName","Player protection",
                    "effectMarker",true,"markerKind","player-protection","zone","Command",
                    "currentType","Player state","currentRules",String.join("\n",protections)));
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
            view.put("players",players);
            try {view.put("boardKey",Base64.getEncoder().encodeToString(java.security.MessageDigest.getInstance("SHA-256").digest(JSON.toJson(view).getBytes(java.nio.charset.StandardCharsets.UTF_8))));}
            catch(java.security.NoSuchAlgorithmException impossible){throw new IllegalStateException(impossible);}
            views.add(view);
        }
        return views;
    }
    static boolean publicMarkerSource(Card source) {
        if(source==null || source.isFaceDown())return false;
        // AtEOT stores an LKI source with no live zone. Its recorded public zone
        // is valid evidence; a live source in a hidden zone remains concealed.
        var zone=source.getZone()!=null?source.getZone():source.getLastKnownZone();
        return zone!=null && !List.of(ZoneType.Hand,ZoneType.Library,ZoneType.Sideboard).contains(zone.getZoneType());
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
        int viewerSeat=guis.stream().filter(g->g.controller.getPlayer()==viewer).map(g->g.seat).findFirst().orElse(0);
        if(c.isFaceDown() && (c.getView().canFaceDownBeShownTo(viewer.getView()) || libraryKnowledge.knowsBattlefieldFace(c,viewerSeat))) {
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

    static Object meldComponent(Card c) {
        // Physical originals are public while linked on the battlefield. Use
        // their printed front without changing either native card's state.
        var front=c.getState(forge.card.CardStateName.Original);
        Map<String,Object> result=new LinkedHashMap<>();
        result.put("id",c.getId());result.put("name",front.getName());
        result.put("ownerSeat",guis.stream().filter(g->g.controller.getPlayer()==c.getOwner()).map(g->g.seat).findFirst().orElse(0));
        result.put("zone","MeldComponent");result.put("componentPreview",true);
        result.put("stateName","Original");result.put("backFace",false);result.put("faceDown",false);
        result.put("currentType",front.getType().toString());result.put("currentRules",front.getOracleText());
        result.put("manaCost",front.getManaCost().toString());
        if(c.getPaperCard() instanceof forge.item.PaperCard)
            result.put("printingKey",ForgeCatalog.key((forge.item.PaperCard)c.getPaperCard()));
        return result;
    }
    static Object card(Card c) {
        Map<String,Object> result = new LinkedHashMap<>();
        result.put("id", c.getId()); result.put("name", c.isFaceDown() ? "Face-down card" : c.getName());
        result.put("ownerSeat",guis.stream().filter(g->g.controller.getPlayer()==c.getOwner()).map(g->g.seat).findFirst().orElse(0));
        result.put("zone",c.getZone()==null?"":c.getZone().getZoneType().name());
        result.put("tapped", c.isTapped()); result.put("faceDown", c.isFaceDown());
        if(!c.isFaceDown()) {
            result.put("contraption",c.isContraption()); result.put("attraction",c.isAttraction());
            if(c.isContraption())result.put("sprocket",c.getSprocket());
            if(c.isAttraction()) {
                result.put("attractionLights",new TreeSet<>(c.getAttractionLights()));
                result.put("functionalVariant",c.getCurrentState().getFunctionalVariantName());
            }
        }
        if(c.isInZone(ZoneType.Battlefield)) {
            result.put("detained",c.isDetained());
            result.put("exertedThisTurn",c.getExertedThisTurn());
            result.put("exertedBySeats",game.getPlayers().stream().filter(c::isExertedBy).map(p->game.getRegisteredPlayers().indexOf(p)+1).toList());
            // Attachment relationships are public even if either face is hidden.
            // Serialize identities only, never recurse into a private card face.
            var attached=c.getEntityAttachedTo();
            result.put("attachedToKind",attached instanceof Card?"card":attached instanceof Player?"player":"");
            result.put("attachedToId",attached==null?0:attached.getId());
            result.put("attachedByIds",c.getAllAttachedCards().stream().map(Card::getId).toList());
        }
        result.put("emblem",c.isEmblem());result.put("ringBearer",c.isRingBearer());
        // Generated effect objects retain their own identity. Their artwork may
        // come from a public source, including its LKI after leaving the stack.
        // Do not assign the source printing identity to the effect itself.
        Card effectSource=c.isImmutable()&&!c.isEmblem()?c.getEffectSource():null;
        if(publicMarkerSource(effectSource) && effectSource.getPaperCard() instanceof forge.item.PaperCard
            && java.util.Objects.equals(c.getImageKey(),effectSource.getImageKey())) {
            result.put("effectSourcePrintingKey",ForgeCatalog.key((forge.item.PaperCard)effectSource.getPaperCard()));
            result.put("effectSourceName",effectSource.getName());
            result.put("effectSourceBackFace",effectSource.isBackSide());
            result.put("genericEffectName",c.getName().equals(effectSource+"'s Effect"));
        }
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
            // Public face attributes only. Keep hidden cards' underlying flags private.
            boolean onBattlefield=c.isInZone(ZoneType.Battlefield);
            if(onBattlefield && c.getCurrentStateName()==forge.card.CardStateName.Meld && c.getMeldedWith()!=null)
                result.put("componentCards",List.of(meldComponent(c),meldComponent(c.getMeldedWith())));
            result.put("harnessed",onBattlefield && c.isHarnessed());
            result.put("solved",onBattlefield && c.isSolved());
            result.put("suspected",onBattlefield && c.isSuspected());
            result.put("saddled",onBattlefield && c.isSaddled());
            result.put("plotted",c.isInZone(ZoneType.Exile) && c.isPlotted());
            boolean liveLand=c.isInZone(ZoneType.Battlefield) && c.isLand();
            result.put("originalType",c.getCurrentState().getType().toString());
            result.put("originalRules",c.getCurrentState().getOracleText());
            result.put("currentType",c.getType().toString());
            result.put("currentRules",liveLand ? currentLandRules(c) : c.getCurrentState().getOracleText());
            result.put("hasCurrentLandState",liveLand);
            boolean support=c.isInZone(ZoneType.Battlefield) && !c.isCreature() && !c.isPlaneswalker() && (c.isArtifact() || c.isEnchantment());
            boolean stackable=c.isInZone(ZoneType.Battlefield) && !c.isRoom() && !c.isCloned() && !c.isAttachedToEntity()
                && c.getAttachedCards().isEmpty() && !c.hasRemembered() && !c.hasImprintedCard() && !c.hasExiledCard()
                && !c.hasChosenType() && !c.hasChosenNumber() && !c.hasChosenPlayer()
                && !c.hasChosenColor() && c.getChangedCardTraits().isEmpty() && c.getChangedCardKeywords().isEmpty()
                && c.getChangedTextColorWords().isEmpty() && c.getChangedTextTypeWords().isEmpty()
                && c.getAbilityActivatedThisTurn().isEmpty() && c.getAbilityActivatedThisGame().isEmpty();
            result.put("stackableSupport",support && stackable);
            result.put("stackableStateKnown",true);
            result.put("stackablePermanent",stackable);
            result.put("protectingPlayerSeat",c.isBattle()?game.getRegisteredPlayers().indexOf(c.getProtectingPlayer())+1:0);
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
            result.put("manaCost",c.getManaCost().toString());
            String previewCost=ForgeCardCostPreview.forCard(c);
            if(previewCost!=null)result.put("previewManaCost",previewCost);
            String adventureCost=ForgeCardCostPreview.adventure(c);
            if(adventureCost!=null)result.put("adventurePreviewManaCost",adventureCost);
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
        result.put("effectiveBasePower",c.getCurrentPower());result.put("effectiveBaseToughness",c.getCurrentToughness());
        result.put("powerBaseChanged",c.getCurrentPower()!=basePower);result.put("toughnessBaseChanged",c.getCurrentToughness()!=baseToughness);
        java.util.Set<String> intrinsic=new java.util.HashSet<>();
        for(var keyword:c.getCurrentState().getIntrinsicKeywords())intrinsic.add(keyword.getKeyword().name());
        // Ward granted by the face-down procedure is inherent to cloak/disguise.
        if(c.isFaceDown() && (c.isCloaked() || c.getCastSA()!=null && "Ward:2".equals(c.getCastSA().getParam("FaceDownKeyword"))))intrinsic.add("WARD");
        result.put("grantedKeywords",c.getKeywords().stream().map(k->k.getKeyword().name()).distinct().filter(k->!intrinsic.contains(k)).sorted().toList());
        List<Object> counters=new ArrayList<>();
        for(var entry:c.getCounters().entrySet())counters.add(Map.of("kind",entry.getElement().toString(),"label",entry.getElement().getCounterOnCardDisplayName(),"count",entry.getCount()));
        result.put("counters",counters);
        ForgeCharacteristicDetails.add(c,result);
        ForgeSectorDetails.add(c,result);
        if(!c.isFaceDown())result.put("keywords",c.getKeywords().stream().map(k->k.getKeyword().name()).distinct().sorted().toList());
        if(!c.isFaceDown() && c.isInZone(ZoneType.Battlefield)) {
            Player controller=c.getController();
            if(c.hasKeyword(forge.game.keyword.Keyword.STORIED)) {
                result.put("qualificationKind","storied");result.put("qualificationRequired",3);
                result.put("qualificationCount",controller.getCardsIn(ZoneType.Battlefield).stream().filter(Card::isHistoric).count());
                result.put("qualificationEarned",controller.hasEnduringStory());
            } else if(c.hasKeyword(forge.game.keyword.Keyword.ASCEND)) {
                result.put("qualificationKind","ascend");result.put("qualificationRequired",10);
                result.put("qualificationCount",controller.getCardsIn(ZoneType.Battlefield).size());
                result.put("qualificationEarned",controller.hasBlessing());
            }
        }
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
            if(c.hasMergedCard())result.put("merged",true);
            if(c.isCloned() || c.getCopiedPermanent()!=null || c.hasMergedCard()) {
                String imageKey=c.getCurrentState().getImageKey();
                boolean reverse=imageKey.endsWith(forge.ImageKeys.BACKFACE_POSTFIX);
                String frontKey=reverse?imageKey.substring(0,imageKey.length()-forge.ImageKeys.BACKFACE_POSTFIX.length()):imageKey;
                var imageCard=forge.util.ImageUtil.getPaperCardFromImageKey(frontKey);
                if(imageCard!=null) {
                    result.put("currentArtPrintingKey",ForgeCatalog.key(imageCard));
                    result.put("currentArtBackFace",reverse);
                }
            }
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
        var attackingBand=game.getCombat()==null?null:game.getCombat().getBandOfAttacker(c);
        result.put("attackingBandIds",attackingBand==null?List.of():attackingBand.getAttackers().stream().map(Card::getId).toList());
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
        // Public rules helpers must keep their artwork in ability previews too,
        // including a pending trigger after the helper leaves the Command zone.
        if(!c.isFaceDown() && c.getGamePieceType()==forge.card.GamePieceType.EFFECT
            && c.getEffectSource()==null && Set.of("The Monarch","The Initiative","The Ring").contains(c.getName())) {
            result.put("effectMarker",true);result.put("markerKind","effect");
            result.put("helperArtName",c.getName());result.put("currentType","Effect");
            result.put("currentRules",currentLandRules(c));
        }
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
        if(gui.contextInput!=input)gui.highlightedCards.clear();
        Player face=combat.getDefenders().stream().filter(d->d instanceof Player&&d!=gui.controller.getPlayer()).map(d->(Player)d).findFirst().orElse(null);
        if(face!=null&&gui.controller.getPlayer().getCreaturesInPlay().stream().anyMatch(c->forge.game.combat.CombatUtil.canAttack(c,face)))
            options.add(new Option("attack-all","Attack all",()->{
                for(Card attacker:new ArrayList<Card>(gui.controller.getPlayer().getCreaturesInPlay())){
                    if(!forge.game.combat.CombatUtil.canAttack(attacker,face)||combat.isAttacking(attacker,face))continue;
                    if(combat.isAttacking(attacker))input.selectCard(attacker,null,RECALL_ATTACKER);
                    input.selectPlayer(face,null);input.selectCard(attacker,null,null);
                }
            }));
        if(combat.getAttackers().stream().anyMatch(c->c.getController()==gui.controller.getPlayer()))
            options.add(new Option("reset-attacks","Recall all attackers",()->{
                for(Card attacker:new ArrayList<Card>(combat.getAttackers()))if(attacker.getController()==gui.controller.getPlayer())input.selectCard(attacker,null,RECALL_ATTACKER);
            }));
        for(Card attacker:gui.controller.getPlayer().getCreaturesInPlay()) {
            if(combat.isAttacking(attacker)) {
                Option remove=new Option("attack-remove:"+attacker.getId(),"Recall "+attacker.getName(),
                    () -> input.selectCard(attacker,null,RECALL_ATTACKER));
                remove.attackerId=attacker.getId();options.add(remove);
                // Keep ordinary repeated attack assignment idempotent. Band
                // selection is a distinct native click, with no recall first.
                String nativeAction=input.getActivateAction(attacker);
                if(forge.util.Localizer.getInstance().getMessage("lblActivateBand").equals(nativeAction)) {
                    Option band=new Option("attack-band:"+attacker.getId(),nativeAction,
                        ()->input.selectCard(attacker,null,null));
                    band.attackerId=attacker.getId();band.selected=gui.highlightedCards.contains(attacker.getId());
                    options.add(band);
                }
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
            var controller=guis.get(seat-1).controller;
            Input waiting=controller.getInputQueue().getInput();
            controller.concede();
            if(!game.isGameOver())returnLivingControlInput(seat);
            // A surviving multiplayer game has no terminal queue release. Stop
            // only the captured input owned by the departed player; another
            // player's input may share this controller's queue under control.
            if(!game.isGameOver() && waiting instanceof InputSynchronized stopped
                && waiting.getOwner()!=null && waiting.getOwner().getId()==controller.getPlayer().getId()) {
                synchronized(ForgeBridge.class) {
                    if(pending!=null && pending.gui!=null && pending.gui.controller.getPlayer()==controller.getPlayer()) {
                        pending.consumed=true;pending=null;
                    }
                }
                guiTask(stopped::stop);
            }
            finish(); return;
        }
        Pending p; Option option=null; String error=null;List<Integer> order=null;List<Option> attackGroup=null;
        synchronized(ForgeBridge.class) {
            p=pending;
            if(!SESSION.equals(o.get("session").getAsString())) error="wrong-session";
            else if(p==null || p.consumed || o.get("requestId").getAsLong()!=p.requestId) error="stale-request";
            else if(o.get("seat").getAsInt()!=p.seat) error="wrong-seat";
            else {
                String id=o.get("option").getAsString();
                for(Option x:p.options) if(x.id.equals(id)) option=x;
                if(option==null || option.disabled) error="invalid-option";
                else if(id.equals("submit-order")) {
                    try {
                        order=new ArrayList<>();for(JsonElement entry:o.getAsJsonArray("order"))order.add(entry.getAsInt());
                        Set<Integer> available=new HashSet<>();
                        for(Option entry:p.options)if(entry.ability instanceof Map<?,?> preview)available.add(((Number)preview.get("id")).intValue());
                        for(Object entry:p.orderedAbilities)if(entry instanceof Map<?,?> preview)available.add(((Number)preview.get("id")).intValue());
                        if(p.acceptOrder==null || order.size()<p.minCount || order.size()>p.maxCount
                            || new HashSet<>(order).size()!=order.size() || !available.containsAll(order))error="invalid-order";
                    }catch(RuntimeException malformed){error="invalid-order";}
                }
                else if(p.kind.equals("InputAttack") && id.startsWith("attack:") && o.has("order") && !o.get("order").isJsonNull()) {
                    try {
                        // One response assigns a group through the exact offered native actions.
                        // This is UI multiselect, independent of Forge's banding mechanic.
                        Set<Integer> attackers=new LinkedHashSet<>();attackGroup=new ArrayList<>();
                        for(JsonElement entry:o.getAsJsonArray("order")) {
                            int attacker=entry.getAsInt();
                            if(!attackers.add(attacker))throw new IllegalArgumentException();
                            Option candidate=null;
                            for(Option offered:p.options)if(!offered.disabled && offered.id.startsWith("attack:")
                                && offered.attackerId==attacker && Objects.equals(offered.defenderKind,option.defenderKind)
                                && offered.defenderId==option.defenderId){candidate=offered;break;}
                            if(candidate==null)throw new IllegalArgumentException();
                            attackGroup.add(candidate);
                        }
                        if(attackers.isEmpty() || !attackers.contains(option.attackerId))error="invalid-order";
                    }catch(RuntimeException malformed){error="invalid-order";}
                }
                else if(o.has("order") && !o.get("order").isJsonNull() && (!o.get("order").isJsonArray() || o.getAsJsonArray("order").size()>0))error="invalid-order";
            }
            if(error!=null) {
                Map<String,Object> m=envelope("rejected"); m.put("error",error); m.put("requestId",o.get("requestId")); send(m);
                if(error.equals("stale-request") && p!=null && !p.consumed) publish(p);
                return;
            }
            p.consumed=true;
            Gui responding=p.gui!=null?p.gui:guis.get(p.seat-1);
            if(option.id.startsWith("card:") && (p.kind.equals("InputPassPriority") || p.kind.contains("PayMana")))
                responding.interactionSourceCardId=Integer.parseInt(option.id.substring(5));
            else if(p.kind.equals("InputPassPriority"))responding.interactionSourceCardId=0;
        }
        Map<String,Object> ack=envelope("accepted"); ack.put("requestId",p.requestId); send(ack);
        if(attackGroup!=null){List<Option> group=attackGroup;guiTask(()->{for(Option attacker:group)attacker.action.run();});}
        else if(order!=null)p.acceptOrder.accept(order);
        else if(p.modal || p.kind.equals("dialog")) option.action.run(); else guiTask(option.action);
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
    static void loadAuxiliarySection(Deck deck,JsonObject source,String field,forge.deck.DeckSection section,int minimum) {
        if(!source.has(field)||source.get(field).isJsonNull())return;
        JsonArray entries=source.getAsJsonArray(field);
        if(entries.size()==0)return;
        forge.deck.CardPool pool=deck.getOrCreate(section);
        loadSection(pool,entries);
        boolean authorized=authorizedAuxiliary(source,field,pool);
        if(!authorized && pool.countAll()<minimum)throw new IllegalArgumentException(section+" deck requires at least "+minimum+" cards");
        Set<String> names=new HashSet<>();
        for(var row:pool) {
            var paper=row.getKey();
            if(!section.validate(paper)||(!authorized && (row.getValue()!=1||!names.add(paper.getName().toLowerCase(Locale.ROOT)))))
                throw new IllegalArgumentException("Invalid or duplicate "+section+" card: "+ForgeCatalog.key(paper));
        }
    }
    static boolean authorizedAuxiliary(JsonObject source,String field,forge.deck.CardPool pool) {
        if(config==null || !config.has("authorizedAuxiliarySeriesPools") || config.get("authorizedAuxiliarySeriesPools").isJsonNull())return false;
        JsonArray authorized=config.getAsJsonArray("authorizedAuxiliarySeriesPools"),decks=config.getAsJsonArray("decks");
        for(int i=0;i<decks.size() && i<authorized.size();i++) {
            if(decks.get(i)!=source || authorized.get(i).isJsonNull())continue;
            JsonObject expected=authorized.get(i).getAsJsonObject();
            forge.deck.CardPool expectedPool=new forge.deck.CardPool();
            if(expected.has(field)&&!expected.get(field).isJsonNull())loadSection(expectedPool,expected.getAsJsonArray(field));
            Map<String,Integer> actual=new TreeMap<>(),original=new TreeMap<>();
            for(var row:pool)actual.put(ForgeCatalog.key(row.getKey()),row.getValue());
            for(var row:expectedPool)original.put(ForgeCatalog.key(row.getKey()),row.getValue());
            return actual.equals(original);
        }
        return false;
    }
    static boolean supportedInput(Input input) {
        return input instanceof InputPassPriority || input instanceof InputPayMana || input instanceof InputSelectTargets
            || input instanceof InputAttack || input instanceof InputBlock
            || input.getClass().getSimpleName().equals("InputMulligan")
            || input.getClass().getSimpleName().equals("InputConfirmMulligan")
            || input instanceof InputLondonMulligan
            || input instanceof InputChooseStartingHand
            || input instanceof InputSelectEntitiesFromList
            || input instanceof InputSelectCardsForConvokeOrImprovise
            || input instanceof InputSelectCardToRemoveCounter
            || input instanceof InputConfirm;
    }
    static Set<GameEntity> selectedTargets(Input input) {
        try {
            Field field=InputSelectTargets.class.getDeclaredField("targets");field.setAccessible(true);
            return new HashSet<>((Collection<GameEntity>)field.get(input));
        }catch(ReflectiveOperationException error){throw new IllegalStateException("Pinned target selection API changed",error);}
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
        for(var answer:modalAnswers)answer.completeExceptionally(new GameEnded());
        modalAnswers.clear();
        int winner=0;
        for(Gui gui:guis) if(game.getOutcome().isWinner(gui.controller.getPlayer().getRegisteredPlayer())) winner=gui.seat;
        Map<String,Object> message=envelope("ended"); message.put("winnerSeat",winner); message.put("views",views()); message.put("matchRemovals",matchRemovals.snapshot()); message.put("matchOwnership",seriesOwnership.snapshot(winner)); send(message);
    }
    public static void main(String[] args) throws Exception {
        run(args,null);
    }
    // Package-level entry for the isolated engine acceptance harness. Normal startup has no hook.
    static void run(String[] args,Runnable startGameHook) throws Exception {
        run(args,startGameHook,null);
    }
    static void run(String[] args,Runnable startGameHook,java.util.function.Consumer<List<RegisteredPlayer>> registrationHook) throws Exception {
        System.setErr(new PrintStream(new FileOutputStream(FileDescriptor.err),true,StandardCharsets.UTF_8));
        System.setOut(new PrintStream(System.err,true,StandardCharsets.UTF_8));
        Thread.setDefaultUncaughtExceptionHandler((t,e) -> {if(terminalCancellation(e))finish();else fatal(e);});
        config=JsonParser.parseString(java.nio.file.Files.readString(java.nio.file.Path.of(args[1]))).getAsJsonObject();
        SESSION=config.get("session").getAsString();
        String assets=new File(args[0]).getCanonicalPath().replace('\\','/')+"/";
        if(Boolean.getBoolean("mtgb.bridge.lazyCatalog"))
            System.setProperty("mtgb.bridge.cardNameIndex",assets+"res/cardsfolder/mtgb-name-index.bin");
        GuiBase.setInterface((IGuiBase)Proxy.newProxyInstance(IGuiBase.class.getClassLoader(),new Class<?>[]{IGuiBase.class},(p,m,a)->base(p,m,a,assets)));
        Thread input=new Thread(() -> {
            try(BufferedReader reader=new BufferedReader(new InputStreamReader(System.in,StandardCharsets.UTF_8))) {
                String line; while((line=reader.readLine())!=null) receive(JsonParser.parseString(line).getAsJsonObject());
                System.exit(0);
            } catch(Throwable e) { fatal(e); }
        },"mtgb-wire"); input.setDaemon(true); input.start();
        FModel.initialize(null,prefs -> {
            prefs.setPref(FPref.UI_LANGUAGE,"en-US"); prefs.setPref(FPref.PLAYER_NAME,"Proof");
            prefs.setPref(FPref.DECKGEN_CARDBASED,false); prefs.setPref(FPref.LOAD_CARD_SCRIPTS_LAZILY,Boolean.getBoolean("mtgb.bridge.lazyCatalog"));
            prefs.setPref(FPref.YIELD_AUTO_PASS_NO_ACTIONS,false);
            prefs.setPref(FPref.UI_SHOW_ACTIONABLE_HIGHLIGHTS,true); return null;
        });
        MyRandom.setRandom(new Random(config.get("seed").getAsInt()));
        catalog=Boolean.getBoolean("mtgb.bridge.lazyCatalog")
            ? ForgeCatalog.registeredPrintings(java.nio.file.Path.of(args[0]).resolve("../catalog.jsonl").normalize(),config)
            : ForgeCatalog.printings();
        List<RegisteredPlayer> players=new ArrayList<>();
        JsonArray decks=config.getAsJsonArray("decks");
        for(int seat=1;seat<=2;seat++) {
            JsonObject source=decks.get(seat-1).getAsJsonObject();
            Deck deck=new Deck("MTGB Player "+seat);
            loadSection(deck.getMain(),source.getAsJsonArray("cards"));
            loadSection(deck.getOrCreate(forge.deck.DeckSection.Sideboard),source.getAsJsonArray("sideboard"));
            loadAuxiliarySection(deck,source,"attractions",forge.deck.DeckSection.Attractions,10);
            loadAuxiliarySection(deck,source,"contraptions",forge.deck.DeckSection.Contraptions,15);
            RegisteredPlayer registered=new RegisteredPlayer(deck).setPlayer(new BridgePlayer("Player "+seat));
            registered.setStartingLife(config.get("startingLife").getAsInt());
            players.add(registered);
        }
        if(registrationHook!=null)registrationHook.accept(players);
        GameRules rules=new GameRules(GameType.Constructed); rules.setGamesPerMatch(1);
        rules.setPlayForAnte(config.has("useAnte") && config.get("useAnte").getAsBoolean());
        Match match=new Match(rules,players,"Unity bridge proof"); game=match.createGame();
        game.subscribeToEvents(libraryKnowledge); game.subscribeToEvents(new BridgeCleanupNotices()); game.subscribeToEvents(matchRemovals); game.subscribeToEvents(seriesOwnership);
        game.subscribeToEvents(new BridgeRestartNotices());
        game.subscribeToEvents(new BridgeCardNotices());
        for(int i=0;i<2;i++) {
            Gui handler=new Gui(i+1); handler.controller=(PlayerControllerHuman)game.getPlayers().get(i).getController();
            handler.controller.setGui((IGuiGame)Proxy.newProxyInstance(IGuiGame.class.getClassLoader(),new Class<?>[]{IGuiGame.class},handler));
            guis.add(handler);
        }
        game.getAction().invoke(() -> {
            // Forge's own worker exception handler can itself fail in headless mode.
            // Report failures before they escape it so clients never wait indefinitely.
            try { match.startGame(game,()->{seriesOwnership.capture();if(startGameHook!=null)startGameHook.run();}); finish(); } catch(Throwable e) { if(terminalCancellation(e))finish();else fatal(e); }
        });
        new CountDownLatch(1).await(); // Forge game workers are daemon threads.
    }
}
