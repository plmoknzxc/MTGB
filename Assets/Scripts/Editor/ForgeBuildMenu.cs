#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ForgeBuildMenu
{
    private static bool building;
    // Release output is separate from the development/validation player.
    public static void BuildReleaseFromCommandLine()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-clientOutput");
        if (index < 0 || index + 1 >= args.Length)
            throw new ArgumentException("Expected -clientOutput <new output directory>.");
        BuildReleasePlayer(args[index + 1]);
    }
    public static void BuildReleasePlayer(string outputDirectory)
    {
        string root = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new InvalidOperationException("Release output must be empty.");
        Directory.CreateDirectory(root);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/Scenes/Main.unity", "Assets/Scenes/IntegratedBattlefieldRedesign.unity" },
            locationPathName = Path.Combine(root, "MTGB.exe"), target = BuildTarget.StandaloneWindows64,
            subtarget = (int)StandaloneBuildSubtarget.Player, options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Forge release player build failed.");
        // The client hosts the same headless Forge engine as the dedicated server.
        CopyRuntimeTo(root, dedicatedServer: true);
        CopyClientNotices(root);
        CopyVideoToolsTo(root);
        ReplayVideoExportJob.EditorWorkerExecutable=Path.Combine(root,"MTGB.exe");
        Debug.Log("MTGB release player built: " + root);
    }
    private static void CopyClientNotices(string root)
    {
        foreach(string name in new[]{"LICENSE","THIRD_PARTY_NOTICES.md"})
            File.Copy(name,Path.Combine(root,name),true);
        string target=Path.Combine(root,"Licenses","Fonts");
        Directory.CreateDirectory(target);
        const string fonts="Assets/Resources/UI/CardTemplates/Fonts";
        foreach(string file in Directory.GetFiles(fonts)) {
            string name=Path.GetFileName(file);
            if(name.EndsWith("-OFL.txt",StringComparison.Ordinal)||name is "ARPHICPL.TXT" or "README.md" or "open-fonts.json")
                File.Copy(file,Path.Combine(target,name),true);
        }
        File.Copy("Assets/Fonts/Noto-OFL.txt",Path.Combine(target,"Noto-OFL.txt"),true);
        string forge=Path.Combine(root,"Licenses","Forge");Directory.CreateDirectory(forge);
        File.Copy("Tools/ForgeBridge/LICENSE",Path.Combine(forge,"LICENSE"),true);
        File.Copy("Docs/OpenSourceAndDistribution.zh-CN.md",Path.Combine(root,"SOURCE_CODE.md"),true);
    }
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
            scenes = new[] { "Assets/Scenes/Main.unity", "Assets/Scenes/IntegratedBattlefieldRedesign.unity" },
            locationPathName = Path.Combine(root, "MTGB.exe"), target = BuildTarget.StandaloneWindows64,
            subtarget = (int)StandaloneBuildSubtarget.Player,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Forge validation player build failed.");
        CopyRuntimeTo(root);
        CopyVideoToolsTo(root);
        ReplayVideoExportJob.EditorWorkerExecutable=Path.Combine(root,"MTGB.exe");
        Debug.Log("MTGB Forge validation player built: " + root);
    }
    [Serializable] private sealed class RuntimeFile { public string path, sha256; }
    // Ship the encoder with the player. A successful developer PATH lookup is
    // not evidence that the installed client has its required dependency.
    public static void CopyVideoToolsTo(string root)
    {
        string source=Path.GetFullPath("VideoTools");
        if(!File.Exists(Path.Combine(source,"ffmpeg.exe")))
            throw new FileNotFoundException("Client export requires VideoTools/ffmpeg.exe and its distribution notices.");
        string target=Path.Combine(Path.GetFullPath(root),"VideoTools");
        if(string.Equals(source,target,StringComparison.OrdinalIgnoreCase))return;
        CopyTree(source,target);
    }
    [Serializable] private sealed class RuntimeManifest { public RuntimeFile[] files; }

    private static readonly string[] ServerResourceFolders = { "ai", "blockdata", "cardsfolder", "defaults", "editions", "effects", "formats", "licenses", "lists", "setlookup", "tokenscripts" };

    public static void CopyRuntimeTo(string root, string linuxJavaRoot = null, bool dedicatedServer = false)
    {
        ForgeCatalogStoreBuild.EnsureBuilt();
        string runtime = Path.Combine(root, "ForgeRuntime");
        if (dedicatedServer && Directory.Exists(runtime))
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullRuntime = Path.GetFullPath(runtime);
            if (!fullRuntime.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fullRuntime, Path.GetFullPath(ForgeCardCatalog.Root), StringComparison.OrdinalIgnoreCase)
                || (File.GetAttributes(runtime) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("拒绝清理源运行环境或链接目录。");
            Directory.Delete(fullRuntime, true);
        }
        Directory.CreateDirectory(runtime);
        foreach (string directory in dedicatedServer ? (linuxJavaRoot == null ? new[] { "classes", "lib", "jre" } : new[] { "classes", "lib" })
            : linuxJavaRoot == null ? new[] { "classes", "lib", "assets", "jre", "source" } : new[] { "classes", "lib", "assets" })
            CopyTree(Path.Combine(ForgeCardCatalog.Root, directory), Path.Combine(runtime, directory));
        if (dedicatedServer)
        {
            foreach (string folder in ServerResourceFolders)
                CopyTree(Path.Combine(ForgeCardCatalog.Root, "assets", "res", folder), Path.Combine(runtime, "assets", "res", folder));
            string languages = Path.Combine(runtime, "assets", "res", "languages");
            Directory.CreateDirectory(languages);
            File.Copy(Path.Combine(ForgeCardCatalog.Root, "assets", "res", "languages", "en-US.properties"), Path.Combine(languages, "en-US.properties"), true);
            foreach (string name in new[] { "LICENSE.txt", "README.txt" })
                File.Copy(Path.Combine(ForgeCardCatalog.Root, "assets", name), Path.Combine(runtime, "assets", name), true);
        }
        foreach (string name in new[] { "runtime.json", "files.json", "catalog.jsonl", ForgeCatalogStore.FileName })
            File.Copy(Path.Combine(ForgeCardCatalog.Root, name), Path.Combine(runtime, name), true);
        if (linuxJavaRoot != null || dedicatedServer)
        {
            if (linuxJavaRoot != null) CopyTree(linuxJavaRoot, Path.Combine(runtime, "jre"));
            var manifest = JsonUtility.FromJson<RuntimeManifest>(File.ReadAllText(Path.Combine(runtime, "files.json")));
            var files = new List<RuntimeFile>(manifest.files.Where(file => !file.path.StartsWith("jre/", StringComparison.Ordinal)
                && (!dedicatedServer || !file.path.StartsWith("assets/", StringComparison.Ordinal) || IncludeServerResource(file.path))));
            foreach (string file in Directory.GetFiles(Path.Combine(runtime, "jre"), "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
                files.Add(new RuntimeFile { path = "jre/" + file.Substring(Path.Combine(runtime, "jre").Length + 1).Replace('\\', '/'), sha256 = Hash(file) });
            File.WriteAllText(Path.Combine(runtime, "files.json"), JsonUtility.ToJson(new RuntimeManifest { files = files.ToArray() }));
            var config = JsonUtility.FromJson<ForgeCardCatalog.RuntimeConfig>(File.ReadAllText(Path.Combine(runtime, "runtime.json")));
            config.java = linuxJavaRoot != null ? "jre/bin/java" : "jre/bin/java.exe";
            config.runtimeHash = Hash(Path.Combine(runtime, "files.json"));
            File.WriteAllText(Path.Combine(runtime, "runtime.json"), JsonUtility.ToJson(config));
        }
        Directory.CreateDirectory(Path.Combine(root, "Decks"));
        foreach (string name in new[] { "Starter.mdk", "Forge-Validation.mdk", "README.md" })
        {
            string source = Path.Combine(DeckRepository.DeckRoot, name);
            if (File.Exists(source)) File.Copy(source, Path.Combine(root, "Decks", name), true);
        }
    }
    private static bool IncludeServerResource(string path)
        => path == "assets/res/languages/en-US.properties" || ServerResourceFolders.Any(folder => path.StartsWith("assets/res/" + folder + "/", StringComparison.Ordinal));
    private static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
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
