package mtgb.bridge;

import forge.ai.ComputerUtilMana;
import forge.ai.PlayerControllerAi;
import forge.card.mana.ManaCost;
import forge.card.mana.ManaCostShard;
import forge.game.GameActionUtil;
import forge.game.card.*;
import forge.game.cost.*;
import forge.game.mana.ManaCostBeingPaid;
import forge.game.player.Player;
import forge.game.spellability.*;
import forge.game.zone.ZoneType;
import forge.game.staticability.StaticAbilityLayer;
import java.util.*;

/** Lowest native mana proposal, never payment or a commitment to substitutions. */
final class ForgeCardCostPreview {
    private record Preview(String text, int mana, boolean payable) {}
    static final ThreadLocal<Player> viewer = new ThreadLocal<>();

    static boolean hasVariableSacrificeReduction(Card card) {
        return card.getSpellAbilities().stream().anyMatch(a -> variableSacrifice(a) != null)
            && card.getStaticAbilities().stream().anyMatch(a -> a.checkMode(forge.game.staticability.StaticAbilityMode.ReduceCost)
                && a.hasParam("Relative") && "Card.Self".equals(a.getParam("ValidCard")));
    }
    private static CostSacrifice variableSacrifice(SpellAbility ability) {
        if (!ability.isSpell() || ability.getPayCosts() == null) return null;
        return ability.getPayCosts().getCostParts().stream()
            .filter(p -> p instanceof CostSacrifice && "X".equals(p.getAmount()))
            .map(p -> (CostSacrifice)p).findFirst().orElse(null);
    }
    static boolean canPayMinimum(Card card, Player payer) {
        for (var ability : card.getAllPossibleAbilities(payer, true)) {
            if (!ability.isSpell() || !ForgeActionLegality.hasLegalTargets(ability)) continue;
            Preview result = minimum(ability, payer, true, true);
            if (result != null && result.payable) return true;
        }
        return false;
    }
    static boolean canPayMinimum(SpellAbility ability, Player payer) {
        Preview result = minimum(ability, payer, false, true);
        return result != null && result.payable;
    }
    static String forCard(Card card) { return viewer.get() == null ? null : forCard(card, viewer.get()); }
    static String forCard(Card card, Player payer) {
        Preview result = null;
        if (canPreview(card, payer)) {
            var original = card.getSpellAbilities().stream().filter(a -> a.isSpell() && !a.isAdventure() && !a.isOmen()).findFirst().orElse(null);
            result = original == null ? null : minimum(original, payer, true, false);
        }
        // A card offered beside the hand may only be usable through a zone
        // activation. Its printed spell cost is not the cost of that action.
        if (result == null && !card.isFaceDown() && card.getZone() != null && card.getOwner() == payer) {
            ZoneType zone = card.getZone().getZoneType();
            if (zone == ZoneType.Graveyard || zone == ZoneType.Exile)
                for (var ability : card.getAllSpellAbilities()) {
                    if (!ability.isActivatedAbility() || ability.isManaAbility() || ability.getRestrictions().getZone() != zone) continue;
                    Preview activation = minimum(ability, payer, false, false);
                    if (activation != null && (result == null || activation.mana < result.mana)) result = activation;
                }
        }
        return result == null ? null : result.text;
    }
    static String knownExileFace(Card card, Player payer) {
        if(!card.isFaceDown()||!card.isInZone(ZoneType.Exile)
            ||!card.getView().canFaceDownBeShownTo(payer.getView()))return null;
        Preview best=null;
        for(var ability:card.getAllPossibleAbilities(payer,false)) {
            if(!ability.isSpell()||ability.getRestrictions().getZone()!=ZoneType.Exile)continue;
            Preview value=minimum(ability,payer,false,false);
            if(value!=null&&(best==null||value.mana<best.mana))best=value;
        }
        return best==null?null:best.text;
    }
    static String adventure(Card card) { return viewer.get() == null ? null : adventure(card, viewer.get()); }
    static String adventure(Card card, Player payer) {
        if (!canPreview(card, payer)) return null;
        var spell = card.getAllSpellAbilities().stream().filter(SpellAbility::isAdventure).findFirst().orElse(null);
        Preview result = spell == null ? null : minimum(spell, payer, true, false);
        return result == null ? null : result.text;
    }
    static String secondary(Card card) {
        Player payer = viewer.get();
        if (payer == null) return null;
        if (!canPreview(card, payer)) return null;
        var state = card.hasState(forge.card.CardStateName.Backside) ? forge.card.CardStateName.Backside
            : card.hasState(forge.card.CardStateName.Secondary) ? forge.card.CardStateName.Secondary
            : card.hasState(forge.card.CardStateName.RightSplit) ? forge.card.CardStateName.RightSplit : null;
        if (state == null) return null;
        var spell = card.getState(state).getSpellAbilities().stream().filter(SpellAbility::isSpell).findFirst().orElse(null);
        Preview result = spell == null ? null : minimum(spell, payer, true, false);
        return result == null ? null : result.text;
    }
    private static boolean canPreview(Card card, Player payer) {
        if (card.isFaceDown() || card.getZone() == null) return false;
        ZoneType zone = card.getZone().getZoneType();
        if (!List.of(ZoneType.Hand, ZoneType.Graveyard, ZoneType.Exile, ZoneType.Library, ZoneType.Command, ZoneType.Sideboard).contains(zone)) return false;
        if (card.mayPlay(payer).stream().anyMatch(o -> o.grantsZonePermissions())) return true;
        return card.getOwner() == payer && (zone == ZoneType.Hand || zone == ZoneType.Command && card.isCommander()
            || card.getAllSpellAbilities().stream().anyMatch(a -> a.isSpell() && a.getRestrictions().getZone() == zone)
            || (zone==ZoneType.Exile||zone==ZoneType.Graveyard)
                &&card.getAllPossibleAbilities(payer,false).stream().anyMatch(a->a.isSpell()&&a.getRestrictions().getZone()==zone));
    }
    static String forAbility(SpellAbility original) {
        if ((!original.isSpell() && (!original.isActivatedAbility() || original.isManaAbility()))
                || original.getHostCard().isFaceDown()) return null;
        Player payer = original.getActivatingPlayer();
        Preview result = minimum(original, payer == null ? original.getHostCard().getOwner() : payer, false, false);
        return result == null ? null : result.text;
    }

