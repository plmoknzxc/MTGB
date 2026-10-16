using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public static class CardSearchMetadata
{
    [Serializable] public sealed class Entry
    {
        public string key, name, rarity, setName, artist, layout;
        public int colors, identity, released, games, legalityMask;
        public float manaValue;
    }
    static Dictionary<string, Entry> entries;
    static Task loading;
    public static Task LoadAsync()
    {
        if (entries != null) return Task.CompletedTask;
        if (loading != null) return loading;
        string path = Path.Combine(Application.streamingAssetsPath, "CardSearch.jsonl");
        return loading = Read(path);
    }
    static async Task Read(string path)
    {
        try
        {
            entries = await Task.Run(() =>
            {
                var result = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
                // Scope the pool to this read. Entries keep their shared strings;
                // the temporary index must not keep a discarded catalog alive.
                var labels = new Dictionary<string, string>(StringComparer.Ordinal);
                string Share(string value)
                {
                    if (value == null) return null;
                    if (value.Length == 0) return string.Empty;
                    if (labels.TryGetValue(value, out var shared)) return shared;
                    labels.Add(value, value);
                    return value;
                }
                foreach (string line in File.ReadLines(path))
                {
                    var entry = JsonUtility.FromJson<Entry>(line);
                    entry.name = Share(entry.name);
                    entry.rarity = Share(entry.rarity);
                    entry.setName = Share(entry.setName);
                    entry.artist = Share(entry.artist);
                    entry.layout = Share(entry.layout);
                    result.Add(entry.key, entry);
                }
                return result;
            });
        }
        finally { loading = null; }
    }
    public static Entry Get(CardDefinition card)
        => card != null && !card.customContent && entries != null && entries.TryGetValue(card.PrintingKey, out var entry)
            && string.Equals(entry.name, card.cardName, StringComparison.Ordinal) ? entry : null;
}
