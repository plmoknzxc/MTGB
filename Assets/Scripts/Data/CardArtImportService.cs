using System;
using System.IO;
using UnityEngine;

/// <summary>Local per-printing artwork overrides. All methods run on Unity's main thread.</summary>
public static class CardArtImportService
{
    public static string Root => Path.Combine(Application.persistentDataPath, "CustomCardImages");
    public static event Action<string, int> Changed;

    public static string GetPath(CardDefinition card, int faceIndex = 0)
    {
        if (card == null || card.forgeAmbiguous || !PrintingKeyUtility.TryGetPrinting(card, out var set, out var number)
            || set.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || number.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
            throw new ArgumentException("需要有效且无歧义的卡牌版本。");
        bool validFace = faceIndex == 0;
        if (card.features != null)
            foreach (var feature in card.features)
                if (feature != null && feature.faceIndex == faceIndex && faceIndex >= 0) validFace = true;
        if (!validFace) throw new ArgumentOutOfRangeException(nameof(faceIndex));
        // One canonical file avoids stale JPG/PNG precedence when replacing formats.
        return PrintingKeyUtility.BuildImagePath(Root, set.ToUpperInvariant(), number, faceIndex, ".image");
    }

    public static bool TryGetPath(CardDefinition card, int faceIndex, out string path)
    {
        path = null;
        try { path = GetPath(card, faceIndex); }
        catch (ArgumentException) { return false; }
        return File.Exists(path);
    }

    public static string Import(CardDefinition card, string sourceFile, int faceIndex = 0)
    {
        string target = GetPath(card, faceIndex);
        var info = new FileInfo(sourceFile);
        if (!info.Exists || info.Length < 8 || info.Length > 16 * 1024 * 1024)
            throw new InvalidDataException("请选择不超过 16 MiB 的 PNG 或 JPEG 卡面。");
        byte[] bytes = File.ReadAllBytes(info.FullName);
        bool jpeg = bytes[0] == 0xff && bytes[1] == 0xd8;
        bool png = bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71
            && bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10;
        if (!jpeg && !png) throw new InvalidDataException("图片内容不是 PNG 或 JPEG。");
        var texture = new Texture2D(2, 2);
        try
        {
            if (!texture.LoadImage(bytes, true) || texture.width < 100 || texture.height < 100
                || texture.width > 4096 || texture.height > 4096)
                throw new InvalidDataException("卡图无法解码，或尺寸不在 100～4096 像素范围内。");
        }
        finally
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        string temporary = target + "." + Guid.NewGuid().ToString("N") + ".import";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(target)) File.Replace(temporary, target, null);
            else File.Move(temporary, target);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Changed?.Invoke(card.PrintingKey, faceIndex);
        return target;
    }

    public static bool Remove(CardDefinition card, int faceIndex = 0)
    {
        string path = GetPath(card, faceIndex);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        Changed?.Invoke(card.PrintingKey, faceIndex);
        return true;
    }
}