    private static Preview minimum(SpellAbility original, Player payer, boolean alternatives, boolean affordability) {
        if (original.getPayCosts() == null) return null;
        Card alternateHost = original.getAlternateHost(original.getHostCard());
        if (alternateHost != null) original = copyForPreview(original, alternateHost, payer);
        Card host = original.getHostCard();
        // Keep native prediction scratch state scoped to this projection.
        // A preview must not announce or pay any cost.
        var pips = new ArrayList<>(original.getPipsToReduce());
        var convoked = new CardCollection(original.getTappedForConvoke());
        var castFrom = host.getCastFrom();
        var used = new LinkedHashMap<Card,Boolean>();
        for (Card c : payer.getCardsIn(ZoneType.Battlefield)) used.put(c, c.isUsedToPay());
        Preview[] best = {null}; boolean[] payable = {false};
        try {
            var base = copyForPreview(original, payer);
            base.removeParam("AIPhyrexianPayment");base.putParam("MTGBHumanAffordability", "True");
            if (!host.isInZone(ZoneType.Stack)) host.setCastFrom(host.getZone());
            List<SpellAbility> abilities = new ArrayList<>();
            if (!alternatives || host.getZone() == null || original.getRestrictions().getZone() == host.getZone().getZoneType()) abilities.add(base);
            if (alternatives) for (var alternate : host.getAllPossibleAbilities(payer, false))
                if (alternate.isSpell() && alternate.getCardStateName() == original.getCardStateName()
                        && abilities.stream().noneMatch(a -> a.getPayCosts().toString().equals(alternate.getPayCosts().toString())
                            && a.getAlternativeCost() == alternate.getAlternativeCost())) abilities.add(copyForPreview(alternate, payer));
            var targeted = new ArrayList<SpellAbility>();
            for (var ability : abilities) targeted.addAll(targetPlans(ability, payer));
            for (var ability : targeted) {
                restorePips(ability, pips);
                var reductions = GameActionUtil.getOptionalCostValues(ability).stream()
                    .filter(v -> v.getType().name().startsWith("Reduce") || v.getType() == OptionalCost.Offering).toList();
                var variants = new ArrayList<List<OptionalCostValue>>();variants.add(List.of());
                Set<String> seen = new HashSet<>();
                for (var reduction : reductions) for (var previous : new ArrayList<>(variants)) {
                    var choices = new ArrayList<>(previous);choices.add(reduction);
                    restorePips(ability, pips);
                    var intent = GameActionUtil.addOptionalCosts(copyForPreview(ability, payer), choices);
                    if (!intent.getPayCosts().canPay(intent, payer, false)) continue;
                    String key = intent.getPayCosts().toString() + "/" + intent.getPipsToReduce();
                    if (seen.add(key)) variants.add(choices);
                }
                for (var choices : variants) {
                  List<Card> sacrificePlans = new ArrayList<>();sacrificePlans.add(null);
                  int maximumX = 0;
                  var variable = variableSacrifice(ability);
                  if (variable != null && hasVariableSacrificeReduction(host)) {
                      Integer maximum = variable.getMaxAmountX(ability, payer, false);
                      maximumX = maximum == null ? 0 : maximum;
                  }
                  for (int x = maximumX; x >= (affordability ? 0 : maximumX); x--)
                  for (int plan = 0; plan < sacrificePlans.size(); plan++) for (boolean preserveMana : new boolean[]{false, true}) {
                    restorePips(ability, pips);
                    var intent = GameActionUtil.addOptionalCosts(copyForPreview(ability, payer), choices);
                    var sacrifice = variableSacrifice(intent);
                    if (sacrifice != null && hasVariableSacrificeReduction(host)) {
                        intent.setXManaCostPaid(x);
                    }
                    var controller = new PreviewController(payer, preserveMana, sacrificePlans.get(plan));
                    if (affordability && sacrifice != null && hasVariableSacrificeReduction(host))
                        controller.reserveSacrifice(intent, sacrifice);
                    payer.runWithController(() -> withStackProjection(intent,payer,projected -> {
                        var raised = CostAdjustment.adjust(projected.getPayCosts().copy(), projected, false);
                        if (!raised.canPay(projected, payer, false)) return;
                        ManaCost mana = raised.getCostMana() == null ? ManaCost.ZERO : raised.getCostMana().getManaCostFor(projected);
                        if (mana.isNoCost()) return;
                        var cost = new ManaCostBeingPaid(mana);
                        if (!CostAdjustment.adjust(cost, projected, payer, new CardCollection(), true, false)) return;
                        if (projected.isOffering() && projected.getSacrificedAsOffering() == null
                                || projected.isEmerge() && projected.getSacrificedAsEmerge() == null) return;
                        String text = cost.isPaid() && cost.getXcounter() == 0 ? "{0}" : cost.toManaCost().toString();
                        int amount = cost.getConvertedManaCost();
                        if (best[0] == null || amount < best[0].mana) best[0] = new Preview(text, amount, false);
                        // Do not count the same permanent as both a substitution
                        // and a mana source in this predictive payment plan.
                        try {
                            for (Card c : controller.reserved) c.setUsedToPay(true);
                            if (affordability && controller.sacrificeFeasible) payable[0] |= ComputerUtilMana.canPayManaCost(cost, projected, payer, false);
                        } finally { for (Card c : used.keySet()) c.setUsedToPay(used.get(c)); }
                    }), controller);
                    for (Card candidate : controller.sacrificeOptions) if (!sacrificePlans.contains(candidate)) sacrificePlans.add(candidate);
                    if (affordability && payable[0]) return new Preview(best[0].text, best[0].mana, true);
                  }
                }
            }
            return best[0] == null ? null : new Preview(best[0].text, best[0].mana, payable[0]);
        } finally {
            restorePips(original, pips);original.clearTappedForConvoke();for (Card c : convoked) original.addTappedForConvoke(c);
            host.setCastFrom(castFrom);for (Card c : used.keySet()) c.setUsedToPay(used.get(c));
        }
    }
    private static void withStackProjection(SpellAbility intent,Player payer,java.util.function.Consumer<SpellAbility> action) {
        var game=payer.getGame();var host=intent.getHostCard();
        if(!intent.isSpell()||host.isInZone(ZoneType.Stack)
            ||!game.getAction().hasStaticAbilityAffectingZone(ZoneType.Stack,StaticAbilityLayer.ABILITIES)) {
            action.accept(intent);return;
        }
        // Mirror native stack-keyword forecasting on an LKI card. Zone-bound
        // permissions can grant affinity only after the chosen spell is cast.
        var copy=CardCopyService.getLKICopy(host);
        var projected=copyForPreview(intent,copy,payer);
        projected.clearPipsToReduce();projected.getPipsToReduce().addAll(intent.getPipsToReduce());
        projected.setXManaCostPaid(intent.getXManaCostPaid());
        copy.setLastKnownZone(game.getStackZone());copy.setCastFrom(host.getZone());copy.setCastSA(projected);
        game.getTracker().freeze();
        try {
            copy.clearStaticChangedCardKeywords(false);
            game.getAction().checkStaticAbilities(false,new HashSet<>(List.of(copy)),new CardCollection(copy));
            action.accept(projected);
        } finally {
            game.getAction().checkStaticAbilities(false);
            game.getTracker().clearDelayed();game.getTracker().unfreeze();
        }
    }
    private static SpellAbility copyForPreview(SpellAbility original, Player payer) {
        return copyForPreview(original, original.getHostCard(), payer);
    }
    private static SpellAbility copyForPreview(SpellAbility original, Card host, Player payer) {
        var copy = original.copy(host, payer, false);
        // Native ordinary copies reset runtime parameters to printed values.
        // Keep effect-added payment modifiers, as native addOptionalCosts does.
        for (String key : List.of("ReduceCost", "ReduceAmount", "RaiseCost"))
            if (original.hasParam(key)) copy.putParam(key, original.getParam(key));
        return copy;
    }
    private static void restorePips(SpellAbility ability, List<String> pips) {
        ability.clearPipsToReduce();ability.getPipsToReduce().addAll(pips);
    }
    private static List<SpellAbility> targetPlans(SpellAbility original, Player payer) {
        var sources = new CardCollection(payer.getGame().getCardsIn(ZoneType.Battlefield));
        sources.addAll(payer.getGame().getCardsIn(ZoneType.Command));sources.add(original.getHostCard());
        var conditions = sources.stream().flatMap(c -> c.getStaticAbilities().stream())
            .filter(a -> a.hasParam("ValidTarget") && (a.checkMode(forge.game.staticability.StaticAbilityMode.ReduceCost)
                || a.checkMode(forge.game.staticability.StaticAbilityMode.RaiseCost))).toList();
        if (conditions.isEmpty()) return List.of(original);
        var result = new ArrayList<SpellAbility>();
        var ordinary = copyForPreview(original, payer);
        if (completeTargets(ordinary)) result.add(ordinary);
        int index = 0;
        for (var sub = original; sub != null; sub = sub.getSubAbility(), index++) {
            if (!sub.usesTargeting() || !sub.getTargets().isEmpty() || sub.getMaxTargets() == 0) continue;
            Set<String> seen = new HashSet<>();
            for (var candidate : targetCandidates(sub)) {
                String signature = conditions.stream().map(a -> Boolean.toString(a.matchesValidParam("ValidTarget", candidate))).toList().toString();
                if (!seen.add(signature)) continue;
                var plan = copyForPreview(original, payer);var chosen = plan;
                for (int i = 0; i < index; i++) chosen = chosen.getSubAbility();
                chosen.getTargets().add(candidate);
                if (completeTargets(plan)) result.add(plan);
            }
        }
        return result;
    }
    private static List<forge.game.GameObject> targetCandidates(SpellAbility ability) {
        var candidates = new ArrayList<forge.game.GameObject>(ability.getTargetRestrictions().getAllCandidates(ability));
        for (var item : ability.getActivatingPlayer().getGame().getStack())
            if (ability.canTarget(item.getSpellAbility())) candidates.add(item.getSpellAbility());
        return candidates;
    }
    private static boolean completeTargets(SpellAbility ability) {
        for (var sub = ability; sub != null; sub = sub.getSubAbility()) if (sub.usesTargeting()) {
            for (var candidate : targetCandidates(sub)) {
                if (sub.getTargets().size() >= sub.getMinTargets()) break;
                if (candidate instanceof forge.game.GameEntity entity ? sub.canTarget(entity)
                        : sub.canTarget((SpellAbility)candidate)) sub.getTargets().add(candidate);
            }
            if (!sub.isTargetNumberValid()) return false;
        }
        return true;
    }
    private static final class PreviewController extends PlayerControllerAi {
        final boolean preserveMana;
        final Card sacrificePlan;
        final Set<Card> reserved = new LinkedHashSet<>();
        final Set<Card> sacrificeOptions = new LinkedHashSet<>();
        PreviewController(Player payer, boolean preserveMana, Card sacrificePlan) {
            super(payer.getGame(), payer, payer.getOriginalLobbyPlayer());this.preserveMana = preserveMana;this.sacrificePlan = sacrificePlan;
        }
        boolean sacrificeFeasible = true;
        void reserveSacrifice(SpellAbility intent, CostSacrifice sacrifice) {
            String type = sacrifice.getType();
            boolean different = type.contains("+WithDifferentNames");
            type = type.replace("+WithDifferentNames", "");
            var valid = payerCards(intent, type);
            // Mana can be floated before sacrificing a permanent. Only exclude
            // sources whose mana activation consumes the same resource first.
            var chosen = valid.stream().sorted(Comparator.comparingInt(c -> consumesSelfForMana(c) ? 1 : 0)).toList();
            Set<String> names = new HashSet<>();int remaining = intent.getXManaCostPaid();
            for (Card c : chosen) {
                if (remaining == 0) break;
                if (different && !names.add(c.getName())) continue;
                if (consumesSelfForMana(c)) reserved.add(c);
                remaining--;
            }
            sacrificeFeasible = remaining == 0;
        }
        private CardCollection payerCards(SpellAbility intent, String type) {
            var cards = intent.getActivatingPlayer().getCardsIn(ZoneType.Battlefield);
            var valid = type.contains("X") ? new CardCollection(cards)
                : CardLists.getValidCards(cards, type.split(";"), intent.getActivatingPlayer(), intent.getHostCard(), intent);
            return CardLists.filter(valid, CardPredicates.canBeSacrificedBy(intent, false));
        }
        private boolean consumesSelfForMana(Card card) {
            return card.getManaAbilities().stream().anyMatch(a -> a.getPayCosts() != null
                && a.getPayCosts().getCostParts().stream().anyMatch(p -> p instanceof CostSacrifice && p.payCostFromSource()
                    || p instanceof CostExile && p.payCostFromSource()));
        }
        @Override public CardCollectionView chooseCardsToDelve(int generic, CardCollection grave) {
            return new CardCollection(grave.stream().limit(generic).toList());
        }
        @Override public CardCollectionView choosePermanentsToSacrifice(SpellAbility sa, int min, int max, CardCollectionView valid, String message) {
            sacrificeOptions.addAll(valid);
            Card best = sacrificePlan != null ? valid.contains(sacrificePlan) ? sacrificePlan : null
                : valid.stream().filter(c -> !preserveMana || c.getManaAbilities().isEmpty()).max(Comparator.comparingInt(Card::getCMC)).orElse(null);
            if (best == null) return CardCollection.EMPTY;
            reserved.add(best);return new CardCollection(best);
        }
        @Override public Map<Card,ManaCostShard> chooseCardsForConvokeOrImprovise(SpellAbility sa, ManaCost cost, CardCollectionView untapped, boolean artifacts, boolean creatures, Integer maxReduction) {
            var cards = new ArrayList<>(untapped.stream().filter(c -> !preserveMana || c.getManaAbilities().isEmpty()).toList());
            // Match colored pips before generic payment. Augmenting paths keep a
            // multicolor creature available when a single-color one can replace
            // it, rather than letting candidate order overstate the minimum.
            var slots = new ArrayList<ManaCostShard>();
            if (creatures && !artifacts) for (var shard : cost)
                if (!shard.isSnow() && !shard.isColorless() && shard.getColorMask() != 0) slots.add(shard);
            Map<Card,Integer> matched = new LinkedHashMap<>();
            for (int i = 0; i < slots.size(); i++) match(i, slots, cards, matched, new HashSet<>());
            var choices = new LinkedHashMap<Card,ManaCostShard>();
            for (var entry : matched.entrySet()) choices.put(entry.getKey(), slots.get(entry.getValue()));
            int generic = cost.getGenericCost();
            for (Card c : cards) if (!choices.containsKey(c) && generic > 0) {choices.put(c, ManaCostShard.GENERIC);generic--;}
            var result = new LinkedHashMap<Card,ManaCostShard>();
            choices.entrySet().stream().sorted(Comparator.comparingInt(e -> e.getKey().getId()))
                .limit(maxReduction == null ? choices.size() : maxReduction).forEach(e -> result.put(e.getKey(), e.getValue()));
            reserved.addAll(result.keySet());return result;
        }
        private boolean match(int slot, List<ManaCostShard> slots, List<Card> cards, Map<Card,Integer> matched, Set<Card> visited) {
            for (Card c : cards) if ((c.getColor().getColor() & slots.get(slot).getColorMask()) != 0 && visited.add(c)) {
                Integer previous = matched.get(c);
                if (previous == null || match(previous, slots, cards, matched, visited)) {matched.put(c, slot);return true;}
            }
            return false;
        }
    }
}
