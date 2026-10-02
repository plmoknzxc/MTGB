using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class ForgeCatalogStoreValidation
{
    [MenuItem("MTGB/验证/卡牌/预生成数据库")]
    public static async void Run()
    {
        SessionState.SetString("CatalogStore.Validation", "running");
        try
        {
            ForgeCatalogStoreBuild.EnsureBuilt();
            string root = ForgeCardCatalog.Root;
            string flavor = Path.Combine(Application.streamingAssetsPath, "CardFlavor.jsonl");
            string scratch = Path.GetFullPath(".tools/catalog-validation");
            Directory.CreateDirectory(scratch);
            string report = await Task.Run(() =>
            {
                var messages = new List<string>();
                var expectedWarnings = new List<string>();
                var timer = Stopwatch.StartNew();
                var expected = ForgeCardCatalog.Load(root, expectedWarnings, CancellationToken.None, null, false);
                CardFlavorCatalog.Apply(expected, flavor, CancellationToken.None);
                messages.Add("JSON + flavor: " + timer.ElapsedMilliseconds + " ms");
                List<CardDefinition> actual = null;
                var times = new List<long>();
                for (int run = 0; run < 3; run++)
                {
                    var warnings = new List<string>(); timer.Restart();
                    actual = ForgeCatalogStore.Load(root, warnings, CancellationToken.None);
                    times.Add(timer.ElapsedMilliseconds);
                    if (string.Join("\n", warnings) != string.Join("\n", expectedWarnings)) throw new Exception("Warnings changed.");
                }
                messages.Add("Prepared database (3 fresh reads): " + string.Join(", ", times) + " ms");
                if (actual.Count != expected.Count) throw new Exception("Card count changed.");
                for (int i = 0; i < expected.Count; i++)
                {
                    var a = expected[i]; var b = actual[i];
                    if (JsonUtility.ToJson(a) != JsonUtility.ToJson(b) || a.flavorText != b.flavorText || a.sourceDatabasePath != b.sourceDatabasePath)
                        throw new Exception("Card/face/rules mismatch: " + a.PrintingKey);
                }
                messages.Add("PASS: every field, face, rules text, flavor and printing identity matched for " + actual.Count + " printings.");
                string standalone = Path.Combine(scratch, "standalone");
                Directory.CreateDirectory(standalone);
                File.Copy(Path.Combine(root, ForgeCatalogStore.FileName), Path.Combine(standalone, ForgeCatalogStore.FileName), true);
                File.Copy(Path.Combine(root, "runtime.json"), Path.Combine(standalone, "runtime.json"), true);
                if (ForgeCatalogStore.Load(standalone, new List<string>(), CancellationToken.None).Count != expected.Count)
                    throw new Exception("Prepared database required source JSON files.");
                messages.Add("PASS: loads with only the prepared database and runtime metadata, without source JSON/flavor files.");
                string original = Path.Combine(root, ForgeCatalogStore.FileName);
                string hash = JsonUtility.FromJson<ForgeCardCatalog.RuntimeConfig>(File.ReadAllText(Path.Combine(root, "runtime.json"))).catalogHash;
                Action<string, string> reject = (path, digest) =>
                {
                    try { ForgeCatalogStore.Read(path, digest, root, new List<string>(), CancellationToken.None); }
                    catch (Exception e) when (e is IOException || e is InvalidDataException) { return; }
                    throw new Exception("Invalid database was accepted: " + path);
                };
                reject(Path.Combine(scratch, "missing.cache"), hash);
                reject(original, "wrong-source-version");
                string truncated = Path.Combine(scratch, "truncated.cache");
                byte[] bytes = File.ReadAllBytes(original);
                File.WriteAllBytes(truncated, new ArraySegment<byte>(bytes, 0, bytes.Length - 3).ToArray());
                reject(truncated, hash);
                string version = Path.Combine(scratch, "wrong-version.cache");
                bytes[4] = 127; File.WriteAllBytes(version, bytes); reject(version, hash);
                if (ForgeCatalogStore.MatchesSources(version, hash, "")) throw new Exception("Stale cache accepted by builder.");
                using (var cancel = new CancellationTokenSource())
                {
                    cancel.Cancel();
                    try { ForgeCatalogStore.Load(root, new List<string>(), cancel.Token); throw new Exception("Cancellation ignored."); }
                    catch (OperationCanceledException) { }
                }
                messages.Add("PASS: missing/truncated/stale/version-mismatched databases rejected; cancellation honored.");
                return string.Join("\n", messages);
            });
            SessionState.SetString("CatalogStore.Validation", report); UnityEngine.Debug.Log(report);
        }
        catch (Exception error) { SessionState.SetString("CatalogStore.Validation", "FAIL: " + error); UnityEngine.Debug.LogException(error); }
    }
}
