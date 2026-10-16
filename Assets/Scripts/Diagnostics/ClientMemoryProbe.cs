using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

// Opt-in standalone-player diagnostics. It does not run during ordinary play.
public sealed class ClientMemoryProbe : MonoBehaviour
{
    string output;
    float exitAfter;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
#if !UNITY_SERVER
        string path = OnlineClientProfileStore.ReadArgument("-client-memory-probe");
        if (string.IsNullOrEmpty(path)) return;
        var go = new GameObject("ClientMemoryProbe"); DontDestroyOnLoad(go);
        var probe = go.AddComponent<ClientMemoryProbe>(); probe.output = path;
        float.TryParse(OnlineClientProfileStore.ReadArgument("-client-memory-probe-exit-seconds"), out probe.exitAfter);
#endif
    }
    IEnumerator Start()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        foreach (float at in new[] { 2f, 6f, 12f, 25f, 45f, 90f, 150f, 210f, 300f })
        {
            while (Time.realtimeSinceStartup < at)
            {
                if (exitAfter > 0 && Time.realtimeSinceStartup >= exitAfter) { Sample(); Application.Quit(); yield break; }
                yield return null;
            }
            Sample();
        }
    }
    void Sample()
    {
        var objects = Resources.FindObjectsOfTypeAll<UnityEngine.Object>();
        var categories = new Dictionary<string, (long bytes, int count)>();
        var largest = new List<Dictionary<string, object>>();
        foreach (var item in objects)
        {
            if (item == null) continue;
            long bytes = Profiler.GetRuntimeMemorySizeLong(item);
            string type = item.GetType().Name;
            categories.TryGetValue(type, out var value);
            categories[type] = (value.bytes + bytes, value.count + 1);
            if (bytes < 1024 * 1024) continue;
            var entry = new Dictionary<string, object> { ["type"] = type, ["name"] = item.name, ["bytes"] = bytes };
            if (item is Texture2D texture) { entry["width"] = texture.width; entry["height"] = texture.height; entry["readable"] = texture.isReadable; entry["format"] = texture.format.ToString(); }
            largest.Add(entry);
        }
        var collections = new Dictionary<string, int>();
        foreach (var type in new[] { typeof(CardDatabaseRepository), typeof(CardSearchMetadata), typeof(GamePreferences), typeof(CardImageLibrary) })
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (field.GetValue(null) is ICollection collection) collections[type.Name + "." + field.Name] = collection.Count;
        var result = new Dictionary<string, object> {
            ["seconds"] = Time.realtimeSinceStartup, ["scene"] = SceneManager.GetActiveScene().name,
            ["allocated"] = Profiler.GetTotalAllocatedMemoryLong(), ["reserved"] = Profiler.GetTotalReservedMemoryLong(),
            ["monoUsed"] = Profiler.GetMonoUsedSizeLong(), ["monoHeap"] = Profiler.GetMonoHeapSizeLong(),
            ["gcUsed"] = GC.GetTotalMemory(false), ["objects"] = objects.Length,
            ["categories"] = categories.OrderByDescending(p => p.Value.bytes).Take(15).Select(p => new Dictionary<string,object>{{"type",p.Key},{"bytes",p.Value.bytes},{"count",p.Value.count}}).ToArray(),
            ["largest"] = largest.OrderByDescending(p => (long)p["bytes"]).Take(20).ToArray(), ["collections"] = collections
        };
        File.AppendAllText(output, ServerSnapshotJson.Serialize(result) + "\n");
    }
}
