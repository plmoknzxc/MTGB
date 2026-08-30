using System;

public static class TargetingRules
{
    public static bool CanTargetCard(RuntimeCard source, RuntimeCard target, bool targetOnBattlefield)
    {
        if (source == null || target == null || target.IsDead) return false;
        if (!targetOnBattlefield) return false;
        if (target.HasKeyword("Shroud")) return false;
        if (target.HasKeyword("Hexproof")
            && source.ControllerPlayerID != target.ControllerPlayerID)
        {
            return false;
        }
        return !IsProtectedFrom(target, source);
    }

    public static bool CanAttachToCard(RuntimeCard source, RuntimeCard target)
    {
        if (source == null
            || target == null
            || target.IsDead
            || ReferenceEquals(source, target)
            || IsProtectedFrom(target, source))
        {
            return false;
        }

        if (source.HasSubtype("Equipment"))
        {
            return target.HasType(CardType.Creature)
                && (!source.HasType(CardType.Creature) || source.HasKeyword("Reconfigure"));
        }

        if (source.HasSubtype("Fortification"))
        {
            return target.HasType(CardType.Land);
        }

        if (source.HasSubtype("Aura"))
        {
            return MatchesTargetMode(source, target);
        }

        return true;
    }

    public static bool CanSpellTargetCard(RuntimeCard source, RuntimeCard target, bool targetOnBattlefield)
    {
        return CanSpellTargetCard(source, target, targetOnBattlefield, source?.GetTargetMode() ?? CardTargetMode.None);
    }

    public static bool CanSpellTargetCard(
        RuntimeCard source,
        RuntimeCard target,
        bool targetOnBattlefield,
        CardTargetMode targetMode)
    {
        if (!CanTargetCard(source, target, targetOnBattlefield)) return false;

        bool friendly = source.ControllerPlayerID == target.ControllerPlayerID;
        bool creature = target.HasType(CardType.Creature);
        bool permanent = IsPermanent(target.GetCardTypes());

        switch (targetMode)
        {
            case CardTargetMode.AnyCreature:
                return creature;
            case CardTargetMode.OpponentCreature:
                return creature && !friendly;
            case CardTargetMode.FriendlyCreature:
                return creature && friendly;
            case CardTargetMode.AnyPermanent:
                return permanent;
            case CardTargetMode.OpponentPermanent:
                return permanent && !friendly;
            case CardTargetMode.FriendlyPermanent:
                return permanent && friendly;
            case CardTargetMode.AnyCreatureOrPlayer:
                return creature;
            case CardTargetMode.OpponentCreatureOrPlayer:
                return creature && !friendly;
            default:
                return false;
        }
    }

    public static bool CanSpellTargetPlayer(RuntimeCard source, int targetPlayerID)
    {
        return CanSpellTargetPlayer(
            source,
            targetPlayerID,
            source?.GetTargetMode() ?? CardTargetMode.None);
    }

    public static bool CanSpellTargetPlayer(
        RuntimeCard source,
        int targetPlayerID,
        CardTargetMode targetMode)
    {
        if (source == null || source.Data == null) return false;
        if (targetPlayerID != 1 && targetPlayerID != 2) return false;

        bool friendly = source.ControllerPlayerID == targetPlayerID;
        switch (targetMode)
        {
            case CardTargetMode.AnyPlayer:
            case CardTargetMode.AnyCreatureOrPlayer:
                return true;
            case CardTargetMode.OpponentPlayer:
            case CardTargetMode.OpponentCreatureOrPlayer:
                return !friendly;
            default:
                return false;
        }
    }

    public static bool IsProtectedFrom(RuntimeCard protectedCard, RuntimeCard source)
    {
        if (protectedCard == null || source == null) return false;

        foreach (string keyword in protectedCard.GetKeywords())
        {
            if (!TryGetProtectionQuality(keyword, out string quality)) continue;
            if (SourceMatchesProtectionQuality(source, quality)) return true;
        }

        return false;
    }

    private static bool TryGetProtectionQuality(string keyword, out string quality)
    {
        quality = string.Empty;
        if (string.IsNullOrWhiteSpace(keyword)) return false;

        string normalized = keyword
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(":", string.Empty);

        const string protectionFrom = "ProtectionFrom";
        const string protection = "Protection";
        if (normalized.StartsWith(protectionFrom, StringComparison.OrdinalIgnoreCase))
        {
            quality = normalized.Substring(protectionFrom.Length);
        }
        else if (normalized.StartsWith(protection, StringComparison.OrdinalIgnoreCase))
        {
            quality = normalized.Substring(protection.Length);
        }

        quality = NormalizeProtectionQuality(quality);
        return !string.IsNullOrEmpty(quality);
    }

