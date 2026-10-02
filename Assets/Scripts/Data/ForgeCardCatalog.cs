using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Display projection of the pinned Forge catalog. Contains no executable card effects.</summary>
public static class ForgeCardCatalog
{
    public const string Revision = "4ec5f1a2c32fa90ecb983a72b9eb47aa5c5d7676";
    public const int Protocol = 1;
    public static string Root => Path.Combine(PrintingKeyUtility.ApplicationRoot, "ForgeRuntime");
    private static string verifiedRuntimeHash;
    private static string verifiedRuntimeRoot;
    private static readonly object verificationLock = new object();

    public static void InvalidateVerification()
    {
        lock (verificationLock) { verifiedRuntimeHash = null; verifiedRuntimeRoot = null; }
    }

    [Serializable] private sealed class RuntimeFiles { public RuntimeFile[] files; }
    [Serializable] private sealed class RuntimeFile { public string path, sha256; }

    [Serializable]
    public sealed class RuntimeConfig
    {
        public string java;
        public string assets;
        public string revision;
        public int protocol;
        public string catalogHash;
        public string runtimeHash;
    }

    [Serializable]
    private sealed class Printing
    {
        public string key, forgeSet, name, splitType;
        public int artIndex;
        public int deckLimit;
        public bool ambiguous;
        public Face[] faces;
    }

    [Serializable]
    private sealed class Face
    {
        public string name, manaCost, text, power, toughness, loyalty, defense;
        public string[] types, supertypes, subtypes;
    }

    public static RuntimeConfig ReadConfig()
        => ReadConfig(Root, CancellationToken.None, null);

    // Only the authority starts Forge. Check its launch configuration and required
    // paths without scanning content; full integrity checks are explicitly requested.
    public static RuntimeConfig ReadConfigForMatch(string runtimeRoot = null)
    {
        runtimeRoot = runtimeRoot ?? Root;
        RuntimeConfig config = ReadConfigMetadata(runtimeRoot);
        if (config.java != "jre/bin/java.exe" || config.assets != "assets")
            throw new InvalidDataException("Forge 运行资源配置无效。");
        if (!File.Exists(Path.Combine(runtimeRoot, config.java)) ||
            !Directory.Exists(Path.Combine(runtimeRoot, config.assets)) ||
            !Directory.Exists(Path.Combine(runtimeRoot, "classes")) ||
            !Directory.Exists(Path.Combine(runtimeRoot, "lib")))
            throw new InvalidDataException("Forge 运行资源缺失。");
        return config;
    }

    private static RuntimeConfig ReadConfig(string runtimeRoot, CancellationToken cancellation, Action<string> progress)
    {
        RuntimeConfig config = ReadConfigMetadata(runtimeRoot);
        lock (verificationLock) VerifyRuntime(config, runtimeRoot, cancellation, progress);
        return config;
    }

    internal static RuntimeConfig ReadConfigMetadata(string runtimeRoot)
    {
        string path = Path.Combine(runtimeRoot, "runtime.json");
        if (!File.Exists(path)) throw new FileNotFoundException("缺少 ForgeRuntime，请先运行 Tools/ForgeBridge/Build-Bridge.ps1。", path);
        RuntimeConfig config = JsonUtility.FromJson<RuntimeConfig>(File.ReadAllText(path));
        if (config == null || config.revision != Revision || config.protocol != Protocol)
            throw new InvalidDataException("Forge 引擎或桥接协议版本不匹配。");
        return config;
    }

