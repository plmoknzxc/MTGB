using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class CardArtSources
{
    [Serializable] public sealed class Face { public string small, normal, large; }
    [Serializable] public sealed class Entry { public string key, name, language; public Face[] faces; }
    private static Dictionary<string, Entry> entries;
    private static Task loading;

    public static Task LoadAsync()
    {
        if (entries != null) return Task.CompletedTask;
        if (loading != null) return loading;
        string path = Path.Combine(Application.streamingAssetsPath, "CardArtSources.jsonl");
        return loading = LoadCore(path);
    }

    private static async Task LoadCore(string path)
    {
        try
        {
            entries = await Task.Run(() =>
            {
                var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
                foreach (string line in File.ReadLines(path))
                {
                    var entry = JsonUtility.FromJson<Entry>(line);
                    result.Add(entry.key, entry);
                }
                return result;
            });
        }
        finally { loading = null; }
    }

    public static bool TryGet(CardDefinition card, out Entry entry)
    {
        entry = null;
        return card != null && entries != null && !card.forgeAmbiguous &&
            entries.TryGetValue(card.PrintingKey, out entry) && entry.name == card.cardName;
    }

    public static Uri Url(CardDefinition card, int face, string quality)
    {
        if (!TryGet(card, out var entry) || face < 0 || face >= entry.faces.Length)
            throw new InvalidOperationException("此版本暂无可用的图片来源。");
        var image = entry.faces[face];
        string value = quality == "small" ? image.small : quality == "large" ? image.large : image.normal;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "cards.scryfall.io")
            throw new InvalidDataException("卡图来源地址无效。");
        return uri;
    }
}
