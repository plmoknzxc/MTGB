using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

// Reads complete dated format snapshots (card pool plus banned/restricted cards).
// Most are packaged with Forge; community/Commander snapshots live in StreamingAssets.
public sealed class DeckFormatLegality
{
    public static readonly string[] Keys = { "standard", "pioneer", "modern", "premodern", "legacy", "vintage", "pauper", "commander", "duelcommander", "brawl", "historic", "timeless" };
    public static readonly string[] Names = { "标准", "先驱", "摩登", "前摩登", "薪传", "特选", "纯铁", "指挥官·官禁", "指挥官·法禁", "争锋", "史迹", "永恒" };
    public const string Current = "current";
    public const string NoBanlist = "none";
    public static readonly string[] OnlineKeys = { "standard", "pioneer", "modern", "premodern", "legacy", "vintage", "pauper", "historic", "timeless", "none" };
    public static string FormatName(string key)
    {
        int index = Array.IndexOf(Keys, key);
        return index == 0 ? "标准赛" : index >= 0 ? Names[index] : "N/A";
    }

    // Authority-side preparation is cached; reads format rules, never hashes runtime resources.
    public void PrepareForValidation(string format, IReadOnlyList<CardDefinition> catalog)
    {
        if (!ReferenceEquals(indexedCatalog, catalog) || indexedCatalogCount != catalog.Count)
        {
            nameSets.Clear();
            foreach (var card in catalog)
            {
                if (card == null || string.IsNullOrWhiteSpace(card.cardName)) continue;
                string set = string.IsNullOrWhiteSpace(card.forgeSet) ? card.setCode : card.forgeSet;
                if (string.IsNullOrWhiteSpace(set)) continue;
                if (!nameSets.TryGetValue(card.cardName, out var sets)) nameSets[card.cardName] = sets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                sets.Add(set);
            }
            indexedCatalog = catalog; indexedCatalogCount = catalog.Count;
        }
        string key = Key(format, Current);
        if (!snapshots.TryGetValue(key, out var rule)) snapshots[key] = rule = Parse(ResolvePath(format, Current));
        if (rule.CommonOnly && commonDates == null) commonDates = ReadCommonDates(Path.Combine(ForgeCardCatalog.Root, "assets", "res", "editions"));
    }

    sealed class Snapshot
    {
        public bool HasSets, CommonOnly, RestrictedLegendary;
        public DateTime Effective = DateTime.MaxValue;
        public readonly HashSet<string> Sets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Banned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Restricted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Additional = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    readonly Dictionary<string, Snapshot> snapshots = new Dictionary<string, Snapshot>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, HashSet<string>> nameSets = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, DateTime> commonDates;
    IReadOnlyList<CardDefinition> indexedCatalog;
    int indexedCatalogCount = -1;
    public bool Loading { get; private set; }
    public string Error { get; private set; }
    public event Action Changed;

    static string Root => Path.Combine(ForgeCardCatalog.Root, "assets", "res", "formats");
    static string LocalRoot => Path.Combine(Application.streamingAssetsPath, "DeckFormats");

    public static List<string> Dates(string format)
    {
        var result = new List<string>();
        string folder = HasLocalSnapshots(format)
            ? Path.Combine(LocalRoot, Folder(format))
            : Path.Combine(Root, "Archived", Folder(format));
        if (!Directory.Exists(folder)) return result;
        foreach (string file in Directory.GetFiles(folder, "*.txt"))
        {
            string date = Path.GetFileNameWithoutExtension(file);
            if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                result.Add(date);
        }
        result.Sort((a, b) => string.CompareOrdinal(b, a));
        return result;
    }

    public static string Latest(string format)
    {
        var dates = Dates(format);
        return dates.Count > 0 ? dates[0] : Current;
    }

    public static string Label(string banlist) => banlist == Current ? "现行规则（无历史日期）" : banlist == NoBanlist ? "N/A（无禁限）" : banlist;

    public string Get(CardDefinition card, string format, string banlist)
    {
        if (card == null || string.IsNullOrWhiteSpace(card.cardName)) return "unknown";
        if (!snapshots.TryGetValue(Key(format, banlist), out var rule)) return "unknown";
        string name = card.cardName;
        if (banlist != NoBanlist)
        {
            if (rule.Banned.Contains(name)) return "banned";
            if (rule.Restricted.Contains(name) || rule.RestrictedLegendary && card.HasSupertype(CardSupertype.Legendary))
                return "restricted";
        }
        if (rule.Additional.Contains(name)) return "legal";
        if (rule.HasSets && (!nameSets.TryGetValue(name, out var editions) || !editions.Overlaps(rule.Sets)))
            return "not_legal";
        if (rule.CommonOnly)
        {
            if (commonDates == null) return "unknown";
            if (!card.HasSupertype(CardSupertype.Basic)
                && (!commonDates.TryGetValue(name, out var firstCommon) || firstCommon > rule.Effective))
                return "not_legal";
        }
        return "legal";
    }

