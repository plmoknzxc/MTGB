package mtgb.bridge;

import forge.game.card.Card;
import forge.game.zone.ZoneType;
import forge.model.FModel;
import java.util.*;

/** Zero-effect adapter: exact temporary native confirm argument, no spell resolution. */
public final class GeneratedConfirmPreviewFixture {
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            var player=ForgeBridge.game.getPlayers().get(0);
            Card source=RoyalStateFixture.place("Garth One-Eye",player,ZoneType.Battlefield);
            var ability=source.getSpellAbilities().get(0);ability.setActivatingPlayer(player);
            Card copy=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getUniqueByName("Shivan Dragon"),player);
            copy.setCopiedPermanent(copy);copy.setGamePieceType(forge.card.GamePieceType.TOKEN);
            copy.setZone(player.getZone(ZoneType.None));
            boolean inCollection=ForgeBridge.game.getCardsIn(ZoneType.None).contains(copy);
            var gui=ForgeBridge.guis.get(0);Map<Integer,Card> previous=new HashMap<>(gui.choiceObjects);
            List<Boolean> answers=new ArrayList<>();
            for(int i=0;i<2;i++) {
                answers.add(player.getController().confirmAction(ability,null,"Do you want to play Shivan Dragon?",copy,null));
                if(inCollection||!gui.choiceObjects.equals(previous))throw new AssertionError("Exact detached copy/context cleanup");
            }
            ForgeBridge.WIRE.println(ForgeBridge.JSON.toJson(Map.of("type","adapter-result","answers",answers,"id",copy.getId(),"detached",true,"contextRestored",true,"effectOperations",0)));
        });
    }
}
