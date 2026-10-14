package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.player.PlayerController;
import forge.game.spellability.SpellAbility;
import forge.game.zone.ZoneType;
import forge.gamemodes.match.input.InputPayMana;
import java.nio.file.*;
import java.util.*;
import java.lang.reflect.Proxy;

/** Opening placement only; native ETB chooses every secret, all later actions are inputs. */
public final class RevealChosenPayloadFixture {
    static final List<Object> paidEvents=Collections.synchronizedList(new ArrayList<>());
    static Map<String,Object> ability(SpellAbility a) {
        Map<String,Object> v=new LinkedHashMap<>();v.put("id",a.getId());v.put("source",a.getHostCard().getId());v.put("name",a.getHostCard().getName());v.put("api",a.getApi()==null?"":a.getApi().name());v.put("spell",a.isSpell());v.put("trigger",a.isTrigger());v.put("actor",a.getActivatingPlayer()==null?0:ForgeBridge.game.getPlayers().indexOf(a.getActivatingPlayer())+1);
        v.put("hostChosenType",a.getHostCard().getChosenType());v.put("hostViewChosenType",String.valueOf(a.getHostCard().getView().getChosenType()));v.put("hostChosenPlayer",a.getHostCard().getChosenPlayer()==null?0:ForgeBridge.game.getPlayers().indexOf(a.getHostCard().getChosenPlayer())+1);v.put("hostZone",String.valueOf(a.getHostCard().getZone()));v.put("hostTimestamp",a.getHostCard().getGameTimestamp());v.put("activationGame",a.getActivationsThisGame());v.put("activationTurn",a.getActivationsThisTurn());v.put("params",a.getMapParams());v.put("cost",a.getPayCosts()==null?"":a.getPayCosts().toString());v.put("mana",a.getPayingMana().stream().map(DrainManaDeltaFixture::mana).toList());
        v.put("targets",a.getTargets().getTargetCards().stream().map(Card::getId).toList());List<Integer> players=new ArrayList<>();for(Player p:a.getTargets().getTargetPlayers())players.add(ForgeBridge.game.getPlayers().indexOf(p)+1);v.put("targetPlayers",players);List<Object> spells=new ArrayList<>();for(SpellAbility s:a.getTargets().getTargetSpells())spells.add(Map.of("ability",s.getId(),"source",s.getHostCard().getId(),"controller",ForgeBridge.game.getPlayers().indexOf(s.getActivatingPlayer())+1));v.put("targetSpells",spells);return v;
    }
    static Map<String,Object> card(Card c) {
        Map<String,Object> v=new LinkedHashMap<>(DrainManaDeltaFixture.card(c));v.put("power",c.getNetPower());v.put("toughness",c.getNetToughness());v.put("damage",c.getDamage());v.put("token",c.isToken());v.put("type",c.getType().toString());v.put("counters",c.getCounters().toString());v.put("deathtouch",c.hasKeyword(forge.game.keyword.Keyword.DEATHTOUCH));
        v.put("keywords",c.getKeywords().stream().map(forge.game.keyword.KeywordInterface::getOriginal).toList());v.put("chosenType",c.getChosenType());v.put("chosenPlayer",c.getChosenPlayer()==null?0:ForgeBridge.game.getPlayers().indexOf(c.getChosenPlayer())+1);v.put("viewChosenType",String.valueOf(c.getView().getChosenType()));v.put("viewChosenPlayer",c.getView().getChosenPlayer()==null?0:c.getView().getChosenPlayer().getId()+1);v.put("abilities",c.isInZone(ZoneType.Library)?List.of():c.getSpellAbilities().stream().map(RevealChosenPayloadFixture::ability).toList());v.put("viewText",c.getView().getText());return v;
    }
    static Object field(Object o,Class<?> type,String name)throws Exception {var f=type.getDeclaredField(name);f.setAccessible(true);return f.get(o);}
    static Map<String,Object> observe(long id,ForgeBridge.Pending r)throws Exception {
        Map<String,Object> v=new LinkedHashMap<>();v.put("requestId",id);List<Object> players=new ArrayList<>();
        for(Player p:ForgeBridge.game.getPlayers()) {
            Map<String,Object> zones=new LinkedHashMap<>();for(ZoneType z:new ZoneType[]{ZoneType.Hand,ZoneType.Library,ZoneType.Battlefield,ZoneType.Graveyard,ZoneType.Exile,ZoneType.Stack})zones.put(z.name(),p.getCardsIn(z).stream().map(RevealChosenPayloadFixture::card).toList());
            List<String> keywords=new ArrayList<>();for(var kw:p.getKeywords())keywords.add(kw.getOriginal());Map<String,Object> pv=new LinkedHashMap<>();pv.put("seat",ForgeBridge.game.getPlayers().indexOf(p)+1);pv.put("life",p.getLife());pv.put("zones",zones);pv.put("mana",DrainManaDeltaFixture.pool(p));pv.put("keywords",keywords);players.add(pv);
        }v.put("players",players);List<Object> stack=new ArrayList<>();for(var si:ForgeBridge.game.getStack()){var a=ability(si.getSpellAbility());a.put("instance",si.getId());stack.add(a);}v.put("stack",stack);
        var input=r.gui.controller.getInputQueue().getInput();Map<String,Object> payment=new LinkedHashMap<>();
        if(input instanceof InputPayMana){payment.put("ability",ability((SpellAbility)field(input,InputPayMana.class,"saPaidFor")));payment.put("remaining",field(input,InputPayMana.class,"manaCost").toString());}
        v.put("payment",payment);List<Object> costs=new ArrayList<>();for(var e:ForgeBridge.game.costPaymentStack)costs.add(Map.of("part",e.cost().getClass().getSimpleName(),"snapshot",field(e.payment(),forge.game.cost.CostPayment.class,"cancellationSnapshot")!=null));v.put("costs",costs);
        var combat=ForgeBridge.game.getCombat();List<Object> attackers=new ArrayList<>();if(combat!=null)for(Card c:combat.getAttackers()){var defender=combat.getDefenderByAttacker(c);attackers.add(Map.of("id",c.getId(),"controller",ForgeBridge.game.getPlayers().indexOf(c.getController())+1,"defenderSeat",defender instanceof Player p?ForgeBridge.game.getPlayers().indexOf(p)+1:0));}v.put("attackers",attackers);synchronized(paidEvents){v.put("paidEvents",List.copyOf(paidEvents));}v.put("turn",ForgeBridge.game.getPhaseHandler().getTurn());v.put("turnSeat",ForgeBridge.game.getPlayers().indexOf(ForgeBridge.game.getPhaseHandler().getPlayerTurn())+1);v.put("phase",ForgeBridge.game.getPhaseHandler().getPhase().name());return v;
    }
    static final class Events {
        @com.google.common.eventbus.Subscribe public void cast(forge.game.event.GameEventSpellAbilityCast e) {
            for(var si:ForgeBridge.game.getStack())if(si.getSpellAbility().getId()==e.sa().getId()){paidEvents.add(ability(si.getSpellAbility()));break;}
        }
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()-> {
            for(int i=0;i<ForgeBridge.game.getPlayers().size();i++) {
                Player p=ForgeBridge.game.getPlayers().get(i);RoyalStateFixture.setup(p,ForgeBridge.config.getAsJsonArray("positions").get(i).getAsJsonObject());p.getController().getFullControl().add(PlayerController.FullControlFlag.ChooseCostOrder);
            }
            ForgeBridge.game.getAction().checkStaticAbilities();ForgeBridge.game.subscribeToEvents(new Events());
            Thread observer=new Thread(()->{Set<Long> seen=new HashSet<>();while(!ForgeBridge.finished)try {synchronized(ForgeBridge.class){var r=ForgeBridge.pending;if(r!=null&&!r.consumed&&!seen.contains(r.requestId)) {
                Path dir=Path.of(ForgeBridge.config.get("auditDir").getAsString()),tmp=dir.resolve(r.requestId+".tmp"),dst=dir.resolve(r.requestId+".json");Files.writeString(tmp,ForgeBridge.JSON.toJson(observe(r.requestId,r)));Files.move(tmp,dst,StandardCopyOption.REPLACE_EXISTING);seen.add(r.requestId);
            }}Thread.sleep(5);}catch(Exception e){e.printStackTrace();throw new RuntimeException(e);}},"reveal-chosen-read-only-observer");observer.setDaemon(true);observer.start();
        },registered->{
            // A third native human is only needed for Leonin's nonchosen attacker.
            // Attach its ordinary Bridge input controller during game creation;
            // this is fixture wiring, not production multiplayer integration.
            if(ForgeBridge.config.getAsJsonArray("positions").size()>2) {
                var deck=new forge.deck.Deck("Native Leonin negative seat3");ForgeBridge.loadSection(deck.getMain(),ForgeBridge.config.getAsJsonArray("decks").get(2).getAsJsonObject().getAsJsonArray("cards"));
                var lobby=new forge.player.LobbyPlayerHuman("Player 3") {
                    @Override public Player createIngamePlayer(forge.game.Game game,int id) {
                        Player player=new Player(getName(),game,id);var controller=new ForgeBridge.BridgeController(game,player,this);player.setFirstController(controller);var handler=new ForgeBridge.Gui(3);handler.controller=controller;controller.setGui((forge.gui.interfaces.IGuiGame)Proxy.newProxyInstance(forge.gui.interfaces.IGuiGame.class.getClassLoader(),new Class<?>[]{forge.gui.interfaces.IGuiGame.class},handler));
                        Thread wiring=new Thread(()->{while(!ForgeBridge.finished)try {synchronized(ForgeBridge.class){if(ForgeBridge.guis.size()==2){ForgeBridge.guis.add(handler);return;}}Thread.sleep(1);}catch(InterruptedException e){throw new RuntimeException(e);}},"initial-third-native-human-wiring");wiring.setDaemon(true);wiring.start();return player;
                    }
                };
                var extra=new forge.game.player.RegisteredPlayer(deck).setPlayer(lobby);extra.setStartingLife(ForgeBridge.config.get("startingLife").getAsInt());registered.add(extra);
            }
        });
    }
}

