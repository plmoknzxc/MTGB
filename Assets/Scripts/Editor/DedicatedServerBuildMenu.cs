#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class DedicatedServerBuildMenu
{
    private const string DefaultOutput = "Builds/Server/MTGBServer.exe";
    private static bool building;

    [MenuItem("MTGB/构建/Windows 独立服务器")]
    public static void BuildFromMenu()
    {
        if (building) return;
        building = true;
        EditorApplication.delayCall += () => {
            try { Build(DefaultOutput); }
            finally { building = false; }
        };
    }

    public static void BuildFromCommandLine()
    {
        string output = ReadArgument("-serverOutput") ?? DefaultOutput;
        Build(output);
    }

    private static void Build(string output)
    {
        string fullOutput = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutput) ?? "Builds/Server");
        string[] scenes = { "Assets/Scenes/Main.unity" };

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = fullOutput,
            target = BuildTarget.StandaloneWindows64,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.CleanBuildCache
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Dedicated server build failed: {report.summary.result}");
        }
        ForgeBuildMenu.CopyRuntimeTo(Path.GetDirectoryName(fullOutput));
        Debug.Log($"MTGB dedicated server built: {fullOutput}\nRun: MTGBServer.exe -batchmode -nographics -mtgb-server -port 7777");
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
