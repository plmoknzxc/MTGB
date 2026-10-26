package mtgb.bridge;

import forge.game.spellability.SpellAbility;
import java.util.*;

/** Receipt of the committed stack ability; never read pending payment intent. */
final class ForgeStackPayment {
    static Map<String,Object> project(SpellAbility ability) {
        var root=ability.getRootAbility();
        Map<String,Object> receipt=new LinkedHashMap<>();
        if(root.isCopied())return Map.of("cost","","manaCost","","spentMana","","lifePaid",0);
        receipt.put("cost",root.getHostCard().isFaceDown()?"":root.getPayCosts()==null?"":root.getPayCosts().toSimpleString());
        receipt.put("manaCost",root.getHostCard().isFaceDown()?"":root.getSVar("MTGBCastingManaCost"));
        StringBuilder mana=new StringBuilder();
        for(var paid:root.getPayingMana())mana.append('{').append(forge.card.MagicColor.toShortString(paid.getColor())).append('}');
        receipt.put("spentMana",mana.toString());receipt.put("lifePaid",root.getAmountLifePaid());
        return receipt;
    }
}
