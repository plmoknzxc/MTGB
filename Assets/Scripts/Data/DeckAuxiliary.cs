using System;
using System.Collections.Generic;
using System.Linq;

// Auxiliary cards are registered separately; they never contribute to the main/sideboard size.
public static class DeckAuxiliary
{
    public static bool IsAttraction(CardDefinition card) => HasSubtype(card, "Attraction");
    public static bool IsContraption(CardDefinition card) => HasSubtype(card, "Contraption");
    public static bool IsAuxiliary(CardDefinition card) => IsAttraction(card) || IsContraption(card);

    public static List<DeckEntry> Entries(DeckDefinition deck, bool attraction)
    {
        if (deck == null) return null;
        if (attraction) return deck.attractions ?? (deck.attractions = new List<DeckEntry>());
        return deck.contraptions ?? (deck.contraptions = new List<DeckEntry>());
    }

    public static bool ContainsName(DeckDefinition deck, CardDefinition card, IReadOnlyList<CardDefinition> cards)
    {
        if (!IsAuxiliary(card)) return false;
        var entries = Entries(deck, IsAttraction(card));
        if (entries == null) return false;
        foreach (var entry in entries)
            if (string.Equals(DeckRepository.ResolveCard(entry, cards)?.cardName, card.cardName, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool Add(DeckDefinition deck, CardDefinition card, IReadOnlyList<CardDefinition> cards)
    {
        if (deck == null || !IsAuxiliary(card) || card.forgeAmbiguous || ContainsName(deck, card, cards)) return false;
        var entry = new DeckEntry { setCode = card.setCode, collectorNumber = card.CollectorNumber, cardId = card.cardID, count = 1 };
        if (!ReferenceEquals(DeckRepository.ResolveCard(entry, cards), card)) return false;
        Entries(deck, IsAttraction(card)).Add(entry);
        return true;
    }

    public static bool Remove(DeckDefinition deck, CardDefinition card)
    {
        if (deck == null || !IsAuxiliary(card)) return false;
        return Entries(deck, IsAttraction(card)).RemoveAll(entry => entry != null
            && string.Equals(entry.setCode, card.setCode, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.collectorNumber, card.CollectorNumber, StringComparison.OrdinalIgnoreCase)) > 0;
    }

    private static bool HasSubtype(CardDefinition card, string subtype)
    {
        if (card == null) return false;
        foreach (string part in (card.subtypes ?? string.Empty).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            if (string.Equals(part, subtype, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static bool Validate(DeckDefinition deck, IReadOnlyList<CardDefinition> cards, out string error, DeckDefinition authorizedPool = null)
    {
        bool authorized = authorizedPool != null && SameEntries(deck.attractions,authorizedPool.attractions)
            && SameEntries(deck.contraptions,authorizedPool.contraptions);
        if (!ValidateSection(deck.attractions, cards, true, authorized, out error)
            || !ValidateSection(deck.contraptions, cards, false, authorized, out error)) return false;
        foreach (var entries in new[] { deck.cards, deck.sideboard })
        {
            if (entries == null) continue;
            foreach (var entry in entries)
                if (IsAuxiliary(DeckRepository.ResolveCard(entry, cards)))
                { error = "游乐设施与装置必须放在各自的辅助牌组，不能放入主牌或备牌。"; return false; }
        }
        error = string.Empty;
        return true;
    }

    internal static bool SameEntries(List<DeckEntry> first,List<DeckEntry> second)
    {
        Dictionary<string,int> Counts(List<DeckEntry> entries) => (entries ?? new List<DeckEntry>())
            .Where(e=>e!=null && e.count>0).GroupBy(e=>e.PrintingKey,StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g=>g.Key,g=>g.Sum(e=>e.count),StringComparer.OrdinalIgnoreCase);
        var a=Counts(first);var b=Counts(second);
        return a.Count==b.Count && a.All(e=>b.TryGetValue(e.Key,out int count)&&count==e.Value);
    }

    private static bool ValidateSection(List<DeckEntry> entries, IReadOnlyList<CardDefinition> cards, bool attraction, bool authorized, out string error)
    {
        error = string.Empty;
        if (entries == null || entries.Count == 0) return true;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int count = 0;
        string label = attraction ? "游乐设施" : "装置";
        foreach (var entry in entries)
        {
            var card = DeckRepository.ResolveCard(entry, cards);
            if (entry == null || entry.count <= 0 || !authorized && entry.count != 1 || card == null
                || !(attraction ? IsAttraction(card) : IsContraption(card)) || !authorized && !names.Add(card.cardName))
            { error = label + "辅助牌组必须由不同名且印次可唯一映射的对应卡牌组成，每种只能一张。"; return false; }
            count++;
        }
        int minimum = attraction ? 10 : 15; // Pinned Forge Constructed DeckFormat.
        if (!authorized && count < minimum)
        { error = label + "辅助牌组至少需要 " + minimum + " 张，或不使用该辅助牌组。"; return false; }
        return true;
    }
}
