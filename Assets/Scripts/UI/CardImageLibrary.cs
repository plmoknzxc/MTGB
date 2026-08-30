using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CardImageLibrary
{
    public const string FolderName = "CardImages";
    public const string AlternateFolderName = "卡图";
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg" };
    private static readonly Dictionary<string, CacheEntry> Cache = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> FailedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static Sprite cardBackSprite;

    private sealed class CacheEntry
    {
        public Sprite Sprite;
        public long LastWriteTicks;
        public long Length;
    }

    public static string ExternalRoot => Path.Combine(PrintingKeyUtility.ApplicationRoot, FolderName);
    public static string AlternateExternalRoot => Path.Combine(PrintingKeyUtility.ApplicationRoot, AlternateFolderName);
    public static string PersistentRoot => Path.Combine(Application.persistentDataPath, FolderName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        ClearCache();
        EnsureDirectory(ExternalRoot);
    }

    public static bool TryLoadFullCard(CardDefinition card, out Sprite sprite)
    {
        return TryLoadFullCard(card, 0, out sprite);
    }

    public static bool TryLoadFullCard(CardDefinition card, int faceIndex, out Sprite sprite)
    {
        sprite = null;
        if (!TryFindImagePath(card, faceIndex, out string path)) return false;
        sprite = LoadSprite(path);
        return sprite != null;
    }

    public static bool TryLoadFeature(
        CardDefinition card,
        CardFeatureDefinition feature,
        out Sprite sprite)
    {
        sprite = null;
        return feature != null && TryLoadFullCard(card, feature.faceIndex, out sprite);
    }

    public static bool TryLoadBackFace(CardDefinition card, out Sprite sprite)
    {
        return TryLoadFullCard(card, 1, out sprite);
    }

    public static bool HasBackFace(CardDefinition card)
    {
        return HasCardFace(card, 1);
    }

    public static bool HasCardFace(CardDefinition card, int faceIndex)
    {
        return TryFindImagePath(card, faceIndex, out _);
    }

    public static bool TryLoadCardBack(out Sprite sprite)
    {
        if (cardBackSprite != null)
        {
            sprite = cardBackSprite;
            return true;
        }

        sprite = Resources.Load<Sprite>("UI/CardBack");
        if (sprite != null)
        {
            cardBackSprite = sprite;
            return true;
        }

        Texture2D texture = Resources.Load<Texture2D>("UI/CardBack");
        if (texture == null) return false;

        cardBackSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        cardBackSprite.name = "CardBack";
        sprite = cardBackSprite;
        return true;
    }

    public static string GetExpectedRelativePath(CardDefinition card)
    {
        return GetExpectedRelativePath(card, 0);
    }

    public static string GetExpectedRelativePath(CardDefinition card, int faceIndex)
    {
        if (faceIndex < 0
            || !PrintingKeyUtility.TryGetPrinting(card, out string setCode, out string collectorNumber))
        {
            return string.Empty;
        }

        return PrintingKeyUtility.BuildImagePath(
            FolderName,
            setCode,
            collectorNumber,
            faceIndex,
            ".png");
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
        cardBackSprite = null;
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

    private static bool TryFindImagePath(CardDefinition card, int faceIndex, out string path)
    {
        path = string.Empty;
        if (faceIndex < 0
            || !PrintingKeyUtility.TryGetPrinting(card, out string setCode, out string collectorNumber))
        {
            return false;
        }

        foreach (string root in EnumerateRoots())
        {
            for (int i = 0; i < Extensions.Length; i++)
            {
                string candidate = PrintingKeyUtility.BuildImagePath(
                    root,
                    setCode,
                    collectorNumber,
                    faceIndex,
                    Extensions[i]);
                if (!File.Exists(candidate)) continue;
                path = candidate;
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateRoots()
    {
        yield return ExternalRoot;
        yield return AlternateExternalRoot;
        if (!string.Equals(ExternalRoot, PersistentRoot, StringComparison.OrdinalIgnoreCase)) yield return PersistentRoot;
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
