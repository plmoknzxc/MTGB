using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

// Shared published vocabulary for native characteristics and search labels.
// Cache canonical terms, never locale-specific output: replay language can change.
public static class CardTypeText
{
    [Serializable] private sealed class Entry { public string en, zh, ja; }
    [Serializable] private sealed class Index { public Entry[] entries; }
    private static Dictionary<string, Entry> entries;
    private static Regex terms;
    private static readonly Dictionary<string, string> Fallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        {"Artifact", "神器"}, {"Battle", "战役"}, {"Creature", "生物"}, {"Enchantment", "结界"}, {"Instant", "瞬间"},
        {"Kindred", "亲缘"}, {"Land", "地"}, {"Planeswalker", "鹏洛客"}, {"Sorcery", "法术"},
        {"Basic", "基本"}, {"Legendary", "传奇"}, {"Ongoing", "持续"}, {"Snow", "雪境"}, {"World", "世界"},
        {"Human", "人类"}, {"Elf", "妖精"}, {"Goblin", "鬼怪"}, {"Dragon", "龙"}, {"Zombie", "灵俑"},
        {"Vampire", "吸血鬼"}, {"Angel", "天使"}, {"Wizard", "法术师"}, {"Warrior", "战士"}, {"Soldier", "士兵"},
        {"Island", "海岛"}, {"Plains", "平原"}, {"Swamp", "沼泽"}, {"Mountain", "山脉"}, {"Forest", "树林"}
    };

    private static void EnsureVocabulary()
    {
        if (entries != null) return;
        entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (string resource in new[] { "TokenTypeAliases", "SearchTypeLabels" }) {
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null) continue;
            foreach (var term in JsonUtility.FromJson<Index>(asset.text).entries) {
                if (!entries.TryGetValue(term.en, out var existing)) entries[term.en] = term;
                else {
                    if (!string.IsNullOrEmpty(term.zh)) existing.zh = term.zh;
                    if (!string.IsNullOrEmpty(term.ja)) existing.ja = term.ja;
                }
            }
            Resources.UnloadAsset(asset);
        }
        var keys = entries.Keys.Concat(Fallback.Keys).Concat(DeckTokenLinks.SubtypeNames).Concat(new[] { "Token", "Tribal" }).Distinct(StringComparer.OrdinalIgnoreCase);
        // Match multiword and hyphenated subtypes before their component words.
        terms = new Regex(@"(?<![\p{L}\p{N}_])(?:" + string.Join("|", keys.OrderByDescending(k => k.Length).ThenBy(k => k, StringComparer.Ordinal).Select(Regex.Escape)) + @")(?![\p{L}\p{N}_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static string Term(string value)
    {
        if (string.IsNullOrEmpty(value) || GamePreferences.Language == "en-US") return value ?? "";
        EnsureVocabulary();
        if (entries.TryGetValue(value, out var entry)) {
            string translated = GamePreferences.Language == "ja-JP" ? entry.ja : entry.zh;
            if (!string.IsNullOrEmpty(translated)) return translated;
        }
        if (value.Equals("Token", StringComparison.OrdinalIgnoreCase)) return GamePreferences.Language == "ja-JP" ? "トークン" : "衍生物";
        if (value.Equals("Tribal", StringComparison.OrdinalIgnoreCase)) return GamePreferences.Language == "ja-JP" ? "同族" : "亲缘";
        string subtype = DeckTokenLinks.LocalizedSubtype(value);
        if (!string.Equals(subtype, value, StringComparison.Ordinal)) return subtype;
        return Fallback.TryGetValue(value, out var key) ? GamePreferences.T(key) : value;
    }

    public static string Line(string value)
    {
        if (string.IsNullOrEmpty(value) || GamePreferences.Language == "en-US") return value ?? "";
        EnsureVocabulary();
        return terms.Replace(value, match => Term(match.Value));
    }

    public static IEnumerable<string> Parts(string value)
    {
        EnsureVocabulary();
        value = value ?? "";
        int start = 0;
        foreach (Match match in terms.Matches(value)) {
            foreach (string part in value.Substring(start, match.Index - start).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) yield return part;
            yield return match.Value;
            start = match.Index + match.Length;
        }
        foreach (string part in value.Substring(start).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) yield return part;
    }
}
