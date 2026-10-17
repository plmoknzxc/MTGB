package mtgb.bridge;

import forge.game.card.Card;
import forge.game.zone.ZoneType;
import java.util.Map;

/** Public state only; IDs avoid recursive card graphs and preserve zone visibility. */
final class ForgePublicCardDetails {
    static void add(Card card,Map<String,Object> out) {
        if(card.isFaceDown())return;
        if(card.isInZone(ZoneType.Battlefield)) {
            var controller=card.getController();
            if(hasCondition(card,"Threshold")) {
                out.put("qualificationKind","threshold");out.put("qualificationCount",controller.getCardsIn(ZoneType.Graveyard).size());
                out.put("qualificationRequired",7);out.put("qualificationEarned",controller.hasThreshold());
            } else if(hasCondition(card,"Delirium")) {
                out.put("qualificationKind","delirium");out.put("qualificationCount",forge.game.ability.AbilityUtils.countCardTypesFromList(controller.getCardsIn(ZoneType.Graveyard),false));
                out.put("qualificationRequired",4);out.put("qualificationEarned",controller.hasDelirium());
            } else if(hasCondition(card,"Metalcraft")) {
                out.put("qualificationKind","metalcraft");out.put("qualificationCount",controller.getCardsIn(ZoneType.Battlefield).stream().filter(Card::isArtifact).count());
                out.put("qualificationRequired",3);out.put("qualificationEarned",controller.hasMetalcraft());
            }
            var linked=card.getUntilLeavesBattlefield().stream()
                .filter(c->c.isInZone(ZoneType.Exile)).map(Card::getId).toList();
            if(!linked.isEmpty())out.put("temporaryExileIds",linked);
            boolean sacrifices=card.getSVars().values().stream()
                .anyMatch(s->s.equals("PlayerCountPropertyYou$SacrificedThisTurn Permanent"));
            boolean threshold=card.getSVars().values().stream().anyMatch(s->s.contains("ConditionSVarCompare$ GE3"));
            if(sacrifices&&threshold) {
                int count=(int)card.getController().getSacrificedThisTurn().stream().filter(Card::isPermanent).count();
                out.put("qualificationKind","sacrificedThisTurn");out.put("qualificationCount",count);
                out.put("qualificationRequired",3);out.put("qualificationEarned",count>=3);
            }
        }
        var cast=card.getCastSA();
        if(card.wasCast()&&!card.isCopiedSpell()&&cast!=null&&!cast.getPayingMana().isEmpty()) {
            StringBuilder symbols=new StringBuilder();
            for(var mana:cast.getPayingMana())symbols.append('{').append(forge.card.MagicColor.toShortString(mana.getColor())).append('}');
            out.put("spentMana",symbols.toString());
        }
    }
    private static boolean hasCondition(Card card,String condition) {
        return card.getStaticAbilities().stream().anyMatch(a->condition.equals(a.getParam("Condition")))
            ||card.getTriggers().stream().anyMatch(a->condition.equals(a.getParam("Condition")))
            ||card.getSpellAbilities().stream().anyMatch(a->condition.equals(a.getParam("Condition"))||"True".equals(a.getParam("Activation"+condition)));
    }
}