    private static string NormalizeProtectionQuality(string quality)
    {
        if (string.IsNullOrWhiteSpace(quality)) return string.Empty;

        string normalized = quality.Trim();
        if (normalized.EndsWith("s", StringComparison.OrdinalIgnoreCase) && normalized.Length > 1)
        {
            normalized = normalized.Substring(0, normalized.Length - 1);
        }

        switch (normalized.ToUpperInvariant())
        {
            case "W": return "White";
            case "U": return "Blue";
            case "B": return "Black";
            case "R": return "Red";
            case "G": return "Green";
            case "C": return "Colorless";
            default: return normalized;
        }
    }

    private static bool SourceMatchesProtectionQuality(RuntimeCard source, string quality)
    {
        if (source == null || string.IsNullOrWhiteSpace(quality)) return false;

        switch (quality.ToUpperInvariant())
        {
            case "ALL":
            case "ANYTHING":
                return true;
            case "COLORED": return !IsColorless(source);
            case "MONOCOLORED": return CountColors(source) == 1;
            case "MULTICOLORED": return CountColors(source) > 1;
            case "WHITE": return CardHasColor(source, ManaColor.White);
            case "BLUE": return CardHasColor(source, ManaColor.Blue);
            case "BLACK": return CardHasColor(source, ManaColor.Black);
            case "RED": return CardHasColor(source, ManaColor.Red);
            case "GREEN": return CardHasColor(source, ManaColor.Green);
            case "COLORLESS": return IsColorless(source);
            case "NONARTIFACT": return !source.HasType(CardType.Artifact);
            case "NONCREATURE": return !source.HasType(CardType.Creature);
            case "NONLAND": return !source.HasType(CardType.Land);
            case "ARTIFACT": return source.HasType(CardType.Artifact);
            case "BATTLE": return source.HasType(CardType.Battle);
            case "CREATURE": return source.HasType(CardType.Creature);
            case "ENCHANTMENT": return source.HasType(CardType.Enchantment);
            case "INSTANT": return source.HasType(CardType.Instant);
            case "KINDRED": return source.HasType(CardType.Kindred);
            case "LAND": return source.HasType(CardType.Land);
            case "PLANESWALKER": return source.HasType(CardType.Planeswalker);
            case "SORCERY": return source.HasType(CardType.Sorcery);
            case "PERMANENT": return IsPermanent(source.GetCardTypes());
            default: return false;
        }
    }

    private static bool CardHasColor(RuntimeCard card, ManaColor color)
    {
        if (card == null || card.Data == null) return false;

        switch (color)
        {
            case ManaColor.White: return (card.GetColors() & ManaColorFlags.White) != 0;
            case ManaColor.Blue: return (card.GetColors() & ManaColorFlags.Blue) != 0;
            case ManaColor.Black: return (card.GetColors() & ManaColorFlags.Black) != 0;
            case ManaColor.Red: return (card.GetColors() & ManaColorFlags.Red) != 0;
            case ManaColor.Green: return (card.GetColors() & ManaColorFlags.Green) != 0;
            default: return false;
        }
    }

    private static bool IsColorless(RuntimeCard card)
    {
        if (card == null || card.Data == null) return false;
        return card.GetColors() == ManaColorFlags.None;
    }

    private static int CountColors(RuntimeCard card)
    {
        if (card == null || card.Data == null) return 0;
        int count = 0;
        ManaColorFlags colors = card.GetColors();
        if ((colors & ManaColorFlags.White) != 0) count++;
        if ((colors & ManaColorFlags.Blue) != 0) count++;
        if ((colors & ManaColorFlags.Black) != 0) count++;
        if ((colors & ManaColorFlags.Red) != 0) count++;
        if ((colors & ManaColorFlags.Green) != 0) count++;
        return count;
    }

    private static bool IsPermanent(CardType cardTypes)
    {
        return cardTypes.Has(CardType.Artifact)
            || cardTypes.Has(CardType.Battle)
            || cardTypes.Has(CardType.Creature)
            || cardTypes.Has(CardType.Enchantment)
            || cardTypes.Has(CardType.Land)
            || cardTypes.Has(CardType.Planeswalker);
    }

    private static bool MatchesTargetMode(RuntimeCard source, RuntimeCard target)
    {
        bool friendly = source.ControllerPlayerID == target.ControllerPlayerID;
        bool creature = target.HasType(CardType.Creature);
        bool permanent = IsPermanent(target.GetCardTypes());

        switch (source.GetTargetMode())
        {
            case CardTargetMode.AnyCreature:
            case CardTargetMode.AnyCreatureOrPlayer:
                return creature;
            case CardTargetMode.OpponentCreature:
            case CardTargetMode.OpponentCreatureOrPlayer:
                return creature && !friendly;
            case CardTargetMode.FriendlyCreature:
                return creature && friendly;
            case CardTargetMode.AnyPermanent:
                return permanent;
            case CardTargetMode.OpponentPermanent:
                return permanent && !friendly;
            case CardTargetMode.FriendlyPermanent:
                return permanent && friendly;
            default:
                return false;
        }
    }
}
