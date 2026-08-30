using System;
using System.Collections.Generic;

public enum ScriptAbilityTiming
{
    AnyPriority = 0,
    Sorcery = 1,
    Mana = 2
}

public sealed class ScriptAbilityCost
{
    public ManaCost Mana;
    public int Life;
    public int DiscardCount;
    public int SacrificePermanentCount;
    public int ExileHandCount;
    public int RevealHandCount;
    public string CounterName;
    public int CounterCount;
    public bool TapSource;
    public bool SacrificeSource;
    public bool ExileSource;

    public bool IsEmpty =>
        Mana.IsEmpty
        && Life <= 0
        && DiscardCount <= 0
        && SacrificePermanentCount <= 0
        && ExileHandCount <= 0
        && RevealHandCount <= 0
        && CounterCount <= 0
        && !TapSource
        && !SacrificeSource
        && !ExileSource;

    public ScriptAbilityCost Clone()
    {
        return new ScriptAbilityCost
        {
            Mana = Mana,
            Life = Life,
            DiscardCount = DiscardCount,
            SacrificePermanentCount = SacrificePermanentCount,
            ExileHandCount = ExileHandCount,
            RevealHandCount = RevealHandCount,
            CounterName = CounterName,
            CounterCount = CounterCount,
            TapSource = TapSource,
            SacrificeSource = SacrificeSource,
            ExileSource = ExileSource
        };
    }

    public static ScriptAbilityCost Combine(
        ScriptAbilityCost left,
        ScriptAbilityCost right)
    {
        left ??= new ScriptAbilityCost();
        right ??= new ScriptAbilityCost();
        return new ScriptAbilityCost
        {
            Mana = ManaCostMath.Add(left.Mana, right.Mana),
            Life = left.Life + right.Life,
            DiscardCount = left.DiscardCount + right.DiscardCount,
            SacrificePermanentCount =
                left.SacrificePermanentCount + right.SacrificePermanentCount,
            ExileHandCount = left.ExileHandCount + right.ExileHandCount,
            RevealHandCount = left.RevealHandCount + right.RevealHandCount,
            CounterName = MergeCounterName(left, right),
            CounterCount = left.CounterCount + right.CounterCount,
            TapSource = left.TapSource || right.TapSource,
            SacrificeSource = left.SacrificeSource || right.SacrificeSource,
            ExileSource = left.ExileSource || right.ExileSource
        };
    }

    public static bool TryParse(string text, out ScriptAbilityCost result, out string error)
    {
        result = new ScriptAbilityCost();
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return true;

        string[] entries = text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        var manaSymbols = new List<string>();
        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i].Trim();
            if (entry.Length == 0) continue;
            if (entry.StartsWith("{", StringComparison.Ordinal))
            {
                manaSymbols.Add(entry);
                continue;
            }

            string[] parts = entry.Split(':');
            string key = parts[0].Trim();
            if (key.Equals("Tap", StringComparison.OrdinalIgnoreCase))
            {
                result.TapSource = true;
            }
            else if (key.Equals("Sacrifice", StringComparison.OrdinalIgnoreCase))
            {
                result.SacrificeSource = true;
            }
            else if (key.Equals("Exile", StringComparison.OrdinalIgnoreCase))
            {
                result.ExileSource = true;
            }
            else if (key.Equals("Life", StringComparison.OrdinalIgnoreCase)
                && TryReadPositive(parts, 1, out int life))
            {
                result.Life += life;
            }
            else if (key.Equals("Discard", StringComparison.OrdinalIgnoreCase)
                && TryReadPositive(parts, 1, out int discard))
            {
                result.DiscardCount += discard;
            }
            else if (key.Equals("SacrificePermanent", StringComparison.OrdinalIgnoreCase)
                && TryReadPositive(parts, 1, out int sacrificePermanent))
            {
                result.SacrificePermanentCount += sacrificePermanent;
            }
            else if (key.Equals("ExileHand", StringComparison.OrdinalIgnoreCase)
                && TryReadPositive(parts, 1, out int exileHand))
            {
                result.ExileHandCount += exileHand;
            }
            else if (key.Equals("RevealHand", StringComparison.OrdinalIgnoreCase)
                && TryReadPositive(parts, 1, out int revealHand))
            {
                result.RevealHandCount += revealHand;
            }
            else if (key.Equals("Counter", StringComparison.OrdinalIgnoreCase)
                && parts.Length >= 3
                && TryReadPositive(parts, 2, out int counters))
            {
                result.CounterName = parts[1].Trim();
                result.CounterCount += counters;
            }
            else if (key.Equals("Mana", StringComparison.OrdinalIgnoreCase)
                && parts.Length >= 2)
            {
                manaSymbols.Add(parts[1].Trim());
            }
            else
            {
                error = $"Unsupported scripted ability cost '{entry}'.";
                return false;
            }
        }

        result.Mana = ManaCost.Parse(string.Concat(manaSymbols));
        if (result.SacrificeSource && result.ExileSource)
        {
            error = "An ability cannot both sacrifice and exile its source as a cost.";
            return false;
        }

        return true;
    }

    private static bool TryReadPositive(string[] values, int index, out int value)
    {
        value = 0;
        return index < values.Length
            && int.TryParse(values[index], out value)
            && value > 0;
    }

    private static string MergeCounterName(
        ScriptAbilityCost left,
        ScriptAbilityCost right)
    {
        if (left.CounterCount <= 0) return right.CounterName;
        if (right.CounterCount <= 0) return left.CounterName;
        return string.Equals(
            left.CounterName,
            right.CounterName,
            StringComparison.OrdinalIgnoreCase)
            ? left.CounterName
            : string.Empty;
    }
}
