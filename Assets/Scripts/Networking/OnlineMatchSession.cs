using System;
using UnityEngine;

public sealed class OnlineMatchSession : IDisposable
{
    private static bool executingRules;

    private readonly DeckDefinition player1Deck;
    private readonly DeckDefinition player2Deck;
    private readonly int randomSeed;
    private readonly int startingLife;
    private readonly int firstPlayerSeat;
    private GameReplay replay;
    private OnlinePlayerViewState player1View;
    private OnlinePlayerViewState player2View;
    private OnlinePlayerViewState spectatorView;
    private bool disposed;

    public string MatchId { get; }
    public int RandomSeed => randomSeed;
    public long AppliedSequence => replay?.actions?.Count ?? 0;

    public OnlineMatchSession(
        string matchId,
        DeckDefinition first,
        DeckDefinition second,
        int seed,
        OnlineRoomConfig config = null)
    {
        if (first == null) throw new ArgumentNullException(nameof(first));
        if (second == null) throw new ArgumentNullException(nameof(second));
        MatchId = string.IsNullOrWhiteSpace(matchId) ? Guid.NewGuid().ToString("N") : matchId;
        player1Deck = CloneDeck(first);
        player2Deck = CloneDeck(second);
        randomSeed = seed;
        startingLife = Math.Max(1, Math.Min(200, config?.startingLife ?? 20));
        firstPlayerSeat = config?.firstPlayerSeat == 2 ? 2 : 1;

        ExecuteWithRules(null, rules =>
        {
            replay = rules.GetReplay().Clone();
            player1View = rules.CreateOnlinePlayerView(1);
            player2View = rules.CreateOnlinePlayerView(2);
            spectatorView = rules.CreateOnlinePlayerView(0);
            return true;
        }, out string error);
        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
    }

    public OnlinePlayerViewState GetView(int seat)
    {
        OnlinePlayerViewState source = seat == 1 ? player1View : seat == 2 ? player2View : seat == 0 ? spectatorView : null;
        return source == null ? null : JsonUtility.FromJson<OnlinePlayerViewState>(JsonUtility.ToJson(source));
    }

    public GameActionResult ApplyAction(GameAction action)
    {
        if (disposed) return GameActionResult.Rejected("Match has ended.", action?.sequence ?? 0);
        if (action == null) return GameActionResult.Rejected("Action is null.", 0);

        GameActionResult result = null;
        bool executed = ExecuteWithRules(replay, rules =>
        {
            result = rules.ApplyAction(action.Clone());
            if (!result.accepted) return true;
            GameReplay nextReplay = rules.GetReplay().Clone();
            OnlinePlayerViewState nextPlayer1View = rules.CreateOnlinePlayerView(1);
            OnlinePlayerViewState nextPlayer2View = rules.CreateOnlinePlayerView(2);
            OnlinePlayerViewState nextSpectatorView = rules.CreateOnlinePlayerView(0);
            if (nextReplay == null || nextPlayer1View == null || nextPlayer2View == null || nextSpectatorView == null)
                throw new InvalidOperationException("Could not create the authoritative post-action state.");
            replay = nextReplay;
            player1View = nextPlayer1View;
            player2View = nextPlayer2View;
            spectatorView = nextSpectatorView;
            return true;
        }, out string error);
        if (!executed) return GameActionResult.Rejected(error, action.sequence);
        return result ?? GameActionResult.Rejected("Action did not produce a result.", action.sequence);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        replay = null;
        player1View = null;
        player2View = null;
        spectatorView = null;
    }

    private bool ExecuteWithRules(GameReplay source, Func<GameRules, bool> operation, out string error)
    {
        error = string.Empty;
        if (executingRules)
        {
            error = "Another authoritative rules operation is already running.";
            return false;
        }

        executingRules = true;
        bool loggingWasEnabled = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = false;
        GameObject host = null;
        try
        {
            host = new GameObject($"OnlineMatchSession-{MatchId}");
            host.hideFlags = HideFlags.HideAndDontSave;
            GameRules rules = host.AddComponent<GameRules>();
            GameReplayResult restored = rules.RestoreOnlineSession(
                player1Deck,
                player2Deck,
                randomSeed,
                source,
                startingLife,
                firstPlayerSeat);
            if (restored == null || !string.IsNullOrEmpty(restored.error))
            {
                error = restored?.error ?? "Could not restore authoritative match state.";
                return false;
            }
            return operation(rules);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            try
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
            }
            finally
            {
                MatchStartConfig.ClearOnlineSession();
                Debug.unityLogger.logEnabled = loggingWasEnabled;
                executingRules = false;
            }
        }
    }

    private static DeckDefinition CloneDeck(DeckDefinition source)
    {
        return JsonUtility.FromJson<DeckDefinition>(JsonUtility.ToJson(source));
    }
}
