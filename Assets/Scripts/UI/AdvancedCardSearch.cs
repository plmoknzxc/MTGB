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
    public int Games = 1;
    public static readonly string[] Rarities = { "", "common", "uncommon", "rare", "mythic", "special", "bonus" };
    public static readonly string[] Layouts = { "", "normal", "transform", "modal_dfc", "adventure", "split", "flip", "meld", "saga", "class" };
    public AdvancedCardSearch Copy() => (AdvancedCardSearch)MemberwiseClone();

    public Func<CardDefinition, bool> Compile(IReadOnlyList<CardDefinition> cards)
    {
        var value = Number(Value); var power = Number(Power); var toughness = Number(Toughness); var loyalty = Number(Loyalty);
        int from = Date(From), to = Date(To);
        if (from > 0 && to > 0 && from > to) throw new FormatException("日期范围无效");
        string[] sets = Sets.Split(new[] { ',', '，', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
        var namedSets=new HashSet<string>(sets.SelectMany(SetBrowserPanel.MatchingCodes),StringComparer.OrdinalIgnoreCase);
        var mana = Symbols(Mana);
        var costCache = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var textCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        var legalCache = new Dictionary<string, string>(StringComparer.Ordinal);
        var legality = new DeckFormatLegality();
        string format = Format == 0 ? null : DeckFormatLegality.Keys[Format - 1];
        if (format != null) legality.PrepareForValidation(format, cards);
        return card =>
        {
            if (!MainMenuController.MatchesCollectionFilter(card, Query, Source)) return false;
            if (card.customContent || !textCache.TryGetValue(card.cardName, out bool textMatch))
            {
                textMatch = TextMatches(card, Name, 1) && TextMatches(card, Type, 2) && TextMatches(card, Rules, 3);
                if (!card.customContent) textCache[card.cardName] = textMatch;
            }
            if (!textMatch) return false;
            var meta = CardSearchMetadata.Get(card);
            if(card.customContent ? (Games&1)==0 : meta==null || (meta.games&Games)==0) return false;
            if(format==null && !card.customContent && (meta.legalityMask&(1<<Status))==0) return false;
            if ((Colors != 0 && (meta == null || !ColorMatches(meta.colors, Colors, ColorMode)))
                || (Identity != 0 && (meta == null || !ColorMatches(meta.identity, Identity, IdentityMode)))) return false;
            if (Value.Length > 0 && (meta == null || !value(meta.manaValue))) return false;
            if (Rarity > 0 && (meta == null || meta.rarity != Rarities[Rarity])) return false;
            if (Layout > 0 && (meta == null || meta.layout != Layouts[Layout])) return false;
            if (Artist.Length > 0 && (meta == null || !Contains(meta.artist, Artist))) return false;
            if ((from > 0 || to > 0) && (meta == null || meta.released == 0 || meta.released < from || (to > 0 && meta.released > to))) return false;
            if (sets.Length > 0 && !sets.Any(s => string.Equals(s, card.setCode, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, card.forgeSet, StringComparison.OrdinalIgnoreCase) || (meta != null && Contains(meta.setName, s))) && !namedSets.Contains(card.setCode)) return false;
            if (format != null)
            {
                if (!legalCache.TryGetValue(card.cardName, out string state))
                    legalCache[card.cardName] = state = legality.Get(card, format, DeckFormatLegality.Current);
                if(state!=new[]{"legal","restricted","banned"}[Status])return false;
            }
            bool stats = Power.Length > 0 || Toughness.Length > 0 || Loyalty.Length > 0 || mana.Count > 0;
            if (!stats) return true;
            // P/T and mana constraints must be satisfied by the same face/part.
            if (card.features != null && card.features.Count > 0)
                return card.features.Any(f => f != null && Face(f.cardTypes, f.manaCost, f.powerExpression, f.toughnessExpression, f.attack, f.defence, f.loyalty));
            return Face(card.cardTypes, card.manaCost, null, null, card.attack, card.defence, card.loyalty);

            bool Face(CardType type, string cost, string p, string t, int attack, int defence, int loy)
            {
                if ((Power.Length > 0 || Toughness.Length > 0) && (type & CardType.Creature) == 0) return false;
                if (Loyalty.Length > 0 && (type & CardType.Planeswalker) == 0) return false;
                if (Power.Length > 0 && !Stat(p, attack, power) || Toughness.Length > 0 && !Stat(t, defence, toughness) || !loyalty(loy)) return false;
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
            bool found = Contains(original, term) || GamePreferences.MatchesAnyCardTranslation(card, term, field);
            if (!found && card.features != null) found = card.features.Any(f => f != null && Contains(field == 1 ? f.cardName : field == 2 ? f.supertypes + " " + f.cardTypes + " " + f.subtypes : f.description, term));
            if (!found) return false;
        }
        return true;
    }
    public static Func<float, bool> Number(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return _ => true;
        var m = Regex.Match(input.Trim(), @"^(<=|>=|!=|=|<|>)?\s*(-?\d+(?:\.\d+)?)$");
        if (!m.Success) throw new FormatException("数值格式无效，例如 >=3");
        float n = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
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
    static Dictionary<string, int> Symbols(string input, bool validate = true)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(input)) return result;
        string normalized = input.Replace(" ", "").ToUpperInvariant();
        if (validate && !Regex.IsMatch(normalized, @"^(\{(?:\d+|[WUBRGCXSYZ]|[WUBRGC2]/[WUBRGP]|[WUBRG]/[WUBRG]/P)\})+$")) throw new FormatException("费用格式无效，例如 {2}{U}{U}");
        foreach (Match m in Regex.Matches(normalized, @"\{([^}]+)\}"))
        { string key = m.Groups[1].Value; result[key] = result.TryGetValue(key, out int n) ? n + 1 : 1; }
        return result;
    }
    public void Order(List<CardDefinition> results)
    {
        if (Sort > 0) results.Sort((a, b) =>
        {
            int n = Sort == 1 ? string.Compare(GamePreferences.CardName(a), GamePreferences.CardName(b), StringComparison.CurrentCulture)
                : Sort == 2 ? (CardSearchMetadata.Get(a)?.manaValue ?? float.MaxValue).CompareTo(CardSearchMetadata.Get(b)?.manaValue ?? float.MaxValue)
                : (CardSearchMetadata.Get(b)?.released ?? 0).CompareTo(CardSearchMetadata.Get(a)?.released ?? 0);
            return n != 0 ? n : string.CompareOrdinal(a.PrintingKey, b.PrintingKey);
        });
        if (Unique) { var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); results.RemoveAll(c => !seen.Add(c.cardName ?? c.PrintingKey)); }
    }
}
