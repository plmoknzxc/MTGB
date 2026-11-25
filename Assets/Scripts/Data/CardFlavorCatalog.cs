using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

// Optional display metadata, separate from Forge's authoritative rules catalog.
public static class CardFlavorCatalog
{
    [Serializable] public sealed class Entry { public string key, name, text; }

    public static Dictionary<string, Entry> ReadLocalized(string path)
    {
        var entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path)) return entries;
        foreach (string line in File.ReadLines(path))
        {
            var entry = JsonUtility.FromJson<Entry>(line);
            if (entry == null || string.IsNullOrWhiteSpace(entry.key) || string.IsNullOrWhiteSpace(entry.name)
                || string.IsNullOrWhiteSpace(entry.text)) continue;
            if (!entries.TryAdd(entry.key, entry))
                throw new InvalidDataException("重复的本地化风味文字印次：" + entry.key);
        }
        return entries;
    }

    public static void Apply(List<CardDefinition> cards, string path, CancellationToken cancellation)
    {
        if (!File.Exists(path)) return;
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(path))
        {
            cancellation.ThrowIfCancellationRequested();
            var entry = JsonUtility.FromJson<Entry>(line);
            if (entry != null && !string.IsNullOrEmpty(entry.key)) entries[entry.key] = entry;
        }
        foreach (var card in cards)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!card.customContent && !card.forgeAmbiguous && entries.TryGetValue(card.PrintingKey, out var entry)
                && string.Equals(card.cardName, entry.name, StringComparison.Ordinal))
                card.flavorText = entry.text;
        }
    }
}
