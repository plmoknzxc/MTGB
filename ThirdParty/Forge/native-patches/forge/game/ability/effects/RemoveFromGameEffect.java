package forge.game.ability.effects;

import forge.game.ability.SpellAbilityEffect;
import forge.game.card.Card;
import forge.game.spellability.SpellAbility;

public class RemoveFromGameEffect extends SpellAbilityEffect {

    @Override
    public void resolve(SpellAbility sa) {
        for (final Card tgtC : getTargetCards(sa)) {
            // A preceding zone-change child leaves targets pointing at their LKI.
            // Remove the actual current object, not the old object in its old zone.
            final Card current = tgtC.getGame().getCardState(tgtC, null);
            if (current != null) {
                current.getGame().getAction().ceaseToExist(current, true);
            }
        }

    }
}
