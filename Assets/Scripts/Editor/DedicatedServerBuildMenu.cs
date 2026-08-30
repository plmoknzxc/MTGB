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

    [MenuItem("MTGB/Networking/Build Dedicated Server")]
    public static void BuildFromMenu()
    {
        Build(DefaultOutput);
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
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0) throw new InvalidOperationException("No enabled scenes in Build Settings.");

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
