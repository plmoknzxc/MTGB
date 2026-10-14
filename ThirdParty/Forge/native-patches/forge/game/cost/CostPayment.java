/*
 * Forge: Play Magic: the Gathering.
 * Copyright (C) 2011  Forge Team
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */
package forge.game.cost;

import com.google.common.collect.Lists;
import com.google.common.collect.Maps;
import forge.card.MagicColor;
import forge.card.mana.ManaCostShard;
import forge.game.Game;
import forge.game.ability.AbilityKey;
import forge.game.card.Card;
import forge.game.card.CardCollection;
import forge.game.card.CardZoneTable;
import forge.game.mana.*;
import forge.game.player.Player;
import forge.game.spellability.SpellAbility;
import org.apache.commons.lang3.tuple.Pair;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;

/**
 * <p>
 * Cost_Payment class.
 * </p>
 * 
 * @author Forge
 * @version $Id$
 */
public class CostPayment extends ManaConversionMatrix {
    private final Cost cost;
    private Cost adjustedCost;
    private final SpellAbility ability;
    private final List<CostPart> paidCostParts = Lists.newArrayList();
    private forge.game.GameSnapshot cancellationSnapshot;
    private boolean cancellationRestored;
    private final Map<Integer, com.google.common.collect.Multiset<forge.game.card.CounterType>> cardCounters = Maps.newHashMap();
    private final Map<Player, List<Integer>> battlefieldOrder = Maps.newHashMap();
    private final Map<Player, List<Integer>> libraryOrder = Maps.newHashMap();
    private final Map<Player, List<Integer>> graveyardOrder = Maps.newHashMap();
    private final Map<Player, List<Integer>> beholdHandOrder = Maps.newHashMap();
    private final Map<Integer, Integer> exertCounts = Maps.newHashMap();
    private final Map<Integer, List<Player>> exertPlayers = Maps.newHashMap();
    private final Map<Player, int[]> spellHistory = Maps.newHashMap();
    private final Map<Integer, com.google.common.collect.Table<Long, Long, List<String>>> hiddenKeywords = Maps.newHashMap();
    // Retain the announcing object: a source sacrifice creates a new zone object,
    // while this host still carries its choices and legally revealed CardView.
    private Card originalRevealChosenHost;
    private List<String> originalChosenColors;
    private List<forge.game.spellability.SpellAbilityStackInstance> originalStack;
    private LibraryPaymentEvents libraryPaymentEvents;

    // Only subscribed for the live payment, after making its snapshot. A library
    // can return to the same ID order after irreversible moves or shuffles.
    public static final class LibraryPaymentEvents {
        private boolean irreversible;
        @com.google.common.eventbus.Subscribe
        public void onShuffle(forge.game.event.GameEventShuffle event) {
            irreversible = true;
        }
        @com.google.common.eventbus.Subscribe
        public void onZoneChange(forge.game.event.GameEventCardChangeZone event) {
            if (event.to() != null && event.to().zoneType() == forge.game.zone.ZoneType.Library
                    || event.from() != null && event.from().zoneType() == forge.game.zone.ZoneType.Library
                        && (event.to() == null || event.to().zoneType() != forge.game.zone.ZoneType.Stack))
                irreversible = true;
        }
    }

