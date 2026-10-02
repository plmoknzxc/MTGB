package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.game.trigger.Trigger;
import forge.game.trigger.TriggerHandler;
import forge.game.ability.AbilityFactory;
import forge.game.spellability.SpellAbility;
import forge.game.phase.ExtraPhase;
import forge.game.phase.PhaseType;
import java.util.*;
public final class MarkerCoverageFixture {
 static Trigger make(Card source,String name) {
  Trigger t=TriggerHandler.parseTrigger("Mode$ Phase | Phase$ End Of Turn | TriggerDescription$ "+name,source,false);
  SpellAbility sa=AbilityFactory.getAbility("DB$ Draw | NumCards$ 1",source);sa.setActivatingPlayer(source.getController());
  t.setSpawningAbility(sa);t.setOverridingAbility(sa);return t;
 }
 public static void main(String[] args)throws Exception {
  ForgeBridge.run(args,()->{
   Player p=ForgeBridge.game.getPlayers().get(0);Card source=EffectMarkersFixture.place("Grizzly Bears",p,ZoneType.Battlefield);
   var handler=ForgeBridge.game.getTriggerHandler();
   Trigger active=make(source,"coverage-active"),thisTurn=make(source,"coverage-this-turn"),player=make(source,"coverage-player"),future=make(source,"coverage-future"),extra=make(source,"coverage-extra-turn"),phase=make(source,"coverage-extra-phase"),mana=make(source,"coverage-no-spawning");
   mana.setSpawningAbility(null);
   handler.registerDelayedTrigger(active);handler.registerThisTurnDelayedTrigger(thisTurn);handler.registerPlayerDefinedDelayedTrigger(p,player);
   ForgeBridge.game.getCleanup().addUntil(()->handler.registerThisTurnDelayedTrigger(future));
   ForgeBridge.game.getPhaseHandler().addExtraTurn(p).addTrigger(extra);
   try {
    var field=ForgeBridge.game.getPhaseHandler().getClass().getDeclaredField("extraPhases");field.setAccessible(true);
    var map=(Map<PhaseType,Stack<ExtraPhase>>)field.get(ForgeBridge.game.getPhaseHandler());var ep=new ExtraPhase(PhaseType.MAIN2);ep.addTrigger(phase);var stack=new Stack<ExtraPhase>();stack.add(ep);map.put(PhaseType.MAIN1,stack);
   }catch(Exception e){throw new RuntimeException(e);}
   handler.registerDelayedTrigger(mana);
   Card hidden=EffectMarkersFixture.place("Ephemerate",p,ZoneType.Hand);handler.registerDelayedTrigger(make(hidden,"SECRET-NAME"));
   p.createMonarchEffect("CN2");p.createInitiativeEffect("CLB");p.createRadiationEffect("PIP");
   Card generic=new Card(ForgeBridge.game.nextCardId(),ForgeBridge.game);generic.setName("Secret Ephemerate effect");generic.setOwner(p);generic.setGamePieceType(forge.card.GamePieceType.EFFECT);generic.setEffectSource(hidden);p.getZone(ZoneType.Command).add(generic);
   if(Boolean.getBoolean("mtgb.specialStates")) {
    p.setCounters(forge.game.card.CounterType.getType("POISON"),2);
    p.setCounters(forge.game.card.CounterType.getType("ENERGY"),3);
    p.setCounters(forge.game.card.CounterType.getType("EXPERIENCE"),4);
    p.setCounters(forge.game.card.CounterType.getType("RAD"),5);
    p.setCounters(forge.game.card.CounterType.getType("TICKET"),6);
    p.setBlessing(true,"RIX");p.setEnduringStory(true,"HOB");
    p.createTheRing("LTR");p.setRingLevel(1);p.setNumRingTemptedYou(1);
    p.setSpeed(2);p.createSpeedEffect();ForgeBridge.game.setDayTime(false);
    Card dungeon=forge.game.card.CardFactory.getCard(forge.StaticData.instance().getAllTokens().getToken("lost_mine_of_phandelver","AFR"),p,ForgeBridge.game);
    dungeon.setGamePieceType(forge.card.GamePieceType.DUNGEON);p.getZone(ZoneType.Command).add(dungeon);dungeon.setCurrentRoom("Cave Entrance");
   }
  });
 }
}


