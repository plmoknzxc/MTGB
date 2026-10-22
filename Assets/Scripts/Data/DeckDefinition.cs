using System;
using System.Collections.Generic;

[Serializable]
public sealed class DeckDefinition
{
    public string name = "New Deck";
    // Local file identity, never included in the network deck payload.
    [NonSerialized] public string sourcePath;
    public string format = "standard";
    public string banlist = "current";
    public List<DeckEntry> commanders = new List<DeckEntry>();
    public List<DeckEntry> cards = new List<DeckEntry>();
    public List<DeckEntry> sideboard = new List<DeckEntry>();
    // Optional physical-copy order. Empty keeps legacy grouped-entry order.
    public List<string> mainOrder = new List<string>();
    public List<string> sideboardOrder = new List<string>();
    // A designation referencing one existing sideboard card, not another copy.
    public DeckEntry companion;
    public List<DeckEntry> attractions = new List<DeckEntry>();
    public List<DeckEntry> contraptions = new List<DeckEntry>();
}

public static class DeckCopyOrder
{
    public static void Synchronize(DeckDefinition deck)
    {
        if (deck == null) return;
        Synchronize(deck.cards, deck.mainOrder);
        Synchronize(deck.sideboard, deck.sideboardOrder);
    }
    public static string Key(DeckEntry entry) => string.IsNullOrEmpty(entry.PrintingKey) ? "id:" + entry.cardId : entry.PrintingKey;

    public static List<DeckEntry> Expand(List<DeckEntry> entries, List<string> order)
    {
        var remaining = new Dictionary<string, Queue<DeckEntry>>(StringComparer.OrdinalIgnoreCase);
        var grouped = new List<DeckEntry>();
        if (entries != null) foreach (var entry in entries)
        {
            if (entry == null || entry.count <= 0) continue;
            string key = Key(entry);
            if (!remaining.TryGetValue(key, out var copies)) remaining[key] = copies = new Queue<DeckEntry>();
            for (int i = 0; i < entry.count; i++) { copies.Enqueue(entry); grouped.Add(entry); }
        }
        var result = new List<DeckEntry>(grouped.Count);
        if (order != null) foreach (string key in order)
            if (key != null && remaining.TryGetValue(key, out var copies) && copies.Count > 0) result.Add(copies.Dequeue());
        foreach (var entry in grouped)
            if (remaining.TryGetValue(Key(entry), out var copies) && copies.Count > 0) result.Add(copies.Dequeue());
        return result;
    }

    public static void Synchronize(List<DeckEntry> entries, List<string> order)
    {
        if (order == null || order.Count == 0) return;
        var copies = Expand(entries, order);
        order.Clear();
        foreach (var entry in copies) order.Add(Key(entry));
    }

    public static bool IsValid(List<DeckEntry> entries, List<string> order, int limit)
    {
        if (order == null || order.Count == 0) return true;
        if (order.Count > limit) return false;
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int total = 0;
        if (entries != null) foreach (var entry in entries)
        {
            if (entry == null || entry.count <= 0 || entry.count > limit - total) return false;
            string key = Key(entry);
            counts.TryGetValue(key, out int count); counts[key] = count + entry.count; total += entry.count;
        }
        if (total != order.Count) return false;
        foreach (string key in order)
        {
            if (key == null || key.Length > 64 || !counts.TryGetValue(key, out int count) || count <= 0) return false;
            counts[key] = count - 1;
        }
        return true;
    }
}

[Serializable]
public sealed class DeckEntry
{
    public string setCode;
    public string collectorNumber;
    public int cardId;
    public int count = 1;

    public string PrintingKey => string.IsNullOrWhiteSpace(setCode) || string.IsNullOrWhiteSpace(collectorNumber)
        ? string.Empty
        : $"{setCode.Trim()}/{collectorNumber.Trim()}";
}
