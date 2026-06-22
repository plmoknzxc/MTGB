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

[System.Serializable]
public struct ManaPool
{
    public int White;
    public int Blue;
    public int Black;
    public int Red;
    public int Green;
    public int Colorless;

    public int Total => White + Blue + Black + Red + Green + Colorless;

    public void Add(ManaColor color, int amount)
    {
        switch (color)
        {
            case ManaColor.White: White += amount; break;
            case ManaColor.Blue: Blue += amount; break;
            case ManaColor.Black: Black += amount; break;
            case ManaColor.Red: Red += amount; break;
            case ManaColor.Green: Green += amount; break;
            case ManaColor.Colorless: Colorless += amount; break;
        }
    }

    public bool TryRemove(ManaColor color, int amount)
    {
        if (amount <= 0) return true;
        if (GetAmount(color) < amount) return false;

        switch (color)
        {
            case ManaColor.White: White -= amount; break;
            case ManaColor.Blue: Blue -= amount; break;
            case ManaColor.Black: Black -= amount; break;
            case ManaColor.Red: Red -= amount; break;
            case ManaColor.Green: Green -= amount; break;
            case ManaColor.Colorless: Colorless -= amount; break;
        }

        return true;
    }

    public int GetAmount(ManaColor color)
    {
        switch (color)
        {
            case ManaColor.White: return White;
            case ManaColor.Blue: return Blue;
            case ManaColor.Black: return Black;
            case ManaColor.Red: return Red;
            case ManaColor.Green: return Green;
            case ManaColor.Colorless: return Colorless;
        }
        return 0;
    }

    public void Clear()
    {
        White = Blue = Black = Red = Green = Colorless = 0;
    }

    public bool IsEmpty => Total == 0;

    public bool CanPay(ManaCost cost)
    {
        return ManaPayment.TrySpend(this, cost, out _);
    }

    public bool Spend(ManaCost cost)
    {
        if (!ManaPayment.TrySpend(this, cost, out ManaPool remaining)) return false;
        this = remaining;
        return true;
    }

    public string ToDisplayString()
    {
        var sb = new StringBuilder();
        if (White > 0) { sb.Append('W'); if (White > 1) sb.Append(White); }
        if (Blue > 0) { sb.Append('U'); if (Blue > 1) sb.Append(Blue); }
        if (Black > 0) { sb.Append('B'); if (Black > 1) sb.Append(Black); }
        if (Red > 0) { sb.Append('R'); if (Red > 1) sb.Append(Red); }
        if (Green > 0) { sb.Append('G'); if (Green > 1) sb.Append(Green); }
        if (Colorless > 0) { sb.Append('C'); if (Colorless > 1) sb.Append(Colorless); }
        return sb.Length > 0 ? sb.ToString() : "0";
    }
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

    public static ManaColor FindFirstProducedColor(string rulesText)
    {
        if (string.IsNullOrWhiteSpace(rulesText)) return ManaColor.Colorless;
        string upper = rulesText.ToUpperInvariant();
        int addIndex = upper.IndexOf("ADD", StringComparison.Ordinal);
        string search = addIndex >= 0 ? upper.Substring(addIndex) : upper;
        MatchCollection matches = SymbolPattern.Matches(search);
        foreach (Match match in matches)
        {
            switch (match.Groups[1].Value)
            {
                case "W": return ManaColor.White;
                case "U": return ManaColor.Blue;
                case "B": return ManaColor.Black;
                case "R": return ManaColor.Red;
                case "G": return ManaColor.Green;
                case "C": return ManaColor.Colorless;
            }
        }
        return ManaColor.Colorless;
    }

    private static void Append(StringBuilder builder, string symbol, int count)
    {
        for (int i = 0; i < count; i++) builder.Append('{').Append(symbol).Append('}');
    }
}

public static class ManaPayment
{
    public static bool TrySpend(ManaPool pool, ManaCost cost, out ManaPool remaining)
    {
        int[] amounts = { pool.White, pool.Blue, pool.Black, pool.Red, pool.Green, pool.Colorless };
        int[] required = { cost.White, cost.Blue, cost.Black, cost.Red, cost.Green, cost.Colorless };
        for (int i = 0; i < amounts.Length; i++)
        {
            if (amounts[i] < required[i])
            {
                remaining = pool;
                return false;
            }
            amounts[i] -= required[i];
        }

        string[] flexible = ManaSymbolUtility.GetSymbolTokens(cost.FlexibleSymbols);
        if (!TrySpendFlexible(amounts, flexible, 0, cost.Generic, out int[] paid))
        {
            remaining = pool;
            return false;
        }

        remaining = new ManaPool
        {
            White = paid[0],
            Blue = paid[1],
            Black = paid[2],
            Red = paid[3],
            Green = paid[4],
            Colorless = paid[5]
        };
        return true;
    }

    private static bool TrySpendFlexible(int[] amounts, string[] symbols, int index, int generic, out int[] result)
    {
        if (index >= symbols.Length)
        {
            int[] final = (int[])amounts.Clone();
            if (!SpendGeneric(final, generic))
            {
                result = null;
                return false;
            }
            result = final;
            return true;
        }

        string[] options = symbols[index].Split('/');
        for (int i = 0; i < options.Length; i++)
        {
            string option = options[i];
            if (option == "P") continue;

            int[] branch = (int[])amounts.Clone();
            bool paid;
            if (option == "S") paid = SpendGeneric(branch, 1);
            else if (int.TryParse(option, out int genericOption)) paid = SpendGeneric(branch, genericOption);
            else paid = TrySpendColor(branch, option);

            if (paid && TrySpendFlexible(branch, symbols, index + 1, generic, out result)) return true;
        }

        result = null;
        return false;
    }

    private static bool TrySpendColor(int[] amounts, string symbol)
    {
        int index;
        switch (symbol)
        {
            case "W": index = 0; break;
            case "U": index = 1; break;
            case "B": index = 2; break;
            case "R": index = 3; break;
            case "G": index = 4; break;
            case "C": index = 5; break;
            default: return false;
        }
        if (amounts[index] <= 0) return false;
        amounts[index]--;
        return true;
    }

    private static bool SpendGeneric(int[] amounts, int amount)
    {
        int[] order = { 5, 0, 1, 2, 3, 4 };
        for (int i = 0; i < order.Length && amount > 0; i++)
        {
            int index = order[i];
            int spent = Math.Min(amounts[index], amount);
            amounts[index] -= spent;
            amount -= spent;
        }
        return amount == 0;
    }
}
