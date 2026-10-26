// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import forge.game.CardTraitBase;
import forge.game.card.Card;
import forge.game.card.CardState;
import forge.game.spellability.SpellAbility;
import java.util.LinkedHashMap;
import java.util.Map;

/** Public display provenance only. Never changes abilities, targets or decisions. */
final class BridgeAbilityText {
    private BridgeAbilityText() { }
    // Selecting later targets of one spell is still casting that spell. A
    // description of an unpriced subability can evaluate Converge/X as zero.
    static SpellAbility targetingPresentation(SpellAbility ability) {
        if (ability == null) return null;
        var root = ability.getRootAbility();
        return root != null && root.isSpell() ? root : ability;
    }
    static CardState origin(SpellAbility ability) {
        if (ability.getGrantorStatic() != null) return ability.getGrantorStatic().getCardState();
        CardTraitBase trait = ability.getTrigger() != null ? ability.getTrigger() : ability;
        return trait.getCardState();
    }
    static boolean concealedOrigin(SpellAbility ability) {
        var state = origin(ability);
        return state != null && state.getCard() != ability.getHostCard() && !ForgeBridge.publicMarkerSource(state.getCard());
    }

    static String rules(SpellAbility ability) {
        if (ability == null || ability.getHostCard().isFaceDown() || concealedOrigin(ability)) return "";
        var trigger = ability.getTrigger();
        if (trigger != null && trigger.hasParam("TriggerDescription")) {
            // WrappedAbility's executing subability often has no description.
            // The registered trigger owns the complete live condition/effect.
            // Keep CARDNAME bound to the recorded source face, rather than
            // baking a copied/granted ability's new host into the lookup text.
            return trigger.replaceAbilityText(trigger.getParam("TriggerDescription"), ability);
        }
        String text = ability.hasParam("SpellDescription") ? ability.getParam("SpellDescription") : ability.getDescription();
        return text == null ? "" : text;
    }

    static Object source(SpellAbility ability) {
        if (ability == null || ability.getHostCard().isFaceDown()) return null;
        var state = origin(ability);
        if (state == null) return null;
        Card origin = state.getCard();
        // A public ability never authorizes revealing a concealed grantor.
        if (origin == null || origin.isFaceDown() || origin != ability.getHostCard() && !ForgeBridge.publicMarkerSource(origin)) return null;
        if (!(origin.getPaperCard() instanceof forge.item.PaperCard paper)) return null;
        Map<String,Object> face = new LinkedHashMap<>();
        face.put("name", state.getName()); face.put("originalName", state.getName());
        face.put("printingKey", ForgeCatalog.key(paper));
        face.put("stateName", state.getStateName().name());
        face.put("originalRules", state.getOracleText()); face.put("currentRules", state.getOracleText());
        face.put("currentType", state.getType().toString());
        return face;
    }

    static void project(Map<String,Object> entry, SpellAbility ability) {
        if (ability == null || ability.getHostCard().isFaceDown()) return;
        entry.put("rulesText", rules(ability));
        Object origin = source(ability);
        if (origin != null) entry.put("rulesSource", origin);
    }
}
