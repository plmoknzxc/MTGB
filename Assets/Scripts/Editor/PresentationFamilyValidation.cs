#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sequentially runs existing presentation assertions. Engine assertions remain separate.
[InitializeOnLoad]
public static class PresentationFamilyValidation
{
    [Serializable] private sealed class Job { public string id, type, result; public string[] engineJobs; }
    [Serializable] private sealed class Configuration { public Job[] uiJobs; }
    [Serializable] private sealed class EngineJob { public string id, reportDir, status; }
    [Serializable] private sealed class Manifest { public EngineJob[] jobs; }
    [Serializable] private sealed class Provenance { public string fingerprint, revision; }
    [Serializable] private sealed class Coverage { public Provenance provenance; public string currentRun; }
    [Serializable] private sealed class EngineRun { public Provenance provenance; public EngineJob[] jobs; }
    [Serializable] private sealed class Result { public string id, status, inputs, inputEvidence, message, evidence; }
    [Serializable] private sealed class RunResult
    {
        public string run, fingerprint, forgeRevision, language, unityVersion, scene;
        public int width, height;
        public List<Result> jobs = new List<Result>();
    }

    private static Configuration configuration;
    private static Manifest manifest;
    private static EngineRun engine;
    private static RunResult run;
    private static int index;
    private static double started;
    private static bool waitingForLocale;
    private static string originalLanguage;
    private static string Root => Directory.GetParent(Application.dataPath).FullName;
    private static string Latest => Path.Combine(Root, ".tools/mechanic-coverage/unity-latest.json");
    private static T Read<T>(string relative) => JsonUtility.FromJson<T>(File.ReadAllText(Path.Combine(Root, relative)));

