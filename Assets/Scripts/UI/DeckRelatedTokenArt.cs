using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Display-only, exact-printing links built offline. Never downloads metadata or images.
public static class DeckRelatedTokenArt
{
    public sealed class Art { public Sprite sprite; public string rules, name, key, group; }
    [Serializable] private sealed class Token { public string key, name, rules; }
    [Serializable] private sealed class Entry { public string key, name; public Token[] tokens; }
    [Serializable] private sealed class Index { public Entry[] entries; }
    private static Dictionary<string, Entry> entries;

    public static IEnumerator Load(CardDefinition card, Action<List<Art>> complete, string format = null)
    {
        complete(GetReferences(card, true, format));
        yield break;
    }

    public static List<Art> GetReferences(CardDefinition card, bool loadImages = false, string format = null)
    {
        var result = new List<Art>();
        if (card == null) return result;
        EnsureLoaded();
        if (!card.forgeAmbiguous && entries.TryGetValue(card.PrintingKey, out var source)
            && source.name == card.cardName)
        {
            foreach (var token in source.tokens)
            {
                Sprite sprite = null;
                if(loadImages) CardImageLibrary.TryLoadPrintingKey(token.key, out sprite);
                result.Add(new Art { sprite = sprite, rules = token.rules, name = token.name, key = token.key });
            }
        }
        var helpers = DeckMechanicArt.GetReferences(card, loadImages, format:format);
        foreach(var helper in helpers) result.RemoveAll(a => a.name == helper.name);
        result.AddRange(helpers);
        return result;
    }

    // Names only, with no mechanic lookup: the glossary uses this to avoid
    // mistaking a referenced Storm Crow token for the Storm keyword.
    public static IEnumerable<string> GetTokenNames(CardDefinition card)
    {
        if(card==null||card.forgeAmbiguous)yield break;
        EnsureLoaded();
        if(entries.TryGetValue(card.PrintingKey,out var source)&&source.name==card.cardName)
            foreach(var token in source.tokens)yield return token.name;
    }

    private static void EnsureLoaded()
    {
        if (entries == null)
        {
            entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var asset = Resources.Load<TextAsset>("RelatedTokenArt");
            if (asset != null)
            {
                foreach (var entry in JsonUtility.FromJson<Index>(asset.text).entries)
                    entries[entry.key] = entry;
                Resources.UnloadAsset(asset);
            }
        }
    }

    // Text index only; local sprites are owned by CardImageLibrary's shared cache.
    public static void Clear() { entries = null; }
}
