package mtgb.bridge;
import forge.game.card.Card;
import forge.game.player.Player;
import forge.game.zone.ZoneType;
import forge.card.CardStateName;
import forge.model.FModel;

/** Starting position only; activation, copies, priority and expiry run natively. */
public final class TemporaryCopyFixture {
    static Card place(String name,Player player,ZoneType zone) {
        Card card=Card.fromPaperCard(FModel.getMagicDb().getCommonCards().getCard(name),player);
        player.getZone(zone).add(card);card.setGameTimestamp(ForgeBridge.game.getNextTimestamp());card.setSickness(false);return card;
    }
    public static void main(String[] args)throws Exception {
        ForgeBridge.run(args,()->{
            Player one=ForgeBridge.game.getPlayers().get(0),two=ForgeBridge.game.getPlayers().get(1);
            String test=ForgeBridge.config.get("fixture").getAsString();
            Card bear=place("Grizzly Bears",one,ZoneType.Battlefield);
            // Adapter/privacy checks are separate from the actual copy operations below.
            Card privateSource=place("Ephemerate",two,ZoneType.Hand);
            var privateTrigger=forge.game.trigger.TriggerHandler.parseTrigger("Mode$ Phase | Phase$ End Of Turn | TriggerDescription$ SECRET-DEPARTURE",privateSource,false);
            privateTrigger.setOverridingAbility(forge.game.ability.AbilityFactory.getAbility("DB$ SacrificeAll | Defined$ DelayTriggerRememberedLKI",privateSource));
            privateTrigger.addRemembered(bear);
            if(!"".equals(ForgePendingDeparture.forCard(bear,java.util.List.of(privateTrigger)).get(0) instanceof java.util.Map<?,?> map?map.get("description"):null))throw new AssertionError("Private departure source leaked");
            privateTrigger.getMapParams().put("Condition", "SomeNativeCondition");
            if("".equals(ForgePendingDeparture.instruction(privateTrigger).get("condition")))throw new AssertionError("Conditional timing translated as unconditional");
            two.getZone(ZoneType.Hand).remove(privateSource);
            for(int i=0;i<6;i++)place("Mountain",one,ZoneType.Battlefield);
            for(int i=0;i<4;i++)place("Island",one,ZoneType.Battlefield);
            if(test.equals("exile"))place("Heat Shimmer",one,ZoneType.Hand);
            else {
                int count=test.equals("reflection-twice")?2:1;
                for(int i=0;i<count;i++) {
                    Card reflection=place("Fable of the Mirror-Breaker",one,ZoneType.Battlefield);
                    reflection.setState(CardStateName.Backside,true);reflection.setBackSide(true);
                }
                if(count==2)place("Quasiduplicate",one,ZoneType.Hand);
                else {place("Stifle",two,ZoneType.Hand);place("Island",two,ZoneType.Battlefield);}
            }
            ForgeBridge.game.getAction().checkStaticAbilities();
        });
    }
}