    static PresentationFamilyValidation()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Interrupt;
        EditorApplication.quitting += Interrupt;
    }

    [MenuItem("MTGB/验证/机制/批量验证共用交互展示")]
    public static void Run()
    {
        if (run != null) { Debug.LogWarning("共用交互展示验证正在运行。"); return; }
        if (!Application.isPlaying || SceneManager.GetActiveScene().name != ForgeBattlefieldController.SceneName)
            throw new InvalidOperationException("先在 IntegratedBattlefield 场景进入运行模式；测试会替换展示的局面。");
        if (UnityEngine.Object.FindObjectsByType<OnlineMatchService>(FindObjectsSortMode.None).Any(s => s.IsInMatch))
            throw new InvalidOperationException("请先结束实际对局，再运行展示验证。");
        var coverage = Read<Coverage>("Tools/Validation/Reports/MechanicCoverage/coverage.json");
        if (string.IsNullOrEmpty(coverage.currentRun))
            throw new InvalidOperationException("先生成清单并运行当前代码的引擎场景，旧记录不能用作本轮证据。");
        configuration = Read<Configuration>("Tools/ForgeBridge/PresentationFamilies.json");
        manifest = Read<Manifest>("Tools/ForgeBridge/MechanicCoverage.json");
        engine = Read<EngineRun>(".tools/mechanic-coverage/latest.json");
        if (engine.provenance.fingerprint != coverage.provenance.fingerprint)
            throw new InvalidOperationException("引擎运行与清单版本不一致；请刷新清单。");
        originalLanguage = GamePreferences.Language;
        GamePreferences.SetLanguage("zh-CN");
        if (UnityEngine.Object.FindFirstObjectByType<ForgeBattlefieldController>() == null)
            new GameObject("FamilyValidationUi").AddComponent<ForgeBattlefieldController>();
        run = new RunResult {
            run = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ"),
            fingerprint = coverage.provenance.fingerprint, forgeRevision = coverage.provenance.revision,
            language = "zh-CN", unityVersion = Application.unityVersion,
            scene = SceneManager.GetActiveScene().name, width = Screen.width, height = Screen.height
        };
        index = -1;
        EditorApplication.update += Poll;
        Application.logMessageReceived += ObserveError;
        Debug.Log("开始共用交互展示验证；规则与界面证据分别保存。");
        Next();
    }

    private static string InputHash(Job job)
    {
        using var bytes = new MemoryStream();
        var paths = job.engineJobs.Select(id => manifest.jobs.First(j => j.id == id).reportDir)
            .SelectMany(dir => Directory.GetFiles(Path.Combine(Root, dir), "*", SearchOption.AllDirectories))
            .Where(p => p.EndsWith(".json") || p.EndsWith(".jsonl"))
            .Distinct().OrderBy(p => Path.GetRelativePath(Root, p).Replace('\\', '/'), StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var name = Encoding.UTF8.GetBytes(Path.GetRelativePath(Root, path).Replace('\\', '/'));
            bytes.Write(name, 0, name.Length); bytes.WriteByte(0);
            var content = File.ReadAllBytes(path); bytes.Write(content, 0, content.Length);
        }
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
    }

    private static void Next()
    {
        index++;
        if (index >= configuration.uiJobs.Length) { Finish(); return; }
        var job = configuration.uiJobs[index];
        var result = new Result { id = job.id, status = "RUNNING" };
        run.jobs.Add(result);
        started = EditorApplication.timeSinceStartup;
        try
        {
            if (job.engineJobs.Any(id => !engine.jobs.Any(j => j.id == id && j.status == "PASS")))
                throw new InvalidOperationException("该展示检查依赖的引擎场景本轮未通过：" + string.Join(", ", job.engineJobs));
            result.inputs = InputHash(job);
            string path = Path.Combine(Root, job.result);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "RUNNING"); // Never inherit a previous suite's PASS.
            waitingForLocale=true;
        }
        catch (Exception error) { result.status = "FAIL"; result.message = error.GetBaseException().ToString(); }
        Save();
    }

    private static void StartCurrentJob()
    {
        waitingForLocale=false;
        try {
            var job=configuration.uiJobs[index];
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(job.type)).FirstOrDefault(t=>t!=null);
            var method=type?.GetMethod("Run",BindingFlags.Public|BindingFlags.Static);
            if(method==null)throw new MissingMethodException(job.type,"Run");
            started=EditorApplication.timeSinceStartup;
            method.Invoke(null,null);
        } catch(Exception error) {run.jobs.Last().status="FAIL";run.jobs.Last().message=error.GetBaseException().ToString();}
    }

    private static void ObserveError(string message, string stack, LogType type)
    {
        if (run == null || type != LogType.Exception && type != LogType.Error) return;
        var result = run.jobs.Last();
        result.status = "FAIL"; result.message = message + "\n" + stack;
    }

    private static void Poll()
    {
        if (!Application.isPlaying) { Interrupt(); return; }
        var result = run.jobs.Last();
        var job = configuration.uiJobs[index];
        if(waitingForLocale&&result.status=="RUNNING") {
            if(GamePreferences.Ready) {StartCurrentJob();Save();return;}
            if(EditorApplication.timeSinceStartup-started<8)return;
            waitingForLocale=false;result.status="FAIL";result.message="当前语言资源没有加载完成。";
        }
        string path = Path.Combine(Root, job.result);
        if (result.status == "RUNNING")
        {
            string text = File.Exists(path) ? File.ReadAllText(path) : "";
            if (text.StartsWith("PASS", StringComparison.Ordinal)) { result.status = "PASS"; result.message = text; }
            else if (text.StartsWith("FAIL", StringComparison.Ordinal)) { result.status = "FAIL"; result.message = text; }
            else if (EditorApplication.timeSinceStartup - started > 60) { result.status = "TIMEOUT"; result.message = "展示测试未产生本轮完成证据。"; }
            else return;
        }
        string folder = Path.Combine(Root, ".tools/mechanic-coverage/unity", run.run, job.id);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "result.txt"), result.message ?? result.status);
        // Screenshots are useful for review, but their presence never makes a test pass.
        foreach (var png in Directory.GetFiles(Path.GetDirectoryName(path), "*.png"))
            if (File.GetLastWriteTimeUtc(png) >= DateTime.UtcNow.AddSeconds(-(EditorApplication.timeSinceStartup - started) - 1))
                File.Copy(png, Path.Combine(folder, Path.GetFileName(png)), true);
        result.evidence = Path.GetRelativePath(Root, folder).Replace('\\', '/');
        if (result.status == "PASS" && result.inputs != InputHash(job)) { result.status = "FAIL"; result.message = "测试期间输入快照发生变化。"; }
        if (result.status == "PASS")
        {
            string inputs = Path.Combine(folder, "inputs");
            foreach (var id in job.engineJobs)
                foreach (var source in Directory.GetFiles(Path.Combine(Root, manifest.jobs.First(j => j.id == id).reportDir), "*", SearchOption.AllDirectories)
                    .Where(p => p.EndsWith(".json") || p.EndsWith(".jsonl")))
                {
                    string target = Path.Combine(inputs, Path.GetRelativePath(Root, source));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(source, target, true);
                }
            Directory.CreateDirectory(inputs);
            result.inputEvidence = Path.GetRelativePath(Root, inputs).Replace('\\', '/');
        }
        foreach (var ui in UnityEngine.Object.FindObjectsByType<ForgeBattlefieldController>(FindObjectsSortMode.None))
        { ui.StopAllCoroutines(); ui.Present(null, 1, false, null, null, ""); }
        Save();
        Next();
    }

    private static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Latest));
        string text = JsonUtility.ToJson(run, true);
        File.WriteAllText(Latest, text);
        var archive = Path.Combine(Root, ".tools/mechanic-coverage/unity", run.run);
        Directory.CreateDirectory(archive); File.WriteAllText(Path.Combine(archive, "results.json"), text);
    }

    private static void Interrupt()
    {
        if (run == null) return;
        run.jobs.Last().status = "INTERRUPTED";
        run.jobs.Last().message = "运行模式停止、编译重载或编辑器退出，不能保留 PASS。";
        Save(); Finish();
    }

    private static void Finish()
    {
        EditorApplication.update -= Poll;
        Application.logMessageReceived -= ObserveError;
        bool failed = run.jobs.Any(j => j.status != "PASS");
        run = null;
        if (!string.IsNullOrEmpty(originalLanguage)) GamePreferences.SetLanguage(originalLanguage);
        originalLanguage = null;
        Debug.Log(failed ? "共用交互展示验证含失败项；证据已保存，请刷新覆盖清单。" : "已完成登记的共用交互展示检查；请刷新覆盖清单汇总证据。");
    }
}
#endif
