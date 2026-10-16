#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class DedicatedServerBuildMenu
{
    private const string DefaultOutput = "Builds/Server/MTGBServer.exe";
    private const string LinuxOutput = "Builds/LinuxServer/MTGBServer";
    private const string PendingKey = "MTGB.DedicatedBuild.Pending";
    private static bool building;
    [Serializable] private sealed class PendingBuild { public string output, javaRoot; public BuildTarget target; }

    [MenuItem("MTGB/构建/Windows 独立服务器")]
    public static void BuildFromMenu()
    {
        if (building) return;
        QueueBuild(DefaultOutput, BuildTarget.StandaloneWindows64, null);
    }

    [MenuItem("MTGB/构建/Linux 独立服务器")]
    public static void BuildLinuxFromMenu()
    {
        if (building) return;
        string javaRoot = EditorUtility.OpenFolderPanel("选择 Linux Java 17 运行环境（含 bin/java）", "", "");
        if (string.IsNullOrEmpty(javaRoot)) return;
        QueueBuild(LinuxOutput, BuildTarget.StandaloneLinux64, javaRoot);
    }

    private static void QueueBuild(string output, BuildTarget target, string javaRoot)
    {
        SessionState.SetString(PendingKey, JsonUtility.ToJson(new PendingBuild { output = output, target = target, javaRoot = javaRoot }));
        EditorApplication.delayCall += ResumePendingBuild;
    }

    [InitializeOnLoadMethod]
    private static void AfterReload()
    {
        if (!string.IsNullOrEmpty(SessionState.GetString(PendingKey, ""))) EditorApplication.delayCall += ResumePendingBuild;
    }

    private static void ResumePendingBuild()
    {
        string json = SessionState.GetString(PendingKey, "");
        if (building || string.IsNullOrEmpty(json)) return;
        if (EditorApplication.isCompiling) { EditorApplication.delayCall += ResumePendingBuild; return; }
        var pending = JsonUtility.FromJson<PendingBuild>(json);
        if (EditorUserBuildSettings.activeBuildTarget != pending.target)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, pending.target))
                throw new InvalidOperationException("无法切换服务器构建平台。");
            EditorApplication.delayCall += ResumePendingBuild;
            return;
        }
        SessionState.EraseString(PendingKey);
        building = true;
        try { Build(pending.output, pending.target, pending.javaRoot); }
        finally { building = false; }
    }

    public static void BuildFromCommandLine()
    {
        bool linux = string.Equals(ReadArgument("-serverPlatform"), "linux", StringComparison.OrdinalIgnoreCase);
        string output = ReadArgument("-serverOutput") ?? (linux ? LinuxOutput : DefaultOutput);
        Build(output, linux ? BuildTarget.StandaloneLinux64 : BuildTarget.StandaloneWindows64, ReadArgument("-serverJavaRoot"));
    }

    public static void Build(string output, BuildTarget target, string linuxJavaRoot)
    {
        if (target == BuildTarget.StandaloneLinux64 && (string.IsNullOrEmpty(linuxJavaRoot) || !File.Exists(Path.Combine(linuxJavaRoot, "bin", "java"))))
            throw new InvalidOperationException("Linux 构建需要包内 Linux Java 17，请使用 -serverJavaRoot 指定运行环境目录。");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            throw new InvalidOperationException("请先安装对应平台的 Dedicated Server 构建模块。");
        string fullOutput = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutput) ?? "Builds/Server");
        string[] scenes = { "Assets/Scenes/DedicatedServer.unity" };

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = fullOutput,
            target = target,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            // App UI supplies this constraint on all of its runtime assemblies.
            extraScriptingDefines = new[] { "APP_UI_EDITOR_ONLY" },
            options = BuildOptions.None
        };
        var appUiPlugins = PluginImporter.GetImporters(target)
            .Where(plugin => plugin.assetPath.StartsWith("Packages/com.unity.dt.app-ui/", StringComparison.Ordinal)).ToArray();
        var addressables = AddressableAssetSettingsDefaultObject.Settings;
        var previousAddressables = addressables.BuildAddressablesWithPlayerBuild;
        BuildReport report;
        try
        {
            // Native App UI plugins are preloaded independently of managed references.
            foreach (var plugin in appUiPlugins) plugin.SetIncludeInBuildDelegate(_ => false);
            addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            foreach (var plugin in appUiPlugins) plugin.SetIncludeInBuildDelegate(null);
            addressables.BuildAddressablesWithPlayerBuild = previousAddressables;
        }
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Dedicated server build failed: {report.summary.result}");
        }
        // Display translations, art indexes and addressable UI bundles belong to clients.
        string streaming = Path.Combine(Path.GetDirectoryName(fullOutput), Path.GetFileNameWithoutExtension(fullOutput) + "_Data", "StreamingAssets");
        if (Directory.Exists(streaming))
        {
            string safeRoot = Path.GetFullPath(streaming).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (string entry in Directory.GetFileSystemEntries(streaming))
            {
                if (Path.GetFileName(entry) == "DeckFormats") continue;
                string fullEntry = Path.GetFullPath(entry);
                if (!fullEntry.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase) || (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("拒绝清理输出目录之外的资源。");
                if (Directory.Exists(entry)) Directory.Delete(fullEntry, true); else File.Delete(fullEntry);
            }
        }
        ForgeBuildMenu.CopyRuntimeTo(Path.GetDirectoryName(fullOutput), target == BuildTarget.StandaloneLinux64 ? linuxJavaRoot : null, dedicatedServer: true);
        if (target == BuildTarget.StandaloneLinux64)
            File.Copy("Tools/ForgeBridge/Run-DedicatedServer.sh", Path.Combine(Path.GetDirectoryName(fullOutput), "run-server.sh"), true);
        Debug.Log($"MTGB dedicated server built: {fullOutput}\nRun: {Path.GetFileName(fullOutput)} -batchmode -nographics -mtgb-server -port 7777");
    }

    private static string ReadArgument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        }
        return null;
    }
}
#endif
