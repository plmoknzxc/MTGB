// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import forge.card.GamePieceType;
import forge.game.Game;
import forge.game.GameRules;
import forge.game.GameType;
import forge.game.ability.AbilityFactory;
import forge.game.ability.AbilityKey;
import forge.game.ability.AbilityUtils;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.player.RegisteredPlayer;
import forge.game.spellability.SpellAbility;
import forge.game.trigger.TriggerHandler;
import forge.game.zone.ZoneType;
import java.util.*;
import java.util.function.Consumer;

/** Magic Presents play aids. The room supplies the event, and Forge owns its effects and choices. */
final class BridgeCommanderEvent {
    static final String STONE = "heart-of-the-mountain", LOKI = "god-of-mischief", NAUCTIS = "nauctis-grand-marine-preview";
    private final String event;
    private final int firstSeat;
    private final Map<Player,Integer> victoryPoints = new HashMap<>();
    private final Map<Player,Card> guides = new HashMap<>();
    private final Set<String> tricksUsed = new HashSet<>();
    private Card lokiGuide;
    private volatile List<Map<String,Object>> publicTricks = List.of();
    private Card stone;

    BridgeCommanderEvent(String id, String format, int firstSeat) {
        event = id == null ? "" : id;
        if (!event.isEmpty() && (!format.equals("commander") || !Set.of(STONE,LOKI,NAUCTIS).contains(event)
                || firstSeat < 1 || firstSeat > 4))
            throw new IllegalArgumentException("Unsupported Commander event or seat");
        this.firstSeat = firstSeat;
    }

    GameRules rules(GameType type) {
        if (!LOKI.equals(event)) return new GameRules(type);
        return new GameRules(type) {
            @Override public boolean hasSharedTeamTurns() { return true; }
        };
    }

    void register(RegisteredPlayer player, int seat) {
        if (LOKI.equals(event)) {
            player.setStartingLife(60);
            player.setTeamNumber(seat == firstSeat ? 1 : 2);
        }
    }

    boolean sharesCardKnowledge(Player viewer, Player holder) {
        return viewer == holder || (holder != null && sharesHeroKnowledge(viewer)
                && holder.isInGame() && viewer.sameTeam(holder));
    }

    boolean sharesHeroKnowledge(Player viewer) {
        return LOKI.equals(event) && viewer.isInGame() && viewer.getTeam() == 2;
    }

    void start(Game game) {
        if (STONE.equals(event)) {
            for (Player player : game.getPlayers()) {
                Card guide = effect(player,"山中之心","己方生物首次对玩家造成战斗伤害时，若宝石无主，你获得阿肯宝石。",false);
                guides.put(player,guide);
                trigger(guide,"DamageDone | CombatDamage$ True | ValidSource$ Creature.YouCtrl | ValidTarget$ Player"
                    + " | IsPresent$ Card.named阿肯宝石 | PresentZone$ Command | PresentCompare$ EQ0",
                    "争夺无主的阿肯宝石", sa -> {
                        if (!hasHolder()) giveStone(player);
                    });
                game.getAction().moveToCommand(guide,null);
            }
        } else if (NAUCTIS.equals(event)) {
            for (Player player : game.getPlayers()) startNauctis(player);
        } else if (LOKI.equals(event)) {
            Player loki = game.getRegisteredPlayers().get(firstSeat-1);
            game.getPhaseHandler().enableLokiHeroRotation(loki);
            Card guide = effect(loki,"洛基的诡计","在你每回合的第一个行动阶段选择一项未使用的诡计。",false);
            lokiGuide = guide;
            updateTrickBoard();
            guide.setSVar("OneEach","PlayerCountOpponents$Amount");
            guide.setSVar("Discount","Mode$ ReduceCost | EffectZone$ Command | ValidCard$ Card | Type$ Spell | Activator$ You | Amount$ 1 | Description$ 本回合你的咒语减少一点通用法术力。");
            guide.setSVar("Villains","DB$ Animate | Defined$ Targeted | Types$ Villain | Keywords$ Haste");
            guide.setSVar("DiscardTwo","DB$ Discard | Defined$ You | NumCards$ 2 | Mode$ TgtChoose | UnlessType$ Instant,Sorcery");
            guide.setSVar("Stun","DB$ PutCounterAll | ValidCards$ Creature.OppCtrl | CounterType$ STUN | CounterNum$ 1");
            trigger(guide,"Phase | Phase$ Main1 | ValidPlayer$ You | ActivationLimit$ 1 | Static$ True",
                "选择一项未使用的诡计",sa->chooseTrick(guide,sa));
            game.getAction().moveToCommand(guide,null);
        }
    }

