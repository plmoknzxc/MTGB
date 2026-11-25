using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

// A draft is independent of the active collection query; compile once when Apply is pressed.
[Serializable]
public sealed class AdvancedCardSearch
{
    public string Query = "", Name = "", Rules = "", Type = "", Mana = "", Value = "", Power = "", Toughness = "", Loyalty = "", Sets = "", Artist = "", From = "", To = "";
    public int Colors, Identity, ColorMode, IdentityMode = 3, Rarity, Layout, Format, Status, Source, Sort;
    public bool Unique;
    public bool CommanderFit, CompanionFit;
    public int Games = 1;
    static readonly char[] SetSeparators = { ',', '，', ';', '；' };
    public static readonly string[] Rarities = { "", "common", "uncommon", "rare", "mythic", "special", "bonus" };
    public static readonly string[] Layouts = { "", "normal", "transform", "modal_dfc", "adventure", "split", "flip", "meld", "saga", "class" };
    public AdvancedCardSearch Copy() => (AdvancedCardSearch)MemberwiseClone();

    // Source and deck fit are handled by the collection. Ordering does not filter printings.
    public bool HasSearchOptions => HasPrintingFilters || Source != 0 || Sort != 0 || Unique;
    public bool HasPrintingFilters => Colors != 0 || Identity != 0 || Rarity != 0 || Layout != 0
        || Format != 0 || Status != 0 || Games != 1
        || new[] { Name, Rules, Type, Mana, Value, Power, Toughness, Loyalty, Artist, From, To }
            .Any(value => !string.IsNullOrWhiteSpace(value))
        || SetTerms(Sets).Any();

    internal static IEnumerable<string> SetTerms(string input) => (input ?? "").Split(SetSeparators, StringSplitOptions.RemoveEmptyEntries)
        .Select(term => term.Trim()).Where(term => term.Length > 0);

    Func<CardDefinition, CardSearchMetadata.Entry, bool> CompileSetFilter(IReadOnlyList<CardDefinition> cards)
    {
        var comparer = StringComparer.OrdinalIgnoreCase;
        var terms = SetTerms(Sets).Distinct(comparer).ToArray();
        if (terms.Length == 0) return null;
        var codes = new HashSet<string>(comparer);
        var aliases = new HashSet<string>(comparer);
        var fragments = new List<string>();
        var catalog = SetBrowserPanel.Entries;
        foreach (var term in terms)
        {
            // The picker writes printing set codes. Never reinterpret ONE as part of "Throne".
            if (catalog.Any(e => comparer.Equals(e.code, term)) || cards.Any(c => comparer.Equals(c.setCode, term)))
            {
                codes.Add(term);
                continue;
            }
            if (cards.Any(c => comparer.Equals(c.forgeSet, term)))
            {
                aliases.Add(term);
                continue;
            }
            var exactNames = catalog.Where(e => comparer.Equals(e.name, term)
                || comparer.Equals(e.zh, term) || comparer.Equals(e.ja, term)).ToArray();
            if (exactNames.Length > 0)
            {
                codes.UnionWith(exactNames.Select(e => e.code));
                continue;
            }
            codes.UnionWith(SetBrowserPanel.MatchingCodes(term));
            fragments.Add(term);
        }
        return (card, meta) =>
        {
            if (codes.Contains(card.setCode) || aliases.Contains(card.forgeSet)) return true;
            foreach (var term in fragments)
                if (Contains(meta?.setName, term)) return true;
            return false;
        };
    }

