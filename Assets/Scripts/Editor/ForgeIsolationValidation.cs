#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;

public static class ForgeIsolationValidation
{
    private static OnlineMatchSession first, second;
    private static double deadline;
    private static bool killed;
    private static int firstPid, secondPid;
    private static string secondHash;
    public static bool IsRunning => first != null;
    public static Exception LastError { get; private set; }

    [MenuItem("MTGB/验证/对局/进程隔离")]
    public static void Run()
    {
        if (first != null) throw new InvalidOperationException("Isolation validation is running.");
        LastError = null;
        var deck = DeckRepository.Load(Path.Combine(DeckRepository.DeckRoot, "Forge-Validation.mdk"));
        try
        {
            first = new OnlineMatchSession(Guid.NewGuid().ToString("N"), deck, deck, 41);
            second = new OnlineMatchSession(Guid.NewGuid().ToString("N"), deck, deck, 42);
            firstPid = Engine(first).Id; secondPid = Engine(second).Id;
            deadline = EditorApplication.timeSinceStartup + 90; killed = false;
            EditorApplication.update += Tick;
        }
        catch { Cleanup(); throw; }
    }

    private static Process Engine(OnlineMatchSession session) => (Process)typeof(OnlineMatchSession)
        .GetField("process", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(session);

    private static void Tick()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup < deadline, "Isolation validation timed out.");
            first.Poll(); second.Poll();
            Require(second.Failure == null, "Healthy session failed.");
            if (!first.Ready || !second.Ready) return;
            if (!killed)
            {
                Require(firstPid != secondPid, "Sessions shared a Java process.");
                secondHash = second.GetView(1).viewHash;
                Engine(first).Kill(); killed = true; return;
            }
            if (first.Failure == null) return;
            Require(!first.GetView(1).outcome.isFinished, "Engine crash was recorded as a game loss.");
            Require(second.GetView(1).viewHash == secondHash, "Other session changed after engine crash.");
            Cleanup();
            Require(!Alive(firstPid) && !Alive(secondPid), "Java survived session disposal.");
            UnityEngine.Debug.Log("MTGB Forge process isolation validation passed: crash detected without loss, other session preserved, both processes cleaned.");
        }
        catch (Exception error) { LastError = error; Cleanup(); UnityEngine.Debug.LogException(error); }
    }
    private static bool Alive(int pid)
    {
        try { using (var process = Process.GetProcessById(pid)) return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }
    private static void Cleanup()
    {
        EditorApplication.update -= Tick;
        first?.Dispose(); second?.Dispose(); first = null; second = null;
    }
    private static void Require(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); }
}
#endif
