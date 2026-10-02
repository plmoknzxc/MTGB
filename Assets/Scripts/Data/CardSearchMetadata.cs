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
    public static Entry Get(CardDefinition card)
        => card != null && !card.customContent && entries != null && entries.TryGetValue(card.PrintingKey, out var entry)
            && string.Equals(entry.name, card.cardName, StringComparison.Ordinal) ? entry : null;
}
