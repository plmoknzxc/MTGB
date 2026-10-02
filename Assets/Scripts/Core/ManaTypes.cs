using System;
using System.Text;
using System.Text.RegularExpressions;

public enum ManaColor
{
    White,
    Blue,
    Black,
    Red,
    Green,
    Colorless
}

[Flags]
public enum ManaColorFlags
{
    None = 0,
    White = 1 << 0,
    Blue = 1 << 1,
    Black = 1 << 2,
    Red = 1 << 3,
    Green = 1 << 4
}

[System.Serializable]
public struct ManaCost
{
    public string Symbols;
    public string FlexibleSymbols;
    public int Generic;
    public int White;
    public int Blue;
    public int Black;
    public int Red;
    public int Green;
    public int Colorless;

    public int ColoredPips => White + Blue + Black + Red + Green + Colorless;
    public int FlexiblePips => ManaSymbolUtility.CountSymbols(FlexibleSymbols);
    public int XSymbols => ManaSymbolUtility.CountToken(Symbols, "X");
    public int Total => Generic + ColoredPips + FlexiblePips;

    public static ManaCost FromGeneric(int amount) => Parse(amount > 0 ? $"{{{amount}}}" : string.Empty);

    public static ManaCost Parse(string symbols) => ManaSymbolUtility.ParseCost(symbols);

    public bool IsEmpty => Total == 0;

    public string ToSymbolString()
    {
        if (!string.IsNullOrEmpty(Symbols)) return Symbols;
        var sb = new StringBuilder();
        if (Generic > 0) sb.Append('{').Append(Generic).Append('}');
        for (int i = 0; i < White; i++) sb.Append("{W}");
        for (int i = 0; i < Blue; i++) sb.Append("{U}");
        for (int i = 0; i < Black; i++) sb.Append("{B}");
        for (int i = 0; i < Red; i++) sb.Append("{R}");
        for (int i = 0; i < Green; i++) sb.Append("{G}");
        for (int i = 0; i < Colorless; i++) sb.Append("{C}");
        if (sb.Length == 0) sb.Append("{0}");
        return sb.ToString();
    }
}

public static class ManaSymbolUtility
{
    private static readonly Regex SymbolPattern = new Regex(@"\{([^{}]+)\}", RegexOptions.Compiled);

    public static ManaCost ParseCost(string input)
    {
        var result = new ManaCost();
        if (string.IsNullOrWhiteSpace(input)) return result;

        var normalized = new StringBuilder();
        var flexible = new StringBuilder();
        MatchCollection matches = SymbolPattern.Matches(input.ToUpperInvariant());
        foreach (Match match in matches)
        {
            string token = match.Groups[1].Value.Trim();
            if (token.Length == 0) continue;
            normalized.Append('{').Append(token).Append('}');

            if (int.TryParse(token, out int generic)) result.Generic += Math.Max(0, generic);
            else if (token == "W") result.White++;
            else if (token == "U") result.Blue++;
            else if (token == "B") result.Black++;
            else if (token == "R") result.Red++;
            else if (token == "G") result.Green++;
            else if (token == "C") result.Colorless++;
            else if (token != "X") flexible.Append('{').Append(token).Append('}');
        }

        result.Symbols = normalized.ToString();
        result.FlexibleSymbols = flexible.ToString();
        return result;
    }

    public static int CountSymbols(string symbols)
    {
        return string.IsNullOrEmpty(symbols) ? 0 : SymbolPattern.Matches(symbols).Count;
    }

    public static int CountToken(string symbols, string expectedToken)
    {
        if (string.IsNullOrEmpty(symbols) || string.IsNullOrWhiteSpace(expectedToken)) return 0;

        int count = 0;
        MatchCollection matches = SymbolPattern.Matches(symbols.ToUpperInvariant());
        string normalized = expectedToken.Trim().ToUpperInvariant();
        for (int i = 0; i < matches.Count; i++)
        {
            if (matches[i].Groups[1].Value.Trim() == normalized) count++;
        }
        return count;
    }

    public static string[] GetSymbolTokens(string symbols)
    {
        if (string.IsNullOrEmpty(symbols)) return Array.Empty<string>();
        MatchCollection matches = SymbolPattern.Matches(symbols.ToUpperInvariant());
        var result = new string[matches.Count];
        for (int i = 0; i < matches.Count; i++) result[i] = matches[i].Groups[1].Value;
        return result;
    }

    public static string BuildLegacyCost(int generic, int white, int blue, int black, int red, int green, int colorless)
    {
        var result = new StringBuilder();
        if (generic > 0) result.Append('{').Append(generic).Append('}');
        Append(result, "W", white);
        Append(result, "U", blue);
        Append(result, "B", black);
        Append(result, "R", red);
        Append(result, "G", green);
        Append(result, "C", colorless);
        return result.ToString();
    }

    private static void Append(StringBuilder builder, string symbol, int count)
    {
        for (int i = 0; i < count; i++) builder.Append('{').Append(symbol).Append('}');
    }
}
