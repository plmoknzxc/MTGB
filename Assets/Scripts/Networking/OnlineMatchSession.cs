using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Owns one persistent Forge process and separately projected seat snapshots.</summary>
public sealed class OnlineMatchSession : IDisposable
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Opt-in validation may configure initial positions before the native worker starts.
    internal static Action<ProcessStartInfo,string> ValidationBeforeStart;
#endif
    private static readonly HashSet<OnlineMatchSession> activeSessions = new HashSet<OnlineMatchSession>();
    private static readonly HashSet<Task> stoppingWorkers = new HashSet<Task>();
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
        var sessions = activeSessions.ToArray();
        foreach (OnlineMatchSession session in sessions) session.Dispose();
        // Exit/reload may unload managed tasks. Normal match disposal never waits.
        Task[] stopping; lock (stoppingWorkers) stopping = stoppingWorkers.ToArray();
        try { Task.WaitAll(stopping, 4000); }
        catch (AggregateException error) { UnityEngine.Debug.LogWarning("Forge 退出清理：" + error.Message); }
    }
    [Serializable] private sealed class StartConfig
    {
        public string session;
        public string formatId;
        public string commanderEvent;
        public string localPuzzle;
        public int seed, startingLife, firstPlayerSeat, aiSeat;
        public bool useAnte, localPractice;
        public bool noShuffle;
        public string shuffleAuditFile, runtimeHash, forgeRevision, roomId;
        public int gameNumber;
        public DeckDefinition[] decks;
        public DeckDefinition[] authorizedAuxiliarySeriesPools;
    }
    private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> diagnostics = new ConcurrentQueue<string>();
    private bool diagnosticsWritten;
    private readonly OnlinePlayerViewState[] views;
    private Process process;
    private Task workerShutdown = Task.CompletedTask;
    [Serializable] private sealed class ShutdownMessage { public string type = "shutdown", session; }
    private string configPath;
    private bool disposed, inputPending;
    private int voteEpoch, voteWindow, heroVoters, heroVotes, voteTurn;
    private string votePhase;
    private readonly int aiSeat;
    private readonly Stopwatch startup = Stopwatch.StartNew();
    // A prepared puzzle has not been polled or recorded yet. Start its replay
    // clock and startup timeout when the player actually enters the challenge.
    internal void BeginPreparedPuzzle() => startup.Restart();
    internal void SetPuzzleEditorPaused(bool paused)
    {
        if (paused) startup.Stop();
        else startup.Start();
    }
    private readonly MatchReplayRecorder replay;
    public RecordedMatchReplay CompleteReplay(string[] players, int game, int bestOf, int winner, string reason)
    {
        replay.Record(startup.Elapsed.TotalSeconds, views.Select(v => v.forge).ToArray());
        return replay.Complete(players, game, bestOf, winner, reason);
    }
    public string MatchId { get; }
    public int RandomSeed { get; }
    // Authority-local path, deliberately absent from lobby/view/replay payloads.
    public string ShuffleAuditFile { get; }
    public long AppliedSequence { get; private set; }
    public string Failure { get; private set; }
    public bool Ready { get; private set; }

    public OnlineMatchSession(string matchId, DeckDefinition first, DeckDefinition second, int seed, OnlineRoomConfig config = null, string roomId = null, int gameNumber = 1, int aiSeat = 0, bool localPractice = false, string localPuzzle = null)
        : this(matchId, new[] { first, second }, seed, config, roomId, gameNumber, aiSeat, localPractice, localPuzzle) { }

    public OnlineMatchSession(string matchId, DeckDefinition[] decks, int seed, OnlineRoomConfig config, string roomId, int gameNumber, int aiSeat = 0, bool localPractice = false, string localPuzzle = null)
    {
        if (decks == null || decks.Length != (config?.PlayerCapacity ?? 2) || decks.Any(deck => deck == null))
            throw new ArgumentException("Every player seat needs a deck.", nameof(decks));
        views = new OnlinePlayerViewState[decks.Length];
        if(aiSeat<0||aiSeat>2)throw new ArgumentOutOfRangeException(nameof(aiSeat));
        this.aiSeat=aiSeat;
        MatchId = string.IsNullOrWhiteSpace(matchId) ? Guid.NewGuid().ToString("N") : matchId;
        replay = new MatchReplayRecorder(MatchId);
        RandomSeed = seed;
        var runtime = ForgeCardCatalog.ReadConfigForMatch();
        ShuffleAuditFile = Path.Combine(Application.persistentDataPath, "ShuffleAudits", Guid.NewGuid().ToString("N") + ".jsonl.gz");
        for (int i = 0; i < views.Length; i++)
        {
            views[i] = new OnlinePlayerViewState { matchId = MatchId, viewerSeat = i + 1,
                forge = new ForgeSeatView { seat = i + 1, formatId = config?.formatId, phase = "Starting Forge" } };
            views[i].SealHash();
        }
        string directory = Path.Combine(Application.persistentDataPath, "ForgeSessions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        configPath = Path.Combine(directory, "start.json");
        File.WriteAllText(configPath, JsonUtility.ToJson(new StartConfig {
            session = MatchId, formatId = config?.formatId ?? "none", seed = seed, startingLife = config?.startingLife ?? 20,
            commanderEvent = OnlineRoomConfig.EventFor(config?.formatId, config?.commanderEvent),
            firstPlayerSeat = config?.firstPlayerSeat ?? 1, useAnte = config?.useAnte == true, decks = decks,
            noShuffle = config?.noShuffle == true, aiSeat = aiSeat, localPractice = localPractice,
            localPuzzle = localPuzzle,
            shuffleAuditFile = ShuffleAuditFile, runtimeHash = runtime.runtimeHash, forgeRevision = runtime.revision,
            roomId = roomId, gameNumber = gameNumber,
            authorizedAuxiliarySeriesPools = config?.authorizedAuxiliarySeriesPools
        }));
        try
        {
            string java = Path.IsPathRooted(runtime.java) ? runtime.java : Path.Combine(ForgeCardCatalog.Root, runtime.java);
            string assets = Path.IsPathRooted(runtime.assets) ? runtime.assets : Path.Combine(ForgeCardCatalog.Root, runtime.assets);
            process = new Process { StartInfo = new ProcessStartInfo {
                FileName = java,
                Arguments = "-Djava.awt.headless=true -Dfile.encoding=UTF-8 -Duser.home=" + Quote(directory) + " -cp " + Quote(Path.Combine(ForgeCardCatalog.Root, "classes") + Path.PathSeparator + Path.Combine(ForgeCardCatalog.Root, "lib/*"))
                    + " mtgb.bridge.ForgeBridge " + Quote(assets) + " " + Quote(configPath),
                WorkingDirectory = assets, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            }};
            // Bound JIT startup contention; AI retains a second optimizing compiler.
            // Full tiered optimization and game/AI worker parallelism remain enabled.
            string compilerThreads = aiSeat > 0 ? "3" : "2";
            process.StartInfo.Arguments = "-XX:CICompilerCount=" + compilerThreads + " -Dmtgb.bridge.lazyCatalog=true " + process.StartInfo.Arguments;
            // A puzzle can keep a second worker parked at a verified input.
            // Start both heaps small; large puzzles can still grow normally.
            if (!string.IsNullOrEmpty(localPuzzle))
                process.StartInfo.Arguments = "-Xms16m -XX:+UseSerialGC " + process.StartInfo.Arguments;
            Utf8ProcessIO.Configure(process.StartInfo);
            process.StartInfo.EnvironmentVariables["APPDATA"] = directory;
            process.StartInfo.EnvironmentVariables["LOCALAPPDATA"] = directory;
            process.OutputDataReceived += (_, args) => { if (args.Data != null) incoming.Enqueue(args.Data); };
            // Engine diagnostics may contain hidden cards. Never broadcast stderr to clients.
            process.ErrorDataReceived += (_, args) => {
                if (args.Data == null) return;
                diagnostics.Enqueue(args.Data);
                if (args.Data.StartsWith("[shuffle-audit]", StringComparison.Ordinal))
                    UnityEngine.Debug.LogWarning("洗牌审计（" + MatchId + "）：" + args.Data);
                while (diagnostics.Count > 128) diagnostics.TryDequeue(out string discarded);
            };
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ValidationBeforeStart?.Invoke(process.StartInfo,configPath);
#endif
            process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
            activeSessions.Add(this);
        }
        catch { Dispose(); throw; }
    }
    public OnlinePlayerViewState GetView(int seat)
    {
        if (seat > views.Length) return OnlineSpectatorView.Create(views[0], seat);
        if (seat < 1) return null;
        return ServerSnapshotJson.Copy(views[seat - 1]);
    }
    // Only synchronous serialization may borrow an authority-owned view.
    // Public GetView keeps its independent-copy contract for all other callers.
    internal OnlinePlayerViewState GetViewForTransport(int seat)
        => seat>views.Length?OnlineSpectatorView.Create(views[0],seat):seat>=1?views[seat-1]:null;
    internal MatchOutcome Outcome=>views[0].outcome;

    public ForgeMatchRemoval[] MatchRemovals { get; private set; } = Array.Empty<ForgeMatchRemoval>();
    public ForgeMatchOwnership[] MatchOwnership { get; private set; } = Array.Empty<ForgeMatchOwnership>();

    public bool Poll()
    {
        if (disposed) return false;
        bool changed = false;
        while (incoming.TryDequeue(out string line))
        {
            try
            {
                ForgeWireMessage message = ServerSnapshotJson.Deserialize<ForgeWireMessage>(line);
                if (message == null || message.session != MatchId) throw new InvalidDataException("Invalid Forge session envelope.");
                if (message.type == "fatal") { Failure = "Forge 遇到未适配交互或引擎异常，本局异常终止。"; changed = true; break; }
                if (message.type == "invalidPuzzleAttachment")
                {
                    Failure = "「" + GamePreferences.CardFaceName(message.error) + "」未能结附于指定对象。请在编辑器中检查对象和起始状态。";
                    changed = true; break;
                }
                if (message.type == "accepted") continue;
                if (message.type == "rejected")
                {
                    // GUI callbacks may replace an input after the server queued its response.
                    // Forge rejected it without executing it and republishes the current request.
                    if (message.error == "stale-request") continue;
                    diagnostics.Enqueue("Rejected envelope: " + line);
                    Failure = "Forge 拒绝已通过服务器校验的输入，本局异常终止。"; changed = true; break;
                }
                if (message.type == "ended")
                {
                    MatchRemovals = message.matchRemovals ?? Array.Empty<ForgeMatchRemoval>();
                    MatchOwnership = message.matchOwnership ?? Array.Empty<ForgeMatchOwnership>();
                    AppliedSequence++;
                    if (message.views != null && message.views.Length == views.Length)
                        for (int i = 0; i < views.Length; i++) {
                            if(message.views[i].seat != i + 1) throw new InvalidDataException("Wrong final projection seat.");
                            views[i].forge = message.views[i];
                        }
                    foreach (var view in views)
                    {
                        view.appliedSequence = AppliedSequence; view.forge.request = null; view.forge.awaitingInput = false; view.forge.inputPending = false;
                        view.outcome = new MatchOutcome { isFinished = true, winnerPlayerId = message.winnerSeat, message = "Forge 对局结束。" };
                        view.SealHash();
                    }
                    replay.Record(startup.Elapsed.TotalSeconds, views.Select(v => v.forge).ToArray());
                    changed = true; continue;
                }
                if (message.type != "request") continue;
                if (message.views == null || message.views.Length != views.Length || message.request == null)
                    throw new InvalidDataException("Incomplete Forge projection.");
                AppliedSequence++; Ready = true; inputPending = false;
                for (int i = 0; i < views.Length; i++)
                {
                    ForgeSeatView seatView = message.views[i];
                    if (seatView.commanderEvent == OnlineRoomConfig.Loki)
                    {
                        // Only the public combat step and actor are shared.
                        seatView.heroCombatChoice = message.request.selectionPurpose == "combatDamage" ? "damage" :
                            message.request.kind == "InputAttack" ? "attack" : message.request.kind == "InputBlock" ? "block" : null;
                        seatView.heroCombatSeat = seatView.heroCombatChoice == null ? 0 : message.request.seat;
                    }
                    if (seatView.seat != i + 1) throw new InvalidDataException("Wrong projection seat.");
                    seatView.awaitingInput = message.request.seat == i + 1;
                    seatView.request = seatView.awaitingInput ? message.request : null;
                    views[i].forge = seatView; views[i].appliedSequence = AppliedSequence; views[i].SealHash();
                }
                UpdateHeroStepWindow(message.request);
                replay.Record(startup.Elapsed.TotalSeconds, views.Select(v => v.forge).ToArray());
                PassForReadyHero();
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
            // Release the worker before diagnostics and final snapshots allocate more memory.
            StopWorker();
            diagnosticsWritten = true;
            string directory = Path.Combine(Application.persistentDataPath, "ForgeDiagnostics");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".log");
            File.WriteAllLines(path, new[] { "Session: " + MatchId, "Failure: " + Failure }.Concat(diagnostics.ToArray()));
            UnityEngine.Debug.LogError("Forge 会话异常；仅服务器可见的引擎诊断：" + path);
        }
        return changed;
    }
    private void UpdateHeroStepWindow(ForgeRequest request)
    {
        var board = views[0].forge;
        if (board.commanderEvent != OnlineRoomConfig.Loki) return;
        bool eligible = board.turn > 0 && board.turnSeat != board.lokiSeat && board.stackCount == 0
            && request.kind == "InputPassPriority" && board.players != null;
        if (!eligible || board.turn != voteTurn || board.phase != votePhase)
            ResetHeroStepWindow();
        if (eligible && (request.seat != board.lokiSeat || voteWindow != 0))
        {
            if (voteWindow == 0) voteWindow = ++voteEpoch;
            voteTurn = board.turn;
            votePhase = board.phase;
            heroVoters = 0;
            foreach (var player in board.players)
                if (!player.eliminated && player.seat != board.lokiSeat) heroVoters |= 1 << (player.seat - 1);
            heroVotes &= heroVoters;
        }
        ProjectHeroVotes();
    }

    private void ResetHeroStepWindow()
    {
        voteWindow = heroVoters = heroVotes = 0;
    }

    private void ProjectHeroVotes()
    {
        foreach (var view in views)
        {
            view.forge.heroVoteWindow = voteWindow;
            view.forge.heroVoters = heroVoters;
            view.forge.heroVotes = heroVotes;
            view.appliedSequence = AppliedSequence;
            view.SealHash();
        }
    }

    private GameActionResult SetHeroStepVote(GameAction action)
    {
        int bit = 1 << (action.playerId - 1);
        if (views[0].outcome.isFinished || voteWindow == 0 || action.sourceObjectId != voteWindow ||
            (heroVoters & bit) == 0 || heroVotes == heroVoters || (action.value != 0 && action.value != 1))
            return GameActionResult.Rejected("本轮结束确认已关闭。", AppliedSequence);
        int votes = action.value == 1 ? heroVotes | bit : heroVotes & ~bit;
        if (votes != heroVotes)
        {
            heroVotes = votes;
            AppliedSequence++;
            ProjectHeroVotes();
            replay.Record(startup.Elapsed.TotalSeconds, views.Select(v => v.forge).ToArray());
            PassForReadyHero();
        }
        return GameActionResult.Accepted(AppliedSequence, views[action.playerId - 1].viewHash);
    }

    private void PassForReadyHero()
    {
        if (voteWindow == 0 || inputPending || heroVotes == 0) return;
        int actor = views[0].forge.prioritySeat;
        if (actor < 1 || actor > views.Length || (heroVotes & (1 << (actor - 1))) == 0) return;
        var request = views[actor - 1].forge.request;
        if (request?.kind != "InputPassPriority") return;
        int pass = Array.FindIndex(request.options, o => o.id == "ok" && !o.disabled);
        if (pass >= 0) ApplyAction(new GameAction { type=GameActionType.ConfirmChoice, playerId=actor,
            sourceObjectId=request.requestId, optionId=pass });
    }

    public GameActionResult ApplyAction(GameAction action)
    {
        if (disposed || Failure != null || action == null) return GameActionResult.Rejected("会话不可用。", AppliedSequence);
        if (action.playerId < 1 || action.playerId > views.Length) return GameActionResult.Rejected("无效席位。", AppliedSequence);
        if(action.playerId==aiSeat)return GameActionResult.Rejected("AI 席位由本地引擎控制。",AppliedSequence);
        if (action.type == GameActionType.SetHeroStepVote) return SetHeroStepVote(action);
        ForgeRequest request = views[action.playerId - 1].forge.request;
        bool concede = action.type == GameActionType.Concede;
        if (concede && (!Ready || views[0].outcome.isFinished))
            return GameActionResult.Rejected("对局尚未就绪或已经结束。", AppliedSequence);
        if (!concede && (action.type != GameActionType.ConfirmChoice || inputPending || request == null
            || !views[action.playerId - 1].forge.awaitingInput || action.sourceObjectId != request.requestId || action.optionId < 0 || action.optionId >= request.options.Length))
            return GameActionResult.Rejected("过期请求、无效候选或正在等待 Forge。", AppliedSequence);
        bool bulkOrder = !concede && request.options[action.optionId].id == "submit-order";
        bool combatGroup = !concede && !bulkOrder && action.optionIds!=null && action.optionIds.Count>0;
        if (bulkOrder) {
            var available = request.options.Where(o => o.id.StartsWith("order:") && o.ability != null).Select(o => o.ability.id)
                .Concat((request.orderedAbilities ?? Array.Empty<ForgeStackItem>()).Select(a => a.id)).ToHashSet();
            if (request.kind != "OrderAbilities" || action.optionIds == null || action.optionIds.Count < request.minCount
                || action.optionIds.Count > request.maxCount || action.optionIds.Distinct().Count() != action.optionIds.Count
                || action.optionIds.Any(id => !available.Contains(id)))
                return GameActionResult.Rejected("无效异能顺序。", AppliedSequence);
        }
        else if(combatGroup && !LegalCombatGroup(request,action.optionId,action.optionIds))
            return GameActionResult.Rejected("此分组包含无效的生物或攻防指派。",AppliedSequence);
        try
        {
            bool emptyPass = !concede && voteWindow > 0 && request.kind == "InputPassPriority"
                && request.options[action.optionId].id == "ok";
            process.StandardInput.WriteLine(JsonUtility.ToJson(new ForgeResponse {
                type = concede ? "concede" : "respond", session = MatchId, seat = action.playerId,
                requestId = request?.requestId ?? 0, option = concede ? null : request.options[action.optionId].id,
                heroHandoff = emptyPass && action.playerId != views[0].forge.lokiSeat && heroVotes != heroVoters,
                order = bulkOrder || combatGroup ? action.optionIds.ToArray() : null
            }));
            process.StandardInput.Flush();
            if (voteWindow != 0 && !emptyPass)
            {
                ResetHeroStepWindow();
                ProjectHeroVotes();
            }
            // An out-of-turn concession can leave another player's request outstanding.
            if (concede && views.Length > 2 && request == null)
                return GameActionResult.Accepted(AppliedSequence, views[action.playerId - 1].viewHash);
            inputPending = true;
            foreach (var view in views) { view.forge.request = null; view.forge.awaitingInput = false; view.forge.inputPending = true; view.SealHash(); }
            return GameActionResult.Accepted(AppliedSequence, views[action.playerId - 1].viewHash);
        }
        catch (Exception) { Failure = "无法向 Forge 提交输入。"; return GameActionResult.Rejected(Failure, AppliedSequence); }
    }
    internal static bool LegalCombatGroup(ForgeRequest request,int optionIndex,IReadOnlyList<int> members)
    {
        if(request?.options==null || optionIndex<0 || optionIndex>=request.options.Length || members==null || members.Count==0)return false;
        var first=request.options[optionIndex];
        var parts=first.id.Split(':');
        bool attack=request.kind=="InputAttack" && parts[0] is ("attack" or "attack-remove");
        bool block=request.kind=="InputBlock" && parts[0] is ("block" or "block-remove");
        if(first.disabled || !(attack || block) || parts.Length<2 || !int.TryParse(parts[1],out var firstId)
            || !members.Contains(firstId) || members.Distinct().Count()!=members.Count)return false;
        string suffix=parts.Length==2?"":":"+string.Join(":",parts,2,parts.Length-2);
        var offered=request.options.Where(o=>!o.disabled && (parts[0]!="block" || o.selected==first.selected)).Select(o=>o.id).ToHashSet();
        if(parts[0]=="block" && !first.selected && (first.blockTargetSlots>0 && members.Count>first.blockTargetSlots ||
            first.blockSeatSlots>0 && members.Count(id=>!offered.Contains("block-remove:"+id))>first.blockSeatSlots))return false;
        return members.All(id=>offered.Contains(parts[0]+":"+id+suffix));
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        activeSessions.Remove(this);
        try{replay.Dispose();}
        catch(IOException error){UnityEngine.Debug.LogWarning("无法清理录像临时文件："+error.Message);}
        catch(UnauthorizedAccessException error){UnityEngine.Debug.LogWarning("无法清理录像临时文件："+error.Message);}
        StopWorker();
    }
    private void StopWorker()
    {
        var worker = process;
        if (worker == null && !workerShutdown.IsCompleted) return;
        process = null;
        string shutdown = JsonUtility.ToJson(new ShutdownMessage { session = MatchId });
        string sessionDirectory = configPath == null ? null : Path.GetFullPath(Path.GetDirectoryName(configPath));
        string sessionsRoot = Path.GetFullPath(Path.Combine(Application.persistentDataPath, "ForgeSessions")) + Path.DirectorySeparatorChar;
        workerShutdown = Task.Run(() => {
            try {
                if (worker != null && !worker.HasExited) {
                    // A wedged stdin must not stall the authority or the timeout.
                    _ = Task.Run(() => { try { worker.StandardInput.WriteLine(shutdown); worker.StandardInput.Flush(); }
                        catch (IOException) { } catch (InvalidOperationException) { } });
                    if (!worker.WaitForExit(3000)) { worker.Kill(); worker.WaitForExit(500); }
                }
            }
            catch (InvalidOperationException) { } // Worker never started.
            catch (System.ComponentModel.Win32Exception error) { UnityEngine.Debug.LogError("无法结束 Forge 会话进程：" + error.Message); }
            finally {
                worker?.Dispose();
                if (sessionDirectory != null && sessionDirectory.StartsWith(sessionsRoot, StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(sessionDirectory)) {
                    try { Directory.Delete(sessionDirectory, true); }
                    catch (IOException error) { UnityEngine.Debug.LogWarning("Forge 临时目录清理：" + error.Message); }
                    catch (UnauthorizedAccessException error) { UnityEngine.Debug.LogWarning("Forge 临时目录清理：" + error.Message); }
                }
            }
        });
        lock (stoppingWorkers) stoppingWorkers.Add(workerShutdown);
        _ = workerShutdown.ContinueWith(task => { lock (stoppingWorkers) stoppingWorkers.Remove(task); });
    }
    private static string Quote(string value)
    {
        if (value.Contains("\"")) throw new ArgumentException("Invalid runtime path.");
        return "\"" + value + "\"";
    }
}