    // This transaction is local to one announced ability, never the priority-loop snapshot.
    // Restore before rollbackAbility returns the announced spell to its original zone.
    public void restoreCancelledPayment() {
        if (cancellationSnapshot == null || cancellationRestored) return;
        // CR 733.1: library movements during payment cannot be reversed.
        // Retain Forge's existing irreversible-payment path for these transactions.
        for (var entry : libraryOrder.entrySet()) {
            List<Integer> current = new ArrayList<>();
            for (Card card : entry.getKey().getZone(forge.game.zone.ZoneType.Library).getCards())
                current.add(card.getId());
            if (libraryPaymentEvents != null && libraryPaymentEvents.irreversible
                    || !current.equals(entry.getValue())) {
                cardCounters.clear();
                cancellationSnapshot = null;
                battlefieldOrder.clear();
                libraryOrder.clear();
        graveyardOrder.clear();
        beholdHandOrder.clear();
        exertCounts.clear();
        exertPlayers.clear();
        spellHistory.clear();
        hiddenKeywords.clear();
        originalRevealChosenHost = null;
        originalChosenColors = null;
        originalStack = null;
                return;
            }
        }
        cancellationSnapshot.restoreGameState(ability.getActivatingPlayer().getGame());
        Game game = ability.getActivatingPlayer().getGame();
        // Native snapshot player assignment increments the game/achievement cast
        // counters. Restore their exact opening values without casting a spell.
        for (var entry : spellHistory.entrySet()) {
            Player player = entry.getKey();
            int[] history = entry.getValue();
            player.resetSpellCastThisGame();
            for (int i = 0; i < history[0]; i++) player.addSpellCastThisTurn();
            player.getAchievementTracker().spellsCast = history[1];
            player.getAchievementTracker().maxStormCount = history[2];
        }
        for (var entry : hiddenKeywords.entrySet()) {
            Card card = game.findById(entry.getKey());
            if (card != null) card.restoreHiddenKeywordsForCancelledPayment(entry.getValue());
        }
        for (var entry : cardCounters.entrySet()) {
            Card card = game.findById(entry.getKey());
            if (card != null) card.setCounters(com.google.common.collect.HashMultiset.create(entry.getValue()));
        }
        for (var entry : exertCounts.entrySet()) {
            Card card = game.findById(entry.getKey());
            if (card != null) card.restoreExertStateForCancelledPayment(entry.getValue(), exertPlayers.get(entry.getKey()));
        }
        if (originalChosenColors != null) {
            Card source = game.findById(ability.getHostCard().getId());
            if (source != null) source.setChosenColors(new ArrayList<>(originalChosenColors));
        }
        if (originalRevealChosenHost != null) {
            Card source = game.findById(originalRevealChosenHost.getId());
            if (source != null) {
                source.setSecretChosenType(originalRevealChosenHost.getChosenType());
                source.setSecretChosenPlayer(originalRevealChosenHost.getChosenPlayer());
                source.setTurnInController(originalRevealChosenHost.getTurnInController());
                // Undo resources, never knowledge disclosed by a legal reveal.
                if (originalRevealChosenHost.getView().getChosenType() != null
                        && !originalRevealChosenHost.getView().getChosenType().isEmpty())
                    source.revealChosenType();
                if (originalRevealChosenHost.getView().getChosenPlayer() != null)
                    source.revealChosenPlayer();
            }
        }
        if (originalStack != null) game.getStack().restoreStackForCancelledPayment(originalStack);
        game.getAction().checkStaticAbilities();
        cardCounters.clear();
        // Snapshot zone restoration appends returned hand resources. Preserve the
        // player's original hand arrangement for this new exile-cost branch.
        for (var entry : beholdHandOrder.entrySet()) {
            var zone = entry.getKey().getZone(forge.game.zone.ZoneType.Hand);
            for (int i = 0; i < entry.getValue().size(); i++)
                zone.reorder(game.findById(entry.getValue().get(i)), i);
        }
        for (var entry : graveyardOrder.entrySet()) {
            var zone = entry.getKey().getZone(forge.game.zone.ZoneType.Graveyard);
            for (int i = 0; i < entry.getValue().size(); i++)
                zone.reorder(game.findById(entry.getValue().get(i)), i);
        }
        for (var entry : battlefieldOrder.entrySet()) {
            var zone = entry.getKey().getZone(forge.game.zone.ZoneType.Battlefield);
            for (int i = 0; i < entry.getValue().size(); i++)
                zone.reorder(game.findById(entry.getValue().get(i)), i);
            entry.getKey().updateAllZonesForView();
        }
        battlefieldOrder.clear();
        libraryOrder.clear();
        graveyardOrder.clear();
        beholdHandOrder.clear();
        exertCounts.clear();
        exertPlayers.clear();
        spellHistory.clear();
        hiddenKeywords.clear();
        originalRevealChosenHost = null;
        originalChosenColors = null;
        originalStack = null;
        cancellationSnapshot = null;
        cancellationRestored = true;
        for (CostPart part : paidCostParts)
            if (part instanceof CostPartWithList listCost) listCost.resetLists();
        paidCostParts.clear();
        ability.getPayingMana().clear();
        ability.getPayingManaAbilities().clear();
    }