    private static void VerifyRuntime(RuntimeConfig config, string runtimeRoot, CancellationToken cancellation, Action<string> progress)
    {
        if (string.IsNullOrEmpty(config.runtimeHash)) throw new InvalidDataException("缺少 Forge 运行资源哈希。");
        string root = Path.GetFullPath(runtimeRoot) + Path.DirectorySeparatorChar;
        if (config.java != "jre/bin/java.exe" || config.assets != "assets")
            throw new InvalidDataException("Forge 必须使用包内 Java 和资源目录。");
        if (verifiedRuntimeHash == config.runtimeHash && verifiedRuntimeRoot == root) return;
        string manifestPath = Path.Combine(runtimeRoot, "files.json");
        if (HashFile(manifestPath) != config.runtimeHash) throw new InvalidDataException("Forge 运行资源清单哈希不匹配。");
        RuntimeFiles manifest = JsonUtility.FromJson<RuntimeFiles>(File.ReadAllText(manifestPath));
        if (manifest?.files == null || manifest.files.Length == 0) throw new InvalidDataException("Forge 运行资源清单为空。");
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new string[manifest.files.Length];
        int index = 0;
        foreach (RuntimeFile file in manifest.files)
        {
            cancellation.ThrowIfCancellationRequested();
            string path = Path.GetFullPath(Path.Combine(runtimeRoot, file.path));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !listed.Add(path))
                throw new InvalidDataException("Forge 运行资源路径非法或重复。");
            paths[index++] = path;
        }
        int verified = 0;
        progress?.Invoke($"正在校验游戏资源… 0 / {manifest.files.Length:N0}");
        // Bound disk concurrency and reuse the hashing instance per worker. Every file
        // is still read and hashed; timestamps are never accepted as evidence of integrity.
        try
        {
            Parallel.For(0, paths.Length,
                new ParallelOptions { CancellationToken = cancellation, MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) },
                () => SHA256.Create(),
                (i, state, sha) =>
                {
                    if (HashFile(paths[i], sha) != manifest.files[i].sha256)
                        throw new InvalidDataException("Forge 运行资源损坏：" + manifest.files[i].path);
                    int done = Interlocked.Increment(ref verified);
                    if (done % 256 == 0 || done == paths.Length)
                        progress?.Invoke($"正在校验游戏资源… {done:N0} / {paths.Length:N0}");
                    return sha;
                }, sha => sha.Dispose());
        }
        catch (AggregateException error)
        {
            throw error.Flatten().InnerExceptions[0];
        }
        foreach (string folder in new[] { "classes", "lib", "assets/res", "jre" })
            foreach (string path in Directory.EnumerateFiles(Path.Combine(runtimeRoot, folder), "*", SearchOption.AllDirectories))
            {
                cancellation.ThrowIfCancellationRequested();
                if (!listed.Contains(Path.GetFullPath(path))) throw new InvalidDataException("Forge 存在未登记资源：" + path);
            }
        if (config.java != "jre/bin/java.exe" || config.assets != "assets")
            throw new InvalidDataException("Forge 必须使用包内 Java 和资源目录。");
        verifiedRuntimeHash = config.runtimeHash;
        verifiedRuntimeRoot = root;
    }

    private static string HashFile(string path)
    {
        using (var sha = SHA256.Create())
            return HashFile(path, sha);
    }

    private static string HashFile(string path, SHA256 sha)
    {
        using (var file = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
    }

    public static List<CardDefinition> Load(List<string> warnings)
        => Load(Root, warnings, CancellationToken.None, null);

    // runtimeRoot is captured on the main thread: Application.dataPath is not worker-safe.
    public static List<CardDefinition> Load(string runtimeRoot, List<string> warnings, CancellationToken cancellation, Action<string> progress,
        bool verifyRuntime = true)
    {
        RuntimeConfig config = ReadConfigMetadata(runtimeRoot);
        cancellation.ThrowIfCancellationRequested();
        // Startup only needs the catalog projection. Explicit reloads can request
        // a full runtime verification; matches use ReadConfigForMatch().
        if (!verifyRuntime) return LoadCatalog(config, runtimeRoot, warnings, cancellation, progress);
        // Catalog projection and engine validation are independent. Never publish cards until
        // both succeed, and always join both jobs so reload/exit cannot leave an orphaned worker.
        var validation = Task.Run(() =>
        {
            lock (verificationLock) VerifyRuntime(config, runtimeRoot, cancellation, progress);
        }, cancellation);
        var projection = Task.Run(() => LoadCatalog(config, runtimeRoot, warnings, cancellation, null), cancellation);
        Task.WhenAll(validation, projection).GetAwaiter().GetResult();
        cancellation.ThrowIfCancellationRequested();
        return projection.GetAwaiter().GetResult();
    }

    private static List<CardDefinition> LoadCatalog(RuntimeConfig config, string runtimeRoot, List<string> warnings, CancellationToken cancellation, Action<string> progress)
    {
        progress?.Invoke("正在读取卡牌目录…");
        string path = Path.Combine(runtimeRoot, "catalog.jsonl");
        using (var sha = SHA256.Create())
        using (var file = File.OpenRead(path))
        {
            string actual = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
            if (!string.Equals(actual, config.catalogHash, StringComparison.Ordinal))
                throw new InvalidDataException("Forge 目录哈希不匹配，请重新构建运行资源。");
        }
        var cards = new List<CardDefinition>();
        int id = 0;
        var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadLines(path))
        {
            cancellation.ThrowIfCancellationRequested();
            if (id % 2048 == 0) progress?.Invoke($"正在加载卡牌… {id:N0}");
            Printing source = JsonUtility.FromJson<Printing>(line);
            if (source?.faces == null || source.faces.Length == 0)
                throw new InvalidDataException("Forge 目录条目缺少牌面。");
            int slash = source.key.IndexOf('/');
            if (slash <= 0 || slash == source.key.Length - 1) throw new InvalidDataException("无效印刷键：" + source.key);
            Face front = source.faces[0];
            var card = new CardDefinition
            {
                cardID = ++id, cardName = source.name,
                setCode = source.key.Substring(0, slash), collectorNumberText = source.key.Substring(slash + 1),
                sourceDatabasePath = path, forgeSet = source.forgeSet, forgeArtIndex = source.artIndex,
                forgeAmbiguous = source.ambiguous, forgeSplitType = source.splitType, forgeDeckLimit = source.deckLimit,
                // Forge's canonical rules name groups alternate printings for deck copy limits.
                oracleID = "forge:" + source.name,
                cardTypes = Types(front.types), supertypes = Supertypes(front.supertypes),
                subtypes = string.Join(" ", front.subtypes ?? Array.Empty<string>()),
                manaCost = Cost(front.manaCost), description = Text(front.text),
                attack = Number(front.power), defence = Number(front.toughness),
                loyalty = Number(front.loyalty), battleDefense = Number(front.defense),
                features = new List<CardFeatureDefinition>()
            };
            for (int f = 0; f < source.faces.Length; f++)
            {
                Face face = source.faces[f];
                bool separateFace = source.splitType == "Transform" || source.splitType == "Modal" || source.splitType == "Meld";
                var feature = new CardFeatureDefinition
                {
                    faceIndex = separateFace ? f : 0, featureIndex = separateFace ? 0 : f,
                    role = f == 0 || separateFace ? CardFeatureRole.Primary : CardFeatureRole.Generic,
                    cardName = face.name, cardTypes = Types(face.types), supertypes = Supertypes(face.supertypes),
                    subtypes = string.Join(" ", face.subtypes ?? Array.Empty<string>()),
                    manaCost = Cost(face.manaCost), description = Text(face.text),
                    attack = Number(face.power), defence = Number(face.toughness),
                    powerExpression = face.power ?? "0", toughnessExpression = face.toughness ?? "0",
                    loyalty = Number(face.loyalty), battleDefense = Number(face.defense)
                };
                if (!string.IsNullOrEmpty(feature.description))
                    feature.ruleEntries.Add(new CardRuleEntryDefinition { text = feature.description });
                card.features.Add(feature);
            }
            cards.Add(card);
            if (source.ambiguous) ambiguous.Add(source.key);
        }
        if (cards.Count == 0) throw new InvalidDataException("Forge 官方目录为空。");
        if (ambiguous.Count > 0) warnings.Add($"Forge 目录含 {ambiguous.Count} 个歧义印刷键；这些条目不能提交对局。");
        return cards;
    }

    private static string Text(string value) => (value ?? string.Empty).Replace("\\n", "\n");
    private static string Cost(string value) => value == "no cost" ? string.Empty : value ?? string.Empty;
    private static int Number(string value) => int.TryParse(value, out int n) ? n : 0;
    private static CardType Types(string[] values)
    {
        CardType result = 0;
        foreach (string value in values ?? Array.Empty<string>())
            if (Enum.TryParse(value == "Tribal" ? "Kindred" : value, true, out CardType type)) result |= type;
        return result;
    }
    private static CardSupertype Supertypes(string[] values)
    {
        CardSupertype result = 0;
        foreach (string value in values ?? Array.Empty<string>())
            if (Enum.TryParse(value, true, out CardSupertype type)) result |= type;
        return result;
    }
}
