using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using UnityEngine;

/// <summary>Owns one persistent Forge process and separately projected seat snapshots.</summary>
public sealed class OnlineMatchSession : IDisposable
{
    private static readonly HashSet<OnlineMatchSession> activeSessions = new HashSet<OnlineMatchSession>();
    static OnlineMatchSession()
    {
        Application.quitting += DisposeAll;
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
        UnityEditor.EditorApplication.quitting += DisposeAll;
#endif
    }
    private static void DisposeAll()
    {
        foreach (OnlineMatchSession session in activeSessions.ToArray()) session.Dispose();
    }
    [Serializable] private sealed class StartConfig
    {
        public string session;
        public int seed, startingLife, firstPlayerSeat;
        public DeckDefinition[] decks;
    }
    private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> diagnostics = new ConcurrentQueue<string>();
    private bool diagnosticsWritten;
    private readonly OnlinePlayerViewState[] views = new OnlinePlayerViewState[2];
    private Process process;
    private string configPath;
    private bool disposed, inputPending;
    private readonly Stopwatch startup = Stopwatch.StartNew();
    private readonly MatchReplayRecorder replay;
    public MatchReplay CompleteReplay(string[] players, int game, int bestOf, int winner, string reason)
    {
        replay.Record(startup.Elapsed.TotalSeconds, views[0].forge, views[1].forge);
        return replay.Complete(players, game, bestOf, winner, reason);
    }
    public string MatchId { get; }
    public int RandomSeed { get; }
    public long AppliedSequence { get; private set; }
    public string Failure { get; private set; }
    public bool Ready { get; private set; }

