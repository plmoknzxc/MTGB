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
    private static readonly Dictionary<string,Sprite> DisplayedFullCards = new Dictionary<string,Sprite>();
    private static readonly Dictionary<string,int> DisplayedFrames = new Dictionary<string,int>();

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
#if !UNITY_SERVER
        ClearCache();
        EnsureDirectory(ExternalRoot);
#endif
    }

    public static bool TryLoadFullCard(CardDefinition card, out Sprite sprite)
    {
        return TryLoadFullCard(card, 0, out sprite);
    }

    private static string DisplayKey(CardDefinition card,int faceIndex)
        => card.PrintingKey+"/"+faceIndex+"/"+GamePreferences.Language+"/"+CardArtPreferences.Get(card);
    internal static bool TryReuseFullCard(CardDefinition card,out Sprite sprite)=>TryReuseFullCard(card,0,out sprite);
    private static bool TryReuseFullCard(CardDefinition card,int faceIndex,out Sprite sprite)
    {
        sprite=null;
        if(card==null||!DisplayedFullCards.TryGetValue(DisplayKey(card,faceIndex),out var displayed)||displayed==null)return false;
        // Retain file-change checks when reusing the art from the current game.
        string path=null;
        foreach(var pair in Cache)if(pair.Value.Sprite==displayed){path=pair.Key;break;}
        if(path==null||!File.Exists(path))return false;
        sprite=LoadSprite(path);
        if(sprite!=null)DisplayedFullCards[DisplayKey(card,faceIndex)]=sprite;
        return sprite!=null;
    }
    public static bool TryLoadFullCard(CardDefinition card, int faceIndex, out Sprite sprite)
    {
        string key=card==null?null:DisplayKey(card,faceIndex);
        // A render can ask for the same printing for seven hand cards, plus its
        // previews. Resolve once per frame; later frames still discover new art.
        if(key!=null&&DisplayedFrames.TryGetValue(key,out int frame)&&frame==Time.frameCount
            &&TryReuseFullCard(card,faceIndex,out sprite))return true;
        bool loaded=TryLoadFullCardCore(card,faceIndex,out sprite);
        if(loaded&&key!=null){DisplayedFullCards[key]=sprite;DisplayedFrames[key]=Time.frameCount;}
        return loaded;
    }
    private static bool TryLoadFullCardCore(CardDefinition card,int faceIndex,out Sprite sprite)
    {
        sprite = null;
        string preferred = CardArtPreferences.Get(card);
        // Search every local printing in the requested language before allowing
        // an English scan, including a previously selected alternate artwork.
        bool localized=GamePreferences.Language=="zh-CN" || GamePreferences.Language=="ja-JP";
        if(localized && card!=null && !card.customContent)
        {
            if(string.IsNullOrEmpty(preferred) && TryLoadLocalized(card,faceIndex,out sprite))return true;
            var versions=CardArtVersions.GetVersions(card);
            if(!string.IsNullOrEmpty(preferred))
                foreach(var version in versions)
                    if(version.PrintingKey==preferred && TryLoadLocalized(version,faceIndex,out sprite))return true;
            if(TryLoadLocalized(card,faceIndex,out sprite))return true;
            foreach(var version in versions)
                if(version.PrintingKey!=card.PrintingKey && TryLoadLocalized(version,faceIndex,out sprite))return true;
        }
        if (!string.IsNullOrEmpty(preferred))
            foreach (var version in CardArtVersions.GetVersions(card))
                if (version.PrintingKey == preferred && TryLoadExact(version, faceIndex, out sprite)) return true;
        if (CardArtImportService.TryGetPath(card, faceIndex, out string customPath)) sprite = LoadSprite(customPath);
        if (sprite != null) return true;
        if(faceIndex==1&&MeldCardArt.TryLoad(card,out sprite))return true;
        if (TryFindImagePath(card, faceIndex, out string path, false)) sprite = LoadSprite(path);
        if (sprite != null) return true;
        foreach (var version in CardArtVersions.GetVersions(card))
        {
            if (version.PrintingKey == card.PrintingKey || !TryFindImagePath(version, faceIndex, out path, false)) continue;
            sprite = LoadSprite(path);
            if (sprite != null) return true;
        }
        return false;
    }

    private static bool TryLoadLocalized(CardDefinition card,int faceIndex,out Sprite sprite)
    {
        sprite=null;
        if(!PrintingKeyUtility.TryGetPrinting(card,out string setCode,out string collectorNumber))return false;
        foreach(string root in EnumerateRoots())
            foreach(string extension in Extensions)
            {
                string path=PrintingKeyUtility.BuildImagePath(Path.Combine(root,GamePreferences.Language),setCode,collectorNumber,faceIndex,extension);
                if(!File.Exists(path))continue;
                sprite=LoadSprite(path);
                if(sprite!=null)return true;
            }
        return false;
    }

    public static bool TryLoadExact(CardDefinition card, int faceIndex, out Sprite sprite)
    {
        sprite = null;
        if (TryFindImagePath(card, faceIndex, out string path)) sprite = LoadSprite(path);
        return sprite != null;
    }

    // Token metadata has a printing key but is not part of the playable card catalog.
    // Use the same local roots and decoder, without alternate-printing or online fallback.
    public static bool TryLoadPrintingKey(string key, out Sprite sprite)
        => TryLoadPrintingKey(key,0,out sprite);

    public static bool TryLoadPrintingKey(string key, int faceIndex, out Sprite sprite)
        => TryLoadPrintingKey(key,faceIndex,null,out sprite);

    public static bool TryLoadPrintingKey(string key, int faceIndex, string language, out Sprite sprite)
    {
        sprite = null;
        var parts = (key ?? string.Empty).Split('/');
        if (faceIndex<0 || faceIndex>1 || parts.Length != 2 || !PrintingKeyUtility.IsSafePathSegment(parts[0])
            || !PrintingKeyUtility.IsSafePathSegment(parts[1])
            || !string.IsNullOrEmpty(language) && !PrintingKeyUtility.IsSafePathSegment(language)) return false;
        foreach (string root in string.IsNullOrEmpty(language) ? EnumerateLanguageRoots() : EnumerateRoots())
            foreach (string extension in Extensions)
            {
                string artRoot=string.IsNullOrEmpty(language)?root:Path.Combine(root,language);
                string path = PrintingKeyUtility.BuildImagePath(artRoot, parts[0], parts[1], faceIndex,extension);
                if (!File.Exists(path)) continue;
                sprite = LoadSprite(path);
                if (sprite != null) return true;
            }
        return false;
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
        DisplayedFullCards.Clear();
        DisplayedFrames.Clear();
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
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true)
            {
                name = $"CardImage_{info.Directory?.Name}_{Path.GetFileNameWithoutExtension(info.Name)}",
                filterMode = FilterMode.Trilinear,
                // A small positive bias softens minified, rotated scans without
                // blurring the original full-size hover image.
                mipMapBias = .3f,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!texture.LoadImage(bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                WarnFailed(fullPath, "unsupported or invalid image data");
                return null;
            }
            // Filter from downsampled levels when full-resolution card scans appear as small tiles.
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);

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

    // Exact printing only: download status must never mistake a fallback for an installed version.
    public static bool TryFindImagePath(CardDefinition card, int faceIndex, out string path, bool includeCustom = true)
    {
        path = string.Empty;
        if (faceIndex < 0
            || !PrintingKeyUtility.TryGetPrinting(card, out string setCode, out string collectorNumber))
        {
            return false;
        }

        if (includeCustom && CardArtImportService.TryGetPath(card, faceIndex, out path)) return true;

        foreach (string root in EnumerateLanguageRoots())
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

    // Installed localized scans share the exact printing/face key. Fall back only
    // to other local scans when no image exists in the selected language.
    private static IEnumerable<string> EnumerateLanguageRoots()
    {
        string language = GamePreferences.Language;
        if (language == "zh-CN" || language == "ja-JP")
            foreach (string root in EnumerateRoots()) yield return Path.Combine(root, language);
        foreach (string root in EnumerateRoots()) yield return root;
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
