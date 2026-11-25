// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.common.eventbus.Subscribe;
import forge.game.card.CardCollectionView;
import forge.game.combat.Combat;
import forge.game.GameEntity;
import forge.game.event.*;
import forge.game.player.Player;
import forge.game.player.PlayerView;
import forge.game.spellability.SpellAbility;
import java.util.*;

/** Public visual events retained across recipient snapshots. No card faces or choices. */
final class BridgeCombatFeedback implements Player.LibraryAudit {
    private record Batch(long revision, List<Map<String,Object>> events) {}
    private final Deque<Batch> recent = new ArrayDeque<>();
    private long sequence;

    private synchronized void add(String kind, int attacker, boolean player, int target, long amount) {
        long revision = ForgeBridge.revision;
        if (recent.isEmpty() || recent.getLast().revision() != revision)
            recent.addLast(new Batch(revision, new ArrayList<>()));
        recent.getLast().events().add(Map.of("id", ++sequence, "kind", kind, "attacker", attacker,
                "player", player, "target", target, "amount", amount));
    }

    synchronized List<Map<String,Object>> snapshot() {
        // Keep an entire simultaneous damage step, even on a very wide board.
        // Expire older batches so idle snapshots do not carry last turn's hits.
        while (!recent.isEmpty() && ForgeBridge.revision - recent.getFirst().revision() > 12) recent.removeFirst();
        var events = new ArrayList<Map<String,Object>>();
        for (var batch : recent) events.addAll(batch.events());
        return events;
    }

    @Subscribe public synchronized void restart(GameEventGameRestarted event) { recent.clear(); }

    // The native draw hook also serves the optional shuffle audit. Only the
    // fact that a draw completed is public; never send the drawn card's ID.
    private record Draw(Player player, int before, Object audit) {}
    public void shuffle(Player p, SpellAbility cause, boolean after) {
        if (ForgeBridge.shuffleAudit != null) ForgeBridge.shuffleAudit.shuffle(p,cause,after);
    }
    public Object beginDraw(Player p, SpellAbility cause) {
        return new Draw(p, p.getNumDrawnThisTurn(), ForgeBridge.shuffleAudit == null ? null : ForgeBridge.shuffleAudit.beginDraw(p,cause));
    }
    public void endDraw(Object context, CardCollectionView drawn) {
        if (!(context instanceof Draw d)) return;
        if (drawn != null && !drawn.isEmpty() && d.player().getNumDrawnThisTurn() > d.before())
            add("draw",0,true,d.player().getId(),0);
        if (ForgeBridge.shuffleAudit != null) ForgeBridge.shuffleAudit.endDraw(d.audit(),drawn);
    }

    @Subscribe public void land(GameEventLandPlayed event) {
        add("land",0,true,event.player().getId(),0);
    }
    @Subscribe public void block(GameEventBlockersDeclared event) {
        // Forge represents an unblocked attacker as the pair (attacker, attacker).
        if (event.blockers().values().stream().flatMap(group -> group.entries().stream())
                .anyMatch(pair -> pair.getKey().getId() != pair.getValue().getId()))
            add("block",0,true,event.defendingPlayer().getId(),0);
    }

    @Subscribe public void attack(GameEventAttackersDeclared event) {
        for (var entry : event.attackersMap().entries())
            add("attack", entry.getValue().getId(), entry.getKey() instanceof PlayerView, entry.getKey().getId(), 0);
    }

    @Subscribe public void strike(Combat.DamageAssigned event) {
        for (var attacker : event.combat().getAttackers()) {
            // One lunge per attacker per damage step, even when damage is split.
            GameEntity target = null;
            int largest = 0;
            for (var share : event.assignments().row(attacker).entrySet()) {
                int amount = share.getValue();
                if (amount > largest || amount == largest && target != null && share.getKey().getId() < target.getId()) {
                    largest = amount;
                    target = share.getKey();
                }
            }
            if (target != null) add("strike", attacker.getId(), target instanceof Player, target.getId(), 0);
        }
    }

    @Subscribe public void hitPlayer(GameEventPlayerDamaged event) {
        if (event.amount() > 0) add("hit", 0, true, event.target().getId(), event.amount());
    }

    @Subscribe public void hitCard(GameEventCardDamaged event) {
        if (event.amount() > 0) add("hit", 0, false, event.card().getId(), event.amount());
    }

    @Subscribe public void life(GameEventPlayerLivesChanged event) {
        long lost = (long)event.oldLives() - event.newLives();
        if (lost > 0) add("loss", 0, true, event.player().getId(), lost);
    }
}