    /**
     * <p>
     * Getter for the field <code>cost</code>.
     * </p>
     * 
     * @return a {@link forge.game.cost.Cost} object.
     */
    public final Cost getCost() {
        return this.cost;
    }

    public final SpellAbility getAbility() {
        return this.ability;
    }

    /**
     * <p>
     * Constructor for Cost_Payment.
     * </p>
     * 
     * @param cost
     *            a {@link forge.game.cost.Cost} object.
     * @param abil
     *            a {@link forge.game.spellability.SpellAbility} object.
     */
    public CostPayment(final Cost cost, final SpellAbility abil) {
        this.cost = cost;
        this.adjustedCost = cost;
        this.ability = abil;
        restoreColorReplacements();
    }

    /**
     * <p>
     * canPayAdditionalCosts.
     * </p>
     * 
     * @param cost
     *            a {@link forge.game.cost.Cost} object.
     * @param ability
     *            a {@link forge.game.spellability.SpellAbility} object.
     * @return a boolean.
     */
    public static boolean canPayAdditionalCosts(Cost cost, final SpellAbility ability, final boolean effect) {
        return canPayAdditionalCosts(cost, ability, effect, ability.getActivatingPlayer());
    }
    public static boolean canPayAdditionalCosts(Cost cost, final SpellAbility ability, final boolean effect, final Player payer) {
        if (cost == null) {
            return true;
        }

        cost = CostAdjustment.adjust(cost, ability, effect);
        return cost.canPay(ability, payer, effect);
    }

    /**
     * <p>
     * isAllPaid.
     * </p>
     * 
     * @return a boolean.
     */
    public final boolean isFullyPaid() {
        return paidCostParts.containsAll(adjustedCost.getCostParts());
    }

    /**
     * <p>
     * cancelPayment.
     * </p>
     */
    public final void refundPayment() {
        if (cancellationRestored) return; // Pool, life and tapped sources already restored together.
        Card sourceCard = this.ability.getHostCard();
        for (final CostPart part : this.paidCostParts) {
            part.refund(sourceCard);
            // Clear lists to prevent accumulation across multiple cancelled activations
            if (part instanceof CostPartWithList) {
                ((CostPartWithList) part).resetLists();
            }
        }

        new ManaRefundService(this.ability).refundManaPaid();
    }

