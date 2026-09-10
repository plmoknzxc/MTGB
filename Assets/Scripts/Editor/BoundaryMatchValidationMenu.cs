#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Headless authoritative-rule regression, not a substitute for transport/UI testing.
public static class BoundaryMatchValidationMenu
{
    [MenuItem("MTGB/Validate Boundary Match Flow")]
    public static void ValidateForAutomation()
    {
        var errors = new List<string>();
        Application.LogCallback capture = (message, trace, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
        };
        Application.logMessageReceived += capture;
        try
        {
            ValidateDeck("BoundaryResponses.mdk");
            ValidateDeck("BoundarySpecialists.mdk");
        }
        finally { Application.logMessageReceived -= capture; }
        Require(errors.Count == 0, "Boundary match logged errors: " + string.Join("; ", errors));
    }

    private static void ValidateDeck(string deckFile)
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before boundary match validation.");
        var deck = DeckRepository.Load(Path.Combine(PrintingKeyUtility.ApplicationRoot,
            "Decks", deckFile));
        int totalActions = 0, totalCasts = 0, totalAttacks = 0, totalBlocks = 0;
        for (int seed = 7241; seed < 7257; seed++)
        {
            using var events = new IsolatedGameEvents();
            var root = new GameObject("Boundary Match Validation");
            try
            {
                var rules = root.AddComponent<GameRules>();
                using var scope = CardScriptRuntimeManager.UseRules(rules);
                rules.InitializeForOnlineSession(deck, deck, seed, 20, 1 + seed % 2);
                Require(events.CardPlayedListenerCount == 1, "Match must have exactly one card-play listener.");
                var mulliganed = new HashSet<int>();
                bool controlsSet = false;
                int steps = 0, casts = 0;
                while (!rules.IsGameOver() && rules.GetTurnNumber() < 13 && steps++ < 1600)
                {
                    var choice = rules.GetActiveChoice();
                    GameAction action;
                    if (choice != null)
                    {
                        action = Choose(choice, mulliganed);
                    }
                    else if (!controlsSet)
                    {
                        for (int seat = 1; seat <= 2; seat++)
                            Submit(rules, new GameAction { type = GameActionType.SetFullControl,
                                playerId = seat, value = (seed / 2) % 2 });
                        controlsSet = true;
                        continue;
                    }
                    else
                    {
                        int seat = rules.IsAwaitingPriority() ? rules.GetPriorityPlayer() : rules.GetCurrentPlayer();
                        var combatAction = seed >= 7249 ? ChooseCombat(rules, seat) : null;
                        int playable = Enumerable.Range(0, rules.GetHandCount(seat))
                            .Where(i => rules.CanPlayCard(seat, i)).DefaultIfEmpty(-1).First();
                        if (combatAction != null)
                        {
                            action = combatAction;
                            if (action.type == GameActionType.ToggleAttacker) totalAttacks++;
                            if (action.type == GameActionType.DeclareBlocker) totalBlocks++;
                        }
                        else if (playable >= 0)
                        {
                            action = new GameAction { type = GameActionType.BeginHandCast,
                                playerId = seat, zoneIndex = playable };
                            casts++;
                        }
                        else
                        {
                            Require(rules.CanPlayerAdvance(seat), Describe(rules, seed) + " cannot advance.");
                            action = new GameAction { type = rules.GetPrimaryAdvanceActionType(seat), playerId = seat };
                        }
                    }
                    if (action.type == GameActionType.PassPriority)
                    {
                        string state = rules.ComputeDeterministicStateHash();
                        var wrongSeat = action.Clone();
                        wrongSeat.playerId = 3 - action.playerId;
                        wrongSeat.expectedStateSequence = rules.GetAppliedActionSequence();
                        Require(!rules.ApplyAction(wrongSeat).accepted
                            && state == rules.ComputeDeterministicStateHash(), "Other seat passed priority or mutated state.");
                    }
                    Submit(rules, action);
                    CheckViews(rules);
                    // A stale request must not change cards, priority, choices or replay sequence.
                    string before = rules.ComputeDeterministicStateHash();
                    long sequence = rules.GetAppliedActionSequence();
                    var stale = action.Clone();
                    stale.expectedStateSequence = sequence - 1;
                    Require(!rules.ApplyAction(stale).accepted, "Stale action was accepted.");
                    Require(before == rules.ComputeDeterministicStateHash()
                        && sequence == rules.GetAppliedActionSequence(), "Rejected action mutated state.");
                }
                Require(steps < 1600, Describe(rules, seed) + " exceeded action budget (possible loop).");
                Require(rules.GetTurnNumber() >= 13 || rules.IsGameOver(), "Match did not progress.");
                Require(casts > 0, "No hand card was played in the match.");
                var replay = rules.GetReplay().Clone();
                string expected = rules.ComputeDeterministicStateHash();
                var result = rules.RestoreOnlineSession(deck, deck, seed, replay, 20, 1 + seed % 2);
                Require(result.succeeded && result.finalStateHash == expected,
                    "Boundary match replay diverged: " + result.error);
                totalActions += replay.actions.Count;
                totalCasts += casts;
                Debug.Log($"Boundary match deck={deckFile} seed={seed}: turn={rules.GetTurnNumber()}, actions={replay.actions.Count}, hand plays={casts}, replay matched.");
            }
            finally
            {
                DestroyRules(root);
                Require(events.CardPlayedListenerCount == 0, "Match teardown leaked a card-play listener.");
                MatchStartConfig.ClearOnlineSession();
            }
        }
        Require(totalAttacks > 0 && totalBlocks > 0, "Combat strategy never exercised attacks/blocks.");
        Debug.Log($"Boundary match flow passed ({deckFile}): 16 seeded matches, {totalActions} actions, {totalCasts} hand plays, {totalAttacks} attackers, {totalBlocks} blockers; two-seat views, wrong-seat and stale rejection checked.");
    }

    private static GameAction ChooseCombat(GameRules rules, int seat)
    {
        if (rules.IsChoosingAttackers())
        {
            var cards = rules.GetBattlefieldCards(seat);
            for (int i = 0; i < cards.Count; i++)
                if (rules.CanDeclareAttacker(seat, i) && !rules.IsPendingAttacker(cards[i]))
                    return new GameAction { type = GameActionType.ToggleAttacker, playerId = seat, zoneIndex = i };
        }
        if (rules.IsChoosingBlockers())
        {
            int defender = 3 - rules.GetCurrentPlayer();
            var blockers = rules.GetBattlefieldCards(defender);
            foreach (var attacker in rules.GetBattlefieldCards(rules.GetCurrentPlayer()))
                for (int i = 0; i < blockers.Count; i++)
                    if (!blockers[i].IsBlocking && rules.CanDeclareBlocker(defender, i, attacker))
                        return new GameAction { type = GameActionType.DeclareBlocker, playerId = defender,
                            zoneIndex = i, targetObjectId = attacker.ObjectId };
        }
        return null;
    }

    private static GameAction Choose(GameChoiceRequest choice, HashSet<int> mulliganed)
    {
        var action = new GameAction { playerId = choice.choosingPlayerId, choiceRequestId = choice.requestId };
        if (choice.kind == GameChoiceKind.CastFeature)
        {
            action.type = GameActionType.SelectCastFeature;
            action.featureIndex = choice.options.FindIndex(o => o.legal);
            Require(action.featureIndex >= 0, "No legal feature offered.");
        }
        else if (choice.CanConfirm)
            action.type = GameActionType.ConfirmChoice;
        else if (choice.selectionMode == GameChoiceSelectionMode.Number)
        {
            action.type = GameActionType.SetChoiceNumber;
            action.value = choice.minimumValue;
        }
        else
        {
            action.type = GameActionType.ToggleChoiceOption;
            if (choice.kind == GameChoiceKind.MulliganDecision)
                action.optionId = mulliganed.Add(choice.choosingPlayerId) ? 2 : 1;
            else
            {
                var option = choice.options.FirstOrDefault(o => o.legal && !choice.IsSelected(o.optionId));
                Require(option != null, "No choice strategy for " + choice.kind);
                action.optionId = option.optionId;
            }
        }
        return action;
    }

    private static void Submit(GameRules rules, GameAction action)
    {
        action.expectedStateSequence = rules.GetAppliedActionSequence();
        var result = rules.ApplyAction(action);
        Require(result.accepted, Describe(rules, 0) + $" rejected {action.type} by P{action.playerId}: {result.error}");
    }

    private static void CheckViews(GameRules rules)
    {
        for (int seat = 1; seat <= 2; seat++)
        {
            var view = JsonUtility.FromJson<OnlinePlayerViewState>(JsonUtility.ToJson(rules.CreateOnlinePlayerView(seat)));
            var root = new GameObject("Boundary Match Client View");
            try
            {
                var client = root.AddComponent<GameRules>();
                Require(client.ApplyOnlinePlayerView(view, out string error), "View import failed: " + error);
                Require(client.GetCurrentPlayer() == rules.GetCurrentPlayer()
                    && client.GetCurrentPhase() == rules.GetCurrentPhase()
                    && client.GetPriorityPlayer() == rules.GetPriorityPlayer()
                    && client.GetHandCount(seat) == rules.GetHandCount(seat)
                    && client.GetStackCount() == rules.GetStackCount(), "Client view lost match state.");
                for (int i = 0; i < rules.GetHandCount(seat); i++)
                    Require(client.CanPlayCard(seat, i) == rules.CanPlayCard(seat, i), "Client playable-card flag differs from authority.");
                for (int i = 0; i < rules.GetBattlefieldCards(seat).Count; i++)
                    Require(client.CanActivateManaAbility(client.GetBattlefieldCard(seat, i))
                        == rules.CanActivateManaAbility(rules.GetBattlefieldCard(seat, i)), "Client mana-ability flag differs from authority.");
            }
            finally { DestroyRules(root); }
        }
    }

    private static void DestroyRules(GameObject root)
    {
        // Edit-mode tests do not run MonoBehaviour lifecycle callbacks reliably.
        // Explicitly release scripts and subscriptions before destroying the fixture.
        typeof(GameRules).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(root.GetComponent<GameRules>(), null);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private sealed class IsolatedGameEvents : IDisposable
    {
        private readonly Dictionary<FieldInfo, object> saved = new Dictionary<FieldInfo, object>();
        private static readonly FieldInfo CardPlayed = typeof(GameEvents).GetField("OnCardPlayed",
            BindingFlags.Static | BindingFlags.NonPublic);
        public int CardPlayedListenerCount => ((Delegate)CardPlayed.GetValue(null))?.GetInvocationList().Length ?? 0;

        public IsolatedGameEvents()
        {
            foreach (var field in typeof(GameEvents).GetFields(BindingFlags.Static | BindingFlags.NonPublic))
            {
                if (!typeof(Delegate).IsAssignableFrom(field.FieldType)) continue;
                saved.Add(field, field.GetValue(null));
                field.SetValue(null, null);
            }
        }

        public void Dispose()
        {
            foreach (var entry in saved) entry.Key.SetValue(null, entry.Value);
        }
    }

    private static string Describe(GameRules rules, int seed) =>
        $"Seed {seed}, turn {rules.GetTurnNumber()}, phase {rules.GetCurrentPhase()}, priority {rules.GetPriorityPlayer()}, choice {rules.GetActiveChoice()?.kind}";

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