    // Standalone excerpt of the published Grand Marine panel. The preview starts
    // in its named preceding region; it does not simulate unpublished blessings.
    private static void startNauctis(Player player) {
        String threshold = "落坟20——在你位于广袤海域期间，只要你坟墓场中有二十张或更多永久物牌，你便进入雄伟大洋。";
        String blessing = "在你的第一个行动阶段开始时，派出一个3/3红色的鲨鱼衍生生物。本回合，你施放的下一个生物咒语具有吞噬3和「当此生物进场时，如果它吞噬了生物，则它深潜」（其无法被阻挡，直到其在攻击时造成战斗伤害为止）。";
        Card guide = effect(player, "广袤海域", threshold, false);
        guide.setSVar("Depth", "Count$ValidGraveyard Permanent.YouOwn");
        guide.setSVar("GrandMarine", "Number$0");
        trigger(guide, "Always | Static$ True | CheckSVar$ Depth | SVarCompare$ GE20",
                "进入雄伟大洋", sa -> {
                    guide.setSVar("Depth", "Number$0");
                    guide.setSVar("GrandMarine", "Number$1");
                    guide.setName("雄伟大洋");
                    guide.setOracleText(threshold + "\n\n撒饵诱鱼——" + blessing);
                });
        guide.setSVar("AwaitCreature", "Number$0");
        trigger(guide, "Phase | Phase$ Main1 | ValidPlayer$ You | ActivationLimit$ 1 | CheckSVar$ GrandMarine | SVarCompare$ EQ1", "撒饵诱鱼", sa -> {
            resolve(guide, "DB$ Token | TokenScript$ u_3_3_shark | TokenColors$ Red", player);
            guide.setSVar("AwaitCreature", "Number$1");
            player.getGame().getCleanup().addUntil(player, () -> guide.setSVar("AwaitCreature", "Number$0"));
        });
        trigger(guide, "SpellCast | ValidCard$ Creature | ValidActivatingPlayer$ You | Static$ True | CheckSVar$ AwaitCreature | SVarCompare$ EQ1",
                "赋予下一个生物咒语吞噬与深潜异能", sa -> {
                    guide.setSVar("AwaitCreature", "Number$0");
                    guide.addRemembered(sa.getTriggeringObject(AbilityKey.Card));
                });
        guide.setSVar("DevouredCount", "Count$Devoured Card");
        guide.setSVar("DiveOnEntry", "Mode$ ChangesZone | Destination$ Battlefield | ValidCard$ Card.Self | CheckSVar$ DevouredCount | SVarCompare$ GE1 | Execute$ Dive | TriggerDescription$ 当此生物进场时，若它吞噬过生物，则它深潜。");
        guide.setSVar("Dive", "DB$ Effect | Name$ 深潜 | RememberObjects$ Self | StaticAbilities$ DiveEvasion | Triggers$ Surface | ExileOnMoved$ Battlefield | Duration$ Permanent");
        guide.setSVar("DiveEvasion", "Mode$ CantBlockBy | ValidAttacker$ Card.IsRemembered | Description$ 此生物深潜时不能被阻挡。");
        guide.setSVar("Surface", "Mode$ DamageDone | CombatDamage$ True | ValidSource$ Card.IsRemembered+attacking | Static$ True | Execute$ EndDive");
        guide.setSVar("EndDive", "DB$ ChangeZone | Defined$ Self | Origin$ Command | Destination$ Exile");
        guide.addStaticAbility("Mode$ Continuous | EffectZone$ Command | AffectedZone$ Stack,Battlefield | Affected$ Card.IsRemembered | AddKeyword$ Devour:3 | AddTrigger$ DiveOnEntry | AddSVar$ DevouredCount & Dive & DiveEvasion & Surface & EndDive | Description$ 获得祝福的生物具有吞噬3与进场深潜异能。");
        String forget = " | ValidCard$ Card.IsRemembered | Static$ True";
        Consumer<SpellAbility> forgetCreature = sa -> guide.removeRemembered(sa.getTriggeringObject(AbilityKey.Card));
        trigger(guide, "ChangesZone | Origin$ Stack | ExcludedDestinations$ Battlefield" + forget, "结束祝福", forgetCreature);
        trigger(guide, "ChangesZone | Origin$ Battlefield" + forget, "结束祝福", forgetCreature);
        player.getGame().getAction().moveToCommand(guide, null);
    }

