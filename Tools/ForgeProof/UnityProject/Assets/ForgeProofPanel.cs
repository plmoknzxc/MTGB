using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Mtgb.ForgeProof
{
    [Serializable] public class BridgeConfig { public string java, classpath, assets, revision; }
    [Serializable] public class Choice { public string id, label; }
    [Serializable] public class Request { public long requestId; public int seat; public string kind, message; public Choice[] options; }
    [Serializable] public class Card { public int id; public string name; public bool tapped; }
    [Serializable] public class Player { public int seat, life, handCount, libraryCount, redMana; public Card[] hand, battlefield; public string[] graveyard; }
    [Serializable] public class View { public int seat, stackCount; public string phase; public bool fixtureReady; public Player[] players; }
    [Serializable] public class Message { public string type, session, error; public Request request; public long requestId, elapsedMs; public View[] views; }
    [Serializable] public class Response { public string type = "respond", session, option; public int seat; public long requestId; }
    [Serializable] public class RunResult
    {
        public int run, javaPid, finalLife, framesWhileWaiting;
        public double startupMs, durationMs, peakJavaMiB, peakUnityMiB;
        public bool passed, cleanExit, sawLand, sawMana, sawTarget, paidMana, sawStack, passedSeat1, passedSeat2, rejectedInvalid, rejectedDuplicate, hiddenViewsPassed;
        public List<double> roundTripMs = new List<double>();
        public List<double> nextRequestMs = new List<double>();
        public List<string> trace = new List<string>();
    }
    [Serializable] public class ProofReport
    {
        public string unityVersion, forgeRevision, environment, error;
        public bool passed;
        public List<RunResult> runs = new List<RunResult>();
    }

    public sealed class ForgeProofPanel : MonoBehaviour
    {
        public static bool Finished { get; private set; }
        public static bool Passed { get; private set; }
        Process child;
        readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> diagnostics = new ConcurrentQueue<string>();
        readonly Dictionary<long, double> sentAt = new Dictionary<long, double>();
        readonly Stopwatch watch = new Stopwatch();
        StreamWriter transcript, javaLog;
        BridgeConfig config;
        ProofReport report;
        RunResult result;
        Message current;
        string configPath, outputPath, status = "Ready", error, guardBefore;
        bool auto, sent, guardPending, invalidChecked, duplicated;
        double nextAction, pendingSince, nextSample;
        int selectedSeat = 1, frameAtRequest;
        long duplicateId;
        double lastInputAt;
        bool targetCaptured;
        Vector2 scroll;

        public static string Arg(string name, string fallback = null)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return fallback;
        }
        void Start()
        {
            Finished = Passed = false;
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            configPath = Arg("-proof-config", Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../.tools/forge-proof/runtime/bridge-config.json")));
            outputPath = Arg("-proof-report", Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/proof-report.json")));
            auto = Environment.GetCommandLineArgs().Contains("-proof-auto");
            try
            {
                config = JsonUtility.FromJson<BridgeConfig>(File.ReadAllText(configPath));
                report = new ProofReport { unityVersion = Application.unityVersion, forgeRevision = config.revision, environment = Application.isEditor ? "Unity Play Mode" : "Windows Player" };
                if (auto) StartRun();
            }
            catch (Exception e) { Fail(e.ToString()); }
        }
        void StartRun()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            result = new RunResult { run = report.runs.Count + 1, hiddenViewsPassed = true };
            report.runs.Add(result);
            transcript = new StreamWriter(outputPath + ".run" + result.run + ".wire.jsonl", false, new UTF8Encoding(false)) { AutoFlush = true };
            javaLog = new StreamWriter(outputPath + ".run" + result.run + ".java.log", false, new UTF8Encoding(false)) { AutoFlush = true };
            current = null; sent = guardPending = invalidChecked = duplicated = targetCaptured = false; lastInputAt = 0;
            sentAt.Clear(); watch.Restart(); nextSample = 0;
            var info = new ProcessStartInfo
            {
                FileName = config.java,
                Arguments = "-Djava.awt.headless=true -Dfile.encoding=UTF-8 -Xmx1024m -cp " + Quote(config.classpath) + " mtgb.proof.ForgeProof " + Quote(config.assets),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                StandardInputEncoding = new UTF8Encoding(false), WorkingDirectory = Path.GetDirectoryName(configPath)
            };
            child = new Process { StartInfo = info };
            child.OutputDataReceived += (_, e) => { if (e.Data != null) incoming.Enqueue(e.Data); };
            child.ErrorDataReceived += (_, e) => { if (e.Data != null) diagnostics.Enqueue(e.Data); };
            child.Start(); result.javaPid = child.Id;
            child.BeginOutputReadLine(); child.BeginErrorReadLine();
            status = "Starting Forge…";
        }
        static string Quote(string s) { if (s.Contains("\"")) throw new ArgumentException("Unexpected quote in path"); return "\"" + s + "\""; }
        void Update()
        {
            if (Finished || child == null) return;
            try
            {
                while (diagnostics.TryDequeue(out string log)) javaLog.WriteLine(log);
                while (incoming.TryDequeue(out string line))
                {
                    transcript.WriteLine(line);
                    Message m = JsonUtility.FromJson<Message>(line);
                    if (m.type == "fatal") throw new InvalidOperationException(m.error);
                    ValidateViews(m.views);
                    if (m.type == "accepted" && sentAt.TryGetValue(m.requestId, out double at))
                    { result.roundTripMs.Add(watch.Elapsed.TotalMilliseconds - at); sentAt.Remove(m.requestId); }
                    if (m.type == "rejected")
                    {
                        if (m.error == "invalid-option" && guardPending)
                        {
                            if (ViewsJson(m.views) != guardBefore) throw new InvalidOperationException("Invalid target mutated state");
                            result.rejectedInvalid = true; guardPending = false; invalidChecked = true; sent = false; nextAction = watch.Elapsed.TotalSeconds + .2;
                        }
                        else if (m.error == "stale-request" && m.requestId == duplicateId) result.rejectedDuplicate = true;
                        else if (m.error == "stale-request") { sent = false; nextAction = watch.Elapsed.TotalSeconds + .2; }
                        else throw new InvalidOperationException("Unexpected rejection: " + m.error);
                    }
                    if (m.type != "request") continue;
                    if (lastInputAt > 0) { result.nextRequestMs.Add(watch.Elapsed.TotalMilliseconds - lastInputAt); lastInputAt = 0; }
                    if (result.startupMs == 0) result.startupMs = watch.Elapsed.TotalMilliseconds;
                    current = m; sent = false; selectedSeat = auto ? m.request.seat : selectedSeat;
                    pendingSince = watch.Elapsed.TotalSeconds; frameAtRequest = Time.frameCount;
                    nextAction = pendingSince + .3;
                    status = m.request.kind + " — Player " + m.request.seat;
                    result.trace.Add(m.request.requestId + ":" + m.request.seat + ":" + m.request.kind);
                    View v = m.views[0];
                    result.sawLand |= v.players[0].battlefield.Any(c => c.name == "Mountain");
                    result.sawMana |= v.players[0].redMana == 1;
                    result.sawTarget |= m.request.kind == "InputSelectTargets";
                    result.sawStack |= v.stackCount == 1;
                    if (v.players[1].life == 17)
                    {
                        result.finalLife = 17;
                        result.passed = result.sawLand && result.sawMana && result.sawTarget && result.paidMana && result.sawStack && result.passedSeat1 && result.passedSeat2 && v.stackCount == 0 && v.players[0].redMana == 0 && v.players[0].graveyard.Contains("Lightning Bolt");
                        if (auto) result.passed &= result.rejectedInvalid && result.rejectedDuplicate && result.framesWhileWaiting > 5;
                        if (!result.passed) throw new InvalidOperationException("Life changed, but required evidence is missing");
                        CompleteRun(); return;
                    }
                }
                if (child.HasExited) throw new InvalidOperationException("Forge exited unexpectedly: " + child.ExitCode);
                if (watch.Elapsed.TotalSeconds > nextSample)
                {
                    result.peakJavaMiB = Math.Max(result.peakJavaMiB, WorkingSet(child));
                    using (var self = Process.GetCurrentProcess()) result.peakUnityMiB = Math.Max(result.peakUnityMiB, WorkingSet(self));
                    nextSample = watch.Elapsed.TotalSeconds + .1;
                }
                if (auto && watch.Elapsed.TotalSeconds > 90) throw new TimeoutException("Probe exceeded 90 seconds");
                if (auto && current != null && !sent && !guardPending && watch.Elapsed.TotalSeconds >= nextAction) AutoRespond();
            }
            catch (Exception e) { Fail(e.ToString()); }
        }
        void ValidateViews(View[] views)
        {
            if (views == null) return;
            foreach (View view in views)
                foreach (Player player in view.players)
                    if (player.seat != view.seat && player.hand.Length != 0)
                        throw new InvalidOperationException("Opponent hand leaked into seat view");
        }
        static string ViewsJson(View[] views) { return string.Join("\n", views.Select(JsonUtility.ToJson)); }
        [StructLayout(LayoutKind.Sequential)] struct MemoryCounters
        {
            public uint cb, pageFaultCount;
            public UIntPtr peakWorkingSet, workingSet, quotaPeakPaged, quotaPaged, quotaPeakNonPaged, quotaNonPaged, pagefile, peakPagefile;
        }
        [DllImport("psapi.dll", SetLastError = true)] static extern bool GetProcessMemoryInfo(IntPtr process, ref MemoryCounters counters, uint size);
        static double WorkingSet(Process process)
        {
            var counters = new MemoryCounters { cb = (uint)Marshal.SizeOf<MemoryCounters>() };
            if (!GetProcessMemoryInfo(process.Handle, ref counters, counters.cb)) throw new InvalidOperationException("Memory sampling failed: " + Marshal.GetLastWin32Error());
            return counters.workingSet.ToUInt64() / 1048576d;
        }
        void AutoRespond()
        {
            Request r = current.request; View v = current.views[0];
            if (r.kind == "InputSelectTargets" && !invalidChecked)
            {
                if (watch.Elapsed.TotalSeconds - pendingSince < 1)
                { nextAction = pendingSince + 1; return; }
                result.framesWhileWaiting = Time.frameCount - frameAtRequest;
                if (!targetCaptured && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                { ScreenCapture.CaptureScreenshot(outputPath + ".target.png"); targetCaptured = true; }
                guardBefore = ViewsJson(current.views); guardPending = true;
                // A public Mountain is real but is not a legal Lightning Bolt target.
                Send("card:" + v.players[0].battlefield.First(c => c.name == "Mountain").id); return;
            }
            Choice choice = null;
            if (v.fixtureReady && r.seat == 1 && r.kind == "InputPassPriority" && v.phase == "MAIN1" && v.stackCount == 0)
            {
                string label = !result.sawLand ? "Mountain [Hand]" : !result.sawMana ? "Mountain [Battlefield]" : "Lightning Bolt [Hand]";
                choice = r.options.FirstOrDefault(o => o.label == label);
            }
            if (r.kind == "InputSelectTargets") choice = r.options.First(o => o.label == "Player 2");
            if (r.kind.StartsWith("InputPayMana")) choice = r.options.FirstOrDefault(o => o.id == "mana:R");
            choice = choice ?? r.options.FirstOrDefault(o => o.id == "ok") ?? r.options[0];
            Respond(choice.id);
            if (r.kind == "InputSelectTargets" && !duplicated)
            {
                duplicated = true; duplicateId = r.requestId;
                Send(choice.id); // exact same request: must not execute twice
            }
        }
        void Respond(string option)
        {
            if (current.request.kind.StartsWith("InputPayMana") && option == "mana:R") result.paidMana = true;
            if (current.request.kind == "InputPassPriority" && current.views[0].stackCount == 1 && option == "ok")
            { if (current.request.seat == 1) result.passedSeat1 = true; else result.passedSeat2 = true; }
            Send(option);
        }
        void Send(string option)
        {
            Request r = current.request;
            var response = new Response { session = current.session, seat = r.seat, requestId = r.requestId, option = option };
            string json = JsonUtility.ToJson(response); transcript.WriteLine(json);
            sentAt[r.requestId] = watch.Elapsed.TotalMilliseconds;
            lastInputAt = watch.Elapsed.TotalMilliseconds;
            child.StandardInput.WriteLine(json); child.StandardInput.Flush(); sent = true;
        }
        void CompleteRun()
        {
            result.durationMs = watch.Elapsed.TotalMilliseconds;
            StopChild(); result.passed &= result.cleanExit;
            if (!result.passed) { Fail("Child process failed to exit cleanly"); return; }
            SaveReport();
            if (auto && report.runs.Count < 3) { StartRun(); return; }
            report.passed = report.runs.All(r => r.passed); SaveReport();
            Passed = report.passed; Finished = true; status = "PASS — Player 2: 20 → 17";
            Debug.Log("FORGE_UNITY_PROOF_PASS " + outputPath);
            if (!Application.isEditor && auto) StartCoroutine(CaptureAndExit());
        }
        System.Collections.IEnumerator CaptureAndExit()
        {
            yield return null;
            ScreenCapture.CaptureScreenshot(outputPath + ".success.png");
            yield return new WaitForSecondsRealtime(1);
            Application.Quit(0);
        }
        void StopChild()
        {
            if (child == null) return;
            try
            {
                if (!child.HasExited)
                {
                    child.StandardInput.WriteLine("{\"type\":\"shutdown\"}"); child.StandardInput.Flush();
                    if (!child.WaitForExit(3000)) child.Kill();
                    child.WaitForExit(2000);
                }
                result.cleanExit = child.HasExited && child.ExitCode == 0;
            }
            catch (Exception e) { Debug.LogWarning(e.Message); try { if (!child.HasExited) child.Kill(); } catch { } }
            finally
            {
                child.Dispose(); child = null;
                while (diagnostics.TryDequeue(out string line)) javaLog?.WriteLine(line);
                while (incoming.TryDequeue(out string line)) transcript?.WriteLine(line);
                javaLog?.Dispose(); transcript?.Dispose(); javaLog = transcript = null;
            }
        }
        void SaveReport() { File.WriteAllText(outputPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false)); }
        void Fail(string reason)
        {
            error = reason; status = "FAIL"; StopChild();
            report = report ?? new ProofReport(); report.error = reason; report.passed = false;
            if (!string.IsNullOrEmpty(outputPath)) { Directory.CreateDirectory(Path.GetDirectoryName(outputPath)); SaveReport(); }
            Finished = true; Passed = false; Debug.LogError(reason);
            if (!Application.isEditor && auto) Application.Quit(1);
        }
        void OnApplicationQuit() { StopChild(); }
        void OnDestroy() { StopChild(); }
        void OnGUI()
        {
            GUI.skin.label.fontSize = 17; GUI.skin.button.fontSize = 17;
            GUILayout.BeginArea(new Rect(25, 20, Screen.width - 50, Screen.height - 40));
            GUILayout.Label("Forge ↔ Unity — real rules integration probe");
            GUILayout.Label(status);
            if (error != null) GUILayout.TextArea(error);
            if (config != null && child == null && !Finished && GUILayout.Button("Start Forge")) StartRun();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Player 1 view")) selectedSeat = 1;
            if (GUILayout.Button("Player 2 view")) selectedSeat = 2;
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            if (current != null)
            {
                View view = current.views.First(v => v.seat == selectedSeat);
                GUILayout.Label("Phase: " + view.phase + "   Stack: " + view.stackCount);
                foreach (Player p in view.players)
                {
                    GUILayout.Label("Player " + p.seat + "   Life: " + p.life + "   Red mana: " + p.redMana + "   Hand: " + p.handCount);
                    if (p.seat == selectedSeat) GUILayout.Label("Your hand: " + string.Join(", ", p.hand.Select(c => c.name)));
                    GUILayout.Label("Battlefield: " + string.Join(", ", p.battlefield.Select(c => c.name + (c.tapped ? " (tapped)" : ""))));
                }
                if (selectedSeat == current.request.seat)
                {
                    GUILayout.Label(current.request.message);
                    GUI.enabled = !sent && !Finished && !auto;
                    foreach (Choice option in current.request.options) if (GUILayout.Button(option.label)) Respond(option.id);
                    GUI.enabled = true;
                }
                else GUILayout.Label("Waiting for Player " + current.request.seat);
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
    }
}
