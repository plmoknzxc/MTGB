#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public static class MechanicCoverageMenu
{
    private static Process process;
    private static Task<string> output, errors;
    private static string Root => Directory.GetParent(Application.dataPath).FullName;
    private static string Report => Path.Combine(Root, "Tools/Validation/Reports/MechanicCoverage/README.md");

    static MechanicCoverageMenu()
    {
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
        EditorApplication.quitting += Stop;
    }

    [MenuItem("MTGB/验证/机制/生成覆盖清单")]
    public static void Generate() => Begin(false);

    [MenuItem("MTGB/验证/机制/批量验证已登记场景")]
    public static void Run() => Begin(true);

    [MenuItem("MTGB/验证/机制/打开覆盖清单")]
    public static void Open()
    {
        if (File.Exists(Report)) EditorUtility.OpenWithDefaultApp(Report);
        else Generate();
    }

    private static void Begin(bool run)
    {
        if (process != null) { Debug.LogWarning("机制验证任务正在运行。"); return; }
        try
        {
            var script = Path.Combine(Root, "Tools/ForgeBridge/MechanicCoverage.py");
            process = Process.Start(new ProcessStartInfo("python", "-X utf8 \"" + script + "\"" + (run ? " --run" : ""))
            {
                WorkingDirectory = Root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            });
            output = process.StandardOutput.ReadToEndAsync();
            errors = process.StandardError.ReadToEndAsync();
            EditorApplication.update += Poll;
            Debug.Log(run ? "开始批量机制回归；结果、缺口和证据会自动保存。" : "正在生成机制覆盖清单。");
        }
        catch (Exception error) { Stop(); Debug.LogException(error); }
    }

    private static void Poll()
    {
        if (!process.HasExited || !output.IsCompleted || !errors.IsCompleted) return;
        int code = process.ExitCode;
        string text = output.Result + errors.Result;
        EditorApplication.update -= Poll;
        process.Dispose(); process = null;
        if (code == 0) Debug.Log("机制覆盖任务完成。PASS 仅限清单内断言。\n" + text);
        else Debug.LogError("机制覆盖任务失败；请查看清单和 .tools/mechanic-coverage 中的日志。\n" + text);
        if (File.Exists(Report)) EditorUtility.OpenWithDefaultApp(Report);
    }

    private static void Stop()
    {
        EditorApplication.update -= Poll;
        if (process == null) return;
        if (!process.HasExited)
        {
            using (var cleanup = Process.Start(new ProcessStartInfo("taskkill", "/PID " + process.Id + " /T /F")
            { UseShellExecute = false, CreateNoWindow = true })) cleanup?.WaitForExit(5000);
        }
        process.Dispose(); process = null;
    }
}
#endif
