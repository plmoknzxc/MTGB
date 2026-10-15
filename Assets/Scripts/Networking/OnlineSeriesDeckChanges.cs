using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Host-owned temporary card pool changes; never calls the deck/profile repositories.</summary>
public static class OnlineSeriesDeckChanges
{
    public static DeckDefinition[] Apply(DeckDefinition first, DeckDefinition second,
        ForgeMatchRemoval[] removals, ForgeMatchOwnership[] ownership)
    {
        var result = new[] { Remove(first,1,removals), Remove(second,2,removals) };
        var seen = new HashSet<int>((removals ?? Array.Empty<ForgeMatchRemoval>()).Select(r=>r.cardId));
        var incoming = new List<(int seat, string section, DeckEntry entry)>();
        foreach (var change in ownership ?? Array.Empty<ForgeMatchOwnership>())
        {
            if (change == null || change.cardId <= 0 || change.fromOwnerSeat < 1 || change.fromOwnerSeat > 2
                || change.toOwnerSeat != 3-change.fromOwnerSeat || string.IsNullOrWhiteSpace(change.printingKey)
                || !seen.Add(change.cardId)) throw new InvalidOperationException("Invalid match ownership identity.");
            var from = result[change.fromOwnerSeat-1];
            string section = Section(change.fromSection,change.fromSideboard);
            var primary = Entries(from,section);
            // Legacy packets had only the main/sideboard hint. Explicit provenance is exact.
            var entry = Find(primary,change.printingKey);
            if (entry == null && string.IsNullOrEmpty(change.fromSection))
                entry = Find(change.fromSideboard ? from.cards : from.sideboard,change.printingKey);
            if (entry == null) throw new InvalidOperationException("Transferred card is absent from the registered series pool: " + change.printingKey);
            var gained = JsonUtility.FromJson<DeckEntry>(JsonUtility.ToJson(entry));gained.count=1;
            entry.count--;Prune(from);
            incoming.Add((change.toOwnerSeat,section == "Sideboard" ? "Main" : section,gained));
        }
        // Consume all original holdings before adding wins: a missing original cannot be supplied by another transfer.
        foreach (var gain in incoming)
        {
            var entries = Entries(result[gain.seat-1],gain.section);var entry = Find(entries,gain.entry.PrintingKey);
            if(entry==null)entries.Add(gain.entry);else entry.count++;
        }
        foreach(var deck in result)DeckCompanion.ClearMissing(deck);
        return result;
    }

    public static DeckDefinition Remove(DeckDefinition original, int ownerSeat, ForgeMatchRemoval[] removals)
    {
        if (original == null) throw new InvalidOperationException("Missing registered series deck.");
        var result = JsonUtility.FromJson<DeckDefinition>(JsonUtility.ToJson(original));
        var seen = new HashSet<int>();
        foreach (var removal in removals ?? Array.Empty<ForgeMatchRemoval>())
        {
            if (removal == null || removal.cardId <= 0 || removal.ownerSeat < 1 || removal.ownerSeat > 2
                || string.IsNullOrWhiteSpace(removal.printingKey) || !seen.Add(removal.cardId))
                throw new InvalidOperationException("Invalid match removal identity.");
            if (removal.ownerSeat != ownerSeat) continue;
            var primary = Entries(result,Section(removal.fromSection,removal.fromSideboard));
            var entry = Find(primary,removal.printingKey);
            if (entry == null && string.IsNullOrEmpty(removal.fromSection))
                entry = Find(removal.fromSideboard ? result.cards : result.sideboard,removal.printingKey);
            if (entry == null) throw new InvalidOperationException("Removed card is absent from the registered series pool: " + removal.printingKey);
            entry.count--;
            Prune(result);
        }
        DeckCompanion.ClearMissing(result);
        return result;
    }

    private static string Section(string section,bool sideboard)
    {
        if (string.IsNullOrEmpty(section)) return sideboard ? "Sideboard" : "Main";
        if ((section == "Sideboard") != sideboard || section != "Main" && section != "Sideboard"
            && section != "Attractions" && section != "Contraptions")
            throw new InvalidOperationException("Invalid match deck section.");
        return section;
    }

    private static List<DeckEntry> Entries(DeckDefinition deck,string section)
    {
        switch(section) {
            case "Main": return deck.cards;
            case "Sideboard": return deck.sideboard;
            case "Attractions": return deck.attractions ?? (deck.attractions = new List<DeckEntry>());
            case "Contraptions": return deck.contraptions ?? (deck.contraptions = new List<DeckEntry>());
            default: throw new InvalidOperationException("Unknown match deck section.");
        }
    }

    private static void Prune(DeckDefinition deck)
    {
        foreach(var entries in new[]{deck.cards,deck.sideboard,deck.attractions,deck.contraptions})
            entries?.RemoveAll(e=>e.count<=0);
    }

    private static DeckEntry Find(List<DeckEntry> entries,string key) => entries?.FirstOrDefault(e=>e!=null&&e.count>0
        && string.Equals(e.PrintingKey,key,StringComparison.OrdinalIgnoreCase));
}