    private boolean hasHolder() {
        return stone!=null && stone.getController().isInGame() && stone.isInZone(ZoneType.Command);
    }

    private void giveStone(Player player) {
        if (!player.isInGame() || hasHolder() && stone.getController()==player) return;
        Game game=player.getGame();
        if (hasHolder()) game.getAction().exileEffect(stone);
        stone=effect(player,"阿肯宝石",stoneText(player),true);
        stone.addStaticAbility("Mode$ Continuous | EffectZone$ Command | Affected$ Creature.YouCtrl | AddPower$ 1 | AddToughness$ 1 | Description$ 你操控的生物得+1/+1。");
        nativeTrigger(stone,"Phase | Phase$ End of Turn | ValidPlayer$ You","在你的结束步骤开始时，抓一张牌。","DB$ Draw | Defined$ You | NumCards$ 1");
        trigger(stone,"Phase | Phase$ Upkeep | ValidPlayer$ You","获得一点胜利点，累积三点则获胜。",sa->{
            int points=victoryPoints.merge(player,1,Integer::sum);
            guides.get(player).setOverlayText("胜利点 "+points+"/3");
            if (hasHolder() && stone.getController()==player) stone.setOracleText(stoneText(player));
            if (points>=3) resolve(sa.getHostCard(),"DB$ WinsGame | Defined$ You",player);
        });
        trigger(stone,"DamageDone | CombatDamage$ True | ValidSource$ Creature | ValidTarget$ You","对持有者造成战斗伤害的生物之操控者取得阿肯宝石。",sa->{
            if (hasHolder() && stone.getController()==player)
                giveStone(((Card)sa.getTriggeringObject(AbilityKey.Source)).getController());
        });
        game.getAction().moveToCommand(stone,null);
    }

    private String stoneText(Player p) {
        return "胜利点 "+victoryPoints.getOrDefault(p,0)+"/3。你操控的生物得+1/+1。你的结束步骤开始时抓一张牌，维持开始时获得一点胜利点。";
    }

    private static Card effect(Player player,String name,String text,boolean emblem) {
        Game game=player.getGame();
        Card card=new Card(game.nextCardId(),game);
        card.setName(name);
        card.setOwner(player);
        card.setGameTimestamp(game.getNextTimestamp());
        card.setGamePieceType(GamePieceType.EFFECT);
        card.setEmblem(emblem);
        card.setOracleText(text);
        return card;
    }

    private static void nativeTrigger(Card host,String filter,String text,String script) {
        attach(host,filter,text,AbilityFactory.getAbility(script,host));
    }

    private static void trigger(Card host,String filter,String text,Consumer<SpellAbility> action) {
        attach(host,filter,text,new EventAbility(host,action));
    }

    private static final class EventAbility extends SpellAbility.EmptySa implements Cloneable {
        private final Consumer<SpellAbility> action;
        EventAbility(Card host,Consumer<SpellAbility> action) { super(host); this.action=action; }
        @Override public void resolve() { action.accept(this); }
    }
    private static void attach(Card host,String filter,String text,SpellAbility ability) {
        ability.setDescription(text);
        ability.setStackDescription(text);
        var trigger=TriggerHandler.parseTrigger("Mode$ "+filter+" | TriggerZones$ Command | TriggerDescription$ "+text,host,true);
        trigger.setOverridingAbility(ability);
        host.addTrigger(trigger);
    }

    private static void resolve(Card host,String script,Player player) {
        SpellAbility ability=AbilityFactory.getAbility(script,host);
        ability.setActivatingPlayer(player);
        AbilityUtils.resolve(ability);
    }

