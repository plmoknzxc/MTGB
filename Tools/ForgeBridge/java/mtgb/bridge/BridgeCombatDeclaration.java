// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import forge.game.card.Card;
import forge.game.combat.Combat;
import forge.game.combat.CombatUtil;
import forge.game.keyword.Keyword;
import forge.game.player.Player;
import forge.game.staticability.StaticAbilityBlockRestrict;
import forge.game.staticability.StaticAbilityCantAttackBlock;
import forge.game.staticability.StaticAbilityMode;
import forge.game.zone.ZoneType;
import java.util.ArrayList;
import java.util.List;

/** Read-only explanation of the current draft. Forge still owns declaration and payment. */
final class BridgeCombatDeclaration {
    record Destination(String kind, int id) {}
    record BlockLimit(int id, int min, int max, boolean unblockable, String requiredIfAble) {}
    List<Destination> destinations;
    List<BlockLimit> limits;
    String issue;
    int cardId, amount, seatLimit;

    static BridgeCombatDeclaration attack(Combat combat) {
        var result = new BridgeCombatDeclaration();
        result.destinations = combat.getDefenders().stream().map(d ->
                new Destination(d instanceof Player ? "player" : "card", d.getId())).toList();
        var constraints = combat.getAttackConstraints();
        var draft = combat.getAttackersAndDefenders();
        var global = constraints.getGlobalRestrictions();
        if (global.getMax() != null && global.getMax() < Integer.MAX_VALUE) result.seatLimit = global.getMax();
        int violations = constraints.countViolations(draft);
        if (violations == 0) return result;
        if (violations < 0) {
            for (var card : draft.keySet()) {
                var restriction = constraints.getRestrictions().get(card);
                if (restriction == null) continue;
                var unmet = restriction.getViolation(draft);
                if (!unmet.isEmpty()) {
                    result.issue = unmet.iterator().next().name();
                    result.cardId = card.getId();
                    return result;
                }
            }
            result.issue = global.getMax() != null && draft.size() > global.getMax() ? "attackMax" : "attackRestriction";
            return result;
        }
        var best = constraints.getLegalAttackers();
        if (violations <= best.getRight()) return result;
        result.issue = "attackRequirement";
        // A legal example can contain just one of several competing mandatory attackers.
        // Do not name that example's creature as the only way to finish the declaration.
        if (best.getRight() > 0 && constraints.getRequirements().values().stream()
                .filter(r -> r.hasRequirement() && !draft.containsKey(r.getAttacker())).limit(2).count() > 1)
            return result;
        for (var entry : best.getLeft().entrySet()) {
            Card card = entry.getKey();
            var requirement = constraints.getRequirements().get(card);
            if (requirement != null && requirement.countViolations(draft.get(card), draft) >
                    requirement.countViolations(entry.getValue(), best.getLeft())) {
                result.cardId = card.getId();
                if (!draft.containsKey(card)) result.issue = "mustAttack";
                break;
            }
        }
        return result;
    }

    static BridgeCombatDeclaration block(Combat combat, Player defender) {
        var result = new BridgeCombatDeclaration();
        int seatCap = StaticAbilityBlockRestrict.blockRestrictNum(defender);
        if (seatCap < Integer.MAX_VALUE) result.seatLimit = seatCap;
        result.limits = new ArrayList<>();
        // In this Forge revision cantBlockBy(card, null) always rejects the null blocker.
        // Distinguish a blanket restriction from merely having no matching creature this turn.
        var blanketRestrictions = defender.getGame().getCardsIn(ZoneType.STATIC_ABILITIES_SOURCE_ZONES).stream()
                .flatMap(c -> c.getStaticAbilities().stream())
                .filter(a -> a.checkConditions(StaticAbilityMode.CantBlockBy) && !a.isKeyword(Keyword.LANDWALK)
                        && !a.hasParam("ValidBlocker") && !a.hasParam("ValidBlockerRelative") && !a.hasParam("ValidAttackerRelative")
                        && a.matchesValidParam("ValidDefender", defender)).toList();
        for (Card card : combat.getAttackers()) {
            Player victim = combat.getDefendingPlayerRelatedTo(card);
            if (victim == null || !victim.sharesTeamWith(defender)) continue;
            var range = StaticAbilityCantAttackBlock.getMinMaxBlocker(card, defender);
            boolean immune = range.getRight() == 0 || range.getLeft() > range.getRight()
                    || (!defender.getGame().getRules().hasSharedTeamTurns() && range.getLeft() > seatCap)
                    || blanketRestrictions.stream().anyMatch(a -> a.matchesValidParam("ValidAttacker", card));
            String required = card.hasKeyword("All creatures able to block CARDNAME do so.") ? "all"
                    : card.hasStartOfKeyword("CARDNAME must be blocked by two or more creatures if able.") ? "two"
                    : card.hasStartOfKeyword("CARDNAME must be blocked by exactly one creature if able.") ? "exactlyOne"
                    : card.hasStartOfKeyword("CARDNAME must be blocked if able.") ? "one" : null;
            if (immune || range.getLeft() > 1 || range.getRight() < Integer.MAX_VALUE || required != null)
                result.limits.add(new BlockLimit(card.getId(), range.getLeft(),
                        range.getRight() == Integer.MAX_VALUE ? -1 : range.getRight(), immune, required));
        }
        String error = CombatUtil.validateBlocks(combat, defender);
        if (error == null) return result;
        result.issue = "blockRestriction";
        // Match only the exact subject and diagnostics returned by our pinned native validator.
        for (Card card : defender.getCreaturesInPlay()) {
            String prefix = card.toString() + " ";
            if (!error.startsWith(prefix)) continue;
            String reason = error.substring(prefix.length());
            result.cardId = card.getId();
            if (reason.startsWith("must block an attacker,")) {
                result.issue = "blockRequirement";
                result.cardId = 0;
            } else if (reason.startsWith("must ")) result.issue = "mustBlock";
            else if (reason.equals("can't block alone.")) result.issue = "blockTogether";
            else if (reason.contains("two other creatures block")) result.issue = "blockWithTwo";
            else if (reason.contains("greater power")) result.issue = "blockWithGreater";
            return result;
        }
        for (var limit : result.limits) {
            Card attacker = combat.getAttackers().stream().filter(c -> c.getId() == limit.id()).findFirst().orElseThrow();
            if (!error.startsWith(attacker.toString() + " cannot be blocked with ")) continue;
            result.cardId = attacker.getId();
            int count = combat.getBlockers(attacker).size();
            result.issue = count < limit.min() ? "blockMin" : "blockMax";
            result.amount = count < limit.min() ? limit.min() : limit.max();
            break;
        }
        return result;
    }
}
