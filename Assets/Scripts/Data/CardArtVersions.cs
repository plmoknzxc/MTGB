using System;
using System.Collections.Generic;

/// <summary>Display-only printing index. Does not change deck entries or Forge rules.</summary>
public static class CardArtVersions
{
    private static readonly Dictionary<string, IReadOnlyList<CardDefinition>> Groups =
        new Dictionary<string, IReadOnlyList<CardDefinition>>(StringComparer.Ordinal);
    private static bool indexed;

    internal static void Reset() { Groups.Clear(); indexed=false; }

    public static IReadOnlyList<CardDefinition> GetVersions(CardDefinition card)
    {
        if (card == null) return Array.Empty<CardDefinition>();
        var catalog = CardDatabaseRepository.LoadAll();
        string identity = Identity(card);
        if(!indexed)
        {
            // Localized image fallback needs this for every visible card. Build
            // the printing index once rather than scan the entire catalog per tile.
            var grouped=new Dictionary<string,List<CardDefinition>>(StringComparer.Ordinal);
            var seen=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
            foreach(var candidate in catalog)
            {
                if(candidate.forgeAmbiguous)continue;
                string key=Identity(candidate);
                if(!grouped.TryGetValue(key,out var versions))
                {
                    grouped[key]=versions=new List<CardDefinition>();
                    seen[key]=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }
                if(seen[key].Add(candidate.PrintingKey))versions.Add(candidate);
            }
            foreach(var pair in grouped)
            {
                pair.Value.Sort((a,b)=>StringComparer.Ordinal.Compare(a.PrintingKey,b.PrintingKey));
                Groups[pair.Key]=pair.Value.AsReadOnly();
            }
            indexed=true;
        }
        return Groups.TryGetValue(identity,out var existing)?existing:Array.Empty<CardDefinition>();
    }

    private static string Identity(CardDefinition card) =>
        string.IsNullOrEmpty(card.oracleID) ? "printing:" + card.PrintingKey : card.oracleID;
}
