#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ForgeBuildMenu
{
    private static bool building;
    [MenuItem("MTGB/构建/Windows 客户端（开发版）")]
    public static void BuildValidationPlayer()
    {
        if (building) return;
        building = true;
        EditorApplication.delayCall += () => {
            try { BuildPlayer(); }
            finally { building = false; }
        };
    }
    private static void BuildPlayer()
    {
        string root = Path.GetFullPath("Builds/ForgeValidation");
        Directory.CreateDirectory(root);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/Main.unity", "Assets/Scenes/IntegratedBattlefield.unity" },
            locationPathName = Path.Combine(root, "MTGB.exe"), target = BuildTarget.StandaloneWindows64,
            subtarget = (int)StandaloneBuildSubtarget.Player,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Forge validation player build failed.");
        CopyRuntimeTo(root);
        Debug.Log("MTGB Forge validation player built: " + root);
    }
    public static void CopyRuntimeTo(string root)
    {
        ForgeCatalogStoreBuild.EnsureBuilt();
        string runtime = Path.Combine(root, "ForgeRuntime");
        Directory.CreateDirectory(runtime);
        foreach (string directory in new[] { "classes", "lib", "assets", "jre", "source" })
            CopyTree(Path.Combine(ForgeCardCatalog.Root, directory), Path.Combine(runtime, directory));
        foreach (string name in new[] { "runtime.json", "files.json", "catalog.jsonl", ForgeCatalogStore.FileName })
            File.Copy(Path.Combine(ForgeCardCatalog.Root, name), Path.Combine(runtime, name), true);
        Directory.CreateDirectory(Path.Combine(root, "Decks"));
        foreach (string name in new[] { "Starter.mdk", "Forge-Validation.mdk", "README.md" })
            File.Copy(Path.Combine(DeckRepository.DeckRoot, name), Path.Combine(root, "Decks", name), true);
    }
    private static void CopyTree(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source))
        {
            var from = new FileInfo(file);
            var to = new FileInfo(Path.Combine(target, from.Name));
            if (!to.Exists || from.Length != to.Length || from.LastWriteTimeUtc != to.LastWriteTimeUtc)
                File.Copy(file, to.FullName, true);
        }
        foreach (string directory in Directory.GetDirectories(source)) CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)));
    }
}
#endif