    private record Trick(String name,String text,String script) {}
    private static final List<Trick> TRICKS=List.of(
        new Trick("时间戏法","移去所有诡计上的诡计指示物，包括本项。",""),
        new Trick("壮丽幻象","派出三个3/3蓝色虚影衍生生物。","DB$ Token | TokenScript$ u_2_2_illusion | TokenAmount$ 3 | TokenPower$ 3 | TokenToughness$ 3"),
        new Trick("无序之主","你的其他生物成为目标己方生物的非传奇复制品直到回合结束。","DB$ Clone | Defined$ Targeted | CloneTarget$ Valid Creature.YouCtrl+NotDefinedTargeted | NonLegendary$ True | Duration$ UntilEndOfTurn | ValidTgts$ Creature.YouCtrl"),
        new Trick("择乱而行","你可从每位对手操控的神器或结界中各选至多一个，消灭之。","DB$ Destroy | ValidTgts$ Artifact.OppCtrl,Enchantment.OppCtrl | TargetMin$ 0 | TargetMax$ OneEach | TargetsForEachPlayer$ True"),
        new Trick("秘法欺瞒","本回合你施放的咒语减少一点通用法术力。","DB$ Effect | StaticAbilities$ Discount | Duration$ EndOfTurn | Name$ 秘法欺瞒"),
        new Trick("祸乱降临","消灭目标对手操控的所有生物。","DB$ DestroyAll | ValidTgts$ Opponent | ValidCards$ Creature.TargetedPlayerCtrl"),
        new Trick("混乱使者","你可从每位对手操控的生物中各选至多一个。本回合你获得其操控权，重置之。它们额外是反派并具有敏捷直到回合结束。","DB$ GainControl | ValidTgts$ Creature.OppCtrl | TargetMin$ 0 | TargetMax$ OneEach | TargetsForEachPlayer$ True | LoseControl$ EOT | Untap$ True | SubAbility$ Villains"),
        new Trick("幻境揭幕","展示牌库顶三张牌，其中所有地进战场，其余入手。","DB$ Dig | DigNum$ 3 | Reveal$ True | ChangeNum$ All | ChangeValid$ Land | DestinationZone$ Battlefield | DestinationZone2$ Hand"),
        new Trick("谋士洞见","抓五张牌。弃两张牌，除非你弃一张瞬间或法术。","DB$ Draw | NumCards$ 5 | SubAbility$ DiscardTwo"),
        new Trick("随兴织法","横置对手操控的所有生物，并各放一个晕眩指示物。","DB$ TapAll | ValidCards$ Creature.OppCtrl | SubAbility$ Stun")
    );

    void writeTrickBoard(Card card, Map<String,Object> marker) {
        if (card == lokiGuide) marker.put("lokiTricks",publicTricks);
    }

    private void updateTrickBoard() {
        publicTricks = TRICKS.stream().map(t -> Map.<String,Object>of(
            "name",t.name,"rules",t.text,"marked",tricksUsed.contains(t.name))).toList();
        lokiGuide.setOverlayText("已用诡计 "+tricksUsed.size()+"/"+TRICKS.size());
    }

    private void chooseTrick(Card guide,SpellAbility rule) {
        Player player=guide.getController();
        List<SpellAbility> options=new ArrayList<>();
        Map<SpellAbility,Trick> lookup=new IdentityHashMap<>();
        for (Trick trick : TRICKS) {
            if (tricksUsed.contains(trick.name)) continue;
            SpellAbility option=new SpellAbility.EmptySa(guide);
            option.setDescription(trick.name+"　"+trick.text);
            options.add(option);
            lookup.put(option,trick);
        }
        if (options.isEmpty()) return;
        SpellAbility selection=player.getController().chooseSpellAbilitiesForEffect(options,rule,"选择洛基的诡计",1,Map.of()).get(0);
        Trick trick=lookup.get(selection);
        tricksUsed.add(trick.name);
        updateTrickBoard();
        var trigger=TriggerHandler.parseTrigger("Mode$ Immediate | TriggerDescription$ "+trick.name+"："+trick.text,guide,true);
        SpellAbility ability;
        if (trick.script.isEmpty()) {
            ability=new EventAbility(guide,sa->{tricksUsed.clear(); updateTrickBoard();});
        } else ability=AbilityFactory.getAbility(trick.script,guide);
        ability.setActivatingPlayer(player);
        ability.setDescription(trick.text);
        ability.setStackDescription(trick.text);
        trigger.setOverridingAbility(ability);
        guide.getGame().getTriggerHandler().registerDelayedTrigger(trigger);
    }
}