    public boolean payCost(final CostDecisionMakerBase decisionMaker) {
        adjustedCost = CostAdjustment.adjust(cost, ability, decisionMaker.isEffect());
        List<CostPart> costParts = adjustedCost.getCostPartsWithZeroMana();

        if (adjustedCost.getCostParts().size() > 1) {
            // if mana part is shown here it wouldn't include reductions, but that's just a minor inconvenience
            costParts = decisionMaker.getPlayer().getController().orderCosts(costParts);
        }

        final Game game = decisionMaker.getPlayer().getGame();
        // Effect payments are separate irreversible resolution choices. Only announcing
        // a spell/activation can roll back the entire attempted payment.
        final boolean assistAnnouncement = ability.isSpell()
                && ability.getHostCard().hasKeyword(forge.game.keyword.Keyword.ASSIST);
        if (!decisionMaker.isEffect() && (costParts.size() > 1 || assistAnnouncement)
                && costParts.stream().allMatch(part -> part instanceof CostPartMana
                     || part instanceof CostTap || part instanceof CostUntap
                     || part instanceof CostTapType || part instanceof CostUntapType
                     || part instanceof CostPayLife || part instanceof CostPayEnergy
                     // Ordinary hand/exile/public reveals do not move resources.
                     // Derived behold/exile costs retain their separate review scope.
                     || (part.getClass() == CostReveal.class
                         && !((CostReveal) part).getRevealFrom().contains(forge.game.zone.ZoneType.Library))
                     || part.getClass() == CostRevealChosen.class
                     || part instanceof CostChooseColor
                     || part instanceof CostDiscard || part instanceof CostSacrifice
                     || part instanceof CostExile || part instanceof CostReturn
                     || part instanceof CostRemoveCounter || part instanceof CostRemoveAnyCounter
                     || part instanceof CostPutCounter || part instanceof CostUnattach
                     || part instanceof CostExiledMoveToGrave
                     || part instanceof CostCollectEvidence || part instanceof CostForage
                     || part instanceof CostExert || part instanceof CostExileFromStack
                     || part.getClass() == CostBeholdExile.class)
                && (assistAnnouncement || costParts.stream().anyMatch(
                part -> part instanceof CostDiscard || part instanceof CostSacrifice
                     || part instanceof CostExile || part instanceof CostReturn
                     || part instanceof CostRemoveCounter || part instanceof CostRemoveAnyCounter
                     || part instanceof CostPutCounter || part instanceof CostUnattach
                     || part instanceof CostExiledMoveToGrave
                     || part instanceof CostCollectEvidence || part instanceof CostForage
                     || part instanceof CostExert || part instanceof CostExileFromStack
                     || part.getClass() == CostBeholdExile.class))) {
            if (costParts.stream().anyMatch(part -> part instanceof CostChooseColor))
                originalChosenColors = Lists.newArrayList(ability.getHostCard().getChosenColors());
            if (costParts.stream().anyMatch(part -> part.getClass() == CostRevealChosen.class))
                originalRevealChosenHost = ability.getHostCard();
            for (Player player : game.getPlayers())
                spellHistory.put(player, new int[] {player.getSpellsCastThisGame(),
                        player.getAchievementTracker().spellsCast, player.getAchievementTracker().maxStormCount});
            for (Card card : game.getCardsInGame())
                hiddenKeywords.put(card.getId(), card.snapshotHiddenKeywordsForCancelledPayment());
            cancellationSnapshot = new forge.game.GameSnapshot(game);
            if (assistAnnouncement || costParts.stream().anyMatch(part -> part instanceof CostExileFromStack)) {
                originalStack = new ArrayList<>();
                for (var entry : game.getStack()) originalStack.add(entry);
                // Copy card zones/resources but never re-cast existing spells into the
                // cloned stack. This keeps cast history/triggers out of cancellation.
                cancellationSnapshot.makeCopy(null, false);
            } else {
                cancellationSnapshot.makeCopy();
            }
            // Only the newly supported exert payment can change these two fields.
            // Native snapshots do not preserve them, and clearing all exert would
            // incorrectly erase a prior exert or another player's untap marker.
            if (costParts.stream().anyMatch(part -> part instanceof CostExert)) {
                for (Card card : game.getCardsInGame()) {
                    exertCounts.put(card.getId(), card.getExertedThisTurn());
                    List<Player> players = new ArrayList<>();
                    for (Player player : game.getPlayers())
                        if (card.isExertedBy(player)) players.add(player);
                    exertPlayers.put(card.getId(), players);
                }
            }
            for (Card card : game.getCardsInGame())
                cardCounters.put(card.getId(), com.google.common.collect.HashMultiset.create(card.getCounters()));
            for (Player player : game.getPlayers()) {
                List<Integer> ids = new ArrayList<>();
                for (Card card : player.getZone(forge.game.zone.ZoneType.Battlefield).getCards())
                    ids.add(card.getId());
                battlefieldOrder.put(player, ids);
                List<Integer> libraryIds = new ArrayList<>();
                for (Card card : player.getZone(forge.game.zone.ZoneType.Library).getCards())
                    libraryIds.add(card.getId());
                libraryOrder.put(player, libraryIds);
                List<Integer> graveyardIds = new ArrayList<>();
                for (Card card : player.getZone(forge.game.zone.ZoneType.Graveyard).getCards())
                    graveyardIds.add(card.getId());
                graveyardOrder.put(player, graveyardIds);
                if (costParts.stream().anyMatch(part -> part.getClass() == CostBeholdExile.class || part instanceof CostExile && ((CostExile) part).getFrom().contains(forge.game.zone.ZoneType.Hand) || part instanceof CostChooseColor)) {
                    List<Integer> handIds = new ArrayList<>();
                    for (Card card : player.getZone(forge.game.zone.ZoneType.Hand).getCards())
                        handIds.add(card.getId());
                    beholdHandOrder.put(player, handIds);
                }
            }
        }

        if (cancellationSnapshot != null) {
            libraryPaymentEvents = new LibraryPaymentEvents();
            game.subscribeToEvents(libraryPaymentEvents);
        }
        try {
        for (final CostPart part : costParts) {
            // Wrap the cost and push onto the cost stack
            try {
                game.costPaymentStack.push(part, this);

                PaymentDecision pd = part.accept(decisionMaker);

                // Right before we start paying as decided, we need to transfer the CostPayments matrix over?
                if (pd != null) {
                    pd.matrix = this;
                }

                if (pd == null || !part.payAsDecided(decisionMaker.getPlayer(), pd, ability, decisionMaker.isEffect())) {
                    return false;
                }
                this.paidCostParts.add(part);
            } finally {
                game.costPaymentStack.pop(); // cost is resolved
            }
        }
        } finally {
            if (libraryPaymentEvents != null) game.unsubscribeFromEvents(libraryPaymentEvents);
        }

        cardCounters.clear();
        battlefieldOrder.clear();
        libraryOrder.clear();
        graveyardOrder.clear();
        beholdHandOrder.clear();
        exertCounts.clear();
        exertPlayers.clear();
        spellHistory.clear();
        hiddenKeywords.clear();
        originalRevealChosenHost = null;
        originalChosenColors = null;
        originalStack = null;
        cancellationSnapshot = null; // A successful announcement commits the cost.
        // clear lists used for undo
        for (final CostPart part : this.paidCostParts) {
            if (part instanceof CostPartWithList listCost) {
                listCost.resetLists();
            }
        }

        return true;
    }

