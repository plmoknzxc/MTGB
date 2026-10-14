using System;
using System.Linq;
using UnityEngine;

/// <summary>Exact linked artwork for a native Specialize state, without changing its physical printing.</summary>
public static class SpecializeCardArt
{
    [Serializable] private sealed class Entry { public string key, state, name, artKey; }
    [Serializable] private sealed class Index { public Entry[] entries; }
    private static Entry[] entries;

    public static bool TryGetKey(ForgeCardView card, out string key)
    {
        key = null;
        if (card == null || card.faceDown || !(card.stateName ?? "").StartsWith("Specialize", StringComparison.Ordinal)) return false;
        if (entries == null)
        {
            var asset = Resources.Load<TextAsset>("SpecializeCardArt");
            entries = asset == null ? Array.Empty<Entry>() : JsonUtility.FromJson<Index>(asset.text).entries ?? Array.Empty<Entry>();
        }
        var entry = entries.FirstOrDefault(x => x.key == card.printingKey && x.state == card.stateName && x.name == card.name);
        key = entry?.artKey;
        return entry != null;
    }
}
