// SPDX-License-Identifier: GPL-3.0-or-later
package mtgb.bridge;

import com.google.common.eventbus.Subscribe;
import com.google.gson.JsonObject;
import forge.game.Game;
import forge.game.GameEndReason;
import forge.game.event.GameEventPlayerPriority;
import forge.game.event.GameEventTurnPhase;
import forge.game.phase.PhaseType;
import forge.game.player.GameLossReason;
import forge.game.player.PlayerOutcome;
import forge.game.zone.ZoneType;
import java.util.List;
import java.util.ArrayList;

/** Local challenge results are evaluated on the authoritative game, after setup. */
final class BridgePuzzleObjective {
    private record Condition(String metric, int seat, String op, int value, ZoneType zone, String name, String type) {
        boolean matches(Game game) {
            var player = game.getRegisteredPlayers().get(seat);
            long actual = switch (metric) {
                case "life" -> player.getLife();
                case "damageTakenThisTurn" -> player.getAssignedDamage();
                case "cardCount" -> player.getCardsIn(zone).stream()
                    .filter(c -> name.isEmpty() || name.equals(c.getName()))
                    .filter(c -> type.isEmpty() || c.getType().hasStringType(type)).count();
                default -> throw new IllegalArgumentException("Invalid puzzle metric: " + metric);
            };
            return switch (op) {
                case "atLeast" -> actual >= value;
                case "atMost" -> actual <= value;
                case "exactly" -> actual == value;
                default -> throw new IllegalArgumentException("Invalid puzzle comparison: " + op);
            };
        }
    }

    private final Game game;
    private final int deadline;
    private final boolean all;
    private final String checkAt;
    private final List<Condition> conditions = new ArrayList<>();
    private int result;

    BridgePuzzleObjective(Game game, JsonObject objective, int deadline) {
        this.game = game;
        this.deadline = deadline;
        this.checkAt = objective.has("checkAt") ? text(objective, "checkAt") : "state";
        if (!List.of("state", "win", "survive").contains(checkAt)) throw new IllegalArgumentException("Invalid puzzle timing");
        this.all = switch (objective.get("mode").getAsString()) {
            case "all" -> true;
            case "any" -> false;
            default -> throw new IllegalArgumentException("Invalid puzzle mode");
        };
        for (var item : objective.getAsJsonArray("conditions")) {
            var row = item.getAsJsonObject();
            int seat = switch (text(row,"player")) {
                case "you" -> 0;
                case "opponent" -> 1;
                default -> throw new IllegalArgumentException("Invalid puzzle player");
            };
            String metric = text(row, "metric");
            conditions.add(new Condition(metric, seat, text(row,"op"), row.get("value").getAsInt(),
                metric.equals("cardCount") ? ZoneType.smartValueOf(text(row,"zone")) : null, text(row,"name"), text(row,"cardType")));
        }
        if (conditions.isEmpty()) throw new IllegalArgumentException("No puzzle conditions");
    }

    private static String text(JsonObject row, String key) {
        return row.has(key) && !row.get(key).isJsonNull() ? row.get(key).getAsString() : "";
    }

    private boolean matches() {
        return all ? conditions.stream().allMatch(c -> c.matches(game)) : conditions.stream().anyMatch(c -> c.matches(game));
    }

    @Subscribe public void priority(GameEventPlayerPriority event) {
        if (result == 0 && !game.isGameOver() && checkAt.equals("state") && matches()) end(1);
    }

    @Subscribe public void phase(GameEventTurnPhase event) {
        if (result != 0 || game.isGameOver()) return;
        int turn = game.getPhaseHandler().getTurn();
        if (checkAt.equals("survive")) {
            if (turn > deadline && event.phase() == PhaseType.UPKEEP) end(matches() ? 1 : 2);
        } else if (turn >= deadline && event.phase() == PhaseType.CLEANUP) end(2);
    }

    // Lethal damage may end the duel before anyone gets priority again.
    int winner(int duelWinner) {
        if (result != 0) return result;
        return duelWinner == 1 && matches() ? 1 : 2;
    }

    private void end(int winner) {
        result = winner;
        var players = game.getRegisteredPlayers();
        players.get(winner-1).getStats().setOutcome(PlayerOutcome.altWin("Puzzle Goal"));
        players.get(2-winner).getStats().setOutcome(PlayerOutcome.loss(GameLossReason.OpponentWon, "Puzzle Goal"));
        game.setGameOver(GameEndReason.WinsGameSpellEffect);
        ForgeBridge.finish();
    }
}
