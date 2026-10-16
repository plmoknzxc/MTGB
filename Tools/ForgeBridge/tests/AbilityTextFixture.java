package mtgb.bridge;

import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.trigger.WrappedAbility;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.nio.file.*;
import java.util.*;

/** Read-only projection boundaries. These are not effect-resolution proofs. */
public final class AbilityTextFixture {
    static Card place(String name, Player owner, ZoneType zone) {
        Card card=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),owner);
        owner.getZone(zone).add(card);return card;
    }
    static void require(boolean value,String message) { if(!value)throw new AssertionError(message); }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player owner=ForgeBridge.game.getPlayers().get(0);
            List<Object> samples=new ArrayList<>();
            Card white=place("White Plume Adventurer",owner,ZoneType.Battlefield);
            var trigger=white.getTriggers().iterator().next();var execute=trigger.ensureAbility();
            var wrapped=new WrappedAbility(trigger,execute,owner);
            var entry=new LinkedHashMap<String,Object>();BridgeAbilityText.project(entry,wrapped);
            require(String.valueOf(entry.get("rulesText")).contains("take the initiative"),"Wrapped trigger recipe empty");
            require(String.valueOf(entry.get("rulesText")).contains("CARDNAME"),"Self identity baked into lookup text");
            require(((Map<?,?>)entry.get("rulesSource")).get("name").equals(white.getName()),"Wrong source face");
            samples.add(Map.of("case","wrapped","projection",entry,"source",ForgeBridge.card(white)));

            Card bear=place("Grizzly Bears",owner,ZoneType.Battlefield);
            var copiedTrigger=trigger.copy(bear,false);var copiedExecute=execute.copy(bear,false);
            var copied=new WrappedAbility(copiedTrigger,copiedExecute,owner);
            var copiedEntry=new LinkedHashMap<String,Object>();BridgeAbilityText.project(copiedEntry,copied);
            require(((Map<?,?>)copiedEntry.get("rulesSource")).get("name").equals(white.getName()),"Copied trigger lost original face");
            require(copied.getHostCard()==bear,"Projection changed copied host");
            samples.add(Map.of("case","copied-trait","projection",copiedEntry,"source",ForgeBridge.card(bear)));

            Card mantle=place("Paradise Mantle",owner,ZoneType.Battlefield);
            var granted=forge.game.ability.AbilityFactory.getAbility(mantle.getSVar("AnyMana"),bear);
            granted.setGrantorStatic(mantle.getStaticAbilities().iterator().next());
            var grantedEntry=new LinkedHashMap<String,Object>();BridgeAbilityText.project(grantedEntry,granted);
            require(((Map<?,?>)grantedEntry.get("rulesSource")).get("name").equals("Paradise Mantle"),"Granted ability lost grantor face");
            samples.add(Map.of("case","granted-ability","projection",grantedEntry,"source",ForgeBridge.card(bear)));
            owner.getZone(ZoneType.Battlefield).remove(mantle);owner.getZone(ZoneType.Hand).add(mantle);
            var secretGrantor=new LinkedHashMap<String,Object>();BridgeAbilityText.project(secretGrantor,granted);
            require(!secretGrantor.containsKey("rulesSource")&&"".equals(secretGrantor.get("rulesText")),"Concealed static grantor disclosed");

            owner.getZone(ZoneType.Battlefield).remove(white);owner.getZone(ZoneType.Hand).add(white);
            var hidden=new LinkedHashMap<String,Object>();BridgeAbilityText.project(hidden,copied);
            require(!hidden.containsKey("rulesSource")&&"".equals(hidden.get("rulesText")),"Concealed origin disclosed");
            samples.add(Map.of("case","concealed-origin","projection",hidden));
            bear.turnFaceDown(true);
            var faceDown=new LinkedHashMap<String,Object>();BridgeAbilityText.project(faceDown,copied);
            require(faceDown.isEmpty(),"Face-down host disclosed");

            Card student=place("Student of Elements",owner,ZoneType.Battlefield);
            var frontTrigger=student.getTriggers().iterator().next();var front=new WrappedAbility(frontTrigger,frontTrigger.ensureAbility(),owner);
            student.setState(forge.card.CardStateName.Flipped,true);
            var flipped=new LinkedHashMap<String,Object>();BridgeAbilityText.project(flipped,front);
            require(((Map<?,?>)flipped.get("rulesSource")).get("name").equals("Student of Elements"),"Old trigger incorrectly rebound to flipped face");
            samples.add(Map.of("case","changed-face","projection",flipped,"source",ForgeBridge.card(student)));
            try { Files.writeString(Path.of(ForgeBridge.config.get("auditDir").getAsString(),"projection.json"),ForgeBridge.JSON.toJson(Map.of("status","PASS","samples",samples,"faceDownFiltered",true))); }
            catch(Exception error) {throw new RuntimeException(error);}
            System.exit(0);
        });
    }
}