    public IEnumerator Refresh(string format, string banlist, IReadOnlyList<CardDefinition> catalog)
    {
        while (Loading) yield return null;
        Loading = true;
        Error = null;
        try
        {
            if (catalog == null) { Error = "卡牌目录尚未加载。"; yield break; }
            if (!ReferenceEquals(indexedCatalog, catalog) || indexedCatalogCount != catalog.Count)
            {
                nameSets.Clear();
                for (int i = 0; i < catalog.Count; i++)
                {
                    CardDefinition card = catalog[i];
                    if (card != null && !string.IsNullOrWhiteSpace(card.cardName))
                    {
                        string set = string.IsNullOrWhiteSpace(card.forgeSet) ? card.setCode : card.forgeSet;
                        if (!string.IsNullOrWhiteSpace(set))
                        {
                            if (!nameSets.TryGetValue(card.cardName, out var sets))
                                nameSets[card.cardName] = sets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            sets.Add(set);
                        }
                    }
                    if (i % 4000 == 3999) yield return null;
                }
                indexedCatalogCount = catalog.Count;
                indexedCatalog = catalog;
            }
            string key = Key(format, banlist);
            if (!snapshots.TryGetValue(key, out var rule))
            {
                string path = ResolvePath(format, banlist);
                if (!File.Exists(path)) { Error = "没有这个赛制日期的规则文件。"; yield break; }
                rule = Parse(path);
                snapshots[key] = rule;
            }
            if (rule.CommonOnly && commonDates == null)
            {
                string editions = Path.Combine(ForgeCardCatalog.Root, "assets", "res", "editions");
                var job = Task.Run(() => ReadCommonDates(editions));
                while (!job.IsCompleted) yield return null;
                if (job.IsFaulted) { Error = "无法读取 Forge 的稀有度资料。"; yield break; }
                commonDates = job.Result;
            }
        }
        finally
        {
            Loading = false;
            Changed?.Invoke();
        }
    }

    static string Key(string format, string banlist) => format + "/"
        + (banlist == NoBanlist || banlist == Current ? Latest(format) : banlist);

    static string Folder(string format)
    {
        if (string.IsNullOrEmpty(format)) return string.Empty;
        return char.ToUpperInvariant(format[0]) + format.Substring(1);
    }

    static bool HasLocalSnapshots(string format) => format == "commander" || format == "duelcommander"
        || format == "brawl" || format == "premodern";

    static string ResolvePath(string format, string banlist)
    {
        if (banlist == NoBanlist || banlist == Current) banlist = Latest(format);
        if (banlist != Current && banlist != NoBanlist)
            return HasLocalSnapshots(format)
                ? Path.Combine(LocalRoot, Folder(format), banlist + ".txt")
                : Path.Combine(Root, "Archived", Folder(format), banlist + ".txt");
        if (format == "commander") return Path.Combine(Root, "Casual", "Commander.txt");
        if (format == "premodern") return Path.Combine(Root, "Casual", "Premodern.txt");
        if (format == "timeless")
        {
            var dates = Dates(format);
            return dates.Count == 0 ? string.Empty : Path.Combine(Root, "Archived", "Timeless", dates[0] + ".txt");
        }
        return Path.Combine(Root, "Sanctioned", Folder(format) + ".txt");
    }

    static Snapshot Parse(string path)
    {
        var rule = new Snapshot();
        if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(path), "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate)) rule.Effective = fileDate;
        foreach (string original in File.ReadLines(path))
        {
            int separator = original.IndexOf(':');
            if (separator < 0) continue;
            string field = original.Substring(0, separator).Trim();
            string value = original.Substring(separator + 1).Trim();
            switch (field)
            {
                case "Effective":
                    if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) rule.Effective = date;
                    break;
                case "Sets": rule.HasSets = true; Add(rule.Sets, value, ','); break;
                case "Additional": Add(rule.Additional, value, ';'); break;
                case "Banned": Add(rule.Banned, value, ';'); break;
                case "Restricted": Add(rule.Restricted, value, ';'); break;
                case "Rarities": rule.CommonOnly = value.Contains("C"); break;
                case "RestrictedLegendary": rule.RestrictedLegendary = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
            }
        }
        return rule;
    }

    static void Add(HashSet<string> target, string text, char separator)
    {
        foreach (string part in text.Split(separator))
        {
            string value = part.Trim();
            if (value.Length > 0) target.Add(value);
        }
    }

    static readonly Regex EditionCard = new Regex(@"^\S+\s+([CL])\s+(.+?)(?:\s+@.*)?$", RegexOptions.Compiled);
    static Dictionary<string, DateTime> ReadCommonDates(string folder)
    {
        var result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(folder)) return result;
        foreach (string path in Directory.GetFiles(folder, "*.txt"))
        {
            DateTime date = DateTime.MaxValue;
            bool cardSection = false;
            foreach (string line in File.ReadLines(path))
            {
                if (line.StartsWith("Date=", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParseExact(line.Substring(5).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                        date = parsed;
                }
                else if (line.Trim() == "[cards]") cardSection = true;
                else if (line.StartsWith("[", StringComparison.Ordinal)) cardSection = false;
                else if (cardSection)
                {
                    Match match = EditionCard.Match(line);
                    if (!match.Success) continue;
                    string name = match.Groups[2].Value.Trim();
                    if (!result.TryGetValue(name, out var old) || date < old) result[name] = date;
                }
            }
        }
        return result;
    }
}
