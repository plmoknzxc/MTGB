using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CardImageLibrary
{
    public const string FolderName = "CardImages";
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };
    private static readonly Dictionary<string, CacheEntry> Cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> FailedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private sealed class CacheEntry
    {
        public Sprite Sprite;
        public long LastWriteTicks;
        public long Length;
    }

    public static string ExternalRoot => Path.Combine(GetApplicationRoot(), FolderName);
    public static string PersistentRoot => Path.Combine(Application.persistentDataPath, FolderName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        ClearCache();
        EnsureDirectory(ExternalRoot);
    }

    public static bool TryLoadFullCard(CardData card, out Sprite sprite)
    {
        sprite = null;
        if (!TryGetPrinting(card, out string setCode, out string collectorNumber)) return false;

        foreach (string root in EnumerateRoots())
        {
            string setFolder = Path.Combine(root, setCode);
            for (int i = 0; i < Extensions.Length; i++)
            {
                string path = Path.Combine(setFolder, collectorNumber + Extensions[i]);
                if (!File.Exists(path)) continue;
                sprite = LoadSprite(path);
                return sprite != null;
            }
        }

        return false;
    }

    public static string GetExpectedRelativePath(CardData card)
    {
        return TryGetPrinting(card, out string setCode, out string collectorNumber)
            ? Path.Combine(FolderName, setCode, collectorNumber + ".png")
            : string.Empty;
    }

    public static void ClearCache()
    {
        foreach (CacheEntry entry in Cache.Values)
        {
            if (entry.Sprite == null) continue;
            Texture2D texture = entry.Sprite.texture;
            UnityEngine.Object.Destroy(entry.Sprite);
            if (texture != null) UnityEngine.Object.Destroy(texture);
        }
        Cache.Clear();
        FailedFiles.Clear();
    }

    private static Sprite LoadSprite(string path)
    {
        var info = new FileInfo(path);
        string fullPath = info.FullName;
        if (Cache.TryGetValue(fullPath, out CacheEntry cached)
            && cached.LastWriteTicks == info.LastWriteTimeUtc.Ticks
            && cached.Length == info.Length
            && cached.Sprite != null)
        {
            return cached.Sprite;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = $"CardImage_{info.Directory?.Name}_{Path.GetFileNameWithoutExtension(info.Name)}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!texture.LoadImage(bytes, true))
            {
                UnityEngine.Object.Destroy(texture);
                WarnFailed(fullPath, "unsupported or invalid image data");
                return null;
            }

            Sprite loaded = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            loaded.name = texture.name;

            if (cached?.Sprite != null)
            {
                Texture2D oldTexture = cached.Sprite.texture;
                UnityEngine.Object.Destroy(cached.Sprite);
                if (oldTexture != null) UnityEngine.Object.Destroy(oldTexture);
            }

            Cache[fullPath] = new CacheEntry
            {
                Sprite = loaded,
                LastWriteTicks = info.LastWriteTimeUtc.Ticks,
                Length = info.Length
            };
            FailedFiles.Remove(fullPath);
            return loaded;
        }
        catch (Exception exception)
        {
            WarnFailed(fullPath, exception.Message);
            return null;
        }
    }

    private static bool TryGetPrinting(CardData card, out string setCode, out string collectorNumber)
    {
        setCode = card != null ? card.setCode?.Trim() : string.Empty;
        collectorNumber = card != null ? card.CollectorNumber : string.Empty;
        if (string.IsNullOrEmpty(setCode) || string.IsNullOrEmpty(collectorNumber)) return false;
        if (setCode == "." || setCode == ".." || setCode.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        if (collectorNumber == "." || collectorNumber == ".." || collectorNumber.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        return true;
    }

    private static IEnumerable<string> EnumerateRoots()
    {
        yield return ExternalRoot;
        if (!string.Equals(ExternalRoot, PersistentRoot, StringComparison.OrdinalIgnoreCase)) yield return PersistentRoot;
    }

    private static string GetApplicationRoot()
    {
        DirectoryInfo parent = Directory.GetParent(Application.dataPath);
        return parent != null ? parent.FullName : Application.dataPath;
    }

    private static void EnsureDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not create card image directory '{path}': {exception.Message}");
        }
    }

    private static void WarnFailed(string path, string reason)
    {
        if (!FailedFiles.Add(path)) return;
        Debug.LogWarning($"Could not load card image '{path}': {reason}");
    }
}