    public final boolean payComputerCosts(final CostDecisionMakerBase decisionMaker) {
        // Just in case it wasn't set, but honestly it shouldn't have gotten
        // here without being set
        if (this.ability.getActivatingPlayer() == null) {
            this.ability.setActivatingPlayer(decisionMaker.getPlayer());
        }

        Map<CostPart, PaymentDecision> decisions = Maps.newHashMap();
        // for Trinisphere make sure to include Zero
        List<CostPart> parts = CostAdjustment.adjust(cost, ability, decisionMaker.isEffect()).getCostPartsWithZeroMana();

        // Set all of the decisions before attempting to pay anything

        final Game game = decisionMaker.getPlayer().getGame();

        for (final CostPart part : parts) {
            PaymentDecision decision = part.accept(decisionMaker);
            if (null == decision) return false;

            try {
                // wrap the payment and push onto the cost stack
                game.costPaymentStack.push(part, this);
                if (decisionMaker.paysRightAfterDecision() && !part.payAsDecided(decisionMaker.getPlayer(), decision, ability, decisionMaker.isEffect())) {
                    return false;
                }
            } finally {
                game.costPaymentStack.pop(); // cost is either paid or deferred
            }
            decisions.put(part, decision);
        }

        for (final CostPart part : parts) {
            // wrap the payment and push onto the cost stack
            try {
                game.costPaymentStack.push(part, this);

                if (!part.payAsDecided(decisionMaker.getPlayer(), decisions.get(part), this.ability, decisionMaker.isEffect())) {
                    return false;
                }
                // abilities care what was used to pay for them
                if (part instanceof CostPartWithList) {
                    ((CostPartWithList) part).resetLists();
                }
            } finally {
                game.costPaymentStack.pop(); // cost is resolved
            }
        }
        return true;
    }

