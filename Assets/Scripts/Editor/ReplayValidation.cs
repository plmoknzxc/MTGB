#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ReplayValidation
{
    private static OnlineMatchSession session;
    private static double deadline, nextAction;
    private static int actions;
    public static string Result { get; private set; }
    public static string SamplePath { get; private set; }
    [MenuItem("MTGB/验证/回放/录制与文件")]
    public static void Run()
    {
        if (session != null) throw new InvalidOperationException("Already running");
        Result = "RUNNING"; actions = 0;
        var deck = DeckRepository.CreateDefaultDeck(CardDatabaseRepository.LoadAll());
        session = new OnlineMatchSession(Guid.NewGuid().ToString("N"), deck, deck, 42);
        deadline = EditorApplication.timeSinceStartup + 60;
        nextAction = 0;
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Session timed out");
            session.Poll(); Check(session.Failure == null, session.Failure);
            if (session.GetView(1).outcome.isFinished)
            {
                var replay = session.CompleteReplay(new[] { "录像测试 · 玩家一", "录像测试 · 玩家二" }, 2, 3, 2, "Concession");
                Check(replay.frames.Count > 3 && replay.Duration > 0, "Missing recorded states");
                var bytes = ReplayRepository.Encode(replay);
                var copy = ReplayRepository.Decode(bytes);
                Check(copy.frames.Count == replay.frames.Count && copy.gameNumber == 2 && copy.bestOf == 3, "Roundtrip metadata");
                Check(copy.frames[0].views[0].awaitingInput || copy.frames[0].views[1].awaitingInput, "Recorded request was mutated");
                Check(ReplayRepository.FrameAt(copy, -1) == 0 && ReplayRepository.FrameAt(copy, double.MaxValue) == copy.frames.Count - 1, "Timeline boundaries");
                string root = Path.GetFullPath(Path.Combine(".tools", "replay-validation", Guid.NewGuid().ToString("N")));
                SamplePath = ReplayRepository.Save(copy, directory: root);
                string duplicate = ReplayRepository.Save(copy, directory: root);
                Check(duplicate != SamplePath && duplicate.EndsWith(" (1).mtgbr"), "Duplicate overwrote replay");
                Check(ReplayRepository.Load(SamplePath).winnerSeat == 2, "Saved winner");
                bytes[0] = 0; Reject(() => ReplayRepository.Decode(bytes));
                copy.version = 99; Reject(() => ReplayRepository.Encode(copy)); copy.version = 1;
                copy.frames[1].time = -1; Reject(() => ReplayRepository.Encode(copy));
                Finish("PASS: real Forge recording, immutable states, two viewpoints, per-game metadata, compressed roundtrip, filenames, duplicate suffix, seeking and invalid file rejection. " + SamplePath);
                return;
            }
            if (EditorApplication.timeSinceStartup < nextAction) return;
            for (int seat = 1; seat <= 2; seat++)
            {
                var view = session.GetView(seat).forge;
                if (!view.awaitingInput) continue;
                var action = actions >= 8
                    ? new GameAction { type = GameActionType.Concede, playerId = 1 }
                    : new GameAction { type = GameActionType.ConfirmChoice, playerId = seat, sourceObjectId = view.request.requestId, optionId = 0 };
                Check(session.ApplyAction(action).accepted, "Replay fixture action rejected");
                actions++; nextAction = EditorApplication.timeSinceStartup + .15; break;
            }
        }
        catch (Exception e) { Finish(e.ToString()); Debug.LogException(e); }
    }
    private static void Finish(string result)
    {
        EditorApplication.update -= Tick; session?.Dispose(); session = null;
        Result = result; Debug.Log(result);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid replay accepted");
    }
}
#endif
