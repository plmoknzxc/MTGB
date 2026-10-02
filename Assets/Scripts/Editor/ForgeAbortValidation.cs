#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ForgeAbortValidation
{
    [MenuItem("MTGB/验证/对局/异常终止")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var owner = new GameObject("AbortValidation");
        owner.SetActive(false);
        var service = owner.AddComponent<OnlineMatchService>();
        var room = new OnlineRoomState { roomId = "abort-validation" };
        var end = typeof(OnlineMatchService).GetMethod("EndRoomMatch", BindingFlags.Instance | BindingFlags.NonPublic);
        var deck = DeckRepository.Load(Path.Combine(DeckRepository.DeckRoot, "Starter.mdk"));
        try
        {
            room.series.Reset(3);
            // Also cover a failure after a legitimate game and sideboarding.
            for (int completed = 0; completed < 2; completed++)
            {
                room.series.BeginGame();
                room.match = new OnlineMatchSession(Guid.NewGuid().ToString("N"), deck, deck, 4);
                end.Invoke(service, new object[] { room, 0, "Injected engine failure for regression validation", true });
                if (room.match != null || room.series.betweenGames || room.series.finished
                    || room.series.gameNumber != completed + 1 || room.series.player1Wins != completed
                    || room.series.player2Wins != 0 || room.series.draws != 0)
                    throw new Exception("Aborted game changed score, advanced game number, or opened sideboarding.");
                if (completed == 0)
                {
                    room.match = new OnlineMatchSession(Guid.NewGuid().ToString("N"), deck, deck, 4);
                    end.Invoke(service, new object[] { room, 1, "Legitimate win for regression validation", false });
                    if (!room.series.betweenGames || room.series.gameNumber != 2 || room.series.player1Wins != 1)
                        throw new Exception("Legitimate game failed to open BO3 sideboarding.");
                }
            }
            const string result = "PASS: engine abort preserves score/game number, releases session, does not open sideboarding; legitimate BO3 win does.";
            Directory.CreateDirectory(".tools/forge-migration");
            File.WriteAllText(".tools/forge-migration/abort-result.txt", result);
            Debug.Log(result);
        }
        finally { room.Dispose(); UnityEngine.Object.DestroyImmediate(owner); }
    }
}
#endif