    /**
     * <p>
     * getManaFrom.
     * </p>
     *
     * @param saBeingPaidFor
     *            a {@link forge.game.spellability.SpellAbility} object.
     * @return a {@link forge.game.mana.Mana} object.
     */
    public static Mana getMana(final Player player, final ManaCostShard shard, final SpellAbility saBeingPaidFor,
            final byte colorsPaid, Map<String, Integer> xManaCostPaidByColor) {
        final List<Pair<Mana, Integer>> weightedOptions = selectManaToPayFor(player.getManaPool(), shard,
            saBeingPaidFor, colorsPaid, xManaCostPaidByColor);

        // Exclude border case
        if (weightedOptions.isEmpty()) {
            return null; // There is no matching mana in the pool
        }

        // select equal weight possibilities
        List<Mana> manaChoices = new ArrayList<>();
        int bestWeight = Integer.MIN_VALUE;
        for (Pair<Mana, Integer> option : weightedOptions) {
            int thisWeight = option.getRight();
            Mana thisMana = option.getLeft();

            if (thisWeight > bestWeight) {
                manaChoices.clear();
                bestWeight = thisWeight;
            }

            if (thisWeight == bestWeight) {
                // add only distinct Mana-s
                boolean haveDuplicate = false;
                for (Mana m : manaChoices) {
                    if (m.equals(thisMana)) {
                        haveDuplicate = true;
                        break;
                    }
                }
                if (!haveDuplicate) {
                    manaChoices.add(thisMana);
                }
            }
        }

        // got an only one best option?
        if (manaChoices.size() == 1) {
            return manaChoices.get(0);
        }

        // Let them choose then
        return player.getController().chooseManaFromPool(manaChoices);
    }

    private static List<Pair<Mana, Integer>> selectManaToPayFor(final ManaPool manapool, final ManaCostShard shard,
            final SpellAbility saBeingPaidFor, final byte colorsPaid, Map<String, Integer> xManaCostPaidByColor) {
        final List<Pair<Mana, Integer>> weightedOptions = new ArrayList<>();
        for (final Mana thisMana : Lists.newArrayList(manapool)) {
            if (shard == ManaCostShard.COLORED_X && !ManaCostBeingPaid.canColoredXShardBePaidByColor(MagicColor.toShortString(thisMana.getColor()), xManaCostPaidByColor)) {
                continue;
            }

            if (!manapool.canPayForShardWithColor(shard, thisMana.getColor())) {
                continue;
            }

            if (shard.isSnow() && !thisMana.isSnow()) {
                continue;
            }

            if (thisMana.getManaAbility() != null && !thisMana.getManaAbility().meetsSpellAndShardRestrictions(saBeingPaidFor, shard, thisMana.getColor())) {
                continue;
            }

            if (!saBeingPaidFor.allowsPayingWithShard(thisMana.getSourceCard(), thisMana.getColor())) {
                continue;
            }

            int weight = 0;
            if (colorsPaid == -1) {
                // prefer colorless mana to spend
                weight += thisMana.isColorless() ? 5 : 0;
            } else {
                // get more colors for converge
                weight += (thisMana.getColor() | colorsPaid) != colorsPaid ? 5 : 0;
            }

            // prefer restricted mana to spend
            if (thisMana.isRestricted()) {
                weight += 2;
            }

            // Spend non-snow mana first
            if (!thisMana.isSnow()) {
                weight += 1;
            }

            weightedOptions.add(Pair.of(thisMana, weight));
        }
        return weightedOptions;
    }

    public static boolean handleOfferings(final SpellAbility sa, boolean test, boolean costIsPaid) {
        final Game game = sa.getHostCard().getGame();
        final CardZoneTable table = new CardZoneTable(game.getLastStateBattlefield(), game.getLastStateGraveyard());
        Map<AbilityKey, Object> params = AbilityKey.newMap();
        AbilityKey.addCardZoneTableParams(params, table);

        if (sa.isOffering()) {
            if (sa.getSacrificedAsOffering() == null) {
                return false;
            }
            final Card offering = sa.getSacrificedAsOffering();
            offering.setUsedToPay(false);
            if (test) {
                sa.resetSacrificedAsOffering();
            } else if (costIsPaid) {
                game.getAction().sacrifice(new CardCollection(offering), sa, false, params);
            }
        }
        if (sa.isEmerge()) {
            if (sa.getSacrificedAsEmerge() == null) {
                return false;
            }
            final Card emerge = sa.getSacrificedAsEmerge();
            emerge.setUsedToPay(false);
            if (test) {
                sa.resetSacrificedAsEmerge();
            } else if (costIsPaid) {
                game.getAction().sacrifice(new CardCollection(emerge), sa, false, params);
                sa.setSacrificedAsEmerge(game.getChangeZoneLKIInfo(emerge));
            }
        }
        if (!table.isEmpty()) {
            table.triggerChangesZoneAll(sa.getHostCard().getGame(), sa);
        }
        return true;
    }
}