    public OnlineMatchSession(string matchId, DeckDefinition first, DeckDefinition second, int seed, OnlineRoomConfig config = null)
    {
        if (first == null || second == null) throw new ArgumentNullException("decks");
        MatchId = string.IsNullOrWhiteSpace(matchId) ? Guid.NewGuid().ToString("N") : matchId;
        replay = new MatchReplayRecorder(MatchId);
        RandomSeed = seed;
        var runtime = ForgeCardCatalog.ReadConfigForMatch();
        for (int i = 0; i < 2; i++)
        {
            views[i] = new OnlinePlayerViewState { matchId = MatchId, viewerSeat = i + 1,
                forge = new ForgeSeatView { seat = i + 1, phase = "Starting Forge" } };
            views[i].SealHash();
        }
        string directory = Path.Combine(Application.persistentDataPath, "ForgeSessions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        configPath = Path.Combine(directory, "start.json");
        File.WriteAllText(configPath, JsonUtility.ToJson(new StartConfig {
            session = MatchId, seed = seed, startingLife = config?.startingLife ?? 20,
            firstPlayerSeat = config?.firstPlayerSeat ?? 1, decks = new[] { first, second }
        }));
        try
        {
            string java = Path.IsPathRooted(runtime.java) ? runtime.java : Path.Combine(ForgeCardCatalog.Root, runtime.java);
            string assets = Path.IsPathRooted(runtime.assets) ? runtime.assets : Path.Combine(ForgeCardCatalog.Root, runtime.assets);
            process = new Process { StartInfo = new ProcessStartInfo {
                FileName = java,
                Arguments = "-Djava.awt.headless=true -Dfile.encoding=UTF-8 -cp " + Quote(Path.Combine(ForgeCardCatalog.Root, "classes") + ";" + Path.Combine(ForgeCardCatalog.Root, "lib/*"))
                    + " mtgb.bridge.ForgeBridge " + Quote(assets) + " " + Quote(configPath),
                WorkingDirectory = assets, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            }};
            Utf8ProcessIO.Configure(process.StartInfo);
            process.StartInfo.EnvironmentVariables["APPDATA"] = directory;
            process.StartInfo.EnvironmentVariables["LOCALAPPDATA"] = directory;
            process.OutputDataReceived += (_, args) => { if (args.Data != null) incoming.Enqueue(args.Data); };
            // Engine diagnostics may contain hidden cards. Never broadcast stderr to clients.
            process.ErrorDataReceived += (_, args) => {
                if (args.Data == null) return;
                diagnostics.Enqueue(args.Data);
                while (diagnostics.Count > 128) diagnostics.TryDequeue(out string discarded);
            };
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            activeSessions.Add(this);
        }
        catch { Dispose(); throw; }
    }
    public OnlinePlayerViewState GetView(int seat) => seat > 2 ? OnlineSpectatorView.Create(views[0],seat) : seat == 1 || seat == 2
        ? JsonUtility.FromJson<OnlinePlayerViewState>(JsonUtility.ToJson(views[seat - 1])) : null;

    public bool Poll()
    {
        if (disposed) return false;
        bool changed = false;
        while (incoming.TryDequeue(out string line))
        {
            try
            {
                ForgeWireMessage message = JsonUtility.FromJson<ForgeWireMessage>(line);
                if (message == null || message.session != MatchId) throw new InvalidDataException("Invalid Forge session envelope.");
                if (message.type == "fatal") { Failure = "Forge 遇到未适配交互或引擎异常，本局异常终止。"; changed = true; break; }
                if (message.type == "accepted") continue;
                if (message.type == "rejected")
                {
                    // GUI callbacks may replace an input after the server queued its response.
                    // Forge rejected it without executing it and republishes the current request.
                    if (message.error == "stale-request") continue;
                    Failure = "Forge 拒绝已通过服务器校验的输入，本局异常终止。"; changed = true; break;
                }
                if (message.type == "ended")
                {
                    AppliedSequence++;
                    if (message.views != null && message.views.Length == 2)
                        for (int i = 0; i < 2; i++) {
                            if(message.views[i].seat != i + 1) throw new InvalidDataException("Wrong final projection seat.");
                            views[i].forge = message.views[i];
                        }
                    foreach (var view in views)
                    {
                        view.appliedSequence = AppliedSequence; view.forge.request = null; view.forge.awaitingInput = false; view.forge.inputPending = false;
                        view.outcome = new MatchOutcome { isFinished = true, winnerPlayerId = message.winnerSeat, message = "Forge 对局结束。" };
                        view.SealHash();
                    }
                    replay.Record(startup.Elapsed.TotalSeconds, views[0].forge, views[1].forge);
                    changed = true; continue;
                }
                if (message.type != "request") continue;
                if (message.views == null || message.views.Length != 2 || message.request == null)
                    throw new InvalidDataException("Incomplete Forge projection.");
                AppliedSequence++; Ready = true; inputPending = false;
                for (int i = 0; i < 2; i++)
                {
                    ForgeSeatView seatView = message.views[i];
                    if (seatView.seat != i + 1) throw new InvalidDataException("Wrong projection seat.");
                    seatView.awaitingInput = message.request.seat == i + 1;
                    seatView.request = seatView.awaitingInput ? message.request : null;
                    views[i].forge = seatView; views[i].appliedSequence = AppliedSequence; views[i].SealHash();
                }
                replay.Record(startup.Elapsed.TotalSeconds, views[0].forge, views[1].forge);
                changed = true;
            }
            catch (Exception error)
            {
                // Keep the failed envelope and parser/projection error on the host only.
                // stderr alone cannot explain failures in Unity's message processing.
                diagnostics.Enqueue("Bridge message failure: " + error);
                diagnostics.Enqueue("Envelope: " + line);
                Failure = "Forge 桥接消息无效，本局异常终止。"; changed = true; break;
            }
        }
        if (Failure == null && !views[0].outcome.isFinished && process.HasExited)
        { Failure = "Forge 进程意外退出，本局异常终止。"; changed = true; }
        if (!Ready && startup.Elapsed.TotalSeconds > 60)
        { Failure = "Forge 启动超时，本局异常终止。"; changed = true; }
        if (Failure != null && !diagnosticsWritten)
        {
            diagnosticsWritten = true;
            string directory = Path.Combine(Application.persistentDataPath, "ForgeDiagnostics");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".log");
            File.WriteAllLines(path, new[] { "Session: " + MatchId, "Failure: " + Failure }.Concat(diagnostics.ToArray()));
            UnityEngine.Debug.LogError("Forge 会话异常；仅服务器可见的引擎诊断：" + path);
        }
        return changed;
    }
    public GameActionResult ApplyAction(GameAction action)
    {
        if (disposed || Failure != null || action == null) return GameActionResult.Rejected("会话不可用。", AppliedSequence);
        if (action.playerId < 1 || action.playerId > 2) return GameActionResult.Rejected("无效席位。", AppliedSequence);
        ForgeRequest request = views[action.playerId - 1].forge.request;
        bool concede = action.type == GameActionType.Concede;
        if (concede && (!Ready || views[0].outcome.isFinished))
            return GameActionResult.Rejected("对局尚未就绪或已经结束。", AppliedSequence);
        if (!concede && (action.type != GameActionType.ConfirmChoice || inputPending || request == null
            || !views[action.playerId - 1].forge.awaitingInput || action.sourceObjectId != request.requestId || action.optionId < 0 || action.optionId >= request.options.Length))
            return GameActionResult.Rejected("过期请求、无效候选或正在等待 Forge。", AppliedSequence);
        try
        {
            process.StandardInput.WriteLine(JsonUtility.ToJson(new ForgeResponse {
                type = concede ? "concede" : "respond", session = MatchId, seat = action.playerId,
                requestId = request?.requestId ?? 0, option = concede ? null : request.options[action.optionId].id
            }));
            process.StandardInput.Flush(); inputPending = true;
            foreach (var view in views) { view.forge.request = null; view.forge.awaitingInput = false; view.forge.inputPending = true; view.SealHash(); }
            return GameActionResult.Accepted(AppliedSequence, views[action.playerId - 1].viewHash);
        }
        catch (Exception) { Failure = "无法向 Forge 提交输入。"; return GameActionResult.Rejected(Failure, AppliedSequence); }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        activeSessions.Remove(this);
        if (process != null)
        {
            try { if (!process.HasExited) { process.StandardInput.WriteLine("{\"type\":\"shutdown\"}"); process.StandardInput.Flush(); if (!process.WaitForExit(1500)) process.Kill(); } }
            catch (InvalidOperationException) { }
            finally { process.Dispose(); }
        }
        if (configPath != null)
        {
            string sessionDirectory = Path.GetFullPath(Path.GetDirectoryName(configPath));
            string sessionsRoot = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "ForgeSessions")) + Path.DirectorySeparatorChar;
            if (sessionDirectory.StartsWith(sessionsRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(sessionDirectory))
                Directory.Delete(sessionDirectory, true);
        }
    }
    private static string Quote(string value)
    {
        if (value.Contains("\"")) throw new ArgumentException("Invalid runtime path.");
        return "\"" + value + "\"";
    }
}
