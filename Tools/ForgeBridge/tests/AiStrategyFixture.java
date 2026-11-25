package mtgb.bridge;

import forge.ai.*;
import forge.game.card.*;
import forge.game.phase.PhaseType;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.game.spellability.SpellAbility;
import forge.model.FModel;
import java.nio.file.*;
import java.util.*;

/** Real native controller decisions and payment/resolution, isolated from the user's games. */
public final class AiStrategyFixture {
    static final List<Map<String,Object>> checks=new ArrayList<>();
    static void check(boolean value,String name,Object detail) {
        checks.add(Map.of("name",name,"pass",value,"detail",String.valueOf(detail)));
        if(!value) throw new AssertionError(name+": "+detail);
    }
    static Card place(Player p,String name,ZoneType zone) {return StateBranchFixture.place(name,p,zone);}
    static void clear(Player p,ZoneType zone) {
        for(Card c:new ArrayList<>(p.getCardsIn(zone))) p.getZone(zone).remove(c);
    }
    static CardCollection hand(Player p,String... names) {
        clear(p,ZoneType.Hand);
        for(String n:names) place(p,n,ZoneType.Hand);
        return new CardCollection(p.getCardsIn(ZoneType.Hand));
    }
    static List<String> names(Iterable<Card> cards) {
        List<String> result=new ArrayList<>();for(Card c:cards)result.add(c.getName());return result;
    }
    static void decisionChecks(Player ai,Player human) {
        var controller=(ForgeBridge.BridgeAiController)ai.getController();
        var strategy=new BridgeAiStrategy(ai);
        check(strategy.plan()==BridgeAiStrategy.Plan.CONTROL,"deck plan uses registered composition",strategy.plan());
        check(new BridgeAiStrategy(human).plan()==BridgeAiStrategy.Plan.MIDRANGE,"controlled human deck is not inspected",0);
        var h=hand(ai,"Mountain","Mountain","Mountain","Counterspell","Counterspell","Divination","Concentrate");
        check(!ComputerUtil.wantMulligan(ai,0),"baseline accepts wrong-color three-land hand",names(h));
        check(!controller.mulliganKeepHand(human,0),"new AI mulligans wrong-color hand",names(h));
        h=hand(ai,"Island","Island","Mountain","Counterspell","Divination","Concentrate","Air Elemental");
        check(controller.mulliganKeepHand(human,0),"keeps playable mixed-color hand",names(h));
        h=hand(ai,"Island","Mountain","Mountain","Counterspell","Counterspell","Concentrate","Air Elemental");
        check(!controller.mulliganKeepHand(human,0),"one Island cannot pay UU",names(h));
        check(controller.mulliganKeepHand(human,2),"stops excessive mulligans at five",names(h));
        h=hand(ai,"Island","Island","Mountain","Mountain","Counterspell","Divination","Concentrate");
        var ids=h.stream().map(Card::getId).toList();
        var bottom=controller.tuckCardsViaMulligan(h,2);
        CardCollection retained=new CardCollection(h);retained.removeAll(bottom);
        check(bottom.size()==2 && retained.size()==5,"London bottoms exact physical count",names(bottom));
        check(retained.stream().filter(c->c.getName().equals("Island")).count()==2,"London preserves UU sources",names(retained));
        check(h.stream().map(Card::getId).toList().equals(ids),"bottom evaluation does not mutate hand",names(h));
        h=hand(ai,"Island","Island","Island","Island","Island","Opt","Counterspell");
        check(!controller.mulliganKeepHand(human,0),"mulligans five-land seven",names(h));
        h=hand(ai,"Polluted Delta","Polluted Delta","Counterspell","Counterspell","Concentrate","Air Elemental","Opt");
        check(controller.mulliganKeepHand(human,0),"fetchlands are not treated as unable to make blue",names(h));
        h=hand(ai,"Breeding Pool","Breeding Pool","Counterspell","Divination","Opt","Concentrate","Air Elemental");
        check(controller.mulliganKeepHand(human,0),"dual lands can supply UU",names(h));
        h=hand(ai,"Temple Garden","Temple Garden","Counterspell","Divination","Opt","Concentrate","Air Elemental");
        check(!controller.mulliganKeepHand(human,0),"wrong dual colors do not pay blue costs",names(h));
        h=hand(ai,"Island","Island","Mountain","Counterspell","Divination","Concentrate","Air Elemental");
        boolean keep=controller.mulliganKeepHand(human,0);
        hand(human,"Emrakul, the Aeons Torn","Lightning Bolt","Force of Will");
        check(keep==controller.mulliganKeepHand(human,0),"opponent hidden hand cannot change keep",keep);
        h=hand(ai,"Island","Mox Sapphire","Mox Ruby","Counterspell","Divination","Opt","Concentrate");
        check(!strategy.handlesMulligan(h),"zero-mana source opener retains native specialist",names(h));
        h=hand(ai,"Snow-Covered Island","Snow-Covered Island","Icehide Golem","Icehide Golem","Concentrate","Air Elemental","Air Elemental");
        check(controller.mulliganKeepHand(human,0),"snow provenance is not falsely rejected by color-only forecast",names(h));
        var lib=ai.getCardsIn(ZoneType.Library);var before=lib.stream().map(Card::getId).toList();
        controller.mulliganKeepHand(human,1);
        check(lib.stream().map(Card::getId).toList().equals(before),"decision never reorders library",before.size());
        var pool=ai.getRegisteredPlayer().getDeck().getMain();var saved=pool.toFlatList();
        for(String kind:new String[]{"AGGRO","MIDRANGE","BIG_MANA"}) {
            pool.clear();pool.add(FModel.getMagicDb().getCommonCards().getCard("Forest"),24);
            pool.add(FModel.getMagicDb().getCommonCards().getCard(kind.equals("AGGRO")?"Grizzly Bears":kind.equals("BIG_MANA")?"Ancient Brontodon":"Centaur Courser"),36);
            check(new BridgeAiStrategy(ai).plan().name().equals(kind),"plan "+kind,kind);
        }
        pool.clear();pool.addAllFlat(saved);
    }
    static void castChecks(Player ai,Player human) throws Exception {
        var game=ForgeBridge.game;var controller=(ForgeBridge.BridgeAiController)ai.getController();
        clear(ai,ZoneType.Battlefield);clear(human,ZoneType.Battlefield);
        clear(ai,ZoneType.Hand);clear(human,ZoneType.Hand);
        game.getPhaseHandler().devModeSet(PhaseType.MAIN2,ai,false,4);
        for(int i=0;i<4;i++)place(ai,"Island",ZoneType.Battlefield);
        hand(ai,"Serum Visions","Concentrate");
        game.getAction().checkStaticAbilities();
        var choices=controller.chooseSpellAbilityToPlay();
        check(choices!=null&&!choices.isEmpty()&&choices.get(0).getHostCard().getName().equals("Serum Visions"),"cheap information before tapping out",choices);
        var opt=choices.get(0);int handBefore=ai.getCardsIn(ZoneType.Hand).size();
        controller.playChosenSpellAbility(opt);
        check(game.getStack().size()==1 && game.getStack().peekAbility().getHostCard().getName().equals("Serum Visions"),"AI actually pays and casts Serum Visions",game.getStack().size());
        check(ai.getCardsIn(ZoneType.Battlefield).stream().filter(Card::isTapped).count()==1,"Serum Visions paid one source",names(ai.getCardsIn(ZoneType.Battlefield)));
        game.getStack().resolveStack();
        check(ai.getCardsIn(ZoneType.Hand).size()==handBefore,"Serum Visions draw/scry resolves",names(ai.getCardsIn(ZoneType.Hand)));
        clear(ai,ZoneType.Battlefield);hand(ai,"Murder");
        for(int i=0;i<3;i++)place(ai,"Swamp",ZoneType.Battlefield);
        Card engine=place(human,"Dark Confidant",ZoneType.Battlefield);
        Card body=place(human,"Hill Giant",ZoneType.Battlefield);
        game.getAction().checkStaticAbilities();
        var nativeController=new PlayerControllerAi(game,ai,ai.getOriginalLobbyPlayer());
        var baselineScore=ComputerUtilCard.class.getDeclaredMethod("evaluateRemovalTargetPriority",Player.class,Card.class);
        baselineScore.setAccessible(true);
        int engineBase=(Integer)baselineScore.invoke(null,ai,engine),bodyBase=(Integer)baselineScore.invoke(null,ai,body);
        check(bodyBase>engineBase,"baseline removal favours vanilla body",List.of(engineBase,bodyBase));
        choices=controller.chooseSpellAbilityToPlay();
        check(choices!=null&&!choices.isEmpty(),"AI offers live removal",choices);
        SpellAbility removal=choices.get(0);
        check(removal.getTargets().getTargetCards().contains(engine),"prioritises recurring card engine over vanilla body",names(removal.getTargets().getTargetCards()));
        controller.playChosenSpellAbility(removal);
        check(game.getStack().size()==1,"Murder really on stack",game.getStack().size());
        game.getStack().resolveStack();
        check(engine.isInZone(ZoneType.Graveyard)&&body.isInPlay(),"Murder resolves against selected engine",engine.getZone());
        check(ai.getCardsIn(ZoneType.Battlefield).stream().filter(Card::isTapped).count()==3,"Murder paid three sources",3);
        var strategy=new BridgeAiStrategy(ai);
        Card etb=place(human,"Elvish Visionary",ZoneType.Battlefield);
        check(strategy.removalPriority(etb)==0,"spent ETB is not recurring card advantage",etb);
        Card arena=place(human,"Phyrexian Arena",ZoneType.Battlefield);
        check(strategy.removalPriority(arena)>0,"recurring draw enchantment is an engine",arena);
        for(var trigger:arena.getTriggers())trigger.setSuppressed(true);
        check(strategy.removalPriority(arena)==0,"suppressed abilities lose engine bonus",arena);
        Card ours=place(ai,"Dark Confidant",ZoneType.Battlefield);
        check(strategy.removalPriority(ours)==0,"never treats own engine as hostile",ours);
        clear(ai,ZoneType.Hand);hand(ai,"Murder");
        for(Card c:ai.getCardsIn(ZoneType.Battlefield))c.setTapped(false);
        Card protectedEngine=place(human,"Dark Confidant",ZoneType.Battlefield);
        protectedEngine.addIntrinsicKeyword("Shroud");
        game.getAction().checkStaticAbilities();
        choices=controller.chooseSpellAbilityToPlay();
        check(choices==null||choices.isEmpty()||!choices.get(0).getTargets().getTargetCards().contains(protectedEngine),"engine ranking preserves native shroud exclusion",choices);
        hand(ai,"Serum Visions","Concentrate");
        var cantrip=ai.getCardsIn(ZoneType.Hand).getFirst().getFirstSpellAbility();cantrip.setActivatingPlayer(ai);
        ai.setLife(4,null);
        check(strategy.spellPriority(cantrip)==0,"danger suppresses proactive draw bonus",ai.getLife());
        check(nativeController.getStrategicSpellPriority(cantrip)==0&&nativeController.getStrategicRemovalPriority(protectedEngine)==0,"ordinary Forge controllers keep zero override",0);
        ai.setLife(20,null);
        for(Card c:ai.getCardsIn(ZoneType.Battlefield)) c.setTapped(true);
        check(strategy.spellPriority(cantrip)==0,"tapped lands cannot justify information-first bonus",0);
        for(Card c:ai.getCardsIn(ZoneType.Battlefield)) c.setTapped(false);
        var xDraw=place(ai,"Prosperity",ZoneType.Hand).getFirstSpellAbility();xDraw.setActivatingPlayer(ai);
        check(strategy.spellPriority(xDraw)==0,"X draw is not mistaken for one-mana cantrip",xDraw);
        var combatField=game.getPhaseHandler().getClass().getDeclaredField("combat");combatField.setAccessible(true);
        var combat=new forge.game.combat.Combat(human);combat.addAttacker(body,ai);
        combatField.set(game.getPhaseHandler(),combat);ai.setLife(3,null);
        check(ComputerUtilCard.getBestRemovalTargetAI(ai,List.of(body,protectedEngine))==body,"public lethal attacker outranks engine",body);
        combatField.set(game.getPhaseHandler(),null);
        hand(ai,"Island","Island","Mountain","Mountain","Counterspell","Divination","Concentrate");
        long[] samples=new long[25];
        for(int i=0;i<samples.length;i++) {
            long start=System.nanoTime();controller.tuckCardsViaMulligan(ai.getCardsIn(ZoneType.Hand),2);
            samples[i]=System.nanoTime()-start;
        }
        Arrays.sort(samples);
        check(true,"London evaluation timing (25 warm samples, ms)",List.of(samples[12]/1e6,samples[23]/1e6));
    }
    static void existingGuardChecks(Player ai) {
        var db=FModel.getMagicDb().getCommonCards();
        var deck=new forge.deck.Deck("existing guard");deck.getMain().add(db.getCard("Forest"),99);
        deck.getOrCreate(forge.deck.DeckSection.Commander).add(db.getCard("Acolyte of Bahamut"),1);
        check("has an illegal commander".equals(forge.deck.DeckFormat.Commander.getDeckConformanceProblem(deck)),"existing guard rejects sole noncreature background",0);
        deck.getMain().remove(db.getCard("Forest"),1);
        deck.getOrCreate(forge.deck.DeckSection.Commander).add(db.getCard("Wilson, Refined Grizzly"),1);
        check(forge.deck.DeckFormat.Commander.getDeckConformanceProblem(deck)==null,"existing guard preserves legal background pair",0);
        deck.getMain().clear();deck.getMain().add(db.getCard("Wastes"),99);
        deck.getOrCreate(forge.deck.DeckSection.Commander).clear();deck.getOrCreate(forge.deck.DeckSection.Commander).add(db.getCard("Faceless One"),1);
        check(forge.deck.DeckFormat.Commander.getDeckConformanceProblem(deck)==null,"existing guard preserves creature background commander",0);
        Card outside=place(ai,"Forest",ZoneType.Sideboard);var rules=ForgeBridge.game.getRules();
        check(ai.canPlayLand(outside,true,null),"ordinary outside-game land permission remains possible",0);
        rules.setDuelCommander(true);
        check(!ai.canPlayLand(outside,true,null),"existing DC guard forbids wishboard land",0);
        rules.setDuelCommander(false);rules.setAppliedVariants(EnumSet.of(forge.game.GameType.Brawl));
        check(!ai.canPlayLand(outside,true,null),"existing Brawl guard forbids wishboard land",0);
        rules.setAppliedVariants(EnumSet.noneOf(forge.game.GameType.class));
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Path output=Path.of(ForgeBridge.config.get("strategyResult").getAsString());
            try {
                Player human=ForgeBridge.game.getPlayers().get(0),ai=ForgeBridge.game.getPlayers().get(1);
                decisionChecks(ai,human);castChecks(ai,human);existingGuardChecks(ai);
                Files.writeString(output,ForgeBridge.JSON.toJson(Map.of("status","PASS","checks",checks)));
                System.exit(0);
            } catch(Throwable e) {
                try {Files.writeString(output,ForgeBridge.JSON.toJson(Map.of("status","FAIL","checks",checks,"error",e.toString())));}catch(Exception ignored){}
                e.printStackTrace();System.exit(1);
            }
        });
    }
}