    public Func<CardDefinition, bool> Compile(IReadOnlyList<CardDefinition> cards)
    {
        var value = Number(Value); var power = Number(Power); var toughness = Number(Toughness); var loyalty = Number(Loyalty);
        int from = Date(From), to = Date(To);
        if (from > 0 && to > 0 && from > to) throw new FormatException("日期范围无效");
        var matchesSet = CompileSetFilter(cards);
        var mana = Symbols(Mana);
        var costCache = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var textCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        int textRevision = GamePreferences.Revision;
        string format = Format == 0 ? null : DeckFormatLegality.Keys[Format - 1];
        var legality = format == null ? null : DeckFormatLegality.ForSearch(format, cards);
        return card =>
        {
            if (textRevision != GamePreferences.Revision)
            {
                textCache.Clear();
                textRevision = GamePreferences.Revision;
            }
            if (!MainMenuController.MatchesCollectionFilter(card, Query, Source)) return false;
            if (card.customContent || !textCache.TryGetValue(card.cardName, out bool textMatch))
            {
                textMatch = TextMatches(card, Name, 1) && TextMatches(card, Type, 2) && TextMatches(card, Rules, 3);
                if (!card.customContent) textCache[card.cardName] = textMatch;
            }
            if (!textMatch) return false;
            var meta = CardSearchMetadata.Get(card);
            if(card.customContent ? (Games&1)==0 : meta==null || (meta.games&Games)==0) return false;
            if (format == null)
            {
                // Local custom cards have no official banned/restricted classification.
                int allowed = Status == 0 ? 3 : 1 << Status;
                if (card.customContent ? Status != 0 : (meta.legalityMask & allowed) == 0) return false;
            }
            if ((Colors != 0 && (meta == null || !ColorMatches(meta.colors, Colors, ColorMode)))
                || (Identity != 0 && (meta == null || !ColorMatches(meta.identity, Identity, IdentityMode)))) return false;
            if (Value.Length > 0 && (meta == null || !value(meta.manaValue))) return false;
            if (Rarity > 0 && (meta == null || meta.rarity != Rarities[Rarity])) return false;
            if (Layout > 0 && (meta == null || meta.layout != Layouts[Layout])) return false;
            if (Artist.Length > 0 && (meta == null || !Contains(meta.artist, Artist))) return false;
            if ((from > 0 || to > 0) && (meta == null || meta.released == 0 || meta.released < from || (to > 0 && meta.released > to))) return false;
            if (matchesSet != null && !matchesSet(card, meta)) return false;
            if (format != null)
            {
                // Local rules can distinguish digital and paper printings of the same name.
                string state = legality.Get(card, format, DeckFormatLegality.Current);
                if (Status == 0 ? state != "legal" && state != "restricted"
                    : state != (Status == 1 ? "restricted" : "banned")) return false;
            }
            bool stats = Power.Length > 0 || Toughness.Length > 0 || Loyalty.Length > 0 || mana.Count > 0;
            if (!stats) return true;
            // P/T and mana constraints must be satisfied by the same face/part.
            if (card.features != null && card.features.Count > 0)
                return card.features.Any(f => f != null && Face(f.cardName, f.cardTypes, f.manaCost, f.powerExpression, f.toughnessExpression, f.attack, f.defence, f.loyalty));
            return Face(card.cardName, card.cardTypes, card.manaCost, null, null, card.attack, card.defence, card.loyalty);

            bool Face(string faceName, CardType type, string cost, string p, string t, int attack, int defence, int loy)
            {
                if ((Power.Length > 0 || Toughness.Length > 0) && (type & CardType.Creature) == 0) return false;
                if (Loyalty.Length > 0)
                {
                    if ((type & CardType.Planeswalker) == 0) return false;
                    var printed = meta?.loyaltyOverrides?.FirstOrDefault(entry => entry.face == faceName);
                    if (printed != null && !int.TryParse(printed.value, out loy)) return false;
                    if (!loyalty(loy)) return false;
                }
                if (Power.Length > 0 && !Stat(p, attack, power) || Toughness.Length > 0 && !Stat(t, defence, toughness)) return false;
                if (mana.Count == 0) return true;
                cost = cost ?? "";
                if (!costCache.TryGetValue(cost, out var actual)) costCache[cost] = actual = Symbols(cost, false);
                return mana.All(pair => actual.TryGetValue(pair.Key, out int count) && count >= pair.Value);
            }
        };
    }
    static bool Stat(string expression, int fallback, Func<float, bool> test)
        => string.IsNullOrWhiteSpace(expression) ? test(fallback) : float.TryParse(expression, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && test(n);
    public static bool ColorMatches(int actual, int selected, int mode)
    {
        if (selected == 32) return actual == 0;
        return mode == 0 ? (actual & selected) != 0 : mode == 1 ? (actual & selected) == selected : mode == 2 ? actual == selected : (actual & ~selected) == 0;
    }
    static bool Contains(string text, string term) => (text ?? "").IndexOf(term.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    static bool TextMatches(CardDefinition card, string query, int field)
    {
        foreach (string term in query.Split(new[] { ' ', '　' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string original = field == 1 ? card.cardName : field == 2 ? card.supertypes + " " + card.cardTypes + " " + card.subtypes : card.description;
            bool found = Contains(original, term) || GamePreferences.MatchesCurrentCardTranslation(card, term, field);
            if (!found && card.features != null) found = card.features.Any(f => f != null && Contains(field == 1 ? f.cardName : field == 2 ? f.supertypes + " " + f.cardTypes + " " + f.subtypes : f.description, term));
            if (!found) return false;
        }
        return true;
    }
    public static Func<float, bool> Number(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return _ => true;
        var m = Regex.Match(input.Trim(), @"^(<=|>=|!=|=|<|>)?\s*(-?\d+(?:\.\d+)?)$");
        if (!m.Success || !float.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float n)
            || float.IsInfinity(n)) throw new FormatException("数值格式无效，例如 >=3");
        switch (m.Groups[1].Value)
        {
            case "<": return x => x < n; case ">": return x => x > n;
            case "<=": return x => x <= n; case ">=": return x => x >= n;
            case "!=": return x => x != n; default: return x => x == n;
        }
    }
    static int Date(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return 0;
        if (!DateTime.TryParseExact(input.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) throw new FormatException("日期格式应为 YYYY-MM-DD");
        return date.Year * 10000 + date.Month * 100 + date.Day;
    }
    static readonly HashSet<string> ManaSymbols = new HashSet<string>(
        SearchInsertionMenu.ManaGroups().SelectMany(group => group.Values), StringComparer.Ordinal);

    static Dictionary<string, int> Symbols(string input, bool validate = true)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(input)) return result;
        string normalized = input.Replace(" ", "").ToUpperInvariant();
        int end = 0;
        foreach (Match m in Regex.Matches(normalized, @"\{([^{}]+)\}"))
        {
            string key = m.Groups[1].Value;
            if (validate && (m.Index != end || (!ManaSymbols.Contains(m.Value) && !key.All(ch => ch >= '0' && ch <= '9'))))
                throw new FormatException("费用格式无效，例如 {2}{U}{U}");
            result[key] = result.TryGetValue(key, out int n) ? n + 1 : 1;
            end = m.Index + m.Length;
        }
        if (validate && end != normalized.Length) throw new FormatException("费用格式无效，例如 {2}{U}{U}");
        return result;
    }
    public void Order(List<CardDefinition> results)
    {
        if (Unique)
        {
            // OM1 is an alternate treatment of SPM. Prefer the paper original
            // only when it also passed the query; an OM1-only search stays OM1.
            var originals = results.Where(c => c.setCode == "SPM")
                .GroupBy(c => c.cardName ?? c.PrintingKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < results.Count; i++)
                if (results[i].setCode == "OM1" && originals.TryGetValue(results[i].cardName ?? results[i].PrintingKey, out var original))
                    results[i] = original;
        }
        if (Sort > 0)
        {
            var ordered = Sort switch
            {
                1 => results.OrderBy(GamePreferences.CardName, StringComparer.CurrentCulture),
                2 => results.OrderBy(c => CardSearchMetadata.Get(c)?.manaValue ?? float.MaxValue),
                _ => results.OrderByDescending(c => CardSearchMetadata.Get(c)?.released ?? 0)
            };
            var sorted = ordered.ThenBy(c => c.PrintingKey, StringComparer.Ordinal).ToList();
            results.Clear();
            results.AddRange(sorted);
        }
        else if (Format > 0)
        {
            // Preserve catalog order within each group when browsing a format.
            static bool IsBasicLand(CardDefinition card) =>
                card.HasType(CardType.Land) && card.HasSupertype(CardSupertype.Basic);

            var basicLands = results.FindAll(IsBasicLand);
            if (basicLands.Count > 0 && basicLands.Count < results.Count)
            {
                results.RemoveAll(IsBasicLand);
                results.AddRange(basicLands);
            }
        }
        if (Unique)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            results.RemoveAll(c => !seen.Add(c.cardName ?? c.PrintingKey));
        }
    }
}
