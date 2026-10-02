using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public sealed class ForgeCatalogStoreBuild : IPreprocessBuildWithReport
{
    public int callbackOrder => -100;
    static ForgeCatalogStoreBuild() { EditorApplication.playModeStateChanged += OnPlay; }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode) return;
        try { EnsureBuilt(); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.isPlaying = false; }
    }
    public void OnPreprocessBuild(BuildReport report) { EnsureBuilt(); }

    [MenuItem("MTGB/Forge/Prepare Card Database")]
    public static void Rebuild() { Prepare(true); }

    public static void EnsureBuilt() { Prepare(false); }

    static void Prepare(bool force)
    {
        string root = ForgeCardCatalog.Root;
        var config = JsonUtility.FromJson<ForgeCardCatalog.RuntimeConfig>(File.ReadAllText(Path.Combine(root, "runtime.json")));
        string flavor = Path.Combine(Application.streamingAssetsPath, "CardFlavor.jsonl");
        string flavorHash = File.Exists(flavor) ? Hash(flavor) : string.Empty;
        string path = Path.Combine(root, ForgeCatalogStore.FileName);
        // Full source checks happen in the authoring/build pipeline, never on the player's startup path.
        if (Hash(Path.Combine(root, "catalog.jsonl")) != config.catalogHash)
            throw new InvalidDataException("Forge 目录已变化，请先重新导出 ForgeRuntime。");
        if (!force && ForgeCatalogStore.MatchesSources(path, config.catalogHash, flavorHash)) return;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var cards = ForgeCardCatalog.Load(root, new List<string>(), CancellationToken.None, null, verifyRuntime: false);
        CardFlavorCatalog.Apply(cards, flavor, CancellationToken.None);
        ForgeCatalogStore.Write(path, config.catalogHash, flavorHash, cards);
        Debug.Log($"[Catalog build] Prepared {cards.Count} printings in {timer.ElapsedMilliseconds} ms; {new FileInfo(path).Length:N0} bytes.");
    }
    static string Hash(string path)
    {
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}
